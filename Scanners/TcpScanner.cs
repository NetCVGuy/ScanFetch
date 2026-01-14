using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using ScanFetch.Interfaces;
using ScanFetch.Models;

namespace ScanFetch.Scanners;

/// <summary>
/// Реализация TCP сканера через сокеты с событиями
/// </summary>
public class TcpScanner : IScanner
{
    private readonly ILogger<TcpScanner> _logger;
    private TcpClient? _client;
    private TcpListener? _listener;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private readonly bool _isServer;
    private readonly string? _listenInterface;

    public string Ip { get; }
    public int Port { get; }
    public string Role { get; }
    public event EventHandler<ScanDataEventArgs>? OnDataReceived;

    public TcpScanner(string ip, int port, string role, ILogger<TcpScanner> logger, string? listenInterface = null)
    {
        Ip = ip;
        Port = port;
        _logger = logger;
        _listenInterface = listenInterface;
        _isServer = string.Equals(role, "Server", StringComparison.OrdinalIgnoreCase);
        Role = role;
    }

    public async Task ConnectAsync(int timeoutSeconds)
    {
        if (_isServer)
        {
            // In server mode we do not start accepting here. Listener will be created in StartListeningAsync
            _logger.LogInformation("Режим сервера: подготовка завершена, слушатель будет запущен в StartListeningAsync");
            await Task.CompletedTask;
            return;
        }
        else
        {
            _client = new TcpClient();
            _logger.LogInformation("Подключаюсь к {Ip}:{Port} (таймаут {Timeout}с)...", Ip, Port, timeoutSeconds);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await _client.ConnectAsync(Ip, Port).WaitAsync(cts.Token);
                _stream = _client.GetStream();
                _logger.LogInformation("Подключено к сканеру {Ip}:{Port}!", Ip, Port);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Таймаут подключения к {Ip}:{Port}", Ip, Port);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Ошибка подключения к {Ip}:{Port}: {Message}", Ip, Port, ex.Message);
                throw;
            }
        }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        _stream?.Close();
        _client?.Close();
        if (_listener != null)
        {
            try { _listener.Stop(); } catch { }
            _listener = null;
        }
        _logger.LogInformation("Отключено от сканера {Ip}:{Port}", Ip, Port);
        await Task.CompletedTask;
    }

    public async Task StartListeningAsync()
    {
        _cts = new CancellationTokenSource();

        // Server mode: accept clients repeatedly and handle each until disconnect/error, then accept again.
        if (_isServer)
        {
            // Ensure listener exists: create and start if null (use interface selection)
            if (_listener == null)
            {
                IPAddress address;
                if (string.IsNullOrWhiteSpace(Ip) || Ip == "0.0.0.0" || Ip == "*")
                    address = IPAddress.Any;
                else
                    address = IPAddress.Parse(Ip);

                // For server mode we ignore configured Ip and pick a local interface IP if available
                var ifaces = GetActiveIPv4Interfaces();
                IPAddress chosenAddress = address; // fallback
                string displayIp;

                if (!string.IsNullOrWhiteSpace(_listenInterface))
                {
                    // Try to match by interface name or IP
                    var match = ifaces.FirstOrDefault(f => string.Equals(f.Name, _listenInterface, StringComparison.OrdinalIgnoreCase)
                                                          || string.Equals(f.Address.ToString(), _listenInterface, StringComparison.OrdinalIgnoreCase));
                    if (!Equals(match, default((string, IPAddress))))
                    {
                        chosenAddress = match.Address;
                        displayIp = chosenAddress.ToString();
                        _logger.LogInformation("Привязываюсь к интерфейсу (конфигурация ListenInterface) {IfName} ({Ip})", match.Name, displayIp);
                    }
                    else
                    {
                        _logger.LogWarning("Указанный ListenInterface '{Iface}' не найден, продолжаю выбор интерфейса...", _listenInterface);
                    }
                }

                if (ifaces.Count == 0)
                {
                    chosenAddress = address.Equals(IPAddress.Any) ? IPAddress.Any : address;
                    displayIp = chosenAddress.Equals(IPAddress.Any) ? "0.0.0.0" : chosenAddress.ToString();
                    _logger.LogWarning("Не найдено активных сетевых интерфейсов, привязываюсь к {Ip}", displayIp);
                }
                else if (ifaces.Count == 1)
                {
                    chosenAddress = ifaces[0].Address;
                    displayIp = chosenAddress.ToString();
                    _logger.LogInformation("Выбран единственный активный интерфейс {IfName} ({Ip}) для прослушивания", ifaces[0].Name, displayIp);
                }
                else
                {
                    // Multiple interfaces - ask user to choose
                    _logger.LogInformation("Найдено несколько сетевых интерфейсов. Выберите, какой использовать для прослушивания:");
                    for (int i = 0; i < ifaces.Count; i++)
                    {
                        Console.WriteLine($"[{i+1}] {ifaces[i].Name} - {ifaces[i].Address}");
                    }
                    Console.Write("Введите номер интерфейса (по умолчанию 1): ");
                    var input = Console.ReadLine();
                    int idx = 1;
                    if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input.Trim(), out var sel) && sel >= 1 && sel <= ifaces.Count)
                        idx = sel;
                    chosenAddress = ifaces[idx-1].Address;
                    displayIp = chosenAddress.ToString();
                    _logger.LogInformation("Выбрана опция {Index}: {IfName} ({Ip})", idx, ifaces[idx-1].Name, displayIp);
                }

                _listener = new TcpListener(chosenAddress, Port);
                _listener.Start();
                _logger.LogInformation("TCP сервер запущен и слушает на {Ip}:{Port}", displayIp, Port);
            }

            _logger.LogInformation("Запущено прослушивание в режиме сервера на порту {Port}", Port);

            while (!_cts.Token.IsCancellationRequested)
            {
                TcpClient? client = null;
                NetworkStream? clientStream = null;
                try
                {
                    client = await _listener.AcceptTcpClientAsync();
                    if (client == null) continue;
                    clientStream = client.GetStream();
                    var clientRemoteEp = client?.Client?.RemoteEndPoint;
                    _logger.LogInformation("Клиент подключился (Remote: {Remote})", clientRemoteEp);

                    var buffer = new byte[1024];
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        int bytesRead = await clientStream.ReadAsync(buffer, _cts.Token);
                        if (bytesRead == 0)
                        {
                            var remoteEp = client?.Client?.RemoteEndPoint;
                            _logger.LogWarning("Клиент отключился (Remote: {Remote})", remoteEp);
                            break;
                        }

                            var code = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                            var remote = client?.Client?.RemoteEndPoint?.ToString();
                            _logger.LogInformation("Получено на порту {Port} от {Remote}: {Code}", Port, remote, code);

                            OnDataReceived?.Invoke(this, new ScanDataEventArgs { Code = code, Timestamp = DateTime.Now, RemoteEndPoint = remote });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка при обслуживании клиентского соединения на порту {Port}", Port);
                }
                finally
                {
                    try { clientStream?.Close(); } catch { }
                    try { client?.Close(); } catch { }
                }

                await Task.Delay(250, CancellationToken.None);
            }

            _logger.LogInformation("Остановка серверного прослушивания на порту {Port}", Port);
            return;
        }

        // Client mode: existing behavior (connected client already present)
        if (_stream == null || _client == null)
            throw new InvalidOperationException("Сначала вызови ConnectAsync, бро!");

        var bufferClient = new byte[1024];
        try
        {
            _logger.LogInformation("Запущено прослушивание в режиме клиента {Ip}:{Port}", Ip, Port);
            while (!_cts.Token.IsCancellationRequested)
            {
                // Send trigger
                var triggerCommand = Encoding.ASCII.GetBytes("TRG\r\n");
                await _stream.WriteAsync(triggerCommand, _cts.Token);

                int bytesRead = await _stream.ReadAsync(bufferClient, _cts.Token);
                if (bytesRead == 0)
                {
                    _logger.LogWarning("Соединение закрыто {Ip}:{Port}", Ip, Port);
                    break;
                }

                var code = Encoding.UTF8.GetString(bufferClient, 0, bytesRead).Trim();
                var remoteClient = _client?.Client?.RemoteEndPoint?.ToString() ?? (Ip + ":" + Port);
                _logger.LogDebug("Получены данные {Ip}:{Port} (Remote: {Remote}): {Code}", Ip, Port, remoteClient, code);

                OnDataReceived?.Invoke(this, new ScanDataEventArgs { Code = code, Timestamp = DateTime.Now, RemoteEndPoint = remoteClient });

                await Task.Delay(100, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Прослушивание остановлено для {Ip}:{Port}", Ip, Port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при прослушивании сканера {Ip}:{Port}", Ip, Port);
            throw;
        }
    }

    private static string? GetLocalIpForOutbound()
    {
        try
        {
            // Create a UDP socket to a public IP to determine the local outbound IP.
            using var socket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint ep)
                return ep.Address.ToString();
        }
        catch
        {
            // Fallback: enumerate host addresses
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var addr = host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
                return addr?.ToString();
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static List<(string Name, IPAddress Address)> GetActiveIPv4Interfaces()
    {
        var list = new List<(string, IPAddress)>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var props = ni.GetIPProperties();
                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
                    {
                        list.Add((ni.Name, ua.Address));
                    }
                }
            }
        }
        catch
        {
            // ignore and return what we have
        }

        return list;
    }
}
