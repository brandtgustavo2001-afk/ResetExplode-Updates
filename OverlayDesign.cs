using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ResetExplode;

public sealed class OverlayRowStyle : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private string _label="",_color="#FFFFFF",_template="{label}  {value}";
    private double _size=100,_x,_y;
    private void Set<T>(ref T field,T value,[System.Runtime.CompilerServices.CallerMemberName]string? name=null) {
        if(EqualityComparer<T>.Default.Equals(field,value))return;field=value;PropertyChanged?.Invoke(this,new(name));
    }
    public string Key {get;set;}="";
    public string Label {get=>_label;set=>Set(ref _label,value);}
    public string Color {get=>_color;set=>Set(ref _color,value);}
    public double Size {get=>_size;set=>Set(ref _size,value);}
    public double X {get=>_x;set=>Set(ref _x,value);}
    public double Y {get=>_y;set=>Set(ref _y,value);}
    public string Template {get=>_template;set=>Set(ref _template,value);}
    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayName=>Key switch {"GPU"=>"Placa de vídeo", "VRAM"=>"Memória de vídeo", "CPU"=>"Processador", "RAM"=>"Memória RAM", "FPS"=>"FPS e fluidez", "GAME"=>"Nome do jogo", _=>"Relógio"};
}
/// <summary>Shared layout model for the RTSS publisher, desktop fallback and preview.</summary>
public sealed class OverlayDesign
{
    public bool Bold { get; set; } = true;
    public bool FpsFirst { get; set; }
    public bool FreeLayout { get; set; }
    public double LineSpacing { get; set; } = 3;
    public string Background { get; set; } = "#000000";
    public string Separator { get; set; } = "  ";
    public string GameAlias { get; set; } = "";
    public List<OverlayRowStyle> Rows { get; set; } = Defaults();
    public static List<OverlayRowStyle> Defaults() => new[] {
        ("GPU","GPU","#80FF60"),("VRAM","VRAM","#70E7FF"),("CPU","CPU","#FFB060"),
        ("RAM","RAM","#E2A0FF"),("FPS","","#FFFF66"),("GAME","","#F5F5F5"),("CLOCK","","#F5F5F5")
    }.Select((r,i)=>new OverlayRowStyle {Key=r.Item1,Label=r.Item2,Color=r.Item3,Y=i*26}).ToList();
    public static bool IsColor(string? color)=>Regex.IsMatch(color??"", "^#[0-9a-fA-F]{6}$");
    public static bool IsFont(string? font)=>Regex.IsMatch(font??"", @"^[A-Za-z0-9 ._-]{1,64}$");
    public static string Clean(string? text,int max=128)=>new((text??"").Where(c=>!char.IsControl(c)&&c!='<'&&c!='>').Take(max).ToArray());
    public static OverlayDesign Normalize(OverlayDesign? design)
    {
        design ??= new();
        design.LineSpacing=double.IsFinite(design.LineSpacing)?Math.Clamp(design.LineSpacing,0,40):3;
        if(!IsColor(design.Background))design.Background="#000000";
        design.Separator=design.Separator is " | " or " / " or "  "?design.Separator:"  ";
        design.GameAlias=Clean(design.GameAlias,64);
        var supplied=design.Rows??new();
        design.Rows=Defaults().Select(fallback=> {
            var row=supplied.FirstOrDefault(r=>r is not null && r.Key==fallback.Key)??fallback;
            row.Label=Clean(row.Label,24);
            if(!IsColor(row.Color))row.Color=fallback.Color;
            row.Size=double.IsFinite(row.Size)?Math.Clamp(row.Size,50,250):100;
            row.X=double.IsFinite(row.X)?Math.Clamp(row.X,0,4000):0;
            row.Y=double.IsFinite(row.Y)?Math.Clamp(row.Y,0,4000):0;
            row.Template=Clean(row.Template,160);
            return row;
        }).ToList();
        return design;
    }
}

public sealed record OverlayTextRow(string Key,string Label,string Value,IReadOnlyDictionary<string,string>? Metrics=null);
public sealed record OverlayPlacedRow(string Key,string Text,string Color,double FontSize,double X,double Y,double Width,double Height);
public sealed record OverlayRenderPlan(IReadOnlyList<OverlayPlacedRow> Rows,double Width,double Height);

public static class OverlayLayout
{
    public static OverlayRenderPlan Arrange(OverlayDesign design,IReadOnlyList<OverlayTextRow> data,double fontSize,
        bool componentColors,string color,bool bigFps,Func<string,double,(double Width,double Height)> measure)
    {
        var rows=new List<OverlayPlacedRow>();double cursor=0,width=0,height=0;
        var ordered=design.FpsFirst?data.OrderBy(r=>r.Key=="FPS"?0:1):data.AsEnumerable();
        foreach(var dataRow in ordered) {
            var style=design.Rows.First(r=>r.Key==dataRow.Key);
            string value=OverlayDesign.Clean(dataRow.Value,512).Replace("  ",design.Separator);
            string text=Regex.Replace(style.Template,@"\{([a-z0-9_]+)\}",match=>match.Groups[1].Value switch {
                "label"=>style.Label,"value"=>value,var key=>dataRow.Metrics?.GetValueOrDefault(key)??"—"
            }).Trim();
            text=OverlayDesign.Clean(text,320);
            if(text.Length==0)continue;
            double size=fontSize*style.Size/100*(dataRow.Key=="FPS"&&bigFps?1.5:1);
            var extent=measure(text,size);
            double x=design.FreeLayout?style.X:0,y=design.FreeLayout?style.Y:cursor;
            rows.Add(new(dataRow.Key,text,componentColors?style.Color:color,size,x,y,extent.Width,extent.Height));
            width=Math.Max(width,x+extent.Width);height=Math.Max(height,y+extent.Height);
            cursor=y+extent.Height+design.LineSpacing;
        }
        return new(rows,Math.Ceiling(width+16),Math.Ceiling(height+12));
    }
    public static string Rtss(OverlayRenderPlan plan,string font,double baseFontSize,bool bold,double scale,string corner,double offsetX,double offsetY,
        int viewportWidth,int viewportHeight,string background,double opacity,bool border)
    {
        static string N(double v)=>Math.Round(v).ToString(CultureInfo.InvariantCulture);
        double w=plan.Width*scale,h=plan.Height*scale;
        double x=corner.EndsWith('R')?viewportWidth-w-offsetX:offsetX;
        double y=corner.StartsWith('B')?viewportHeight-h-offsetY:offsetY;
        x=Math.Clamp(x,0,Math.Max(0,viewportWidth-w));y=Math.Clamp(y,0,Math.Max(0,viewportHeight-h));
        var text=new StringBuilder();
        text.Append("<FNT=").Append(font).Append(",-").Append(N(baseFontSize*scale)).Append(',').Append(bold?700:400).Append(",1>");
        // Official OverlayEditor syntax: each positioned layer carries its own extent and font.
        text.Append("<P=").Append(N(x)).Append(',').Append(N(y)).Append("><L0><E=").Append(N(w)).Append(',').Append(N(h)).Append('>');
        if(opacity>0)text.Append("<C=").Append(((byte)Math.Round(Math.Clamp(opacity,0,1)*255)).ToString("X2")).Append(background.TrimStart('#')).Append("><B=0,0>\b<C>");
        if(border)text.Append("<C=FF808080><B=0,0,1>\b<C>");
        text.Append('\r');int layer=1;
        foreach(var row in plan.Rows) {
            text.Append("<P=").Append(N(x+(row.X+8)*scale)).Append(',').Append(N(y+(row.Y+6)*scale)).Append("><L").Append(layer++).Append('>');
            text.Append("<S=").Append(N(row.FontSize/baseFontSize*100)).Append("><C=").Append(row.Color.TrimStart('#')).Append('>').Append(row.Text).Append("<C><S>\r");
        }
        return text.Append("<FNT>").ToString();
    }
}
