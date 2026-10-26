using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace ResetExplode;

public sealed record UpdatePackage(string Version, string Url, string Sha256, long Size);
public sealed record UpdateEnvelope(string Payload, string Signature);
public sealed record PreparedUpdate(UpdatePackage Package, string ManifestPath, string InstallerPath);

public static class AutoUpdater
{
    public const string Source = "https://github.com/brandtgustavo2001-afk/ResetExplode-Updates/releases/latest/download/update-v2.json";
    public static Version CurrentVersion => typeof(AutoUpdater).Assembly.GetName().Version!;
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(5) };
    private static string Cache => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ResetExplode", "Updates");

    public static UpdatePackage VerifyManifest(string json)
        => VerifyManifest(json, UpdateTrust.PublicKey);

    internal static UpdatePackage VerifyManifest(string json, string publicKey)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 32768) throw new InvalidDataException("Manifesto vazio ou muito grande.");
        var envelope = JsonSerializer.Deserialize<UpdateEnvelope>(json) ?? throw new InvalidDataException("Manifesto inválido.");
        byte[] payload = Convert.FromBase64String(envelope.Payload);
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
        if (!rsa.VerifyData(payload, Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new InvalidDataException("Assinatura da atualização inválida.");
        var package = JsonSerializer.Deserialize<UpdatePackage>(payload) ?? throw new InvalidDataException("Pacote inválido.");
        if (!Version.TryParse(package.Version, out var version) || version.Build < 0 || version.Revision > 0 || package.Size is <= 0 or > 536870912
            || !System.Text.RegularExpressions.Regex.IsMatch(package.Sha256 ?? "", "^[A-Fa-f0-9]{64}$")
            || !Uri.TryCreate(package.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.Host != "github.com" || uri.Port != 443 || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || uri.AbsolutePath != $"/brandtgustavo2001-afk/ResetExplode-Updates/releases/download/v{package.Version}/ResetExplode-Setup-{package.Version}-x64.exe")
            throw new InvalidDataException("Dados da atualização inválidos.");
        return package;
    }

    public static bool IsNewer(string version) => Version.TryParse(version, out var parsed)
        && parsed > new Version(CurrentVersion.Major, CurrentVersion.Minor, CurrentVersion.Build);

    public static bool VerifyPackage(string path, UpdatePackage package)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != package.Size) return false;
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).Equals(package.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<PreparedUpdate?> PrepareAsync(Action<string> status, CancellationToken token)
        => await PrepareAsync(Client, Source, Cache, CurrentVersion, status, token);

    internal static async Task<PreparedUpdate?> PrepareAsync(HttpClient client, string source, string cache,
        Version currentVersion, Action<string> status, CancellationToken token, string? trustedPublicKey = null)
    {
        status("Verificando atualizações…");
        using var response = await client.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 32768) throw new InvalidDataException("Manifesto muito grande.");
        await using var manifestInput = await response.Content.ReadAsStreamAsync(token);
        using var manifestBytes = new MemoryStream();
        var manifestBuffer = new byte[4096];
        int manifestCount;
        while ((manifestCount = await manifestInput.ReadAsync(manifestBuffer, token)) > 0)
        {
            if (manifestBytes.Length + manifestCount > 32768) throw new InvalidDataException("Manifesto muito grande.");
            manifestBytes.Write(manifestBuffer, 0, manifestCount);
        }
        var json = System.Text.Encoding.UTF8.GetString(manifestBytes.ToArray());
        var package = VerifyManifest(json, trustedPublicKey ?? UpdateTrust.PublicKey);
        if (Version.Parse(package.Version) <= currentVersion) { status("Você está na versão mais recente."); return null; }
        Directory.CreateDirectory(cache);
        string installer = Path.Combine(cache, $"ResetExplode-{package.Version}.exe");
        if (!VerifyPackage(installer, package))
        {
            status($"Baixando versão {package.Version} em segundo plano…");
            string partial = installer + ".partial";
            try
            {
                using var download = await client.GetAsync(package.Url, HttpCompletionOption.ResponseHeadersRead, token);
                download.EnsureSuccessStatusCode();
                if (download.Content.Headers.ContentLength is long contentLength && contentLength != package.Size)
                    throw new InvalidDataException("Tamanho da atualização incorreto.");
                await using (var input = await download.Content.ReadAsStreamAsync(token))
                await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    byte[] buffer = new byte[81920]; long total = 0; int count;
                    while ((count = await input.ReadAsync(buffer, token)) > 0)
                    {
                        total += count;
                        if (total > package.Size) throw new InvalidDataException("Tamanho da atualização incorreto.");
                        await output.WriteAsync(buffer.AsMemory(0, count), token);
                    }
                }
                if (!VerifyPackage(partial, package)) throw new InvalidDataException("A verificação do download falhou.");
                File.Move(partial, installer, true);
            }
            finally { if (File.Exists(partial)) File.Delete(partial); }
        }
        string manifest = Path.Combine(cache, $"update-{package.Version}.json");
        await File.WriteAllTextAsync(manifest, json, token);
        status($"Versão {package.Version} pronta. Será aplicada ao fechar o aplicativo.");
        return new PreparedUpdate(package, manifest, installer);
    }

    public static void Schedule(PreparedUpdate update)
    {
        var package = VerifyManifest(File.ReadAllText(update.ManifestPath));
        if (package != update.Package || !IsNewer(package.Version) || !VerifyPackage(update.InstallerPath, package))
            throw new InvalidDataException("A atualização preparada mudou ou está incompleta. Verifique novamente.");
        // Copy the self-contained executable so the installed file can be replaced after exit.
        string helper = Path.Combine(Cache, "UpdateHelper-" + Guid.NewGuid().ToString("N") + ".exe");
        File.Copy(Environment.ProcessPath!, helper);
        var start = new ProcessStartInfo(helper) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var arg in new[] { "--apply-update", Environment.ProcessId.ToString(), update.ManifestPath,
            update.InstallerPath, AppContext.BaseDirectory }) start.ArgumentList.Add(arg);
        Process.Start(start);
    }

    public static async Task ApplyAsync(string[] args)
    {
        if (args.Length != 5) throw new InvalidDataException("Argumentos da atualização inválidos.");
        try
        {
            using var parent = Process.GetProcessById(int.Parse(args[1]));
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await parent.WaitForExitAsync(timeout.Token);
        }
        catch (ArgumentException) { /* Parent already exited. */ }
        Directory.CreateDirectory(Cache);
        using var installLock = new FileStream(Path.Combine(Cache, "install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var package = VerifyManifest(await File.ReadAllTextAsync(args[2]));
        if (!IsNewer(package.Version) || !VerifyPackage(args[3], package))
            throw new InvalidDataException("Pacote da atualização não foi aprovado.");
        string target = Path.GetFullPath(args[4]);
        string installedFile = Path.Combine(target, "ResetExplode.exe");
        if (!File.Exists(installedFile)) throw new InvalidDataException("Instalação de destino não encontrada.");
        if (Version.TryParse(FileVersionInfo.GetVersionInfo(installedFile).FileVersion, out var installedVersion)
            && installedVersion >= Version.Parse(package.Version)) return;
        var start = new ProcessStartInfo(args[3]) { UseShellExecute = true };
        foreach (var arg in new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-", "/DIR=" + Path.GetFullPath(args[4]), "/LOG=" + Path.Combine(Cache, "install.log") })
            start.ArgumentList.Add(arg);
        using var installer = Process.Start(start) ?? throw new IOException("Não foi possível abrir a atualização.");
        await installer.WaitForExitAsync();
        if (installer.ExitCode is not (0 or 3010)) throw new IOException("Atualização interrompida. Código " + installer.ExitCode);
        if (!Version.TryParse(FileVersionInfo.GetVersionInfo(installedFile).FileVersion, out var completedVersion)
            || completedVersion < Version.Parse(package.Version)) throw new IOException("O instalador terminou sem aplicar a versão esperada.");
        AppDiagnostics.Log($"Atualização concluída: {package.Version}");
        // Do not restart unexpectedly after the user closed the application.
    }
}
