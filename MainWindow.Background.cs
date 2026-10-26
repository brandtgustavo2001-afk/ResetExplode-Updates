using System.Windows;
using System.Windows.Threading;

namespace ResetExplode;

public partial class MainWindow
{
    private TrayIcon? _trayIcon;
    private bool _exitRequested;
    private void InitBackgroundMode()
    {
        SourceInitialized += (_, _) =>
        {
            try
            {
                _trayIcon = new TrayIcon(this, RestoreFromTray, ExitFromTray,
                    () => { RestoreFromTray(); NavUpdates.IsChecked = true; }, () => _exitRequested = true);
            }
            catch (Exception ex) { AppDiagnostics.Log("Criar ícone da bandeja", ex); }
        };
        Closing += (_, e) =>
        {
            if (_exitRequested || _trayIcon?.Available != true || _preparedUpdate != null
                || TestRunning || _ocRunning || _gpuBenchActive || _benchTimer?.IsEnabled == true) return;
            e.Cancel = true; HideToTray();
        };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized && _trayIcon?.Available == true) HideToTray(); };
        Closed += (_, _) => _trayIcon?.Dispose();
        Loaded += async (_, _) =>
        {
            if (_settings.StartWithWindows && SensorMonitor.IsElevated
                && string.Equals(System.IO.Path.GetFileName(Environment.ProcessPath), "ResetExplode.exe", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await Task.Run(() => ResetExplode.EnergyAutostart.SetEnabled(true));
                    if (!_closed) EnergyAutostart.IsChecked = true;
                }
                catch (Exception ex) { AppDiagnostics.Log("Ativar início em segundo plano", ex); }
            }
        };
    }
    public void HideToTray()
    {
        if (_trayIcon?.Available != true) return;
        ShowInTaskbar = false; Hide();
    }
    public void RestoreFromTray()
    {
        Opacity = 1; ShowInTaskbar = true; ShowActivated = true;
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show(); Activate();
    }
    public void ExitFromTray() { _exitRequested = true; Close(); }
    public void StartInBackground()
    {
        // Let Loaded initialize collectors and updates while the first frame remains invisible.
        Opacity = 0; ShowActivated = false; ShowInTaskbar = false;
        RoutedEventHandler? loaded = null;
        loaded = (_, _) =>
        {
            Loaded -= loaded;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_trayIcon?.Available == true) { HideToTray(); Opacity = 1; }
                else RestoreFromTray(); // Never leave an unreachable process if Explorer has no tray.
            }), DispatcherPriority.Background);
        };
        Loaded += loaded;
        Show();
    }
}
