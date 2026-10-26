using System.IO;
using System.Reflection;

namespace ResetExplode;

public static class AppDiagnostics
{
    private static readonly object Gate = new();
    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ResetExplode", "Logs", "app.log");

    public static void Log(string context, Exception? error = null)
    {
        // O diagnóstico nunca pode causar outra falha (pasta sem acesso/disco cheio).
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 2 * 1024 * 1024)
                    File.Move(LogPath, LogPath + ".previous", true);
                File.AppendAllText(LogPath, $"[{DateTimeOffset.Now:O}] v{Assembly.GetExecutingAssembly().GetName().Version} {context}\n{error}\n");
            }
        }
        catch { }
    }

    public static void TryCleanup(string context, Action action)
    {
        try { action(); }
        catch (Exception ex) { Log(context, ex); }
    }
}
