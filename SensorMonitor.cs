using System.Collections.ObjectModel;
using LibreHardwareMonitor.Hardware;

namespace ResetExplode;

/// <summary>Encapsula o LibreHardwareMonitorLib e mantém a lista de sensores atualizada.</summary>
public sealed class SensorMonitor : IDisposable
{
    private readonly Computer _computer;
    private readonly Dictionary<string, SensorRow> _index = new();
    private volatile bool _open;
    private readonly object _lifecycle = new();
    private volatile bool _disposed;
    private readonly HashSet<string> _failed = new();
    private sealed record Reading(string Id, string Hardware, string Category, string Name,
        SensorType Type, HardwareType HardwareType, float? Value, float? Min, float? Max);
    private readonly Dictionary<string, Reading> _readings = new();
    public string? LastError { get; private set; }

    public ObservableCollection<SensorRow> Rows { get; } = new();
    public bool IsOpen => _open;

    public SensorMonitor()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = true,
            IsControllerEnabled = true
        };
    }

    /// <summary>Detecta o hardware (leva de 0,5 a alguns segundos) fora da thread da janela. Até terminar, Refresh não faz nada.</summary>
    public Task OpenAsync() => Task.Run(() =>
    {
        lock (_lifecycle)
        {
            if (_disposed || _open) return;
            try { _computer.Open(); _open = true; }
            catch
            {
                AppDiagnostics.TryCleanup("Limpar inicialização parcial dos sensores", _computer.Close);
                throw;
            }
        }
    });

    public async Task RefreshAsync()
    {
        // Apenas a coleta toca os drivers. A coleção e suas notificações continuam na thread da UI.
        var snapshot = await Task.Run(() =>
        {
        lock (_lifecycle)
        {
            if (!_open || _disposed) return Array.Empty<Reading>();
            foreach (var hw in _computer.Hardware) WalkSafe(hw);
            return _readings.Values.ToArray();
        }
        });
        if (_disposed) return;
        foreach (var reading in snapshot)
        {
            if (!_index.TryGetValue(reading.Id, out var row))
            {
                row = new SensorRow { Id = reading.Id, Hardware = reading.Hardware,
                    Category = reading.Category, Name = reading.Name, Type = reading.Type,
                    HardwareType = reading.HardwareType };
                _index[reading.Id] = row;
                Rows.Add(row);
            }
            row.Update(reading.Value, reading.Min, reading.Max);
        }
    }

    private void WalkSafe(IHardware hw)
    {
        string id = hw.Identifier.ToString();
        if (_failed.Contains(id)) return;
        try { Walk(hw); }
        catch (Exception ex)
        {
            _failed.Add(id);
            LastError = $"{hw.Name}: {ex.Message}";
            AppDiagnostics.Log("Sensor indisponível: " + id, ex);
            // Não reutilizar uma temperatura antiga como se fosse uma leitura atual.
            foreach (var key in _readings.Keys.Where(key => key.StartsWith(id + "/", StringComparison.Ordinal)).ToArray())
                _readings[key] = _readings[key] with { Value = null, Min = null, Max = null };
        }
    }

    private void Walk(IHardware hw)
    {
        hw.Update();
        foreach (var s in hw.Sensors)
        {
            string id = s.Identifier.ToString();
            _readings[id] = new Reading(id, hw.Name, SensorRow.CategoryName(s.SensorType),
                s.Name, s.SensorType, hw.HardwareType, s.Value, s.Min, s.Max);
        }
        foreach (var sub in hw.SubHardware)
            WalkSafe(sub);
    }

    /// <summary>Temperatura principal da CPU: Package (Intel) / Tctl-Tdie (AMD), senão a maior entre os núcleos.</summary>
    public static SensorRow? SelectCpuTemperature(IEnumerable<SensorRow> rows) => rows
        .Where(r => r.HardwareType == HardwareType.Cpu && r.Type == SensorType.Temperature && r.RawValue is > 0
            && !r.Name.Contains("Distance", StringComparison.OrdinalIgnoreCase)
            && !r.Name.Contains("TjMax", StringComparison.OrdinalIgnoreCase)
            && !r.Name.Contains("Critical", StringComparison.OrdinalIgnoreCase))
        .OrderBy(r => r.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase) ? 0
            : r.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ? 1
            : r.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ? 2 : 3)
        .ThenByDescending(r => r.RawValue).FirstOrDefault();

    public float? CpuTemperature => SelectCpuTemperature(Rows)?.RawValue;
    public string CpuTemperatureSource => SelectCpuTemperature(Rows)?.Name ?? "Sem sensor de temperatura disponível";

    /// <summary>Carga total da CPU em %.</summary>
    public float? CpuLoad => Rows.FirstOrDefault(r => r.HardwareType == HardwareType.Cpu
                                                   && r.Type == SensorType.Load
                                                   && r.Name == "CPU Total")?.RawValue;

    /// <summary>Consumo do pacote da CPU em W.</summary>
    public float? CpuPower => Rows.FirstOrDefault(r => r.HardwareType == HardwareType.Cpu
                                                    && r.Type == SensorType.Power
                                                    && r.Name.Contains("Package"))?.RawValue;

    /// <summary>
    /// Driver PawnIO (necessário para temperatura/consumo da CPU via MSR/SMN).
    /// A biblioteca procura exatamente esta chave de desinstalação.
    /// </summary>
    public static bool IsPawnIOInstalled
    {
        get
        {
            try
            {
            using var hklm = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine,
                                                                     Microsoft.Win32.RegistryView.Registry64);
            using var key = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
            return key != null;
            }
            catch (Exception ex) { AppDiagnostics.Log("Consultar PawnIO", ex); return false; }
        }
    }

    public static bool IsElevated =>
        new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

    public enum CpuSensorProblem { None, NotElevated, PawnIOMissing, ReadFailed }

    /// <summary>Por que a temperatura/consumo da CPU não aparecem (o motivo mais provável primeiro).</summary>
    public CpuSensorProblem DiagnoseCpuSensors()
    {
        if (CpuTemperature != null) return CpuSensorProblem.None;
        if (!IsElevated) return CpuSensorProblem.NotElevated;
        if (!IsPawnIOInstalled) return CpuSensorProblem.PawnIOMissing;
        return CpuSensorProblem.ReadFailed;
    }

    public static string Explain(CpuSensorProblem p) => p switch
    {
        CpuSensorProblem.NotElevated =>
            "O RESET EXPLODE não está rodando como administrador, e o Windows só libera os sensores internos da CPU " +
            "nesse modo. Feche e abra de novo aceitando o pedido de administrador.",
        CpuSensorProblem.PawnIOMissing =>
            "Falta o driver PawnIO, que lê a temperatura e o consumo da CPU. Ele é gratuito, assinado e usado também " +
            "pelo LibreHardwareMonitor e pelo FanControl. Depois de instalar, feche e abra o RESET EXPLODE.",
        CpuSensorProblem.ReadFailed =>
            "O PawnIO está instalado, mas a leitura falhou. Se acabou de instalar, reinicie o PC. Se continuar, " +
            "confira se o antivírus não bloqueou o driver ou se a CPU é muito antiga/recente para a biblioteca de sensores.",
        _ => ""
    };

    public void Dispose()
    {
        lock (_lifecycle)
        {
            if (_disposed) return;
            _disposed = true;
            _open = false;
            _computer.Close();
        }
    }
}
