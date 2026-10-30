using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace ResetExplode;

public sealed record RtssFrameSample(double Fps,double FrameTimeMs);
/// <summary>Publishes only our own OSD slot through RTSS's public v2.14+ shared-memory SDK.</summary>
public sealed class RtssOverlay : IDisposable
{
    private readonly string _name;
    private MemoryMappedFile? _map;
    private MemoryMappedViewAccessor? _view;
    private long _retryAt;
    private const string Owner="ResetExplode";
    public string Status {get;private set;}="RTSS ainda não conectado.";
    public RtssOverlay():this("RTSSSharedMemoryV2"){}
    internal RtssOverlay(string name)=>_name=name;
    public bool Publish(string text)=>Write(text,false);
    public void Clear(){if(_view!=null)Write("",true);}
    private bool Connect()
    {
        if(_view!=null)return true;
        if(Environment.TickCount64<_retryAt)return false;
        _retryAt=Environment.TickCount64+2000;
        _map=MemoryMappedFile.OpenExisting(_name,MemoryMappedFileRights.ReadWrite);
        _view=_map.CreateViewAccessor(0,0,MemoryMappedFileAccess.ReadWrite);
        return true;
    }
    public RtssFrameSample? GetFrames(int processId)
    {
        try {
            if(!Connect())return null;
            var v=_view!;
            if(v.Capacity<40 || v.ReadUInt32(0)!=0x52545353 || (v.ReadUInt32(4)>>16)!=2)return null;
            uint size=v.ReadUInt32(8),offset=v.ReadUInt32(12),count=v.ReadUInt32(16);
            if(size<284 || size>1024*1024 || offset<40 || count>4096 || offset+(long)size*count>v.Capacity)return null;
            for(int i=0;i<count;i++) {
                long at=offset+(long)i*size;
                if(v.ReadUInt32(at)!=(uint)processId)continue;
                uint t0=v.ReadUInt32(at+268),t1=v.ReadUInt32(at+272),frames=v.ReadUInt32(at+276),micros=v.ReadUInt32(at+280);
                uint delta=unchecked(t1-t0),age=unchecked((uint)Environment.TickCount64-t1);
                if(t0==0 || delta==0 || delta>10000 || age>5000 || frames==0)return null;
                double fps=1000d*frames/delta;
                if(fps<=0 || fps>10000)return null;
                return new(fps,micros>0?micros/1000d:1000d/fps);
            }
            Status="RTSS aberto, mas não detectou este jogo; usando overlay em janela.";return null;
        }catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or ArgumentException){Status="RTSS não conectado; usando overlay em janela.";Disconnect();return null;}
    }
    public bool HasApplication(int processId)
    {
        try {
            if(!Connect())return false;var v=_view!;
            if(v.Capacity<40||v.ReadUInt32(0)!=0x52545353||(v.ReadUInt32(4)>>16)!=2)return false;
            uint size=v.ReadUInt32(8),offset=v.ReadUInt32(12),count=v.ReadUInt32(16);
            if(size<284||size>1024*1024||offset<40||count>4096||offset+(long)size*count>v.Capacity)return false;
            for(int i=0;i<count;i++)if(v.ReadUInt32(offset+(long)i*size)==(uint)processId)return true;
            return false;
        }catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or ArgumentException){Disconnect();return false;}
    }
    private unsafe bool Write(string text,bool release)
    {
        try {
            if(_view==null) {
                if(release || !Connect())return false;
            }
            var view=_view!;
            if(view.Capacity<40 || view.ReadUInt32(0)!=0x52545353 || view.ReadUInt32(4)<0x2000e || (view.ReadUInt32(4)>>16)!=2)
                throw new InvalidDataException("RTSS requer compartilhamento v2.14 ou superior.");
            uint size=view.ReadUInt32(20),offset=view.ReadUInt32(24),count=view.ReadUInt32(28);
            if(size<4608 || size>1024*1024 || offset<40 || count<2 || count>1024 || (long)offset+(long)size*count>view.Capacity)
                throw new InvalidDataException("Estrutura RTSS inválida.");
            byte* pointer=null;view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
            try {
                pointer+=view.PointerOffset;
                ref int busy=ref *(int*)(pointer+36);
                if(Interlocked.CompareExchange(ref busy,1,0)!=0){Status="RTSS ocupado; tentando no próximo ciclo.";return false;}
                try {
                    long slot=-1,empty=-1;var owner=new byte[256];
                    // Slot zero belongs to the primary client, usually MSI Afterburner.
                    for(int i=1;i<count;i++) {
                        long at=offset+(long)i*size;view.ReadArray(at+256,owner,0,256);
                        string id=Encoding.ASCII.GetString(owner).TrimEnd('\0');
                        if(id==Owner){slot=at;break;}if(owner[0]==0 && empty<0)empty=at;
                    }
                    if(slot<0 && !release)slot=empty;
                    if(slot<0){Status=release?"RTSS liberado.":"RTSS sem slot livre; overlay em janela ativo.";return false;}
                    void Put(long at,string value,int capacity) {
                        byte[] data=Encoding.Latin1.GetBytes(value);int n=Math.Min(data.Length,capacity-1);
                        view.WriteArray(at,data,0,n);view.Write(at+n,(byte)0);
                    }
                    if(!release)Put(slot+256,Owner,256);
                    Put(slot,"",256);Put(slot+512,text,4096);
                    // RTSS >= 2.20 also has an additional text field; leave it empty for this client.
                    if(view.ReadUInt32(4)>=0x20014 && size>=299520)Put(slot+266752,"",32768);
                    if(release)view.WriteArray(slot+256,new byte[256],0,256);
                    view.Write(32,unchecked(view.ReadUInt32(32)+1));
                    if(!release)Status="RTSS conectado · overlay renderizado dentro do jogo.";
                    return true;
                }finally{Interlocked.And(ref busy,~1);}
            }finally{view.SafeMemoryMappedViewHandle.ReleasePointer();}
        } catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) {
            Status=ex is InvalidDataException ? ex.Message : "RTSS não está aberto; usando overlay em janela.";
            Disconnect();return false;
        }
    }
    private void Disconnect(){_view?.Dispose();_view=null;_map?.Dispose();_map=null;}
    public void Dispose(){Clear();Disconnect();}
}
