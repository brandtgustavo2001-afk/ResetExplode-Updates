using ResetExplode;

int passed = 0;
void Check(bool ok, string name)
{
    if (!ok) throw new Exception(name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

var good = PcHealthReport.FromReadings(55, 60, 45, 60, true);
Check(good.Score == 100 && good.ProblemCount == 0, "saude normal recebe 100");
var bad = PcHealthReport.FromReadings(99, 96, 98, 98, false, 2);
Check(bad.Checks.Any(c => c.Id == "cpu" && c.State == HealthState.Critical), "temperatura critica identificada");
Check(bad.Checks.Any(c => c.Id == "sensors" && c.State == HealthState.Unknown), "sensor ausente fica desconhecido");
Check(bad.Score < 60, "problemas graves reduzem a pontuacao");
var mixed = PcHealthReport.Create(new[]
{
    new HealthCheck("a", "A", "ok", HealthState.Good),
    new HealthCheck("b", "B", "atencao", HealthState.Attention),
    new HealthCheck("c", "C", "desconhecido", HealthState.Unknown)
});
Check(mixed.Score == 77, "pontuacao combina estados conhecidos");

string folder = Path.Combine(Path.GetTempPath(), "ResetExplode-tests-" + Guid.NewGuid().ToString("N"));
string path = Path.Combine(folder, "performance.json");
try
{
    var store = new PerformanceHistoryStore(path);
    var samples = Enumerable.Range(0, 3).Select(i => new PerformanceSample(
        DateTime.Now.AddMinutes(i), 50 + i, 20 + i, 45, 60, 70, 100, 120 - i, 8 + i, "Minecraft"));
    store.Save(samples);
    var loaded = store.Load();
    Check(loaded.Count == 3 && loaded[0].Game == "Minecraft", "historico salva e carrega amostras");
    var invalid = new[]
    {
        new PerformanceSample(default, 40, null, null, null, null, null, null, null, null),
        new PerformanceSample(DateTime.Now, float.NaN, null, null, null, null, null, null, null, null)
    };
    store.Save(invalid);
    Check(store.Load().Count == 0, "historico rejeita amostras invalidas");
}
finally
{
    if (Directory.Exists(folder)) Directory.Delete(folder, true);
}

Console.WriteLine($"{passed} verificacoes de prioridade aprovadas.");
