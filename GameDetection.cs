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
    public void SetLibrary(IEnumerable<LibGame> games) => _games = games.ToArray();
    private static readonly HashSet<string> Desktop = new(StringComparer.OrdinalIgnoreCase)
    { "explorer", "chrome", "msedge", "firefox", "brave", "opera", "discord", "steam", "steamwebhelper", "epicgameslauncher", "obs64", "dwm", "ResetExplode", "Validation" };
    private static readonly HashSet<string> KnownGames = new(StringComparer.OrdinalIgnoreCase)
    { "TS4", "TS4_x64", "TS4_DX9_x64" };

    public static bool IsGame(string name, string? path, IEnumerable<LibGame> library, IEnumerable<string> manual)
    {
        name = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? Path.GetFileNameWithoutExtension(name) : name;
        if (Desktop.Contains(name)) return false;
        if (KnownGames.Contains(name)) return true;
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
            return IsGame(_lastName, _lastPath, _games, manual) ? (hwnd, (int)pid, _lastName) : null;
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
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
}
