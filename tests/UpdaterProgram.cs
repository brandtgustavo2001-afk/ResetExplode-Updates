using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ResetExplode;

using var signer = RSA.Create(2048);
string key = Convert.ToBase64String(signer.ExportSubjectPublicKeyInfo());
byte[] bytes = Encoding.UTF8.GetBytes("signed test installer bytes");
string version = "0.16.10";
string url = $"https://github.com/brandtgustavo2001-afk/ResetExplode-Updates/releases/download/v{version}/ResetExplode-Setup-{version}-x64.exe";
var package = new UpdatePackage(version, url, Convert.ToHexString(SHA256.HashData(bytes)), bytes.Length);
string Sign(UpdatePackage value) {
    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value);
    return JsonSerializer.Serialize(new UpdateEnvelope(Convert.ToBase64String(payload),
        Convert.ToBase64String(signer.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))));
}
int passed = 0;
void Check(bool value, string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS: "+name); passed++; }
void Reject(Action test,string name) { bool rejected=false;try{test();}catch{rejected=true;}Check(rejected,name); }
string manifest = Sign(package);
Check(AutoUpdater.VerifyManifest(manifest,key) == package,"assinatura valida aceita");
using var wrongKey=RSA.Create(2048);
Reject(()=>AutoUpdater.VerifyManifest(manifest,Convert.ToBase64String(wrongKey.ExportSubjectPublicKeyInfo())),"chave incorreta rejeitada");
var envelope=JsonSerializer.Deserialize<UpdateEnvelope>(manifest)!;
Reject(()=>AutoUpdater.VerifyManifest(JsonSerializer.Serialize(envelope with {Payload=Convert.ToBase64String(Encoding.UTF8.GetBytes("tampered"))}),key),"manifesto adulterado rejeitado");
Reject(()=>AutoUpdater.VerifyManifest(new string('x',32769),key),"manifesto excessivo rejeitado");
foreach(var invalid in new[]{package with {Url=url.Replace("github.com","example.com")},package with {Size=0},package with {Sha256="wrong"},package with {Version="../escape"},package with {Url=url+"?redirect=evil"}})
    Reject(()=>AutoUpdater.VerifyManifest(Sign(invalid),key),"dados assinados invalidos rejeitados");
string cache=Path.Combine(Path.GetTempPath(),"ResetExplode-update-tests-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(cache);
try {
    var handler=new Handler(manifest,bytes);
    using var client=new HttpClient(handler);
    async Task<PreparedUpdate?> Prepare(Version current,CancellationToken token=default) =>
        await AutoUpdater.PrepareAsync(client,"https://test.local/update.json",cache,current,_=>{},token,key);
    Check(await Prepare(new Version(0,16,10)) is null,"mesma versao nao baixa pacote");
    Check(handler.Downloads==0,"sem download desnecessario");
    Check(await Prepare(new Version(0,16,11)) is null,"downgrade nao oferecido");
    var update=await Prepare(new Version(0,16,9));
    Check(update is not null && AutoUpdater.VerifyPackage(update.InstallerPath,package),"versao nova baixada e validada");
    Check(File.Exists(update!.ManifestPath),"manifesto preservado para verificacao no fechamento");
    int previous=handler.Downloads;
    await Prepare(new Version(0,16,9));
    Check(handler.Downloads==previous,"cache valido reutilizado");
    File.WriteAllText(update.InstallerPath,"broken");
    await Prepare(new Version(0,16,9));
    Check(handler.Downloads==previous+1,"cache corrompido baixado novamente");
    File.Delete(update.InstallerPath);
    handler.Bytes=Encoding.UTF8.GetBytes("wrong bytes same length!!!");
    bool rejected=false;try{await Prepare(new Version(0,16,9));}catch(InvalidDataException){rejected=true;}
    Check(rejected,"download incorreto rejeitado");
    Check(!File.Exists(update.InstallerPath) && !File.Exists(update.InstallerPath+".partial"),"download incompleto nao promovido e parcial removido");
    handler.Bytes=bytes;
    handler.Status=HttpStatusCode.ServiceUnavailable;
    rejected=false;try{await Prepare(new Version(0,16,9));}catch(HttpRequestException){rejected=true;}
    Check(rejected,"falha HTTP nao vira sucesso");
    handler.Status=HttpStatusCode.OK;
    handler.Manifest=new string('x',32769);
    rejected=false;try{await Prepare(new Version(0,16,9));}catch(InvalidDataException){rejected=true;}
    Check(rejected,"resposta de manifesto limitada durante leitura");
    handler.Manifest=manifest;
    using var cancelled=new CancellationTokenSource();cancelled.Cancel();
    rejected=false;try{await Prepare(new Version(0,16,9),cancelled.Token);}catch(OperationCanceledException){rejected=true;}
    Check(rejected,"cancelamento respeitado");
    Reject(()=>AutoUpdater.Schedule(new PreparedUpdate(package,Path.Combine(cache,"missing.json"),Path.Combine(cache,"missing.exe"))),"agendamento sem manifesto rejeitado antes de executar helper");
    bool argumentsRejected=false;
    try { await AutoUpdater.ApplyAsync(new[]{"--apply-update"}); } catch(InvalidDataException){argumentsRejected=true;}
    Check(argumentsRejected,"aplicacao com argumentos incompletos rejeitada");
} finally { Directory.Delete(cache,true); }
Console.WriteLine($"{passed} verificacoes do atualizador aprovadas.");

sealed class Handler(string manifest,byte[] bytes):HttpMessageHandler {
    public string Manifest=manifest;
    public byte[] Bytes=bytes;
    public int Downloads;
    public HttpStatusCode Status=HttpStatusCode.OK;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
        token.ThrowIfCancellationRequested();
        bool isManifest=request.RequestUri!.Host=="test.local";
        if(!isManifest) Downloads++;
        return Task.FromResult(new HttpResponseMessage(Status){Content=isManifest ? new StringContent(Manifest):new ByteArrayContent(Bytes)});
    }
}
