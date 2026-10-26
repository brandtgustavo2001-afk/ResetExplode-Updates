using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;

namespace ResetExplode;

public partial class MainWindow
{
    private EnergyTracker? _energyUsage;
    private EnergyStore? _energyStore;
    private long _energyRevision;
    private bool _energyGpuReady;
    private DateTime _energySavedAt;
    private readonly Stopwatch _energyClock = Stopwatch.StartNew();
    private string _energyReadingStatus = "Aguardando sensores de consumo…";
    public sealed record EnergyDayRow(string Date, string Kwh, string Cost, string Time);

    private void InitEnergyUsage()
    {
        _energyStore = new EnergyStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ResetExplode", "energy-history.json"));
        var boot = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
        _energyUsage = new EnergyTracker(_energyStore.Load(boot), boot);
        EnergyPrice.Text = _settings.EnergyPrice.ToString("0.###", CultureInfo.CurrentCulture);
        EnergyOtherWatts.Text = _settings.EnergyOtherWatts.ToString("0.#", CultureInfo.CurrentCulture);
        EnergyEfficiency.Text = (_settings.EnergyEfficiency * 100).ToString("0.#", CultureInfo.CurrentCulture);
        EnergyAutostart.IsChecked = ResetExplode.EnergyAutostart.IsEnabled();
        SystemEvents.PowerModeChanged += EnergyPowerModeChanged;
        Closed += (_, _) =>
        {
            SystemEvents.PowerModeChanged -= EnergyPowerModeChanged;
            SaveEnergyUsage(false);
        };
        UpdateEnergyUsagePanel();
    }
    private void EnergyPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is PowerModes.Suspend or PowerModes.Resume)
            Dispatcher.BeginInvoke(new Action(() => _energyUsage?.Pause()));
    }
    private void CollectEnergyUsage()
    {
        if (_energyUsage == null) return;
        if (!_energyGpuReady)
        {
            _energyUsage.Pause(); _energyReadingStatus = "Aguardando a detecção das placas de vídeo para iniciar a contagem.";
            if (PageEnergy.IsVisible) UpdateEnergyUsagePanel();
            return;
        }
        double? cpu = _monitor.CpuPower;
        double gpu = 0; bool gpuMissing = false;
        foreach (var adapter in _gpus)
        {
            // Integrated graphics share the CPU package power domain; adding them would double count.
            string name = adapter.Name;
            bool integrated = name.Contains("UHD", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Iris", StringComparison.OrdinalIgnoreCase)
                || name.Contains("HD Graphics", StringComparison.OrdinalIgnoreCase)
                || name.Equals("AMD Radeon(TM) Graphics", StringComparison.OrdinalIgnoreCase)
                || name.Equals("AMD Radeon Graphics", StringComparison.OrdinalIgnoreCase);
            if (integrated) continue;
            double? watts = adapter.Telemetry.Power is uint mw ? mw / 1000.0
                : _monitor.Rows.FirstOrDefault(row => row.Hardware == name && row.Type == SensorType.Power
                    && (row.Name == "GPU Package" || row.Name == "GPU Power" || row.Name == "GPU Total"))?.RawValue;
            if (watts is not double power || !double.IsFinite(power) || power < 0) gpuMissing = true;
            else gpu += power;
        }
        _energyUsage.Sample(DateTimeOffset.Now, _energyClock.Elapsed.TotalSeconds,
            cpu, gpuMissing ? null : gpu, _settings.EnergyOtherWatts, _settings.EnergyEfficiency);
        _energyReadingStatus = cpu is null or <= 0 ? "Contagem pausada: consumo da CPU indisponível. Abra como administrador para ler os sensores."
            : gpuMissing ? "Contagem pausada: uma GPU dedicada está sem leitura de consumo."
            : $"CPU {cpu:0.#} W + GPU dedicada {gpu:0.#} W · demais componentes {_settings.EnergyOtherWatts:0.#} W · eficiência {_settings.EnergyEfficiency:P0}";
        if (PageEnergy.IsVisible) UpdateEnergyUsagePanel();
        if ((DateTime.UtcNow - _energySavedAt).TotalSeconds >= 30) SaveEnergyUsage(true);
    }
    private void SaveEnergyUsage(bool background)
    {
        if (_energyUsage == null || _energyStore == null) return;
        string json = JsonSerializer.Serialize(_energyUsage.History);
        long revision = ++_energyRevision;
        _energySavedAt = DateTime.UtcNow;
        if (background) _ = Task.Run(() => _energyStore.Save(json, revision));
        else _energyStore.Save(json, revision);
    }
    private void UpdateEnergyUsagePanel()
    {
        if (_energyUsage == null) return;
        var history = _energyUsage.History;
        var today = history.Days.GetValueOrDefault(DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) ?? new EnergyTotals();
        static string Energy(EnergyTotals totals) => $"{totals.WattHours / 1000:0.0000} kWh";
        string Cost(EnergyTotals totals) => $"R$ {totals.WattHours / 1000 * _settings.EnergyPrice:0.000}";
        static string Duration(EnergyTotals totals) => $"{(int)(totals.Seconds / 3600)}h {((int)(totals.Seconds / 60) % 60):00}min monitorados";
        EnergyBootValue.Text = Energy(history.Boot); EnergyBootCost.Text = Cost(history.Boot) + " · " + Duration(history.Boot);
        EnergyTodayValue.Text = Energy(today); EnergyTodayCost.Text = Cost(today) + " · " + Duration(today);
        EnergyTotalValue.Text = Energy(history.Total); EnergyTotalCost.Text = Cost(history.Total) + " · " + Duration(history.Total);
        EnergyLiveWatts.Text = _energyUsage.CurrentWatts is double watts ? $"Potência estimada agora: {watts:0.#} W" : "Sem leitura completa de potência";
        EnergyCoverage.Text = _energyReadingStatus + (_energyStore?.LastError is string error ? " · " + error : "");
        EnergyDailyRows.ItemsSource = history.Days.OrderByDescending(pair => pair.Key).Select(pair => new EnergyDayRow(
            DateTime.ParseExact(pair.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd/MM/yyyy"), Energy(pair.Value), Cost(pair.Value), Duration(pair.Value))).ToList();
    }
    private void EnergySettings_Click(object sender, RoutedEventArgs e)
    {
        static bool Number(string text, out double value) => double.TryParse(text.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
        if (!Number(EnergyPrice.Text, out var price) || price is < 0 or > 100
            || !Number(EnergyOtherWatts.Text, out var other) || other is < 0 or > 1000
            || !Number(EnergyEfficiency.Text, out var efficiency) || efficiency is < 50 or > 100)
        { EnergyUsageStatus.Text = "Informe tarifa de 0 a 100, componentes de 0 a 1000 W e eficiência de 50 a 100%."; return; }
        _settings.EnergyPrice = price; _settings.EnergyOtherWatts = other; _settings.EnergyEfficiency = efficiency / 100;
        _settings.Save(); _energyUsage?.Pause(); UpdateEnergyUsagePanel();
        EnergyUsageStatus.Text = "Salvo. Custos do histórico calculados com a tarifa atual; a energia registrada não é alterada.";
    }
    private async void EnergyExport_Click(object sender, RoutedEventArgs e)
    {
        if (_energyUsage == null) return;
        var dialog = new SaveFileDialog { Filter = "Planilha CSV (*.csv)|*.csv", FileName = $"ResetExplode-consumo-{DateTime.Now:yyyy-MM-dd}.csv" };
        if (dialog.ShowDialog(this) != true) return;
        var csv = new StringBuilder("Data;Energia_estimada_kWh;CPU_kWh;GPU_kWh;Horas_monitoradas;Tarifa_R$_kWh;Custo_estimado_R$\r\n");
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        foreach (var (day, totals) in _energyUsage.History.Days.OrderBy(pair => pair.Key))
            csv.AppendLine(string.Join(';', day, (totals.WattHours / 1000).ToString("F6", culture), (totals.CpuWattHours / 1000).ToString("F6", culture),
                (totals.GpuWattHours / 1000).ToString("F6", culture), (totals.Seconds / 3600).ToString("F4", culture),
                _settings.EnergyPrice.ToString("F4", culture), (totals.WattHours / 1000 * _settings.EnergyPrice).ToString("F4", culture)));
        try { await File.WriteAllTextAsync(dialog.FileName, csv.ToString(), new UTF8Encoding(true)); EnergyUsageStatus.Text = "Histórico exportado."; }
        catch (Exception ex) { AppDiagnostics.Log("Exportar consumo", ex); EnergyUsageStatus.Text = "Não foi possível exportar o histórico."; }
    }
    private void EnergyAutostart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ResetExplode.EnergyAutostart.SetEnabled(EnergyAutostart.IsChecked == true);
            _settings.StartWithWindows = EnergyAutostart.IsChecked == true; _settings.Save();
            EnergyUsageStatus.Text = EnergyAutostart.IsChecked == true ? "Inicialização automática ativada. O aplicativo ficará na bandeja, em segundo plano, no logon." : "Inicialização automática desativada.";
        }
        catch (Exception ex)
        {
            AppDiagnostics.Log("Configurar inicialização do monitor", ex);
            EnergyAutostart.IsChecked = ResetExplode.EnergyAutostart.IsEnabled();
            EnergyUsageStatus.Text = "Não foi possível alterar a inicialização. Abra a versão instalada como administrador e tente novamente.";
        }
    }
}
