namespace ScanFetch.Models;

/// <summary>
/// Класс события для данных со сканера
/// </summary>
public class ScanDataEventArgs : EventArgs
{
    public string Code { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    // Remote endpoint (e.g. remote IP:port) from which this scan originated when in server mode
    public string? RemoteEndPoint { get; set; }
}
