using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ResetExplode;

static class Preview
{
    [STAThread]
    static void Main(string[] args)
    {
        var styled=new AppSettings {OverlayStyleVersion=2,OverlayFont="Arial",OverlayFontSize=22,OverlayOffsetX=90,HowManyFpsKeyProtected="not-an-actual-key"};
        styled.OverlayDesign.Rows.First(r=>r.Key=="CPU").Label="Processador";
        var exported=OverlayStyleProfile.Export(styled);
        if(exported.Contains("not-an-actual-key")||exported.Contains("HowManyFpsKeyProtected"))throw new Exception("Exportacao contem dados fora da aparencia");
        var imported=new AppSettings {AutomaticUpdates=false,StartWithWindows=false,EnergyPrice=2};
        OverlayStyleProfile.Import(exported,imported);
        if(imported.OverlayFont!="Arial"||imported.OverlayFontSize!=22||imported.OverlayOffsetX!=90||imported.OverlayDesign.Rows.First(r=>r.Key=="CPU").Label!="Processador")throw new Exception("Perfil nao preservou aparencia");
        if(imported.AutomaticUpdates||imported.StartWithWindows||imported.EnergyPrice!=2)throw new Exception("Perfil alterou preferencias globais");
        bool rejected=false;try {OverlayStyleProfile.Import("{}",imported);}catch(InvalidDataException){rejected=true;}
        if(!rejected)throw new Exception("Formato invalido aceito");
        Console.WriteLine("PASS: exportacao sem credenciais, importacao fiel, preferencias preservadas e formato invalido rejeitado.");
        var application=new ResetExplode.App();application.InitializeComponent();ThemeManager.Apply(false);
        if(args.FirstOrDefault()=="--designer") {
            var xml=System.Xml.Linq.XDocument.Load(args[1]);System.Xml.Linq.XNamespace ns="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var source=xml.Descendants(ns+"TextBlock").First(e=>(string?)e.Attribute("Text")=="APARÊNCIA E PRÉVIA").Parent!.Parent!;
            var clone=new System.Xml.Linq.XElement(source);
            foreach(var attr in clone.DescendantsAndSelf().SelectMany(e=>e.Attributes()).Where(a=>new[]{"Click","Checked","Unchecked","SelectionChanged"}.Contains(a.Name.LocalName)).ToArray())attr.Remove();
            var card=(FrameworkElement)System.Windows.Markup.XamlReader.Parse(clone.ToString());
            ((DataGrid)card.FindName("OvRowStyles")).ItemsSource=new OverlayDesign().Rows;
            foreach(var pair in new Dictionary<string,string>{{"OvBackground","#000000"},{"OvLineSpacing","3"},{"OvGameAlias","Minecraft"}})((TextBox)card.FindName(pair.Key)).Text=pair.Value;
            var panel=new Border {Width=1040,Padding=new Thickness(20),Background=(Brush)application.FindResource("Bg"),Child=card};
            panel.SetValue(System.Windows.Documents.TextElement.ForegroundProperty,application.FindResource("Fg"));
            panel.Measure(new Size(1040,double.PositiveInfinity));panel.Arrange(new Rect(panel.DesiredSize));panel.UpdateLayout();
            var capture=new RenderTargetBitmap(1040,(int)Math.Ceiling(panel.ActualHeight),96,96,PixelFormats.Pbgra32);capture.Render(panel);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(capture));using(var stream=File.Create(args[2]))png.Save(stream);
            Console.WriteLine(args[2]);application.Shutdown();return;
        }
        var overlay=new OverlayWindow();
        overlay.ApplyAppearance(new AppSettings {OverlayFont="Consolas",OverlayFontSize=16,OverlayBackgroundOpacity=.22,OverlayComponentColors=true});
        var frames=new FrameStats(0,"PREVIA - valores ilustrativos",144,6.94,118,
            Enumerable.Range(0,80).Select(i=>6.5+Math.Sin(i*1.7)*.7+(i==46?5:0)).ToArray());
        overlay.Update(frames,new[]{new OverlayLine("GPU","97%  65 °C  2887 MHz  185 W"),new OverlayLine("VRAM","6,2 GB"),new OverlayLine("CPU","42%  62 °C  4,80 GHz  98 W"),new OverlayLine("RAM","12,4 GB")},OverlayMetrics.Defaults(),null,frames.ProcessName);
        var root=(FrameworkElement)overlay.Content;overlay.Content=null;
        var preview=new Border {Background=new SolidColorBrush(Color.FromRgb(40,48,54)),Padding=new Thickness(18),Child=root};
        preview.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
        preview.Arrange(new Rect(preview.DesiredSize));preview.UpdateLayout();
        var image=new RenderTargetBitmap((int)Math.Ceiling(preview.ActualWidth),(int)Math.Ceiling(preview.ActualHeight),96,96,PixelFormats.Pbgra32);
        image.Render(preview);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        string output=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"ResetExplode-overlay-preview.png");
        using(var stream=File.Create(output))encoder.Save(stream);
        Console.WriteLine(output);overlay.Close();application.Shutdown();
    }
}
