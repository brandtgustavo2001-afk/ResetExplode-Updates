using System.Windows;
using System.Windows.Threading;

namespace ResetExplode;

public partial class MainWindow
{
    private readonly CancellationTokenSource _updateCancellation = new();
    private DispatcherTimer? _updateTimer;
    private bool _checkingUpdate;
    private PreparedUpdate? _preparedUpdate;

    private void InitUpdates()
    {
        ChkAutomaticUpdates.IsChecked = _settings.AutomaticUpdates;
        UpdateStatus.Text = "Versão " + AutoUpdater.CurrentVersion.ToString(3) + " · canal estável no GitHub";
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(4) };
        _updateTimer.Tick += async (_, _) => { if (_settings.AutomaticUpdates) await CheckUpdatesAsync(); };
        Loaded += async (_, _) =>
        {
            _updateTimer.Start();
            try { await Task.Delay(TimeSpan.FromSeconds(20), _updateCancellation.Token); }
            catch (OperationCanceledException) { return; }
            if (_settings.AutomaticUpdates) await CheckUpdatesAsync();
        };
        Closed += (_, _) =>
        {
            _updateCancellation.Cancel(); _updateTimer.Stop();
            if (_preparedUpdate != null && _settings.AutomaticUpdates)
                try { AutoUpdater.Schedule(_preparedUpdate); }
                catch (Exception ex) { AppDiagnostics.Log("Agendar atualização", ex); }
        };
    }

    private async Task CheckUpdatesAsync()
    {
        if (_checkingUpdate || _closed) return;
        _checkingUpdate = true; BtnCheckUpdates.IsEnabled = false;
        try
        {
            var progress = new Progress<string>(text => { if (!_closed) UpdateStatus.Text = text; });
            var update = await Task.Run(() => AutoUpdater.PrepareAsync(text => ((IProgress<string>)progress).Report(text), _updateCancellation.Token));
            if (_closed) return;
            _preparedUpdate = update;
            BtnApplyUpdate.IsEnabled = update != null;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppDiagnostics.Log("Verificar atualização", ex);
            if (!_closed) UpdateStatus.Text = "Não foi possível verificar agora. A versão atual continua disponível. Tente novamente com acesso à internet.";
        }
        finally { _checkingUpdate = false; if (!_closed) BtnCheckUpdates.IsEnabled = true; }
    }

    private void AutomaticUpdates_Changed(object sender, RoutedEventArgs e)
    {
        _settings.AutomaticUpdates = ChkAutomaticUpdates.IsChecked == true;
        _settings.Save();
        if (!_settings.AutomaticUpdates && _preparedUpdate != null)
            UpdateStatus.Text = "Atualização pronta. Clique em atualizar quando desejar; a aplicação automática está desativada.";
    }
    private async void CheckUpdates_Click(object sender, RoutedEventArgs e) => await CheckUpdatesAsync();
    private void ApplyUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_preparedUpdate == null) return;
        if (TestRunning || _ocRunning || _gpuBenchActive || _benchTimer?.IsEnabled == true)
        { UpdateStatus.Text = "Encerre os testes antes de atualizar. Você também pode fechar o aplicativo ao terminar."; return; }
        try
        {
            var update = _preparedUpdate;
            AutoUpdater.Schedule(update); _preparedUpdate = null;
            ExitFromTray();
        }
        catch (Exception ex) { AppDiagnostics.Log("Iniciar atualização", ex); UpdateStatus.Text = "Não foi possível iniciar a atualização. Tente novamente."; }
    }
}
