namespace ResetExplode;

/// <summary>
/// Política conservadora para o stress de CPU. A carga é um alvo de duty cycle,
/// não um limite elétrico de potência nem um substituto para o corte térmico.
/// </summary>
public static class CpuStressPolicy
{
    public const int WindowMilliseconds = 100;
    public static readonly TimeSpan SensorTimeout = TimeSpan.FromSeconds(5);

    public static double NextEffectivePercent(int requestedPercent, double currentPercent,
        double? cpuTemperature, double stopTemperature)
    {
        if (!cpuTemperature.HasValue || !double.IsFinite(cpuTemperature.Value)
            || cpuTemperature.Value <= 0 || cpuTemperature.Value >= stopTemperature
            || !double.IsFinite(stopTemperature))
            return 0;

        double allowed = Math.Clamp(requestedPercent, 0, 100);
        double remaining = stopTemperature - cpuTemperature.Value;

        // Reduz a carga ANTES do corte, sem esconder um superaquecimento real.
        if (remaining < 2) allowed = Math.Min(allowed, 5);
        else if (remaining < 5) allowed = Math.Min(allowed, 10);
        else if (remaining < 10) allowed = Math.Min(allowed, 25);
        else if (remaining < 15) allowed = Math.Min(allowed, 40);

        // Crescimento máximo de 5 pontos percentuais por leitura (~1 segundo).
        return Math.Min(allowed, Math.Max(0, currentPercent) + 5);
    }

    public static int ActiveMilliseconds(double percent, int windowMilliseconds)
        => (int)Math.Round(Math.Clamp(percent, 0, 100) * windowMilliseconds / 100.0,
            MidpointRounding.AwayFromZero);

    public static int PhaseOffsetMilliseconds(int workerIndex, int workerCount, int windowMilliseconds)
        => workerCount <= 0 ? 0 : (int)((long)Math.Max(0, workerIndex) * windowMilliseconds / workerCount);

    public static bool IsSensorStale(TimeSpan elapsed) => elapsed > SensorTimeout;
}
