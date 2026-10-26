using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace ResetExplode;

public sealed record AfterburnerReading(string Name,string Unit,float Value,uint SourceId,uint GpuIndex)
{
    public string Display => $"{Value:0.##} {Unit}";
    public float Minimum {get;init;}=Value;
    public float Maximum {get;init;}=Value;
    public string MinDisplay=>$"{Minimum:0.##} {Unit}";
    public string MaxDisplay=>$"{Maximum:0.##} {Unit}";
}
public sealed record AfterburnerSnapshot(IReadOnlyList<AfterburnerReading> Entries,IReadOnlyList<string> Gpus)
{
    public static readonly AfterburnerSnapshot Empty = new(Array.Empty<AfterburnerReading>(),Array.Empty<string>());
    // Aggregate CPU sources are distinct from per-core sources even when they share a source ID.
    public float? Global(uint id,string name) => Entries.FirstOrDefault(e=>e.SourceId==id && e.GpuIndex==uint.MaxValue && e.Name.Equals(name,StringComparison.OrdinalIgnoreCase))?.Value;
    public float? Gpu(uint id,string device)
    {
        if(string.IsNullOrWhiteSpace(device)) return null;
        int index=-1;
        for(int i=0;i<Gpus.Count;i++)
            if(!string.IsNullOrWhiteSpace(Gpus[i]) && (Gpus[i].Equals(device,StringComparison.OrdinalIgnoreCase) || device.Contains(Gpus[i],StringComparison.OrdinalIgnoreCase))) {index=i;break;}
        return index<0 ? null : Entries.FirstOrDefault(e=>e.SourceId==id && e.GpuIndex==(uint)index)?.Value;
    }
}

/// <summary>Read-only consumer of the public MAHM v2 SDK. No driver access or control writes.</summary>
public sealed class AfterburnerMonitor
{
    private long _readAt;
    private byte[] _buffer=Array.Empty<byte>();
    private readonly Dictionary<(uint,uint,string),(float Min,float Max)> _extrema=new();
    private AfterburnerSnapshot _snapshot=AfterburnerSnapshot.Empty;
    public string Status {get;private set;}="MSI Afterburner: ainda não consultado.";
    public AfterburnerSnapshot Read()
    {
        long now=Environment.TickCount64;
        if(now-_readAt<1000) return _snapshot;
        _readAt=now;
        try {
            using var map=MemoryMappedFile.OpenExisting("MAHMSharedMemory",MemoryMappedFileRights.Read);
            using var view=map.CreateViewAccessor(0,0,MemoryMappedFileAccess.Read);
            if(view.Capacity<32 || view.Capacity>32*1024*1024) return Missing("Formato de monitoramento MSI indisponível.");
            if(_buffer.Length!=view.Capacity)_buffer=new byte[(int)view.Capacity];
            view.ReadArray(0,_buffer,0,_buffer.Length);
            var parsed=Parse(_buffer,DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if(_extrema.Count>8192)_extrema.Clear();
            var entries=parsed.Entries.Select(e=>{
                var id=(e.SourceId,e.GpuIndex,e.Name);
                var old=_extrema.GetValueOrDefault(id,(e.Value,e.Value));
                var range=(Math.Min(old.Item1,e.Value),Math.Max(old.Item2,e.Value));_extrema[id]=range;
                return e with {Minimum=range.Item1,Maximum=range.Item2};
            }).ToArray();
            _snapshot=new(entries,parsed.Gpus);
            Status=_snapshot.Entries.Count>0 ? $"MSI Afterburner conectado · {_snapshot.Entries.Count} sensores válidos · coleta de 1 s."
                : "MSI sem amostra recente: usando os sensores internos.";
            return _snapshot;
        } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException) {
            return Missing("MSI indisponível: abra o Afterburner para compartilhar seus sensores.");
        }
    }
    private AfterburnerSnapshot Missing(string status){Status=status;return _snapshot=AfterburnerSnapshot.Empty;}
    public void ResetExtrema(){_extrema.Clear();_readAt=0;}
    public static AfterburnerSnapshot Parse(byte[] bytes,long now)
    {
        if(bytes.Length<32) return AfterburnerSnapshot.Empty;
        uint U(int at)=>BitConverter.ToUInt32(bytes,at);
        if(U(0)!=0x4D41484D || (U(4)>>16)!=2 || Math.Abs(now-U(20))>5) return AfterburnerSnapshot.Empty;
        uint header=U(8),count=U(12),size=U(16),gpuCount=U(24),gpuSize=U(28);
        if(header<32 || header>4096 || count>4096 || size<1324 || size>16384 || gpuCount>64
            || (gpuCount>0 && (gpuSize<1304 || gpuSize>16384))) return AfterburnerSnapshot.Empty;
        long entriesEnd=header+(long)count*size;
        if(entriesEnd+(long)gpuCount*gpuSize>bytes.Length) return AfterburnerSnapshot.Empty;
        string Text(int at,int length) {int end=Array.IndexOf(bytes,(byte)0,at,length);return Encoding.Latin1.GetString(bytes,at,(end<0?at+length:end)-at);}
        var entries=new List<AfterburnerReading>();
        for(int i=0;i<count;i++) {
            int at=(int)(header+(long)i*size);float value=BitConverter.ToSingle(bytes,at+1300);
            if(!float.IsFinite(value) || Math.Abs(value)>1e15f) continue;
            string name=Text(at,260);if(string.IsNullOrWhiteSpace(name)) continue;
            entries.Add(new(name,Text(at+260,260),value,U(at+1320),U(at+1316)));
        }
        var gpus=new List<string>();
        for(int i=0;i<gpuCount;i++) gpus.Add(Text((int)(entriesEnd+(long)i*gpuSize+520),260));
        return new(entries,gpus);
    }
}
