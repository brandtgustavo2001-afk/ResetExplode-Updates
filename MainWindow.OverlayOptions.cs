using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ResetExplode;

public partial class MainWindow
{
    private readonly GameDetection _gameDetection = new();
    private bool _overlayGamesBusy, _hotkeyRegistered;
    private string? _lastOverlayGame;
    private uint _registeredKey, _registeredModifiers;

    private void InitOverlayCustomization()
    {
        OvFont.SelectedItem = OvFont.Items.Cast<ComboBoxItem>().First(i => (string)i.Content == _settings.OverlayFont);
        OvFontSize.Value = _settings.OverlayFontSize;
        OvColor.Text = _settings.OverlayColor;
        OvOpacity.Value = _settings.OverlayBackgroundOpacity * 100;
        OvOffsetX.Text = _settings.OverlayOffsetX.ToString(CultureInfo.CurrentCulture);
        OvOffsetY.Text = _settings.OverlayOffsetY.ToString(CultureInfo.CurrentCulture);
        OvInterval.Text = _settings.OverlayIntervalMs.ToString();
        OvOrder.Text = _settings.OverlayOrder;
        OvBorder.IsChecked = _settings.OverlayBorder;
        OvBigFps.IsChecked = _settings.OverlayBigFps;
        OvComponentColors.IsChecked = _settings.OverlayComponentColors;
        RefreshManualGameList();
        UpdateOverlayPreview();
    }

    private async Task RefreshOverlayGamesAsync()
    {
        if (_overlayGamesBusy || _closed) return;
        _overlayGamesBusy = true;
        try
        {
            var custom = _settings.CustomGames.ToArray();
            var games = await Task.Run(() => GameLibrary.Scan(custom));
            if (!_closed) _gameDetection.SetLibrary(games);
        }
        catch (Exception ex) { AppDiagnostics.Log("Detectar jogos para overlay", ex); }
        finally { _overlayGamesBusy = false; }
    }

    private void OvAddGame_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Filter = "Executáveis (*.exe)|*.exe", Title = "Escolha o executável do jogo (não o launcher)" };
        if (picker.ShowDialog(this) != true) return;
        if (!_settings.OverlayGameExecutables.Contains(picker.FileName, StringComparer.OrdinalIgnoreCase)) _settings.OverlayGameExecutables.Add(picker.FileName);
        _settings.Save(); RefreshManualGameList();
    }

    private void OvRemoveGame_Click(object sender, RoutedEventArgs e)
    {
        if (OvManualGames.SelectedItem is not string path) return;
        _settings.OverlayGameExecutables.Remove(path);
        _settings.Save(); RefreshManualGameList();
    }
    private void RefreshManualGameList() => OvManualGames.ItemsSource = _settings.OverlayGameExecutables.ToArray();

    private bool RegisterOverlayHotkey(uint key, uint modifiers, bool fallback = false)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return false;
        if (_hotkeyRegistered) UnregisterHotKey(hwnd, HotkeyId);
        bool success = RegisterHotKey(hwnd, HotkeyId, modifiers | 0x4000, key);
        int error = success ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        if (!success && fallback)
        {
            key = 0x79; modifiers = 6; // Ctrl+Shift+F10, se Ctrl+Shift+O estiver ocupado.
            success = RegisterHotKey(hwnd, HotkeyId, modifiers | 0x4000, key);
        }
        if (success)
        {
            _hotkeyRegistered = true; _registeredKey = key; _registeredModifiers = modifiers;
            _settings.OverlayHotkeyKey = key; _settings.OverlayHotkeyModifiers = modifiers;
            OvHotkey.Text = HotkeyLabel(key, modifiers);
            OvHotkeyStatus.Text = error == 0 ? "Atalho global registrado. Clique no campo e pressione outra combinação para trocar."
                : "O atalho anterior estava ocupado. Atalho alternativo registrado: " + OvHotkey.Text;
            return true;
        }
        _hotkeyRegistered = _registeredKey != 0 && RegisterHotKey(hwnd, HotkeyId, _registeredModifiers | 0x4000, _registeredKey);
        OvHotkeyStatus.Text = $"Combinação ocupada ou indisponível (Windows {error}). " +
            (_hotkeyRegistered ? "O atalho anterior continua ativo: " + HotkeyLabel(_registeredKey, _registeredModifiers) : "Escolha outra combinação.");
        OvHotkey.Text = _hotkeyRegistered ? HotkeyLabel(_registeredKey, _registeredModifiers) : "Sem atalho global";
        return false;
    }
    private static string HotkeyLabel(uint key, uint modifiers) =>
        ((modifiers & 2) != 0 ? "Ctrl + " : "") + ((modifiers & 1) != 0 ? "Alt + " : "") +
        ((modifiers & 4) != 0 ? "Shift + " : "") + KeyInterop.KeyFromVirtualKey((int)key);

    private void OvHotkey_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        uint modifiers = (uint)Keyboard.Modifiers & 7;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.Escape) return;
        if (modifiers == 0) { OvHotkeyStatus.Text = "Use Ctrl, Alt ou Shift junto com uma tecla."; return; }
        if (RegisterOverlayHotkey((uint)KeyInterop.VirtualKeyFromKey(key), modifiers)) _settings.Save();
    }

    private void OvAppearanceApply_Click(object sender, RoutedEventArgs e)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(OvColor.Text.Trim(), "^#[0-9a-fA-F]{6}$") ||
            !double.TryParse(OvOffsetX.Text, out double x) || !double.IsFinite(x) || x < 0 || x > 1000 ||
            !double.TryParse(OvOffsetY.Text, out double y) || !double.IsFinite(y) || y < 0 || y > 1000 ||
            !int.TryParse(OvInterval.Text, out int interval) || interval < 100 || interval > 2000)
        { OvAppearanceStatus.Text = "Use cor #RRGGBB, deslocamentos de 0 a 1000 e intervalo de 100 a 2000 ms."; return; }
        var order = OvOrder.Text.ToUpperInvariant().Split(',').Select(s => s.Trim()).ToArray();
        if (order.Length != 4 || !order.ToHashSet().SetEquals(new[] { "GPU", "VRAM", "CPU", "RAM" }))
        { OvAppearanceStatus.Text = "Ordem: escreva GPU, VRAM, CPU e RAM uma vez cada, separados por vírgula."; return; }
        _settings.OverlayFont = (string)((ComboBoxItem)OvFont.SelectedItem).Content;
        _settings.OverlayFontSize = OvFontSize.Value;
        _settings.OverlayColor = OvColor.Text.Trim(); _settings.OverlayBackgroundOpacity = OvOpacity.Value / 100;
        _settings.OverlayOffsetX = x; _settings.OverlayOffsetY = y;
        _settings.OverlayIntervalMs = interval; _settings.OverlayOrder = string.Join(',', order);
        _settings.OverlayBorder = OvBorder.IsChecked == true; _settings.OverlayBigFps = OvBigFps.IsChecked == true;
        _settings.OverlayComponentColors = OvComponentColors.IsChecked == true;
        _settings.Save(); ApplyOverlayOptions(); UpdateOverlayPreview(); UpdateOverlay();
        OvAppearanceStatus.Text = "Personalização salva e aplicada. A prévia usa valores de exemplo.";
    }

    private void UpdateOverlayPreview()
    {
        OvPreviewText.Inlines.Clear();
        foreach (var raw in _settings.OverlayOrder.Split(','))
        {
            string label = raw.Trim();
            var run = new System.Windows.Documents.Run(label.PadRight(5) + (label switch
            { "GPU" => "97%  65 °C  2887 MHz", "VRAM" => "6,2 GB", "CPU" => "42%  62 °C  4,80 GHz", _ => "12,4 GB" }) + "\n");
            run.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.OverlayComponentColors ? new OverlayLine(label, "").Accent : _settings.OverlayColor));
            OvPreviewText.Inlines.Add(run);
        }
        OvPreviewText.Inlines.Add(new System.Windows.Documents.Run("FPS  120   8,3 ms\n1% low 98")
        { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.OverlayComponentColors ? "#FFFF66" : _settings.OverlayColor)) });
        OvPreviewText.FontWeight = FontWeights.Bold;
        OvPreviewText.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 3, ShadowDepth = 1, Opacity = 1 };
        OvPreviewText.FontFamily = new FontFamily(_settings.OverlayFont);
        OvPreviewText.FontSize = _settings.OverlayFontSize;
        OvPreviewText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.OverlayColor));
        OvPreviewBox.Background = new SolidColorBrush(Color.FromArgb((byte)(_settings.OverlayBackgroundOpacity * 255), 0, 0, 0));
        OvPreviewBox.BorderThickness = new Thickness(_settings.OverlayBorder ? 1 : 0);
        OvPreviewText.LayoutTransform = new ScaleTransform(_settings.OverlayScale, _settings.OverlayScale);
    }
    private void OvClassic_Click(object sender, RoutedEventArgs e)
    {
        _settings.OverlayFont = "Consolas"; _settings.OverlayFontSize = 16; _settings.OverlayScale = 1;
        _settings.OverlayBackgroundOpacity = .22; _settings.OverlayBorder = false; _settings.OverlayBigFps = false;
        _settings.OverlayComponentColors = true; _settings.OverlayColor = "#F5F5F5"; _settings.OverlayStyleVersion = 2;
        _settings.Save(); InitOverlayCustomization(); ApplyOverlayOptions(); UpdateOverlay();
        OvAppearanceStatus.Text = "Estilo clássico aplicado: texto compacto, cores por componente e sombra para contraste.";
    }
}
