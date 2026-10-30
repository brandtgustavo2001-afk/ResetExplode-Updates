using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ResetExplode;

/// <summary>Detecção por executável/pasta de instalação, sem considerar navegadores como jogos.</summary>
public sealed class GameDetection
{
    private LibGame[] _games = Array.Empty<LibGame>();
    private int _lastPid;
    private string _lastPath = "", _lastName = "";
    private DateTime _pathAt;
    private readonly OverlayGameSession _session=new();
    public OverlayGameTarget? ForOverlay(IEnumerable<string> manual,bool keep)
    {
        var foreground=Foreground(manual);
        return _session.Resolve(foreground.HasValue?new OverlayGameTarget(foreground.Value.Window,foreground.Value.Pid,foreground.Value.Name):null,keep,target=> {
            if(!IsWindow(target.Window))return false;
            GetWindowThreadProcessId(target.Window,out uint pid);return pid==target.Pid;
        });
    }
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    public void SetLibrary(IEnumerable<LibGame> games) => _games = games.ToArray();
    private static readonly HashSet<string> Desktop = new(StringComparer.OrdinalIgnoreCase)
    { "explorer", "chrome", "msedge", "firefox", "brave", "opera", "discord", "steam", "steamwebhelper", "epicgameslauncher", "obs64", "dwm", "ResetExplode", "Validation" };
    private static readonly HashSet<string> KnownGames = new(StringComparer.OrdinalIgnoreCase)
    { "TS4", "TS4_x64", "TS4_DX9_x64", "Minecraft.Windows" };

    public static bool IsGame(string name, string? path, IEnumerable<LibGame> library, IEnumerable<string> manual, string? windowTitle = null)
    {
        name = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? Path.GetFileNameWithoutExtension(name) : name;
        if (Desktop.Contains(name)) return false;
        if (KnownGames.Contains(name)) return true;
        // Minecraft Java uses a shared Java runtime, not the launcher's executable.
        // A versioned game title distinguishes it from other Java applications.
        if ((name.Equals("javaw", StringComparison.OrdinalIgnoreCase) || name.Equals("java", StringComparison.OrdinalIgnoreCase))
            && System.Text.RegularExpressions.Regex.IsMatch(windowTitle ?? "", @"^Minecraft\*?\s+\d+\.\d+(?:\D|$)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return true;
        if (manual.Any(p => string.IsNullOrEmpty(path)
            ? string.Equals(Path.GetFileNameWithoutExtension(p), name, StringComparison.OrdinalIgnoreCase)
            : p.Contains('\\') || p.Contains('/')
            ? !string.IsNullOrEmpty(path) && string.Equals(p, path, StringComparison.OrdinalIgnoreCase)
            : string.Equals(Path.GetFileNameWithoutExtension(p), name, StringComparison.OrdinalIgnoreCase))) return true;
        if (string.IsNullOrEmpty(path)) return library.Any(g => !string.IsNullOrWhiteSpace(g.ExePath)
            && string.Equals(Path.GetFileNameWithoutExtension(g.ExePath), name, StringComparison.OrdinalIgnoreCase));
        return library.Any(g => (!string.IsNullOrEmpty(g.ExePath) && string.Equals(g.ExePath, path, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(g.InstallDir) && path.StartsWith(g.InstallDir.TrimEnd('\\', '/') + "\\", StringComparison.OrdinalIgnoreCase)))
            || new[] { "\\steamapps\\common\\", "\\Epic Games\\", "\\Riot Games\\" }.Any(p => path.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    public (IntPtr Window, int Pid, string Name)? Foreground(IEnumerable<string> manual)
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0 || pid == Environment.ProcessId) return null;
        try
        {
            if (_lastPid != pid || (DateTime.UtcNow - _pathAt).TotalSeconds > 3)
            {
                using var p = Process.GetProcessById((int)pid);
                _lastName = p.ProcessName;
                _lastPath = ReadProcessPath((int)pid);
                _lastPid = (int)pid; _pathAt = DateTime.UtcNow;
            }
            var title = new StringBuilder(512);
            GetWindowText(hwnd, title, title.Capacity);
            return IsGame(_lastName, _lastPath, _games, manual, title.ToString()) ? (hwnd, (int)pid, _lastName) : null;
        }
        catch { return null; }
    }
    // Limited query access avoids reading protected process modules and supports cross-bitness.
    public static string ReadProcessPath(int pid)
    {
        using var handle = OpenProcess(0x1000, false, pid);
        if (handle.IsInvalid) return "";
        var path = new StringBuilder(32768);
        uint length = (uint)path.Capacity;
        return QueryFullProcessImageName(handle, 0, path, ref length) ? path.ToString() : "";
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref uint size);

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    public static (int Width,int Height) ClientSize(IntPtr window)
        => GetClientRect(window,out var rect)?(Math.Max(1,rect.Right),Math.Max(1,rect.Bottom)):(1920,1080);
    [StructLayout(LayoutKind.Sequential)] private struct ClientRect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window,out ClientRect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
}
