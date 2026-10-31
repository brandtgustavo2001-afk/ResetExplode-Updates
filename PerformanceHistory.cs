using System.IO;
using System.Text.Json;

namespace ResetExplode;

public sealed record PerformanceSample(
    DateTime At, float? CpuTemperature, float? CpuLoad, float? CpuPower,
    float? GpuTemperature, float? GpuLoad, float? GpuPower,
    double? Fps, double? FrameTimeMs, string? Game);

public sealed class PerformanceHistoryStore
{
    public const int MaxSamples = 10080; // 7 dias em uma amostra por minuto
    private readonly string _path;
    public string? LastError { get; private set; }

    public PerformanceHistoryStore(string path) => _path = path;

    public IReadOnlyList<PerformanceSample> Load()
    {
        try
        {
            if (!File.Exists(_path)) return Array.Empty<PerformanceSample>();
            var value = JsonSerializer.Deserialize<List<PerformanceSample>>(File.ReadAllText(_path)) ?? new();
            return Normalize(value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LastError = ex.Message;
            return Array.Empty<PerformanceSample>();
        }
    }

    public void Save(IEnumerable<PerformanceSample> samples)
    {
        try
        {
            var value = Normalize(samples).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(value));
            File.Move(temp, _path, true);
            LastError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastError = ex.Message;
        }
    }

    private static IReadOnlyList<PerformanceSample> Normalize(IEnumerable<PerformanceSample> samples)
        => samples.Where(s => s.At != default)
            .Where(s => Valid(s.CpuTemperature) && Valid(s.CpuLoad) && Valid(s.CpuPower)
                     && Valid(s.GpuTemperature) && Valid(s.GpuLoad) && Valid(s.GpuPower)
                     && Valid(s.Fps) && Valid(s.FrameTimeMs))
            .OrderBy(s => s.At)
            .TakeLast(MaxSamples)
            .ToArray();

    private static bool Valid(float? value) => value is null || float.IsFinite(value.Value);
    private static bool Valid(double? value) => value is null || double.IsFinite(value.Value);
}
