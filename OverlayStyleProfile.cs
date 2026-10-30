using System.Text.Json;
using System.IO;

namespace ResetExplode;

/// <summary>Portable appearance only. No credentials, startup settings or history are exported.</summary>
public static class OverlayStyleProfile
{
    private static readonly string[] Properties = {
        nameof(AppSettings.OverlayDesign),nameof(AppSettings.OverlayFont),nameof(AppSettings.OverlayFontSize),
        nameof(AppSettings.OverlayScale),nameof(AppSettings.OverlayColor),nameof(AppSettings.OverlayBackgroundOpacity),
        nameof(AppSettings.OverlayComponentColors),nameof(AppSettings.OverlayBorder),nameof(AppSettings.OverlayBigFps),
        nameof(AppSettings.OverlayCorner),nameof(AppSettings.OverlayOffsetX),nameof(AppSettings.OverlayOffsetY),
        nameof(AppSettings.OverlayOrder),nameof(AppSettings.OverlayIntervalMs),nameof(AppSettings.OverlayItems)
    };
    public static string Export(AppSettings settings)
    {
        var appearance=Properties.ToDictionary(key=>key,key=>typeof(AppSettings).GetProperty(key)!.GetValue(settings));
        return JsonSerializer.Serialize(new {Format="ResetExplode.OverlayStyle",Version=1,Appearance=appearance},new JsonSerializerOptions {WriteIndented=true});
    }
    public static void Import(string json,AppSettings destination)
    {
        if(json.Length>65536)throw new InvalidDataException("O estilo deve ter até 64 KB.");
        using var doc=JsonDocument.Parse(json);
        var root=doc.RootElement;
        if(!root.TryGetProperty("Format",out var format)||format.GetString()!="ResetExplode.OverlayStyle"
            ||!root.TryGetProperty("Version",out var version)||version.GetInt32()!=1
            ||!root.TryGetProperty("Appearance",out var appearance)||appearance.ValueKind!=JsonValueKind.Object)
            throw new InvalidDataException("Este arquivo não é um estilo do ResetExplode compatível.");
        var parsed=appearance.Deserialize<AppSettings>()??throw new InvalidDataException("Estilo vazio.");
        parsed.OverlayStyleVersion=2;
        var source=AppSettings.Normalize(parsed);
        foreach(string key in Properties)typeof(AppSettings).GetProperty(key)!.SetValue(destination,typeof(AppSettings).GetProperty(key)!.GetValue(source));
    }
}
