using ScanFetch.Models;

namespace ScanFetch.Interfaces;

/// <summary>
/// Интерфейс для сканеров (чтобы можно было подключать несколько с разными IP/портами)
/// </summary>
public interface IScanner
{
    string Ip { get; }
    int Port { get; }
    
    /// <summary>
    /// Событие получения данных со сканера
    /// </summary>
    event EventHandler<ScanDataEventArgs>? OnDataReceived;
    
    Task ConnectAsync(int timeoutSeconds);
    Task DisconnectAsync();
    Task StartListeningAsync();
}
