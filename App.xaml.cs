using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace ResetExplode;

public partial class App : Application
{
    private bool _reporting;

    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            AppDiagnostics.Log("Falha não tratada na interface", e.Exception);
            if (_reporting || e.Exception is OutOfMemoryException) return;
            _reporting = true;
            try
            {
                if (MainWindow is MainWindow main)
                {
                    main.PauseAfterError();
                    e.Handled = true;
                }
                MessageBox.Show("A operação foi interrompida por um erro. Os testes e as leituras automáticas foram pausados. " +
                    "Feche e abra o programa novamente.\n\n" + e.Exception.Message +
                    "\n\nRegistro de diagnóstico:\n" + AppDiagnostics.LogPath,
                    "RESET EXPLODE · Falha", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { _reporting = false; }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppDiagnostics.Log("Falha fatal em segundo plano", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppDiagnostics.Log("Falha de tarefa em segundo plano", e.Exception);
            e.SetObserved();
        };
    }
    // Uma única instância: abrir o RESET EXPLODE de novo só traz a janela que já está aberta para a frente.
    private static Mutex? _single;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? cls, string title);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr h, uint message, IntPtr wparam, IntPtr lparam);

    protected override async void OnStartup(StartupEventArgs e)
    {
        if (e.Args.FirstOrDefault() == "--register-startup" && e.Args.Length == 2)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try { EnergyAutostart.SetEnabled(true, e.Args[1]); var settings = AppSettings.Load(); settings.StartWithWindows = true; settings.Save(); Shutdown(0); }
            catch (Exception ex) { AppDiagnostics.Log("Registrar inicialização em segundo plano", ex); Shutdown(1); }
            return;
        }
        if (e.Args.FirstOrDefault() == "--apply-update")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try { await AutoUpdater.ApplyAsync(e.Args); }
            catch (Exception ex)
            {
                AppDiagnostics.Log("Aplicar atualização", ex);
                MessageBox.Show("A atualização não foi concluída. A versão instalada continua disponível.\n\n" + ex.Message,
                    "RESET EXPLODE · Atualização", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            Shutdown(); return;
        }
        bool first;
        try { _single = new Mutex(true, @"Local\ResetExplode.SingleInstance", out first); }
        catch (UnauthorizedAccessException) { first = false; } // criado por outra instância com outro nível de permissão

        if (!first)
        {
            IntPtr h = FindWindow(null, "RESET EXPLODE");
            if (h != IntPtr.Zero && !e.Args.Contains("--background"))
            {
                PostMessage(h, TrayIcon.ShowMainMessage, IntPtr.Zero, IntPtr.Zero);
            }
            Shutdown();
            return;
        }
        base.OnStartup(e);
        AppDiagnostics.Log($"Iniciando em {Environment.OSVersion}; {RuntimeInformation.ProcessArchitecture}");
        try
        {
            MainWindow = new MainWindow();
            if (e.Args.Contains("--background")) ((MainWindow)MainWindow).StartInBackground();
            else MainWindow.Show();
        }
        catch (Exception ex)
        {
            AppDiagnostics.Log("Falha na inicialização", ex);
            MessageBox.Show("Não foi possível abrir o programa.\n\n" + ex.Message + "\n\nDiagnóstico: " + AppDiagnostics.LogPath,
                "RESET EXPLODE", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _single?.Dispose();
        base.OnExit(e);
    }
}
