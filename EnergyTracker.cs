using System.IO;
using System.Text.Json;

namespace ResetExplode;

public sealed class EnergyTotals
{
    public double WattHours { get; set; }
    public double CpuWattHours { get; set; }
    public double GpuWattHours { get; set; }
    public double Seconds { get; set; }
    public void Add(double hours, double cpu, double gpu, double wall)
    {
        Seconds += hours * 3600;
        CpuWattHours += cpu * hours; GpuWattHours += gpu * hours; WattHours += wall * hours;
    }
}
public sealed class EnergyHistory
{
    public DateTimeOffset BootUtc { get; set; }
    public EnergyTotals Boot { get; set; } = new();
    public EnergyTotals Total { get; set; } = new();
    public Dictionary<string, EnergyTotals> Days { get; set; } = new();
}

/// <summary>Integrates watts over observed active intervals. Never extrapolates over sleep or a missing sample.</summary>
public sealed class EnergyTracker
{
    public EnergyHistory History { get; }
    private (DateTimeOffset At, double Monotonic, double Cpu, double Gpu, double Wall)? _last;
    public double? CurrentWatts { get; private set; }
    public EnergyTracker(EnergyHistory history, DateTimeOffset bootUtc)
    {
        History = history;
        History.Days ??= new(); History.Boot ??= new(); History.Total ??= new();
        if (History.Days.Keys.Any(day => !DateTime.TryParseExact(day, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _))) throw new InvalidDataException("Data do histórico inválida.");
        if (Math.Abs((history.BootUtc - bootUtc).TotalSeconds) > 5)
        { history.BootUtc = bootUtc; history.Boot = new(); }
        foreach (var totals in History.Days.Values.Append(History.Boot).Append(History.Total))
        {
            if (totals == null || !double.IsFinite(totals.WattHours) || !double.IsFinite(totals.Seconds)
                || !double.IsFinite(totals.CpuWattHours) || !double.IsFinite(totals.GpuWattHours)
                || totals.WattHours < 0 || totals.Seconds < 0 || totals.CpuWattHours < 0 || totals.GpuWattHours < 0) throw new InvalidDataException("Histórico de energia inválido.");
        }
    }
    public void Pause() { _last = null; CurrentWatts = null; }
    public bool Sample(DateTimeOffset at, double monotonicSeconds, double? cpu, double? gpu, double otherWatts, double efficiency)
    {
        if (cpu is not double c || gpu is not double g || !double.IsFinite(c) || !double.IsFinite(g)
            || c <= 0 || g < 0 || !double.IsFinite(otherWatts) || otherWatts < 0
            || !double.IsFinite(efficiency) || efficiency is < .5 or > 1 || !double.IsFinite(monotonicSeconds))
        { Pause(); return false; }
        double wall = (c + g + otherWatts) / efficiency;
        if (!double.IsFinite(wall) || wall > 5000) { Pause(); return false; }
        var last = _last;
        _last = (at, monotonicSeconds, c, g, wall); CurrentWatts = wall;
        if (last is not { } previous) return false;
        double seconds = monotonicSeconds - previous.Monotonic;
        // Long driver stalls and suspend/resume are unobserved time, not sustained power.
        if (seconds is <= 0 or > 5 || Math.Abs((at - previous.At).TotalSeconds - seconds) > 2) return false;
        c = (c + previous.Cpu) / 2; g = (g + previous.Gpu) / 2; wall = (wall + previous.Wall) / 2;
        double hours = seconds / 3600;
        History.Total.Add(hours, c, g, wall); History.Boot.Add(hours, c, g, wall);
        var cursor = previous.At;
        while (cursor < at)
        {
            var local = TimeZoneInfo.ConvertTime(cursor, TimeZoneInfo.Local);
            var nextDay = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local.Date.AddDays(1), TimeZoneInfo.Local), TimeSpan.Zero);
            var end = nextDay < at ? nextDay : at;
            string day = local.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            if (!History.Days.TryGetValue(day, out var totals)) History.Days[day] = totals = new();
            totals.Add(hours * (end - cursor).TotalSeconds / (at - previous.At).TotalSeconds, c, g, wall);
            cursor = end;
        }
        return true;
    }
}

public sealed class EnergyStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private long _savedRevision = -1;
    public string? LastError { get; private set; }
    public EnergyStore(string path) => _path = path;
    public EnergyHistory Load(DateTimeOffset boot)
    {
        foreach (var path in new[] { _path, _path + ".bak" })
        {
            try
            {
                if (!File.Exists(path)) continue;
                var history = JsonSerializer.Deserialize<EnergyHistory>(File.ReadAllText(path)) ?? throw new InvalidDataException();
                _ = new EnergyTracker(history, boot); return history;
            }
            catch (Exception ex) { LastError = "O histórico não pôde ser lido; verifique o diagnóstico."; AppDiagnostics.Log("Ler histórico de energia", ex); }
        }
        return new EnergyHistory();
    }
    public void Save(string json, long revision = 0)
    {
        lock (_gate)
        {
            if (revision < _savedRevision) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                string temporary = _path + ".tmp";
                File.WriteAllText(temporary, json);
                if (File.Exists(_path)) File.Replace(temporary, _path, _path + ".bak");
                else File.Move(temporary, _path);
                LastError = null;
                _savedRevision = revision;
            }
            catch (Exception ex) { LastError = "Não foi possível salvar o histórico de energia."; AppDiagnostics.Log("Salvar histórico de energia", ex); }
        }
    }
}
