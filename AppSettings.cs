using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ResetExplode;

public sealed record BenchRun(DateTime Date, double? Multi, double? Single, bool Sustained, string Cpu);

/// <summary>Resultado dos benchmarks de jogo (Test = "gpu", "sim" ou "mem"). Score é usado para a barra do histórico.</summary>
public sealed record BenchRecord(DateTime Date, string Test, double Score, string Summary);

/// <summary>Programa extra que abre antes do jogo (ex.: ferramenta de comando/monitoramento) e pode fechar junto com ele.</summary>
public sealed class HelperProgram
{
    public string Path { get; set; } = "";
    public string Args { get; set; } = "";
    public bool CloseWithGame { get; set; } = true;
    public string Display => System.IO.Path.GetFileName(Path) + (string.IsNullOrWhiteSpace(Args) ? "" : " " + Args);
}

/// <summary>Perfil de um jogo na aba RESET JOGOS: desempenho, argumentos de gráficos e programas auxiliares.</summary>
public sealed class GameProfile
{
    public string Priority { get; set; } = "normal";   // normal | above | high
    public bool HighPerfPlan { get; set; }
    public bool GpuHigh { get; set; }
    public bool DisableFso { get; set; }
    public string? ExePath { get; set; }
    public string WinMode { get; set; } = "default";   // default | full | borderless | window
    public string Res { get; set; } = "default";       // default | 1280x720 | 1920x1080 | 2560x1440 | 3840x2160
    public int Hz { get; set; }                        // 0 = não definir
    public string Style { get; set; } = "source";      // source | unity | unreal
    public string Extra { get; set; } = "";
    public List<HelperProgram> Helpers { get; set; } = new();
}

/// <summary>Jogo adicionado à mão (qualquer .exe).</summary>
public sealed class CustomGame
{
    public string Name { get; set; } = "";
    public string Exe { get; set; } = "";
    public string Args { get; set; } = "";
}

/// <summary>Configurações em %AppData%\ResetExplode. A chave da API é cifrada com DPAPI (só este usuário do Windows a lê).</summary>
public sealed class AppSettings
{
    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ResetExplode");
    private static string FilePath => Path.Combine(Folder, "settings.json");
    private static readonly object FileGate = new();

    public string? HowManyFpsKeyProtected { get; set; }

    // RESET BENCH: últimos resultados (mais recente primeiro)
    public List<BenchRun> BenchRuns { get; set; } = new();
    public List<BenchRecord> BenchRecords { get; set; } = new();

    // RESET OTIMIZA: valores originais dos ajustes do Windows antes da primeira alteração ("<ausente>" = não existia)
    public Dictionary<string, string?> TweakBackup { get; set; } = new();
    /// <summary>ComDB original (reservas de números COM) antes da primeira "liberação".</summary>
    public byte[]? ComDbBackup { get; set; }
    /// <summary>Programas de inicialização removidos, para "Restaurar removidos".</summary>
    public List<StartupBackup> StartupRemoved { get; set; } = new();

    // RESET JOGOS
    public Dictionary<string, GameProfile> GameProfiles { get; set; } = new();
    public List<CustomGame> CustomGames { get; set; } = new();

    // Aparência
    public bool LightTheme { get; set; }
    public bool AutomaticUpdates { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    public double EnergyPrice { get; set; } = .975;
    public double EnergyOtherWatts { get; set; } = 40;
    public double EnergyEfficiency { get; set; } = .90;

    public int OverlayStyleVersion { get; set; }

    // Overlay
    public string OverlayEngine { get; set; } = "Auto";
    public bool OverlayUseAfterburner { get; set; }
    public string OverlayCorner { get; set; } = "TL";
    public double OverlayScale { get; set; } = 1.0;
    public double OverlayFontSize { get; set; } = 16;
    public string OverlayFont { get; set; } = "Consolas";
    public string OverlayColor { get; set; } = "#F5F5F5";
    public double OverlayBackgroundOpacity { get; set; } = .22;
    public bool OverlayComponentColors { get; set; } = true;
    public double OverlayOffsetX { get; set; } = 12;
    public double OverlayOffsetY { get; set; } = 12;
    public bool OverlayBorder { get; set; }
    public bool OverlayBigFps { get; set; }
    public string OverlayOrder { get; set; } = "GPU,VRAM,CPU,RAM";
    public int OverlayIntervalMs { get; set; } = 250;
    public uint OverlayHotkeyModifiers { get; set; } = 6;
    public uint OverlayHotkeyKey { get; set; } = 0x4F;
    public List<string> OverlayGameExecutables { get; set; } = new();
    /// <summary>Itens escolhidos para o overlay (chaves de OverlayMetrics); null = padrão.</summary>
    public List<string>? OverlayItems { get; set; }

    [System.Text.Json.Serialization.JsonIgnore] // nunca gravar a chave em texto puro
    public string? HowManyFpsKey
    {
        get
        {
            if (string.IsNullOrEmpty(HowManyFpsKeyProtected)) return null;
            try
            {
                var bytes = ProtectedData.Unprotect(Convert.FromBase64String(HowManyFpsKeyProtected), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch { return null; }
        }
        set
        {
            HowManyFpsKeyProtected = string.IsNullOrWhiteSpace(value)
                ? null
                : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value.Trim()), null, DataProtectionScope.CurrentUser));
        }
    }

    public static AppSettings Load()
    {
        lock (FileGate)
        {
            foreach (string path in new[] { FilePath, FilePath + ".bak" })
            {
                try
                {
                    if (File.Exists(path))
                        return Normalize(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings());
                }
                catch (Exception ex) { AppDiagnostics.Log("Ler configurações: " + path, ex); }
            }
            return new AppSettings();
        }
    }

    internal static AppSettings Normalize(AppSettings settings)
    {
        settings.BenchRuns = (settings.BenchRuns ?? new()).Where(r => r != null)
            .Where(r => (r.Multi == null || double.IsFinite(r.Multi.Value)) && (r.Single == null || double.IsFinite(r.Single.Value)))
            .Select(r => r with { Cpu = r.Cpu ?? "" }).ToList();
        settings.BenchRecords = (settings.BenchRecords ?? new()).Where(r => r != null && double.IsFinite(r.Score))
            .Select(r => r with { Test = r.Test ?? "", Summary = r.Summary ?? "" }).ToList();
        settings.TweakBackup ??= new();
        settings.StartupRemoved = (settings.StartupRemoved ?? new()).Where(r => r != null).ToList();
        settings.GameProfiles = (settings.GameProfiles ?? new()).Where(p => p.Value != null).ToDictionary(p => p.Key, p => p.Value);
        foreach (var profile in settings.GameProfiles.Values)
        {
            profile.Helpers = (profile.Helpers ?? new()).Where(h => h != null && !string.IsNullOrWhiteSpace(h.Path)).ToList();
            profile.Extra ??= "";
            profile.Priority ??= "normal";
            profile.WinMode ??= "default";
            profile.Res ??= "default";
            profile.Style ??= "source";
        }
        settings.CustomGames = (settings.CustomGames ?? new()).Where(g => g != null && !string.IsNullOrWhiteSpace(g.Exe)).ToList();
        settings.EnergyPrice = double.IsFinite(settings.EnergyPrice) ? Math.Clamp(settings.EnergyPrice, 0, 100) : .975;
        settings.EnergyOtherWatts = double.IsFinite(settings.EnergyOtherWatts) ? Math.Clamp(settings.EnergyOtherWatts, 0, 1000) : 40;
        settings.EnergyEfficiency = double.IsFinite(settings.EnergyEfficiency) ? Math.Clamp(settings.EnergyEfficiency, .5, 1) : .9;
        foreach (var game in settings.CustomGames) { game.Name ??= "Jogo"; game.Args ??= ""; }
        if (settings.OverlayStyleVersion < 2)
        {
            settings.OverlayFont = "Consolas"; settings.OverlayFontSize = 16;
            settings.OverlayBackgroundOpacity = .22; settings.OverlayColor = "#F5F5F5";
            settings.OverlayBorder = false; settings.OverlayBigFps = false; settings.OverlayComponentColors = true; settings.OverlayStyleVersion = 2;
        }

        settings.OverlayScale = double.IsFinite(settings.OverlayScale) ? Math.Clamp(settings.OverlayScale, 0.5, 2) : 1;
        if(settings.OverlayEngine is not ("Auto" or "Window")) settings.OverlayEngine="Auto";
        if (settings.OverlayCorner is not ("TL" or "TR" or "BL" or "BR")) settings.OverlayCorner = "TL";
        settings.OverlayFontSize = double.IsFinite(settings.OverlayFontSize) ? Math.Clamp(settings.OverlayFontSize, 9, 26) : 12;
        settings.OverlayBackgroundOpacity = double.IsFinite(settings.OverlayBackgroundOpacity) ? Math.Clamp(settings.OverlayBackgroundOpacity, 0, 1) : .78;
        settings.OverlayOffsetX = double.IsFinite(settings.OverlayOffsetX) ? Math.Clamp(settings.OverlayOffsetX, 0, 1000) : 12;
        settings.OverlayOffsetY = double.IsFinite(settings.OverlayOffsetY) ? Math.Clamp(settings.OverlayOffsetY, 0, 1000) : 12;
        settings.OverlayFont = settings.OverlayFont is "Consolas" or "Cascadia Mono" or "Segoe UI" ? settings.OverlayFont : "Consolas";
        if (!System.Text.RegularExpressions.Regex.IsMatch(settings.OverlayColor ?? "", "^#[0-9a-fA-F]{6}$")) settings.OverlayColor = "#F5F5F5";
        settings.OverlayOrder ??= "GPU,VRAM,CPU,RAM";
        settings.OverlayIntervalMs = Math.Clamp(settings.OverlayIntervalMs, 100, 2000);
        settings.OverlayGameExecutables = (settings.OverlayGameExecutables ?? new()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (settings.OverlayHotkeyKey < 0x08 || settings.OverlayHotkeyKey > 0xFE || (settings.OverlayHotkeyModifiers & 7) == 0)
        { settings.OverlayHotkeyKey = 0x4F; settings.OverlayHotkeyModifiers = 6; }
        settings.OverlayHotkeyModifiers &= 7;
        return settings;
    }

    public void Save()
    {
        lock (FileGate)
        {
        Directory.CreateDirectory(Folder);
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(FilePath))
            {
                // Guardar apenas um JSON válido como cópia de recuperação.
                try
                {
                    JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                    File.Copy(FilePath, FilePath + ".bak", true);
                }
                catch (JsonException) { }
            }
            File.Move(temporary, FilePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
