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
        var application=new Application();
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
