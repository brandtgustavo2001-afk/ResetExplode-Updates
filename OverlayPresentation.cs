using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ResetExplode;

public static class OverlayPresentation
{
    public static IReadOnlyDictionary<string,string> Examples(IReadOnlySet<string> show)=>new Dictionary<string,string> {
        ["gpu_load"]="97%",["gpu_temp"]="65 °C",["gpu_hotspot"]="HS 76 °C",["gpu_clock"]="2887 MHz",["gpu_memclock"]="mem 14000 MHz",
        ["gpu_power"]="185 W",["gpu_volt"]="1,050 V",["gpu_fan"]="1400 RPM",["gpu_perfcap"]="Power",
        ["vram_used"]="6,2 GB",["vram_pct"]="52%",["vram_temp"]="60 °C",["cpu_load"]="42%",["cpu_maxcore"]="núcleo 70%",
        ["cpu_temp"]="62 °C",["cpu_clock"]="4,80 GHz",["cpu_clockmax"]="pico 5,20 GHz",["cpu_power"]="98 W",["ram_used"]="12,4 GB",["ram_pct"]="39%"
    }.Where(p=>show.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value);
    private static readonly Dictionary<(string,double,string,bool),(double,double)> Measurements=new();
    public static List<OverlayTextRow> Rows(AppSettings settings,IReadOnlyList<OverlayLine> lines,FrameStats? frames,
        RtssFrameSample? rtss,IReadOnlySet<string> show,string? game,string? clock,IReadOnlyDictionary<string,string>? metrics=null)
    {
        var rows=lines.Select(l=>new OverlayTextRow(l.Label,l.Label,l.Value)).ToList();
        var fps=new List<string>();
        if(show.Contains("fps"))fps.Add(frames is not null||rtss is not null?$"{rtss?.Fps??frames!.Fps:0} FPS":"— FPS");
        if(show.Contains("frametime"))fps.Add(frames is not null||rtss is not null?$"{rtss?.FrameTimeMs??frames!.FrameTimeMs:0.0} ms":"— ms");
        if(show.Contains("low1") && frames is not null)fps.Add($"1% low {frames.Low1Fps:0}");
        if(fps.Count>0)rows.Add(new("FPS","",string.Join("  ",fps)));
        if(show.Contains("game") && game is not null)rows.Add(new("GAME","",settings.OverlayDesign.GameAlias.Length>0?settings.OverlayDesign.GameAlias:
            game is "javaw" or "java" or "Minecraft.Windows"?"Minecraft":game));
        if(show.Contains("clock") && clock is not null)rows.Add(new("CLOCK","",clock));
        var values=metrics?.ToDictionary(p=>p.Key,p=>p.Value)??new Dictionary<string,string>();
        if(show.Contains("fps"))values["fps"]=frames is not null||rtss is not null?$"{rtss?.Fps??frames!.Fps:0} FPS":"—";
        if(show.Contains("frametime"))values["frametime"]=frames is not null||rtss is not null?$"{rtss?.FrameTimeMs??frames!.FrameTimeMs:0.0} ms":"—";
        if(show.Contains("low1")&&frames is not null)values["low1"]=$"1% low {frames.Low1Fps:0}";
        if(show.Contains("game"))values["game"]=rows.FirstOrDefault(r=>r.Key=="GAME")?.Value??"";
        if(show.Contains("clock"))values["clock"]=clock??"";
        return rows.Select(r=>r with {Metrics=values}).ToList();
    }
    public static OverlayRenderPlan Plan(AppSettings settings,IReadOnlyList<OverlayTextRow> rows)
    {
        (double,double) Measure(string text,double size) {
            var key=(text,size,settings.OverlayFont,settings.OverlayDesign.Bold);
            if(Measurements.TryGetValue(key,out var cached))return cached;
            var format=new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,
                new Typeface(new FontFamily(settings.OverlayFont),FontStyles.Normal,settings.OverlayDesign.Bold?FontWeights.Bold:FontWeights.Normal,FontStretches.Normal),
                size,Brushes.White,1);
            var measured=(Math.Ceiling(format.WidthIncludingTrailingWhitespace),Math.Ceiling(format.Height));
            if(Measurements.Count>=1024)Measurements.Clear();
            Measurements[key]=measured;return measured;
        }
        return OverlayLayout.Arrange(settings.OverlayDesign,rows,settings.OverlayFontSize,settings.OverlayComponentColors,
            settings.OverlayColor,settings.OverlayBigFps,Measure);
    }
    public static Canvas Canvas(AppSettings settings,OverlayRenderPlan plan)
    {
        var canvas=new Canvas {Width=plan.Width,Height=plan.Height};
        foreach(var row in plan.Rows) {
            var text=new TextBlock {Text=row.Text,FontFamily=new FontFamily(settings.OverlayFont),FontSize=row.FontSize,
                FontWeight=settings.OverlayDesign.Bold?FontWeights.Bold:FontWeights.Normal,
                Foreground=new SolidColorBrush((Color)ColorConverter.ConvertFromString(row.Color)),
                Effect=new DropShadowEffect {Color=Colors.Black,BlurRadius=3,ShadowDepth=1,Opacity=1}};
            System.Windows.Controls.Canvas.SetLeft(text,row.X+8);System.Windows.Controls.Canvas.SetTop(text,row.Y+6);canvas.Children.Add(text);
        }
        return canvas;
    }
}
