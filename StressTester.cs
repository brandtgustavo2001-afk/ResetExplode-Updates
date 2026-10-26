using System.Diagnostics;
using System.Management;

namespace ResetExplode;

/// <summary>Gera carga sintética de CPU (todos os threads) e/ou RAM (escrita + verificação).</summary>
public sealed class StressTester
{
    private CancellationTokenSource? _cts;
    private readonly List<Thread> _threads = new();
    private long _memErrors;
    private long _memPasses;
    private bool _cpuEnabled;
    private int _requestedCpuPercent;
    private double _effectiveCpuPercent;
    private long _lastCpuSensorTicks;
    private string? _cpuSafetyIssue;

    public bool Running => _cts != null;
    public long MemoryErrors => Interlocked.Read(ref _memErrors);
    public long MemoryPasses => Interlocked.Read(ref _memPasses);
    public long MemoryAllocatedBytes { get; private set; }
    public string? Error { get; private set; }
    public double EffectiveCpuLoadPercent => Volatile.Read(ref _effectiveCpuPercent);
    public string? CpuSafetyIssue => Volatile.Read(ref _cpuSafetyIssue);

    /// <param name="cpu">Carga em todos os processadores lógicos.</param>
    /// <param name="ram">Aloca uma fração da RAM livre e grava/verifica padrões continuamente.</param>
    /// <param name="ramFraction">Fração da memória livre a usar (0.1 a 0.9).</param>
    public void Start(bool cpu, bool ram, double ramFraction = 0.6, double cpuFraction = .25, int? threadLimit = null)
    {
        if (Running) return;
        if (!double.IsFinite(cpuFraction) || cpuFraction <= 0 || cpuFraction > 1) throw new ArgumentOutOfRangeException(nameof(cpuFraction));
        int cpuLoadPercent = (int)Math.Round(cpuFraction * 100);
        Error = null;
        _cpuEnabled = cpu;
        _requestedCpuPercent = Math.Clamp(cpuLoadPercent, 1, 100);
        Volatile.Write(ref _effectiveCpuPercent, cpu ? Math.Min(10, _requestedCpuPercent) : 0);
        Volatile.Write(ref _cpuSafetyIssue, null);
        Interlocked.Exchange(ref _lastCpuSensorTicks, Stopwatch.GetTimestamp());
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        Interlocked.Exchange(ref _memErrors, 0);
        Interlocked.Exchange(ref _memPasses, 0);
        MemoryAllocatedBytes = 0;

        int cpuThreads = Math.Clamp(threadLimit ?? Environment.ProcessorCount, 1, Environment.ProcessorCount);
        // Com RAM ativa, reserva um thread para o worker de memória.
        if (cpu && ram && threadLimit == null && cpuThreads > 1) cpuThreads--;

        if (cpu)
            for (int i = 0; i < cpuThreads; i++)
            {
                int seed = i;
                StartThread(() => CpuWorker(token, seed, cpuThreads), $"Stress CPU {seed}");
            }

        if (ram)
        {
            long bytes = (long)(FreePhysicalMemory() * Math.Clamp(ramFraction, 0.1, 0.9));
            MemoryAllocatedBytes = bytes;
            StartThread(() => RamWorker(token, bytes), "Stress RAM");
        }
    }

    /// <summary>Recebe apenas leituras recentes do monitor de hardware.</summary>
    public void UpdateCpuTemperature(float? temperature, double stopTemperature)
    {
        if (!Running || !_cpuEnabled) return;
        if (!temperature.HasValue || !float.IsFinite(temperature.Value) || temperature.Value <= 0)
        {
            // Nenhuma carga enquanto a leitura não for válida; timeout aborta a execução.
            Volatile.Write(ref _effectiveCpuPercent, 0);
            return;
        }

        Interlocked.Exchange(ref _lastCpuSensorTicks, Stopwatch.GetTimestamp());
        if (temperature.Value >= stopTemperature)
        {
            AbortCpu($"CPU chegou a {temperature.Value:0.#} °C (limite {stopTemperature:0.#} °C)");
            return;
        }

        Volatile.Write(ref _effectiveCpuPercent,
            CpuStressPolicy.NextEffectivePercent(_requestedCpuPercent, EffectiveCpuLoadPercent,
                temperature.Value, stopTemperature));
    }

    private void AbortCpu(string reason)
    {
        Interlocked.CompareExchange(ref _cpuSafetyIssue, reason, null);
        Volatile.Write(ref _effectiveCpuPercent, 0);
        _cts?.Cancel();
    }

    public void Stop()
    {
        if (_cts == null) return;
        _cts.Cancel();
        foreach (var t in _threads) t.Join(2000);
        _threads.Clear();
        _cts.Dispose();
        _cts = null;
        _cpuEnabled = false;
        GC.Collect();
    }

    private void StartThread(ThreadStart body, string name)
    {
        // BelowNormal mantém a interface responsiva sem reduzir a carga (núcleos continuam a 100%).
        var t = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex)
            {
                Error = ex.Message;
                AppDiagnostics.Log(name, ex);
                _cts?.Cancel();
            }
        }) { IsBackground = true, Name = name, Priority = ThreadPriority.BelowNormal };
        _threads.Add(t);
        t.Start();
    }

    private void CpuWorker(CancellationToken token, int seed, int workerCount)
    {
        // Espalha o início dos workers dentro da janela: 50% não liga todos os
        // núcleos ao mesmo tempo, o que evita picos artificiais de potência.
        int stagger = CpuStressPolicy.PhaseOffsetMilliseconds(seed, workerCount,
            CpuStressPolicy.WindowMilliseconds);
        if (token.WaitHandle.WaitOne(stagger)) return;

        // Trabalho aritmético moderado, sem FMA/sqrt/sin em massa.
        double value = seed + 1.25;
        ulong x = (ulong)(seed + 1) * 0x9E3779B97F4A7C15UL;
        while (!token.IsCancellationRequested)
        {
            if (CpuStressPolicy.IsSensorStale(Stopwatch.GetElapsedTime(
                    Interlocked.Read(ref _lastCpuSensorTicks))))
            {
                AbortCpu("leitura de temperatura da CPU ficou mais de 5 segundos sem atualizar");
                break;
            }

            int activeMs = CpuStressPolicy.ActiveMilliseconds(EffectiveCpuLoadPercent,
                CpuStressPolicy.WindowMilliseconds);
            long until = Stopwatch.GetTimestamp()
                + (long)(activeMs * (double)Stopwatch.Frequency / 1000);

            while (!token.IsCancellationRequested && Stopwatch.GetTimestamp() < until)
            {
                for (int i = 0; i < 256; i++)
                {
                    x ^= x << 13; x ^= x >> 7; x ^= x << 17;
                    value = (value * 1.0000001 + (x & 1023) * 0.0001) % 1000000.0;
                }
            }

            int restMs = CpuStressPolicy.WindowMilliseconds - activeMs;
            if (restMs > 0 && token.WaitHandle.WaitOne(restMs)) break;
        }
        GC.KeepAlive(value + x); // garante que os cálculos não sejam eliminados
    }

    private void RamWorker(CancellationToken token, long totalBytes)
    {
        const int chunkLongs = 16 * 1024 * 1024; // 128 MB por bloco
        var chunks = new List<long[]>();
        try
        {
            long allocated = 0;
            try
            {
                while (allocated + chunkLongs * 8L <= totalBytes && !token.IsCancellationRequested)
                {
                    chunks.Add(new long[chunkLongs]);
                    allocated += chunkLongs * 8L;
                }
            }
            catch (OutOfMemoryException)
            {
                // Segue com o que conseguiu alocar.
            }
            MemoryAllocatedBytes = allocated;
            if (chunks.Count == 0) return;

            ulong pass = 0;
            while (!token.IsCancellationRequested)
            {
                pass++;
                long pattern = unchecked((long)(pass * 0x9E3779B97F4A7C15UL));
                foreach (var c in chunks)
                {
                    for (int i = 0; i < c.Length; i++) c[i] = pattern ^ i;
                    if (token.IsCancellationRequested) return;
                }
                foreach (var c in chunks)
                {
                    long errs = 0;
                    for (int i = 0; i < c.Length; i++)
                        if (c[i] != (pattern ^ i)) errs++;
                    if (errs > 0) Interlocked.Add(ref _memErrors, errs);
                    if (token.IsCancellationRequested) return;
                }
                Interlocked.Increment(ref _memPasses);
            }
        }
        finally
        {
            chunks.Clear();
        }
    }

    private static long FreePhysicalMemory()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject mo in s.Get())
                return Convert.ToInt64(mo["FreePhysicalMemory"]) * 1024; // vem em KB
        }
        catch { }
        return 1L << 30;
    }
}
