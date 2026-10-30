using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ResetExplode;

public static class OverlayColorPicker
{
    public static string? Pick(Window owner,string initial)
    {
        var color=(Color)ColorConverter.ConvertFromString(initial);
        var panel=new StackPanel {Margin=new Thickness(20)};
        var window=new Window {Title="Escolher cor do overlay",Owner=owner,Content=panel,Width=380,SizeToContent=SizeToContent.Height,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,
            Background=(Brush)owner.FindResource("Card"),Foreground=(Brush)owner.FindResource("Fg")};
        var swatch=new Border {Height=60,Margin=new Thickness(0,0,0,12)};panel.Children.Add(swatch);
        var hex=new TextBox {Text=initial,Margin=new Thickness(0,8,0,12),MaxLength=7};
        var sliders=new List<Slider>();string[] names={"Vermelho","Verde","Azul"};byte[] values={color.R,color.G,color.B};
        for(int i=0;i<3;i++) {
            panel.Children.Add(new TextBlock {Text=names[i]});var slider=new Slider {Minimum=0,Maximum=255,Value=values[i],TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(0,4,0,12)};
            sliders.Add(slider);panel.Children.Add(slider);
        }
        void Refresh() {var c=Color.FromRgb((byte)sliders[0].Value,(byte)sliders[1].Value,(byte)sliders[2].Value);swatch.Background=new SolidColorBrush(c);hex.Text=$"#{c.R:X2}{c.G:X2}{c.B:X2}";}
        foreach(var slider in sliders)slider.ValueChanged+=(_,_)=>Refresh();
        panel.Children.Add(new TextBlock {Text="Ou digite #RRGGBB"});panel.Children.Add(hex);
        var error=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)};panel.Children.Add(error);
        var accept=new Button {Content="USAR COR",Padding=new Thickness(16,9,16,9),HorizontalAlignment=HorizontalAlignment.Right,IsDefault=true};
        accept.Click+=(_,_)=> {if(!OverlayDesign.IsColor(hex.Text)){error.Text="Use # seguido de seis caracteres hexadecimais.";hex.Focus();return;}window.DialogResult=true;};
        panel.Children.Add(accept);Refresh();return window.ShowDialog()==true?hex.Text:null;
    }
}
