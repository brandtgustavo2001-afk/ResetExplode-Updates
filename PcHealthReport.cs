namespace ResetExplode;

public enum HealthState { Good, Attention, Critical, Unknown }

public sealed record HealthCheck(string Id, string Title, string Detail, HealthState State)
{
    public bool Ok => State == HealthState.Good;
}

public sealed record PcHealthReport(DateTime At, int Score, IReadOnlyList<HealthCheck> Checks)
{
    public int GoodCount => Checks.Count(c => c.State == HealthState.Good);
    public int ProblemCount => Checks.Count(c => c.State is HealthState.Attention or HealthState.Critical);

    public static PcHealthReport FromReadings(
        float? cpuTemp, float? gpuTemp, float? ramPercent, float? diskPercent,
        bool sensorsAvailable, int hardwareErrors = 0)
    {
        var checks = new List<HealthCheck>();
        checks.Add(Temperature("cpu", "Temperatura da CPU", cpuTemp, 85, 95));
        checks.Add(Temperature("gpu", "Temperatura da GPU", gpuTemp, 85, 95));
        checks.Add(Percent("ram", "Uso da memória RAM", ramPercent, 85, 95,
            "Há memória livre suficiente.", "A RAM está bastante ocupada.", "A RAM está quase cheia."));
        checks.Add(Percent("disk", "Espaço no disco", diskPercent, 90, 97,
            "Há espaço livre no disco principal.", "O disco está ficando cheio.", "O disco está quase cheio."));
        checks.Add(sensorsAvailable
            ? new HealthCheck("sensors", "Sensores de hardware", "Temperatura e consumo disponíveis.", HealthState.Good)
            : new HealthCheck("sensors", "Sensores de hardware", "Não foi possível confirmar todos os sensores.", HealthState.Unknown));
        checks.Add(hardwareErrors == 0
            ? new HealthCheck("events", "Erros recentes de hardware", "Nenhum erro relevante foi encontrado.", HealthState.Good)
            : new HealthCheck("events", "Erros recentes de hardware", $"{hardwareErrors} evento(s) de hardware recente(s).", HealthState.Attention));
        return Create(checks);
    }

    public static PcHealthReport Create(IEnumerable<HealthCheck> checks)
    {
        var list = checks.Where(c => c != null).ToArray();
        if (list.Length == 0) return new(DateTime.Now, 0, list);
        double score = list.Average(c => c.State switch
        {
            HealthState.Good => 100,
            HealthState.Attention => 60,
            HealthState.Critical => 0,
            _ => 70
        });
        return new(DateTime.Now, (int)Math.Round(score), list);
    }

    private static HealthCheck Temperature(string id, string title, float? value, float attention, float critical)
    {
        if (value is not float c || !float.IsFinite(c) || c <= 0)
            return new(id, title, "Sem leitura válida neste momento.", HealthState.Unknown);
        var state = c >= critical ? HealthState.Critical : c >= attention ? HealthState.Attention : HealthState.Good;
        var detail = state switch
        {
            HealthState.Critical => $"{c:0.#} °C · temperatura crítica.",
            HealthState.Attention => $"{c:0.#} °C · merece atenção.",
            _ => $"{c:0.#} °C · dentro do intervalo esperado."
        };
        return new(id, title, detail, state);
    }

    private static HealthCheck Percent(string id, string title, float? value, float attention, float critical,
        string good, string warn, string bad)
    {
        if (value is not float p || !float.IsFinite(p) || p < 0 || p > 100)
            return new(id, title, "Sem leitura válida neste momento.", HealthState.Unknown);
        var state = p >= critical ? HealthState.Critical : p >= attention ? HealthState.Attention : HealthState.Good;
        return new(id, title, $"{p:0.#} % · {(state == HealthState.Critical ? bad : state == HealthState.Attention ? warn : good)}", state);
    }
}
