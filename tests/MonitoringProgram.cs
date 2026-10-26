using System.IO.MemoryMappedFiles;
using System.Text;
using ResetExplode;

if(args.FirstOrDefault()=="--live") {
    var monitor=new AfterburnerMonitor();var actual=monitor.Read();Console.WriteLine(monitor.Status);
    foreach(var entry in actual.Entries.Take(18))Console.WriteLine($"{entry.Name}: {entry.Display} (id {entry.SourceId:X}, gpu {entry.GpuIndex})");
    Console.WriteLine("GPUs: "+string.Join(", ",actual.Gpus));return;
}
if(args.FirstOrDefault()=="--rtss-live") {
    using var publisher=new RtssOverlay();
    if(!publisher.Publish("RESET / TESTE DE INTEGRACAO"))throw new Exception(publisher.Status);
    Console.WriteLine("PASS: slot do RTSS real publicado pelo SDK: "+publisher.Status);
    publisher.Clear();Console.WriteLine("PASS: slot real liberado sem alterar o MSI.");return;
}

int passed=0;
void Check(bool result,string name){if(!result)throw new Exception(name);Console.WriteLine("PASS: "+name);passed++;}
byte[] source=new byte[32+1324*3+1304];
void U(int at,uint value)=>BitConverter.GetBytes(value).CopyTo(source,at);
void Text(int at,string value)=>Encoding.Latin1.GetBytes(value).CopyTo(source,at);
void Entry(int index,string name,uint id,float value,uint gpu=uint.MaxValue){int at=32+1324*index;Text(at,name);Text(at+260,"C");BitConverter.GetBytes(value).CopyTo(source,at+1300);U(at+1316,gpu);U(at+1320,id);}
long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
U(0,0x4D41484D);U(4,0x20000);U(8,32);U(12,3);U(16,1324);U(20,(uint)now);U(24,1);U(28,1304);
Entry(0,"CPU temperature",0x80,58);Entry(1,"CPU1 temperature",0x80,66);Entry(2,"GPU temperature",0,60,0);Text(32+1324*3+520,"Test GPU");
var snapshot=AfterburnerMonitor.Parse(source,now);
Check(snapshot.Entries.Count==3,"layout MAHM v2 interpretado");
Check(snapshot.Global(0x80,"CPU temperature")==58,"temperatura total nao confundida com nucleo");
Check(snapshot.Gpu(0,"Test GPU")==60,"GPU selecionada por identidade");
Check(snapshot.Gpu(0,"Other GPU") is null,"nao atribui sensor de outra GPU");
Check((snapshot with {Gpus=new[]{"Test GPU","Test GPU"}}).Gpu(0,"Test GPU") is null,"GPUs de mesmo nome exigem fallback interno");
Check((snapshot with {Entries=snapshot.Entries.Where(e=>e.Name!="CPU temperature").ToArray()}).Global(0x80,"CPU temperature") is null,"nao inventa temperatura total a partir de nucleos");
Check(AfterburnerMonitor.Parse(source,now+8).Entries.Count==0,"amostra antiga descartada");
U(12,uint.MaxValue);Check(AfterburnerMonitor.Parse(source,now).Entries.Count==0,"contagem corrompida rejeitada");U(12,3);
U(8,uint.MaxValue);Check(AfterburnerMonitor.Parse(source,now).Entries.Count==0,"offset corrompido rejeitado");U(8,32);
Entry(0,"CPU temperature",0x80,float.MaxValue);Check(AfterburnerMonitor.Parse(source,now).Global(0x80,"CPU temperature") is null,"sentinela FLT_MAX nao vira temperatura");
string name="ResetExplode-RTSS-Test-"+Guid.NewGuid().ToString("N");
using(var map=MemoryMappedFile.CreateNew(name,40+4608*4+284))
using(var view=map.CreateViewAccessor()) {
    view.Write(0,0x52545353u);view.Write(4,0x2000eu);view.Write(20,4608u);view.Write(24,40u);view.Write(28,4u);
    long app=40+4608*4;view.Write(8,284u);view.Write(12,(uint)app);view.Write(16,1u);view.Write(app,12345u);
    uint tick=unchecked((uint)Environment.TickCount64);view.Write(app+268,tick-1000);view.Write(app+272,tick);view.Write(app+276,120u);view.Write(app+280,8333u);
    view.WriteArray(40+256,Encoding.ASCII.GetBytes("MSIAfterburner\0"),0,15);
    using var overlay=new RtssOverlay(name);
    Check(overlay.GetFrames(12345) is {Fps:120},"FPS vem do processo correto no RTSS");
    Check(overlay.GetFrames(54321) is null,"nao publica RTSS em jogo sem hook");
    view.Write(app+272,tick-6000);Check(overlay.GetFrames(12345) is null,"estatistica de frames antiga descartada");view.Write(app+272,tick);
    Check(overlay.Publish("GPU 60 C\nCPU 58 C"),"publicacao em slot RTSS proprio");
    var owner=new byte[256];view.ReadArray(40+256,owner,0,256);
    Check(Encoding.ASCII.GetString(owner).StartsWith("MSIAfterburner"),"slot primario do MSI preservado");
    var text=new byte[4096];view.ReadArray(40+4608+512,text,0,4096);
    Check(Encoding.Latin1.GetString(text).StartsWith("GPU 60 C"),"texto gravado no campo estendido");
    Check(view.ReadInt32(36)==0,"trava compartilhada liberada");
    view.Write(36,1);Check(!overlay.Publish("blocked"),"nao escreve enquanto outro cliente segura trava");Check(view.ReadInt32(36)==1,"nao remove trava de outro cliente");view.Write(36,0);
    overlay.Clear();view.ReadArray(40+4608+256,owner,0,256);Check(owner.All(b=>b==0),"slot proprio liberado ao ocultar");
    view.Write(24,uint.MaxValue);Check(!overlay.Publish("invalid"),"offset RTSS invalido nao causa escrita");
}
using(var absent=new RtssOverlay("Absent-"+Guid.NewGuid()))Check(!absent.Publish("no server"),"RTSS ausente permite fallback");
Console.WriteLine($"{passed} verificacoes de integracao aprovadas.");
