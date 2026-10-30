using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ResetExplode;

public partial class MainWindow
{
    private List<OverlayRowStyle> _editingOverlayRows=new();
    private void InitOverlayDesigner()
    {
        var design=_settings.OverlayDesign;
        SetEditingOverlayRows(JsonSerializer.Deserialize<List<OverlayRowStyle>>(JsonSerializer.Serialize(design.Rows))!);
        OvBackground.Text=design.Background;OvLineSpacing.Text=design.LineSpacing.ToString();
        OvBold.IsChecked=design.Bold;OvFpsFirst.IsChecked=design.FpsFirst;OvFreeLayout.IsChecked=design.FreeLayout;
        OvSeparator.SelectedIndex=design.Separator==" | "?1:design.Separator==" / "?2:0;
        OvGameAlias.Text=design.GameAlias;
        OvTemplateHelp.Text=string.Join("   ",OverlayMetrics.All.Where(m=>m.Key!="graph").Select(m=>"{"+m.Key+"} · "+m.Label));
    }
    private bool TryReadOverlayDesigner(out OverlayDesign design,bool live=false)
    {
        design=new();
        if(!live){OvRowStyles.CommitEdit(DataGridEditingUnit.Cell,true);OvRowStyles.CommitEdit(DataGridEditingUnit.Row,true);}
        static bool HasError(DependencyObject element) {
            if(Validation.GetHasError(element))return true;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(element);i++)if(HasError(VisualTreeHelper.GetChild(element,i)))return true;
            return false;
        }
        if(HasError(OvRowStyles)) {OvAppearanceStatus.Text="Corrija o número destacado na tabela antes de aplicar.";if(!live)OvRowStyles.Focus();return false;}
        if(!OverlayDesign.IsColor(OvBackground.Text)||!double.TryParse(OvLineSpacing.Text,out var spacing)||!double.IsFinite(spacing)||spacing<0||spacing>40)
        {OvAppearanceStatus.Text="Use uma cor de fundo #RRGGBB e espaçamento de 0 a 40 pixels.";if(!live)OvBackground.Focus();return false;}
        foreach(var row in _editingOverlayRows) {
            if(!OverlayDesign.IsColor(row.Color)||!double.IsFinite(row.Size)||row.Size<50||row.Size>250||!double.IsFinite(row.X)||row.X<0||row.X>4000||!double.IsFinite(row.Y)||row.Y<0||row.Y>4000)
            {OvAppearanceStatus.Text=$"Linha {row.Key}: use cor #RRGGBB, tamanho entre 50 e 250% e X/Y entre 0 e 4000.";OvRowStyles.SelectedItem=row;if(!live)OvRowStyles.Focus();return false;}
        }
        design=OverlayDesign.Normalize(new(){Rows=JsonSerializer.Deserialize<List<OverlayRowStyle>>(JsonSerializer.Serialize(_editingOverlayRows))!,
            Background=OvBackground.Text,LineSpacing=spacing,Bold=OvBold.IsChecked==true,FpsFirst=OvFpsFirst.IsChecked==true,
            FreeLayout=OvFreeLayout.IsChecked==true,Separator=OvSeparator.SelectedIndex==1?" | ":OvSeparator.SelectedIndex==2?" / ":"  ",GameAlias=OvGameAlias.Text});
        return true;
    }
    private void OvResetRows_Click(object sender,RoutedEventArgs e)
    {
        SetEditingOverlayRows(OverlayDesign.Defaults());QueueLiveAppearance();
        OvAppearanceStatus.Text="Linhas redefinidas. Alterações aplicadas automaticamente.";
    }
    private void OvPickColor_Click(object sender,RoutedEventArgs e)
    {
        OvRowStyles.CommitEdit(DataGridEditingUnit.Cell,true);OvRowStyles.CommitEdit(DataGridEditingUnit.Row,true);
        string target=(string)((FrameworkElement)sender).Tag;
        if(target=="EasyLine"){OvRowStyles.SelectedItem=OvEasyRow.SelectedItem;target="Line";}
        if(target=="Line"&&OvRowStyles.SelectedItem is not OverlayRowStyle){OvAppearanceStatus.Text="Selecione uma linha da tabela para escolher sua cor.";return;}
        string old=target=="Line"?((OverlayRowStyle)OvRowStyles.SelectedItem).Color:target=="Background"?OvBackground.Text:OvColor.Text;
        void Preview(string value) {
            if(target=="Line")((OverlayRowStyle)OvRowStyles.SelectedItem).Color=value;
            else if(target=="Background")OvBackground.Text=value;else OvColor.Text=value;
            QueueLiveAppearance();
        }
        var chosen=OverlayColorPicker.Pick(this,OverlayDesign.IsColor(old)?old:"#FFFFFF",Preview);
        if(chosen is null)return;
        if(target=="Line"){((OverlayRowStyle)OvRowStyles.SelectedItem).Color=chosen;OvRowStyles.Items.Refresh();}
        else if(target=="Background")OvBackground.Text=chosen;else OvColor.Text=chosen;
        OvAppearanceStatus.Text="Cor escolhida. Aplicando no jogo…";QueueLiveAppearance();
    }
    private void OvExportStyle_Click(object sender,RoutedEventArgs e)
    {
        if(!TryApplyOverlayAppearance())return;
        var picker=new Microsoft.Win32.SaveFileDialog {Filter="Estilo ResetExplode (*.json)|*.json",FileName="Meu-overlay.json"};
        if(picker.ShowDialog(this)!=true)return;
        try {File.WriteAllText(picker.FileName,OverlayStyleProfile.Export(_settings));OvAppearanceStatus.Text="Estilo exportado. Inclui aparência e métricas selecionadas.";}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){OvAppearanceStatus.Text="Não foi possível exportar: "+ex.Message;}
    }
    private void OvImportStyle_Click(object sender,RoutedEventArgs e)
    {
        var picker=new Microsoft.Win32.OpenFileDialog {Filter="Estilo ResetExplode (*.json)|*.json"};
        if(picker.ShowDialog(this)!=true)return;
        try {
            if(new FileInfo(picker.FileName).Length>65536)throw new InvalidDataException("O estilo deve ter até 64 KB.");
            OverlayStyleProfile.Import(File.ReadAllText(picker.FileName),_settings);
            _ovShow=_settings.OverlayItems?.ToHashSet()??OverlayMetrics.Defaults();_settings.Save();
            BuildOverlayPicker();InitOverlayCustomization();ApplyOverlayOptions();UpdateOverlay();
            OvAppearanceStatus.Text="Estilo importado e aplicado.";
        }catch(Exception ex)when(ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException){OvAppearanceStatus.Text="Não foi possível importar: "+ex.Message;}
    }
}
