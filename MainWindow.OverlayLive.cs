using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace ResetExplode;

public partial class MainWindow
{
    private double _previousOverlayOpacity=35;
    private bool _syncingTransparency;
    private void OvTransparent_Changed(object sender,RoutedEventArgs e)
    {
        if(!_overlayReady||_syncingTransparency)return;
        _syncingTransparency=true;
        try {
            if(OvTransparent.IsChecked==true){if(OvOpacity.Value>0)_previousOverlayOpacity=OvOpacity.Value;OvOpacity.Value=0;}
            else if(OvOpacity.Value==0)OvOpacity.Value=_previousOverlayOpacity;
        }finally{_syncingTransparency=false;}
        QueueLiveAppearance();
    }
    private void OvOpacity_Changed(object sender,RoutedPropertyChangedEventArgs<double> e)
    {
        if(!_overlayReady||_syncingTransparency||OvTransparent is null)return;
        _syncingTransparency=true;
        try {OvTransparent.IsChecked=e.NewValue==0;if(e.NewValue>0)_previousOverlayOpacity=e.NewValue;}
        finally{_syncingTransparency=false;}
    }
    private DispatcherTimer? _liveAppearanceTimer,_saveAppearanceTimer;
    private bool _liveAppearanceBusy,_liveAppearanceWired;
    private void InitLiveOverlayControls()
    {
        if(_liveAppearanceWired)return;_liveAppearanceWired=true;
        _liveAppearanceTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(150)};
        _liveAppearanceTimer.Tick+=(_,_)=> {
            _liveAppearanceTimer.Stop();if(!_overlayReady||_closed)return;
            _liveAppearanceBusy=true;
            try { if(TryApplyOverlayAppearance(save:false,live:true)) {
                _saveAppearanceTimer!.Stop();_saveAppearanceTimer.Start();
            }}finally{_liveAppearanceBusy=false;}
        };
        _saveAppearanceTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(800)};
        _saveAppearanceTimer.Tick+=(_,_)=> {_saveAppearanceTimer.Stop();_settings.Save();};
        OvAppearancePanel.AddHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler((_,_)=>QueueLiveAppearance()));
        OvAppearancePanel.AddHandler(RangeBase.ValueChangedEvent,new RoutedPropertyChangedEventHandler<double>((_,_)=>QueueLiveAppearance()));
        OvAppearancePanel.AddHandler(ToggleButton.CheckedEvent,new RoutedEventHandler((_,_)=>QueueLiveAppearance()));
        OvAppearancePanel.AddHandler(ToggleButton.UncheckedEvent,new RoutedEventHandler((_,_)=>QueueLiveAppearance()));
        OvAppearancePanel.AddHandler(Selector.SelectionChangedEvent,new SelectionChangedEventHandler((_,e)=> {
            if(e.OriginalSource is ComboBox)QueueLiveAppearance();
        }));
        Closing+=(_,_)=> {if(_saveAppearanceTimer.IsEnabled){_saveAppearanceTimer.Stop();_settings.Save();}};
        Closed+=(_,_)=> {_liveAppearanceTimer.Stop();_saveAppearanceTimer.Stop();};
    }
    private void QueueLiveAppearance()
    {
        if(!_overlayReady||_liveAppearanceBusy||_liveAppearanceTimer is null||_closed)return;
        if(!_liveAppearanceTimer.IsEnabled)_liveAppearanceTimer.Start();
    }
    private void EditingOverlayRowChanged(object? sender,PropertyChangedEventArgs e)=>QueueLiveAppearance();
    private void SetEditingOverlayRows(List<OverlayRowStyle> rows)
    {
        foreach(var row in _editingOverlayRows)row.PropertyChanged-=EditingOverlayRowChanged;
        _editingOverlayRows=rows;
        foreach(var row in rows)row.PropertyChanged+=EditingOverlayRowChanged;
        OvRowStyles.ItemsSource=rows;OvEasyRow.ItemsSource=rows;OvEasyRow.SelectedIndex=0;
    }
    private void OvKeepUnfocused_Changed(object sender,RoutedEventArgs e)
    {
        if(!_overlayReady)return;
        _settings.OverlayKeepWhenUnfocused=OvKeepUnfocused.IsChecked==true;_settings.Save();UpdateOverlay();
    }
    private void OvEasyPreset_Click(object sender,RoutedEventArgs e)
    {
        string preset=(string)((FrameworkElement)sender).Tag;
        _settings.OverlayDesign=new(){FpsFirst=true};
        _settings.OverlayFont="Consolas";_settings.OverlayFontSize=preset=="large"?22:14;
        _settings.OverlayBackgroundOpacity=preset=="minimal"?0:.35;
        _settings.OverlayBigFps=preset=="large";
        _settings.OverlayItems=preset=="minimal"?new(){"fps","frametime"}:OverlayMetrics.Defaults().ToList();
        _ovShow=_settings.OverlayItems.ToHashSet();_settings.Save();BuildOverlayPicker();InitOverlayCustomization();ApplyOverlayOptions();UpdateOverlay();
        OvAppearanceStatus.Text="Estilo aplicado e salvo. Continue ajustando como preferir.";
    }
}
