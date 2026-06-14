namespace GameAccelerator.Core.Network;

public class TrafficCounter
{
    private long _upstreamBytes;
    private long _downstreamBytes;
    private long _lastUpBytes;
    private long _lastDownBytes;
    private DateTime _lastSnapshot;

    public TrafficCounter()
    {
        _lastSnapshot = DateTime.UtcNow;
    }

    public void AddUpload(long bytes) => Interlocked.Add(ref _upstreamBytes, bytes);
    public void AddDownload(long bytes) => Interlocked.Add(ref _downstreamBytes, bytes);

    public (long uploadPerSec, long downloadPerSec) GetSpeed()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastSnapshot).TotalSeconds;
        if (elapsed < 0.1) elapsed = 0.1;

        var currentUp = Interlocked.Read(ref _upstreamBytes);
        var currentDown = Interlocked.Read(ref _downstreamBytes);

        var upSpeed = (long)((currentUp - _lastUpBytes) / elapsed);
        var downSpeed = (long)((currentDown - _lastDownBytes) / elapsed);

        _lastUpBytes = currentUp;
        _lastDownBytes = currentDown;
        _lastSnapshot = now;

        return (upSpeed, downSpeed);
    }

    public (long totalUp, long totalDown) GetTotals()
    {
        return (Interlocked.Read(ref _upstreamBytes), Interlocked.Read(ref _downstreamBytes));
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _upstreamBytes, 0);
        Interlocked.Exchange(ref _downstreamBytes, 0);
        _lastUpBytes = 0;
        _lastDownBytes = 0;
        _lastSnapshot = DateTime.UtcNow;
    }
}
