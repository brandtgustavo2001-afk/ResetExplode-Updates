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
        bool ready=_overlayReady;_overlayReady=false;
        foreach(var rb in OvCornerPanel.Children.OfType<RadioButton>())rb.IsChecked=(string)rb.Tag==_settings.OverlayCorner;
        foreach(var rb in OvScalePanel.Children.OfType<RadioButton>())rb.IsChecked=Math.Abs(double.Parse((string)rb.Tag,CultureInfo.InvariantCulture)-_settings.OverlayScale)<.01;
        OvKeepUnfocused.IsChecked=_settings.OverlayKeepWhenUnfocused;
        OvFont.Text = _settings.OverlayFont;
        OvFontSize.Value = _settings.OverlayFontSize;
        OvColor.Text = _settings.OverlayColor;
        OvOpacity.Value = _settings.OverlayBackgroundOpacity * 100;
        OvTransparent.IsChecked = OvOpacity.Value == 0;
        OvOffsetX.Text = _settings.OverlayOffsetX.ToString(CultureInfo.CurrentCulture);
        OvOffsetY.Text = _settings.OverlayOffsetY.ToString(CultureInfo.CurrentCulture);
        OvInterval.Text = _settings.OverlayIntervalMs.ToString();
        OvOrder.Text = _settings.OverlayOrder;
        OvBorder.IsChecked = _settings.OverlayBorder;
        OvBigFps.IsChecked = _settings.OverlayBigFps;
        OvComponentColors.IsChecked = _settings.OverlayComponentColors;
        InitOverlayDesigner();
        RefreshManualGameList();
        _overlayReady=ready;
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

    private void OvAppearanceApply_Click(object sender, RoutedEventArgs e) => TryApplyOverlayAppearance();
    private bool TryApplyOverlayAppearance(bool save=true,bool live=false)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(OvColor.Text.Trim(), "^#[0-9a-fA-F]{6}$") ||
            !double.TryParse(OvOffsetX.Text, out double x) || !double.IsFinite(x) || x < 0 || x > 1000 ||
            !double.TryParse(OvOffsetY.Text, out double y) || !double.IsFinite(y) || y < 0 || y > 1000 ||
            !int.TryParse(OvInterval.Text, out int interval) || interval < 100 || interval > 2000)
        { OvAppearanceStatus.Text = "Use cor #RRGGBB, deslocamentos de 0 a 1000 e intervalo de 100 a 2000 ms."; return false; }
        var order = OvOrder.Text.ToUpperInvariant().Split(',').Select(s => s.Trim()).ToArray();
        if (order.Length != 4 || !order.ToHashSet().SetEquals(new[] { "GPU", "VRAM", "CPU", "RAM" }))
        { OvAppearanceStatus.Text = "Ordem: escreva GPU, VRAM, CPU e RAM uma vez cada, separados por vírgula."; return false; }
        if(!OverlayDesign.IsFont(OvFont.Text)||!Fonts.SystemFontFamilies.Any(f=>f.Source.Equals(OvFont.Text,StringComparison.OrdinalIgnoreCase)))
        { OvAppearanceStatus.Text="Escolha uma fonte instalada no Windows.";if(!live)OvFont.Focus();return false; }
        if(!TryReadOverlayDesigner(out var design,live))return false;
        _settings.OverlayDesign=design;
        _settings.OverlayFont = OvFont.Text;
        _settings.OverlayFontSize = OvFontSize.Value;
        _settings.OverlayColor = OvColor.Text.Trim(); _settings.OverlayBackgroundOpacity = OvOpacity.Value / 100;
        _settings.OverlayOffsetX = x; _settings.OverlayOffsetY = y;
        _settings.OverlayIntervalMs = interval; _settings.OverlayOrder = string.Join(',', order);
        _settings.OverlayBorder = OvBorder.IsChecked == true; _settings.OverlayBigFps = OvBigFps.IsChecked == true;
        _settings.OverlayComponentColors = OvComponentColors.IsChecked == true;
        if(save)_settings.Save(); ApplyOverlayOptions(); UpdateOverlay();
        OvAppearanceStatus.Text = "Alterações aplicadas no jogo e na prévia. Salvamento automático ativo.";
        return true;
    }

    private void UpdateOverlayPreview()
    {
        var examples=OverlayPresentation.Examples(OvShow);
        var lines=OverlayMetrics.All.Where(m=>examples.ContainsKey(m.Key)).GroupBy(m=>m.Group).Select(g=>new OverlayLine(g.Key,string.Join("  ",g.Select(m=>examples[m.Key])))).ToArray();
        var order=_settings.OverlayOrder.Split(',').Select(s=>s.Trim()).ToList();
        var selected=lines.Where(l=>OverlayMetrics.All.Any(m=>m.Group==l.Label&&OvShow.Contains(m.Key))).OrderBy(l=>order.IndexOf(l.Label)).ToArray();
        var frames=new FrameStats(0,"Minecraft",120,8.3,98,Array.Empty<double>());
        var rows=OverlayPresentation.Rows(_settings,selected,frames,null,OvShow,"Minecraft","19:30",examples);
        var plan=OverlayPresentation.Plan(_settings,rows);
        OvPreviewBox.Child=OverlayPresentation.Canvas(_settings,plan);
        OvPreviewBox.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.OverlayDesign.Background)){Opacity=_settings.OverlayBackgroundOpacity};
        OvPreviewBox.BorderThickness=new Thickness(_settings.OverlayBorder?1:0);
        OvPreviewBox.Padding=new Thickness(0);
        OvPreviewBox.LayoutTransform=new ScaleTransform(_settings.OverlayScale,_settings.OverlayScale);
    }    private void OvClassic_Click(object sender, RoutedEventArgs e)
    {
        _settings.OverlayDesign = new();
        _settings.OverlayFont = "Consolas"; _settings.OverlayFontSize = 16; _settings.OverlayScale = 1;
        _settings.OverlayBackgroundOpacity = .22; _settings.OverlayBorder = false; _settings.OverlayBigFps = false;
        _settings.OverlayComponentColors = true; _settings.OverlayColor = "#F5F5F5"; _settings.OverlayStyleVersion = 2;
        _settings.Save(); InitOverlayCustomization(); ApplyOverlayOptions(); UpdateOverlay();
        OvAppearanceStatus.Text = "Estilo clássico aplicado: texto compacto, cores por componente e sombra para contraste.";
    }
}
