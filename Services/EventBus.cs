using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ScanFetch.Services;

public enum EventType
{
    ScannerConnected,
    ScannerDisconnected,
    ScannerError,
    ScanReceived,
    ApplicationStarted,
    ApplicationStopped
}

public class ScannerEvent
{
    public EventType Type { get; set; }
    public string ScannerName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? RemoteEndpoint { get; set; }
    public string? ErrorDetails { get; set; }
}

public class EventBus
{
    private readonly Channel<ScannerEvent> _eventChannel;
    private readonly ConcurrentBag<ScannerEvent> _eventHistory;
    private readonly int _maxHistorySize;

    public EventBus(int maxHistorySize = 100)
    {
        _maxHistorySize = maxHistorySize;
        _eventChannel = Channel.CreateUnbounded<ScannerEvent>();
        _eventHistory = new ConcurrentBag<ScannerEvent>();
    }

    public void Publish(ScannerEvent scannerEvent)
    {
        // Add to history
        _eventHistory.Add(scannerEvent);
        
        // Trim history if too large
        if (_eventHistory.Count > _maxHistorySize)
        {
            var sorted = _eventHistory.OrderByDescending(e => e.Timestamp).Take(_maxHistorySize).ToList();
            _eventHistory.Clear();
            foreach (var evt in sorted)
            {
                _eventHistory.Add(evt);
            }
        }

        // Publish to channel for SSE subscribers
        _eventChannel.Writer.TryWrite(scannerEvent);
    }

    public ChannelReader<ScannerEvent> Subscribe()
    {
        return _eventChannel.Reader;
    }

    public IEnumerable<ScannerEvent> GetHistory(int count = 50)
    {
        return _eventHistory
            .OrderByDescending(e => e.Timestamp)
            .Take(count);
    }

    public IEnumerable<ScannerEvent> GetErrors(int count = 50)
    {
        return _eventHistory
            .Where(e => e.Type == EventType.ScannerError || e.Type == EventType.ScannerDisconnected)
            .OrderByDescending(e => e.Timestamp)
            .Take(count);
    }
}
