namespace ResetExplode;

public readonly record struct OverlayGameTarget(IntPtr Window,int Pid,string Name);
public sealed class OverlayGameSession
{
    private OverlayGameTarget? _last;
    public OverlayGameTarget? Resolve(OverlayGameTarget? foreground,bool keep,Func<OverlayGameTarget,bool> alive)
    {
        if(foreground.HasValue)_last=foreground;
        if(_last.HasValue&&!alive(_last.Value))_last=null;
        return foreground.HasValue?_last:keep?_last:null;
    }
}
