using ResetExplode;

static void Check(string name, bool ok)
{
    if (!ok) throw new Exception($"FALHOU: {name}");
    Console.WriteLine($"OK: {name}");
}

Check("rampa inicial", CpuStressPolicy.NextEffectivePercent(50, 10, 65, 90) == 15);
Check("respeita limite escolhido", CpuStressPolicy.NextEffectivePercent(25, 25, 65, 90) == 25);
Check("reduz antes de atingir o limite", CpuStressPolicy.NextEffectivePercent(75, 50, 81, 90) <= 25);
Check("reduz acentuadamente perto do limite", CpuStressPolicy.NextEffectivePercent(75, 50, 87, 90) <= 10);
Check("não aguarda limite exato para frear", CpuStressPolicy.NextEffectivePercent(75, 50, 89, 90) <= 5);
Check("para no limite", CpuStressPolicy.NextEffectivePercent(50, 40, 90, 90) == 0);
Check("falha fechada sem sensor", CpuStressPolicy.NextEffectivePercent(50, 40, null, 90) == 0);
Check("não permite carga superior a 100", CpuStressPolicy.NextEffectivePercent(200, 95, 65, 90) <= 100);
Check("50 porcento gera 50 ms por janela", CpuStressPolicy.ActiveMilliseconds(50, 100) == 50);
Check("25 porcento gera 25 ms por janela", CpuStressPolicy.ActiveMilliseconds(25, 100) == 25);
Check("threads têm fases diferentes", Enumerable.Range(0, 4).Select(i => CpuStressPolicy.PhaseOffsetMilliseconds(i, 4, 100)).SequenceEqual(new[] { 0, 25, 50, 75 }));
Check("sensor recém-atualizado está válido", !CpuStressPolicy.IsSensorStale(TimeSpan.FromSeconds(4)));
Check("sensor atrasado é inválido", CpuStressPolicy.IsSensorStale(TimeSpan.FromSeconds(5.1)));
