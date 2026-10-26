using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace ResetExplode;

/// <summary>Native notification-area icon, without starting another UI framework or process.</summary>
public sealed class TrayIcon : IDisposable
{
    private const uint Callback = 0x8000 + 78;
    public static readonly uint ShowMainMessage = RegisterWindowMessage("ResetExplode.ShowMainWindow");
    private readonly HwndSource _source;
    private readonly Action _open, _exit, _shutdown;
    private readonly ContextMenu _menu = new();
    private readonly uint _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private IntPtr _icon;
    private bool _ownsIcon, _disposed;
    public bool Available { get; private set; }

    public TrayIcon(Window window, Action open, Action exit, Action updates, Action shutdown)
    {
        _open = open; _exit = exit; _shutdown = shutdown;
        _source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)!;
        _source.AddHook(Hook);
        var show = new MenuItem { Header = "Abrir RESET EXPLODE" }; show.Click += (_, _) => _open();
        var update = new MenuItem { Header = "Atualizações" }; update.Click += (_, _) => updates();
        var quit = new MenuItem { Header = "Sair e encerrar monitoramento" }; quit.Click += (_, _) => _exit();
        _menu.Items.Add(show); _menu.Items.Add(update); _menu.Items.Add(new Separator()); _menu.Items.Add(quit);
        ExtractIconEx(Environment.ProcessPath!, 0, out var large, out var small, 1);
        if (large != IntPtr.Zero) DestroyIcon(large);
        _icon = small; _ownsIcon = small != IntPtr.Zero;
        if (_icon == IntPtr.Zero) _icon = LoadIcon(IntPtr.Zero, new IntPtr(32512));
        Add();
    }
    private NotifyData Data() => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyData>(), Window = _source.Handle, Id = 1,
        Flags = 1 | 2 | 4, Message = Callback, Icon = _icon,
        Tip = "RESET EXPLODE · monitoramento em segundo plano", Info = "", Title = ""
    };
    private void Add() { var data = Data(); Available = Shell_NotifyIcon(0, ref data); }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        if ((uint)message == ShowMainMessage) { _open(); handled = true; }
        else if (message is 0x11 or 0x16) _shutdown(); // Windows logoff/shutdown/Restart Manager.
        else if ((uint)message == _taskbarCreated && !_disposed) Add();
        else if ((uint)message == Callback)
        {
            int mouse = lparam.ToInt32() & 0xffff;
            if (mouse == 0x202) _open();
            else if (mouse == 0x205 || mouse == 0x7B)
            {
                SetForegroundWindow(hwnd); _menu.Placement = PlacementMode.MousePoint; _menu.IsOpen = true;
            }
            handled = true;
        }
        return IntPtr.Zero;
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _menu.IsOpen = false;
        var data = Data(); Shell_NotifyIcon(2, ref data);
        _source.RemoveHook(Hook);
        if (_ownsIcon) DestroyIcon(_icon);
        Available = false;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyData
    {
        public uint Size; public IntPtr Window; public uint Id, Flags, Message; public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
        public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint command, ref NotifyData data);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint ExtractIconEx(string file, int index, out IntPtr large, out IntPtr small, uint count);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadIcon(IntPtr instance, IntPtr name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
}
