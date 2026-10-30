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
if(args.FirstOrDefault()=="--game-live") {
    var detector=new GameDetection();using var rtss=new RtssOverlay();
    for(int i=0;i<20;i++) {
        var game=detector.Foreground(Array.Empty<string>());
        var frames=game.HasValue?rtss.GetFrames(game.Value.Pid):null;
        Console.WriteLine(game.HasValue?$"Jogo: {game.Value.Name}, PID {game.Value.Pid}, RTSS: {frames?.Fps:0} FPS": "Aguardando jogo em primeiro plano.");
        if(args.Contains("--show-osd")) {
            if(game.HasValue && frames is not null) rtss.Publish($"<C=FFFF66>RESET EXPLODE  {frames.Fps:0} FPS  {frames.FrameTimeMs:0.0} ms<C>\nMinecraft - teste do overlay");
            else rtss.Clear();
        }
        Thread.Sleep(1000);
    }
    return;
}

int passed=0;
void Check(bool result,string name){if(!result)throw new Exception(name);Console.WriteLine("PASS: "+name);passed++;}
var design=OverlayDesign.Normalize(new OverlayDesign {FpsFirst=true,Rows=new(){new(){Key="CPU",Label="Processador",Color="#112233",Size=150,Template="{label}: {value}"}}});
var plan=OverlayLayout.Arrange(design,new[]{new OverlayTextRow("CPU","CPU","50%  60 C"),new OverlayTextRow("FPS","","120 FPS")},16,true,"#FFFFFF",false,(text,size)=>(text.Length*size/2,size));
Check(plan.Rows[0].Key=="FPS" && plan.Rows[1].Text=="Processador: 50%  60 C","ordem FPS e rotulo personalizado");
Check(plan.Rows[1].Color=="#112233" && plan.Rows[1].FontSize==24,"cor e tamanho por linha");
var markup=OverlayLayout.Rtss(plan,"Consolas",16,true,1,"BR",20,30,1920,1080,"#000000",.5,true);
Check(markup.Contains("<FNT=Consolas,-16,700,1>") && markup.Contains("<S=150><C=112233>"),"fonte e estilo emitidos para RTSS");
Check(markup.Contains("<C=80000000><B=0,0>") && markup.Contains("<B=0,0,1>"),"fundo transparente e borda no RTSS");
Check(markup.Contains($"<P={1920-plan.Width-20},{1080-plan.Height-30}>"),"canto inferior direito respeita dimensoes do jogo");
design.FreeLayout=true;design.Rows.First(r=>r.Key=="CPU").X=250;
var free=OverlayLayout.Arrange(design,new[]{new OverlayTextRow("CPU","CPU","60 C")},16,false,"#ABCDEF",false,(text,size)=>(100,20));
Check(free.Rows[0].X==250 && free.Rows[0].Color=="#ABCDEF","posicao livre e cor unica");
var corrupt=OverlayDesign.Normalize(new(){LineSpacing=double.NaN,Background="<bad>",GameAlias="<C=FF0000>\n",Rows=new(){new(){Key="CPU",Size=double.PositiveInfinity,Color="bad"}}});
Check(corrupt.LineSpacing==3 && corrupt.Rows.First(r=>r.Key=="CPU").Size==100 && !corrupt.GameAlias.Contains('<'),"perfil invalido normalizado e tags externas removidas");
Check(!OverlayDesign.IsFont("Arial,1><bad>") && OverlayDesign.IsFont("Segoe UI"),"fonte nao injeta comandos RTSS");
var roundtrip=System.Text.Json.JsonSerializer.Deserialize<OverlayDesign>(System.Text.Json.JsonSerializer.Serialize(design));
Check(roundtrip?.Rows.First(r=>r.Key=="CPU").Label=="Processador","perfil conserva personalizacao ao serializar");
design.Rows.First(r=>r.Key=="CPU").Template="CPU {cpu_temp} | {cpu_load}";
var custom=OverlayLayout.Arrange(design,new[]{new OverlayTextRow("CPU","CPU","unused",new Dictionary<string,string>{{"cpu_temp","60 C"},{"cpu_load","42%"}})},16,true,"#FFFFFF",false,(text,size)=>(100,20));
Check(custom.Rows[0].Text=="CPU 60 C | 42%","metricas individuais podem ser reorganizadas no formato");
design.Rows.First(r=>r.Key=="CPU").Template="";
Check(OverlayLayout.Arrange(design,new[]{new OverlayTextRow("CPU","CPU","60 C")},16,true,"#FFFFFF",false,(text,size)=>(100,20)).Rows.Count==0,"formato vazio oculta a linha");
var noGames=Array.Empty<LibGame>();var noManual=Array.Empty<string>();
Check(GameDetection.IsGame("javaw",@"C:\Users\Test\curseforge\minecraft\runtime\bin\javaw.exe",noGames,noManual,"Minecraft* 1.21.1 - Singleplayer"),"Minecraft Java do CurseForge reconhecido");
Check(GameDetection.IsGame("java",@"C:\Java\bin\java.exe",noGames,noManual,"Minecraft 1.21.1"),"Minecraft com Java externo reconhecido");
Check(!GameDetection.IsGame("javaw",@"C:\Java\bin\javaw.exe",noGames,noManual,"Editor Java"),"outros programas Java nao sao jogos");
Check(!GameDetection.IsGame("minecraft",@"C:\curseforge\minecraft\Install\minecraft.exe",noGames,noManual,"Minecraft Launcher"),"launcher do Minecraft nao recebe overlay");
Check(!GameDetection.IsGame("chrome",null,noGames,noManual,"Minecraft 1.21.1"),"titulo Minecraft no navegador nao vira jogo");
Check(GameDetection.IsGame("Minecraft.Windows",null,noGames,noManual,"Minecraft"),"Minecraft Bedrock reconhecido");
Check(GameDetection.IsGame("TS4_x64",null,noGames,noManual),"deteccao do The Sims preservada");
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
