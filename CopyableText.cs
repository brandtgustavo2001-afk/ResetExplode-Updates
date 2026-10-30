using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ResetExplode;

public static class CopyableText
{
    public static string Read(TextBlock block)=>new TextRange(block.ContentStart,block.ContentEnd).Text.Trim();
    public static void Enable(Window window)
    {
        window.AddHandler(Mouse.PreviewMouseDownEvent,new MouseButtonEventHandler((_,e)=> {
            if(e.ChangedButton!=MouseButton.Right)return;
            DependencyObject? node=e.OriginalSource as DependencyObject;
            while(node is not null && node is not TextBlock) {
                if(node is TextBox)return; // Keep native selection/copy for editable fields.
                node=node is FrameworkContentElement content?content.Parent:node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);
            }
            if(node is TextBlock block && block.ContextMenu is null && Read(block).Length>0)Attach(block);
        }),true);
    }
    public static void Attach(TextBlock block)
    {
        var menu=new ContextMenu();var copy=new MenuItem {Header="Copiar texto"};
        copy.Click+=(_,_)=> {
            try {Clipboard.SetDataObject(Read(block),true);}
            catch(ExternalException) {MessageBox.Show("A área de transferência está ocupada. Tente copiar novamente.","Copiar texto",MessageBoxButton.OK,MessageBoxImage.Information);}
        };
        menu.Items.Add(copy);block.ContextMenu=menu;
    }
}
