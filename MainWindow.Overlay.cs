using System.Windows;
using System.Windows.Controls;
using LibreHardwareMonitor.Hardware;

namespace ResetExplode;

/// <summary>Overlay: o que mostrar (escolhido na aba Overlay) e a montagem das linhas de sensores.</summary>
public partial class MainWindow
{
    private readonly Dictionary<string,string> _overlayMetricValues=new();
    private HashSet<string>? _ovShow;
    private HashSet<string> OvShow => _ovShow ??= _settings.OverlayItems is { } l ? l.ToHashSet() : OverlayMetrics.Defaults();

    /// <summary>Cria as caixas de seleção da aba Overlay, agrupadas por componente.</summary>
    private void BuildOverlayPicker()
    {
        OvItemsPanel.Children.Clear();
        foreach (var group in OverlayMetrics.All.GroupBy(m => m.Group))
        {
            OvItemsPanel.Children.Add(new TextBlock
            {
                Text = group.Key.ToUpperInvariant(), Style = (Style)FindResource("Label"), Margin = new Thickness(0, 10, 0, 4),
            });
            var wrap = new WrapPanel();
            foreach (var m in group)
            {
                var cb = new CheckBox { Content = m.Label, Tag = m.Key, IsChecked = OvShow.Contains(m.Key), Margin = new Thickness(0, 0, 18, 6) };
                cb.Click += (_, _) =>
                {
                    if (cb.IsChecked == true) OvShow.Add(m.Key); else OvShow.Remove(m.Key);
                    SaveOverlayItems();
                };
                wrap.Children.Add(cb);
            }
            OvItemsPanel.Children.Add(wrap);
        }
    }

    private void SaveOverlayItems()
    {
        _settings.OverlayItems = OverlayMetrics.All.Select(m => m.Key).Where(OvShow.Contains).ToList();
        _settings.Save();
        UpdateOverlay();
    }

    private void OvPreset_Click(object sender, RoutedEventArgs e)
    {
        var keys = (string)((FrameworkElement)sender).Tag switch
        {
            "all" => OverlayMetrics.All.Select(m => m.Key).ToHashSet(),
            "min" => new HashSet<string> { "fps", "frametime", "low1" },
            _ => OverlayMetrics.Defaults(),
        };
        _ovShow = keys;
        SaveOverlayItems();
        BuildOverlayPicker();
    }

    // ------------------------------------------------------------------ leitura dos sensores

    private SensorRow? GpuRow(SensorType type, string name, bool startsWith = false) =>
        _gpus.Count == 0 ? null : _monitor.Rows.FirstOrDefault(r => r.Hardware == _gpus[0].Name && r.Type == type &&
                                                                   (startsWith ? r.Name.StartsWith(name) : r.Name == name) && r.RawValue != null);

    private uint? MainGpuClock(int nvmlType, string lhmName)
    {
        if (_gpus.Count > 0 && (nvmlType == Nvml.ClockMem ? _gpus[0].Telemetry.MemoryClock : _gpus[0].Telemetry.Clock) is uint c) return c;
        return GpuRow(SensorType.Clock, lhmName)?.RawValue is float v && v > 0 ? (uint)v : null;
    }

    /// <summary>VRAM usada e total em GB: NVML quando disponível, senão LibreHardwareMonitor (MB).</summary>
    private (double Used, double Total)? MainGpuVram()
    {
        if (_gpus.Count == 0) return null;
        if (_gpus[0].Telemetry.Memory is Nvml.Memory m && m.Total > 0)
            return (m.Used / 1073741824.0, m.Total / 1073741824.0);
        if (GpuRow(SensorType.SmallData, "GPU Memory Used")?.RawValue is float u && GpuRow(SensorType.SmallData, "GPU Memory Total")?.RawValue is float t && t > 0)
            return (u / 1024.0, t / 1024.0);
        return null;
    }

    private (float? Avg, float? Max) CpuClocks()
    {
        var cores = _monitor.Rows.Where(r => r.HardwareType == HardwareType.Cpu && r.Type == SensorType.Clock &&
                                             r.Name.StartsWith("Core #") && !r.Name.Contains("Effective") && r.RawValue > 0)
                                 .Select(r => r.RawValue!.Value).ToList();
        return cores.Count > 0 ? (cores.Average(), cores.Max()) : (null, null);
    }

    /// <summary>Monta as linhas GPU / VRAM / CPU / RAM só com o que foi escolhido; linha vazia não aparece.</summary>
    private List<OverlayLine> BuildOverlayLines(FrameStats? f)
    {
        _overlayMetricValues.Clear();
        void Add(List<string> parts,string key,string value) { _overlayMetricValues[key]=value;parts.Add(value); }
        var show = OvShow;
        var s = BottleneckAnalyzer.Sample(_monitor, _gpus.FirstOrDefault(), f);
        var lines = new List<OverlayLine>();
        var external=_settings.OverlayUseAfterburner?_msiSnapshot:AfterburnerSnapshot.Empty;
        float? G(uint id)=>external.Gpu(id,_gpus.FirstOrDefault()?.Name??"");
        string T(float? v) => v is float x ? $"{x:0} °C" : "—";
        string P(double? v) => v is double x ? $"{x:0}%" : "—";
        void Line(string label, List<string> parts)
        {
            parts.RemoveAll(p => p.Contains('—'));
            if (parts.Count > 0) lines.Add(new OverlayLine(label, string.Join("  ", parts)));
        }

        var gpu = new List<string>();
        if (show.Contains("gpu_load")) Add(gpu,"gpu_load",P(G(0x30)??s.GpuLoad));
        if (show.Contains("gpu_temp")) Add(gpu,"gpu_temp",T(G(0)??s.GpuTemp));
        if (show.Contains("gpu_hotspot")) Add(gpu,"gpu_hotspot","HS " + T(GpuRow(SensorType.Temperature, "GPU Hot Spot")?.RawValue));
        if (show.Contains("gpu_clock")) Add(gpu,"gpu_clock",G(0x20) is float ec ? $"{ec:0} MHz" : MainGpuClock(Nvml.ClockGraphics, "GPU Core") is uint c ? $"{c} MHz" : "— MHz");
        if (show.Contains("gpu_memclock")) Add(gpu,"gpu_memclock",G(0x22) is float emc ? $"mem {emc:0} MHz" : MainGpuClock(Nvml.ClockMem, "GPU Memory") is uint mc ? $"mem {mc} MHz" : "mem —");
        if (show.Contains("gpu_power")) Add(gpu,"gpu_power",(G(0x61)??MainGpuPower()) is float w ? $"{w:0} W" : "— W");
        if (show.Contains("gpu_volt")) Add(gpu,"gpu_volt",GpuRow(SensorType.Voltage, "GPU Core")?.RawValue is float v ? $"{v:0.000} V" : "— V");
        if (show.Contains("gpu_fan"))
            Add(gpu,"gpu_fan",G(0x11) is float erpm ? $"{erpm:0} RPM" : G(0x10) is float epct ? $"fan {epct:0}%" : GpuRow(SensorType.Fan, "GPU Fan", true)?.RawValue is float rpm && rpm > 0 ? $"{rpm:0} RPM"
                  : GpuRow(SensorType.Control, "GPU Fan", true)?.RawValue is float pct ? $"fan {pct:0}%" : "fan —");
        if (show.Contains("gpu_perfcap") && MainGpuPerfCap() is { Length: > 0 } cap) Add(gpu,"gpu_perfcap",cap);
        Line("GPU", gpu);

        var vram = new List<string>();
        var vr = MainGpuVram();
        if (show.Contains("vram_used")) Add(vram,"vram_used",G(0x31) is float em ? $"{em/1024:0.0} GB" : vr is { } a ? $"{a.Used:0.0} GB" : "—");
        if (show.Contains("vram_pct")) Add(vram,"vram_pct",P(s.VramPct));
        if (show.Contains("vram_temp")) Add(vram,"vram_temp",T(GpuRow(SensorType.Temperature, "GPU Memory")?.RawValue));
        Line("VRAM", vram);

        var cpu = new List<string>();
        var (avg, max) = CpuClocks();
        if (show.Contains("cpu_load")) Add(cpu,"cpu_load",P(external.Global(0x90,"CPU usage")??s.CpuTotal));
        if (show.Contains("cpu_maxcore")) Add(cpu,"cpu_maxcore",s.CpuMaxCore is float mx ? $"núcleo {mx:0}%" : "núcleo —");
        if (show.Contains("cpu_temp")) Add(cpu,"cpu_temp",T(external.Global(0x80,"CPU temperature")??s.CpuTemp));
        if (show.Contains("cpu_clock")) Add(cpu,"cpu_clock",(external.Global(0xA0,"CPU clock")??avg) is float ca ? $"{ca / 1000:0.00} GHz" : "— GHz");
        if (show.Contains("cpu_clockmax")) Add(cpu,"cpu_clockmax",max is float cm ? $"pico {cm / 1000:0.00} GHz" : "pico —");
        if (show.Contains("cpu_power")) Add(cpu,"cpu_power",(external.Global(0x100,"CPU power")??_monitor.CpuPower) is float cw && cw > 0 ? $"{cw:0} W" : "— W");
        Line("CPU", cpu);

        var ram = new List<string>();
        var (total, availB) = Optimizer.MemoryStatus();
        if (show.Contains("ram_used")) Add(ram,"ram_used",total > 0 ? $"{(total - availB) / 1073741824.0:0.0} GB" : "—");
        if (show.Contains("ram_pct")) Add(ram,"ram_pct",P(s.RamPct));
        Line("RAM", ram);
        var order = _settings.OverlayOrder.Split(',').Select(s => s.Trim().ToUpperInvariant()).ToList();
        return lines.OrderBy(line => order.IndexOf(line.Label) is var index && index >= 0 ? index : int.MaxValue).ToList();
    }
}
