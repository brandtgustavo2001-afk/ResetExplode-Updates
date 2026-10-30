using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ResetExplode;

public partial class MainWindow
{
    private readonly AfterburnerMonitor _afterburner=new();
    private readonly RtssOverlay _rtss=new();
    private AfterburnerSnapshot _msiSnapshot=AfterburnerSnapshot.Empty;
    private bool _monitoringOptionsReady;
    private long _monitoringUiAt;

    private void InitMonitoringIntegration()
    {
        OvEngine.SelectedIndex=_settings.OverlayEngine=="Window"?1:0;
        OvMsiSensors.IsChecked=_settings.OverlayUseAfterburner;
        _monitoringOptionsReady=true;
        RefreshMonitoringIntegration();
        Closed+=(_,_)=>_rtss.Dispose();
    }
    private void RefreshMonitoringIntegration()
    {
        if(Environment.TickCount64-_monitoringUiAt<1000)return;
        _monitoringUiAt=Environment.TickCount64;
        if(_settings.OverlayUseAfterburner || PageOverlay.IsVisible)_msiSnapshot=_afterburner.Read();
        else _msiSnapshot=AfterburnerSnapshot.Empty;
        if(PageOverlay.IsVisible) {
            OvMsiStatus.Text=_afterburner.Status;
            OvExternalSensors.ItemsSource=_msiSnapshot.Entries;
            OvRtssStatus.Text=_settings.OverlayEngine=="Window"?"Modo janela: funciona sobre jogos em janela sem bordas.":_rtss.Status;
        }
    }
    private void OvIntegration_Changed(object sender,RoutedEventArgs e)
    {
        if(!_monitoringOptionsReady)return;
        _settings.OverlayEngine=OvEngine.SelectedIndex==1?"Window":"Auto";
        _settings.OverlayUseAfterburner=OvMsiSensors.IsChecked==true;
        _settings.Save();_rtss.Clear();_monitoringUiAt=0;
        if (ChkOverlay.IsChecked == true) EnsureRtssStarted();
        RefreshMonitoringIntegration();UpdateOverlay();
    }
    private void EnsureRtssStarted()
    {
        if (_settings.OverlayEngine != "Auto") return;
        var processes = Process.GetProcessesByName("RTSS");
        try { if (processes.Length > 0) return; }
        finally { foreach (var process in processes) process.Dispose(); }
        string file=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"RivaTuner Statistics Server","RTSS.exe");
        if (!System.IO.File.Exists(file)) return;
        try { Process.Start(new ProcessStartInfo(file){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden}); }
        catch(Exception ex) { AppDiagnostics.Log("Iniciar RTSS para overlay",ex); }
    }
    private void OvResetMsi_Click(object sender,RoutedEventArgs e){_afterburner.ResetExtrema();_monitoringUiAt=0;RefreshMonitoringIntegration();}
    private void OvOpenRtss_Click(object sender,RoutedEventArgs e)
    {
        string file=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"RivaTuner Statistics Server","RTSS.exe");
        if(!System.IO.File.Exists(file)){OvRtssStatus.Text="RTSS não encontrado. Instale o RivaTuner Statistics Server pelo instalador oficial do MSI Afterburner.";return;}
        try { Process.Start(new ProcessStartInfo(file){UseShellExecute=true}); }
        catch(Exception ex){AppDiagnostics.Log("Abrir RTSS",ex);OvRtssStatus.Text="Não foi possível abrir o RTSS: "+ex.Message;}
    }
    private string BuildRtssText(FrameStats? frames,IReadOnlyList<OverlayLine> lines,string game,RtssFrameSample? rtssFrames,IntPtr window)
    {
        var rows=OverlayPresentation.Rows(_settings,lines,frames,rtssFrames,OvShow,game,DateTime.Now.ToString("HH:mm"),_overlayMetricValues);
        var plan=OverlayPresentation.Plan(_settings,rows);
        var size=GameDetection.ClientSize(window);
        return OverlayLayout.Rtss(plan,_settings.OverlayFont,_settings.OverlayFontSize,_settings.OverlayDesign.Bold,_settings.OverlayScale,
            _settings.OverlayCorner,_settings.OverlayOffsetX,_settings.OverlayOffsetY,size.Width,size.Height,
            _settings.OverlayDesign.Background,_settings.OverlayBackgroundOpacity,_settings.OverlayBorder);
    }
}
