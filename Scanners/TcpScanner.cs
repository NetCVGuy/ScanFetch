using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using ScanFetch.Interfaces;
using ScanFetch.Models;
using ScanFetch.Services;

namespace ScanFetch.Scanners;

/// <summary>
/// Реализация TCP сканера через сокеты с событиями
/// </summary>
public class TcpScanner : IScanner
{
    private readonly ILogger<TcpScanner> _logger;
    private readonly EventBus? _eventBus;
    private TcpClient? _client;
    private TcpListener? _listener;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private readonly bool _isServer;
    private readonly string? _listenInterface;
    private readonly string? _delimiter; // Custom delimiter
    private readonly string? _startsWithFilter; // Prefix filter
    private readonly int _requestIntervalMs;
    private readonly int _timeoutFlushMs; // Configurable timeout flush delay

    public string Ip { get; }
    public int Port { get; }
    public string Role { get; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool IsConnected { get; private set; } = false;
    public string? RemoteEndpoint { get; private set; }
    public event EventHandler<ScanDataEventArgs>? OnDataReceived;

    public TcpScanner(string ip, int port, string role, ILogger<TcpScanner> logger, string? listenInterface = null, string? delimiter = null, string? startsWithFilter = null, int requestIntervalMs = 100, int timeoutFlushMs = 50, EventBus? eventBus = null)
    {
        Ip = ip;
        Port = port;
        _logger = logger;
        _eventBus = eventBus;
        _listenInterface = listenInterface;
        _startsWithFilter = startsWithFilter;
        _requestIntervalMs = requestIntervalMs;
        _timeoutFlushMs = timeoutFlushMs;
        _isServer = string.Equals(role, "Server", StringComparison.OrdinalIgnoreCase);
        Role = role;

        if (!string.IsNullOrWhiteSpace(delimiter))
        {
            if (delimiter.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                // Hex format: 0x0D0A
                try
                {
                    var hex = delimiter.Substring(2);
                    var bytes = Convert.FromHexString(hex);
                    _delimiter = Encoding.UTF8.GetString(bytes);
                }
                catch (Exception ex) 
                {
                    _logger.LogWarning("Не удалось распарсить HEX разделитель '{Delim}': {Msg}. Использую как текст.", delimiter, ex.Message);
                    _delimiter = delimiter;
                }
            }
            else
            {
                // Text format with escaping support
                _delimiter = delimiter
                    .Replace("\\r", "\r")
                    .Replace("\\n", "\n")
                    .Replace("\\t", "\t")
                    .Replace("\\0", "\0");
            }
        }
        else
        {
            _delimiter = null;
        }
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
                IsConnected = true;
                RemoteEndpoint = _client?.Client?.RemoteEndPoint?.ToString();
                _logger.LogInformation("Подключено к сканеру {Ip}:{Port}!", Ip, Port);
                _eventBus?.Publish(new ScannerEvent
                {
                    Type = EventType.ScannerConnected,
                    ScannerName = Name,
                    Message = $"Подключено к {Ip}:{Port}"
                });
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Таймаут подключения к {Ip}:{Port}", Ip, Port);
                _eventBus?.Publish(new ScannerEvent
                {
                    Type = EventType.ScannerError,
                    ScannerName = Name,
                    Message = $"Таймаут подключения к {Ip}:{Port}",
                    ErrorDetails = "Connection timeout"
                });
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Ошибка подключения к {Ip}:{Port}: {Message}", Ip, Port, ex.Message);
                _eventBus?.Publish(new ScannerEvent
                {
                    Type = EventType.ScannerError,
                    ScannerName = Name,
                    Message = $"Ошибка подключения: {ex.Message}",
                    ErrorDetails = ex.ToString()
                });
                throw;
            }
        }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();

        _eventBus?.Publish(new ScannerEvent
        {
            Type = EventType.ScannerDisconnected,
            ScannerName = Name,
            Message = $"Отключение {(IsConnected ? "активное" : "неактивное")}",
            RemoteEndpoint = RemoteEndpoint
        });
        
        IsConnected = false;
        RemoteEndpoint = null;
        
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

        if (!string.IsNullOrEmpty(_delimiter))
            _logger.LogInformation("Режим разделителя: Пользовательский ['{Delim}'] (Hex: {Hex})", 
                _delimiter.Replace("\r", "<CR>").Replace("\n", "<LF>"),
                BitConverter.ToString(Encoding.UTF8.GetBytes(_delimiter)));
        else
            _logger.LogInformation("Режим разделителя: Автоматический (CR/LF)");

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

                    _eventBus?.Publish(new ScannerEvent
                    {
                        Type = EventType.ScannerConnected,
                        ScannerName = Name,
                        Message = $"Клиент подключился",
                        RemoteEndpoint = clientRemoteEp?.ToString()
                    });

                    var buffer = new byte[1024];
                    var sb = new StringBuilder(); // Buffer for fragmentation

                    while (!_cts.Token.IsCancellationRequested)
                    {
                        int bytesRead = await clientStream.ReadAsync(buffer, _cts.Token);
                        if (bytesRead == 0)
                        {
                            var remoteEp = client?.Client?.RemoteEndPoint;
                            _logger.LogWarning("Клиент отключился (Remote: {Remote})", remoteEp);

                            _eventBus?.Publish(new ScannerEvent
                            {
                                Type = EventType.ScannerDisconnected,
                                ScannerName = Name,
                                Message = "Клиент отключился",
                                RemoteEndpoint = remoteEp?.ToString()
                            });

                            // Flush remaining buffer on disconnect
                            if (sb.Length > 0)
                            {
                                var leftovers = sb.ToString().Trim();
                                if (!string.IsNullOrWhiteSpace(leftovers))
                                {
                                    _logger.LogWarning("В буфере остались данные без разделителя, считаем это сканом: {Code}", leftovers);
                                    OnDataReceived?.Invoke(this, new ScanDataEventArgs { Code = leftovers, Timestamp = DateTime.Now, RemoteEndPoint = remoteEp?.ToString() });
                                }
                            }
                            break;
                        }

                        // Debug log to show raw data arrived
                        var rawChunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        // Используем LogInformation чтобы точно видеть приходящие байты в консоли
                        _logger.LogInformation("[RAW] Принято {Bytes} байт: {RawHex} | Текст: {Ascii}", 
                            bytesRead, 
                            BitConverter.ToString(buffer, 0, bytesRead), 
                            rawChunk.Replace("\r", "<CR>").Replace("\n", "<LF>"));

                        // Append received chunk
                        sb.Append(rawChunk);
                        
                        // Если в буфере нет разделителей, предупреждаем один раз (или можно в дебаг)
                        if (sb.Length > 0 && !rawChunk.Contains('\n') && !rawChunk.Contains('\r'))
                        {
                            _logger.LogDebug("Данные в буфере ({Length} байт), ждем разделитель (CR/LF)...", sb.Length);
                        }

                        // Process complete lines
                        string currentData = sb.ToString();

                        while (true)
                        {
                            int cutIndex = -1;
                            int jump = 0;

                            // Custom delimiter logic
                            if (!string.IsNullOrEmpty(_delimiter))
                            {
                                int idx = currentData.IndexOf(_delimiter);
                                if (idx != -1)
                                {
                                    cutIndex = idx;
                                    jump = _delimiter.Length;
                                }
                            }
                            // Default logic (\r or \n)
                            else 
                            { 
                                int rIndex = currentData.IndexOf('\r');
                                int nIndex = currentData.IndexOf('\n');

                                if (rIndex != -1 && nIndex != -1) cutIndex = Math.Min(rIndex, nIndex);
                                else if (rIndex != -1) cutIndex = rIndex;
                                else if (nIndex != -1) cutIndex = nIndex;

                                if (cutIndex != -1)
                                {
                                    jump = 1;
                                    if (currentData[cutIndex] == '\r' && cutIndex + 1 < currentData.Length && currentData[cutIndex + 1] == '\n')
                                    {
                                        jump = 2;
                                    }
                                }
                            }

                            if (cutIndex == -1) break;

                             var code = currentData.Substring(0, cutIndex).Trim();
                             
                             currentData = currentData.Substring(cutIndex + jump);
                             sb.Clear();
                             sb.Append(currentData);
                             
                             if (!string.IsNullOrWhiteSpace(code))
                             {
                                if (!string.IsNullOrEmpty(_startsWithFilter) && !code.StartsWith(_startsWithFilter))
                                {
                                    _logger.LogTrace("Скан '{Code}' отфильтрован (не начинается с '{Filter}')", code, _startsWithFilter);
                                }
                                else
                                {
                                    var remote = client?.Client?.RemoteEndPoint?.ToString();
                                    _logger.LogInformation("Получено на порту {Port} от {Remote}: {Code}", Port, remote, code);
                                    OnDataReceived?.Invoke(this, new ScanDataEventArgs { Code = code, Timestamp = DateTime.Now, RemoteEndPoint = remote });
                                }
                             }
                        }

                        // ТАЙМАУТ-ФЛАШ (для сканеров без разделителей)
                        // Если в буфере что-то есть, но разделителя мы так и не нашли
                        if (sb.Length > 0 && !clientStream.DataAvailable)
                        {
                            // Подождем немного, вдруг хвост пакета долетает
                            await Task.Delay(_timeoutFlushMs, _cts.Token); 
                            
                            // Если всё еще нет данных
                            if (!clientStream.DataAvailable)
                            {
                                var leftovers = sb.ToString().Trim();
                                if (!string.IsNullOrWhiteSpace(leftovers))
                                {
                                     if (!string.IsNullOrEmpty(_startsWithFilter) && !leftovers.StartsWith(_startsWithFilter))
                                     {
                                         _logger.LogTrace("TIMEOUT FLUSH: Скан '{Code}' отфильтрован (не начинается с '{Filter}')", leftovers, _startsWithFilter);
                                     }
                                     else
                                     {
                                         var remote = client?.Client?.RemoteEndPoint?.ToString();
                                         _logger.LogInformation("TIMEOUT FLUSH (Server): Данные приняты без разделителя {Ip}:{Port}: {Code}", Ip, Port, leftovers);
                                         OnDataReceived?.Invoke(this, new ScanDataEventArgs { Code = leftovers, Timestamp = DateTime.Now, RemoteEndPoint = remote });
                                     }
                                }
                                sb.Clear();
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка при обслуживании клиентского соединения на порту {Port}", Port);
                    
                    _eventBus?.Publish(new ScannerEvent
                    {
                        Type = EventType.ScannerError,
                        ScannerName = Name,
                        Message = $"Ошибка при обслуживании клиента",
                        ErrorDetails = $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"
                    });
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
        var sbClient = new StringBuilder();

        try
        {
            _logger.LogInformation("Запущено прослушивание в режиме клиента {Ip}:{Port}", Ip, Port);
            while (!_cts.Token.IsCancellationRequested)
            {
                // Send trigger
                var triggerCommand = Encoding.ASCII.GetBytes("TRG\r\n");
                await _stream.WriteAsync(triggerCommand, _cts.Token);

                // Wait for response (might need loop if fragmented)
                // For client mode with TRIGGER, usually response is immediate, but better safe.
                
                int bytesRead = await _stream.ReadAsync(bufferClient, _cts.Token);
                if (bytesRead == 0)
                {
                    _logger.LogWarning("Соединение закрыто {Ip}:{Port}", Ip, Port);
                    break;
                }

                sbClient.Append(Encoding.UTF8.GetString(bufferClient, 0, bytesRead));
                string currentData = sbClient.ToString();

                 // Debug log visible
                _logger.LogInformation("[RAW Client] Буфер: {Data}", currentData.Replace("\r", "<CR>").Replace("\n", "<LF>"));


                // Process all complete lines available
                while (true)
                {
                    int cutIndex = -1;
                    int jump = 0;

                    // Custom delimiter logic
                    if (!string.IsNullOrEmpty(_delimiter))
                    {
                        int idx = currentData.IndexOf(_delimiter);
                        if (idx != -1)
                        {
                            cutIndex = idx;
                            jump = _delimiter.Length;
                        }
                    }
                    // Default logic (\r or \n)
                    else 
                    { 
                        int rIndex = currentData.IndexOf('\r');
                        int nIndex = currentData.IndexOf('\n');

                        if (rIndex != -1 && nIndex != -1) cutIndex = Math.Min(rIndex, nIndex);
                        else if (rIndex != -1) cutIndex = rIndex;
                        else if (nIndex != -1) cutIndex = nIndex;

                        if (cutIndex != -1)
                        {
                            jump = 1;
                            if (currentData[cutIndex] == '\r' && cutIndex + 1 < currentData.Length && currentData[cutIndex + 1] == '\n')
                            {
                                jump = 2;
                            }
                        }
                    }

                    if (cutIndex == -1) break;

                    var code = currentData.Substring(0, cutIndex).Trim();
                    
                    int jumpReal = jump; // Avoid modifying loop var unexpectedly
                    currentData = currentData.Substring(cutIndex + jumpReal);
                    sbClient.Clear();
                    sbClient.Append(currentData);

                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        if (!string.IsNullOrEmpty(_startsWithFilter) && !code.StartsWith(_startsWithFilter))
                        {
                            _logger.LogTrace("Скан '{Code}' отфильтрован (не начинается с '{Filter}')", code, _startsWithFilter);
                        }
                        else
                        {
                            var remoteClient = _client?.Client?.RemoteEndPoint?.ToString() ?? (Ip + ":" + Port);
                            _logger.LogDebug("Получены данные {Ip}:{Port} (Remote: {Remote}): {Code}", Ip, Port, remoteClient, code);
                            OnDataReceived?.Invoke(this, new ScanDataEventArgs { Code = code, Timestamp = DateTime.Now, RemoteEndPoint = remoteClient });
                        }
                    }
                }

                // ТАЙМАУТ-ФЛАШ (для режима КЛИЕНТА)
                // Если мы что-то прочитали, но разделителя не нашли, и в стриме пусто
                if (sbClient.Length > 0 && !_stream.DataAvailable)
                {
                     // Обычно ответ на триггер приходит целиком. 
                     // Если там нет \r\n, мы обязаны это обработать, иначе застрянем.
                     await Task.Delay(_timeoutFlushMs, _cts.Token);
                     if (!_stream.DataAvailable)
                     {
                         var content = sbClient.ToString().Trim();
                         if (!string.IsNullOrWhiteSpace(content))
                         {
                             if (!string.IsNullOrEmpty(_startsWithFilter) && !content.StartsWith(_startsWithFilter))
                             {
                                 _logger.LogTrace("TIMEOUT FLUSH: Скан '{Code}' отфильтрован (не начинается с '{Filter}')", content, _startsWithFilter);
                             }
                             else
                             {
                                 var remoteClient = _client?.Client?.RemoteEndPoint?.ToString() ?? (Ip + ":" + Port);
                                 _logger.LogInformation("TIMEOUT FLUSH (Client): Данные без разделителя {Ip}:{Port}: {Code}", Ip, Port, content);
                                 OnDataReceived?.Invoke(this, new ScanDataEventArgs { Code = content, Timestamp = DateTime.Now, RemoteEndPoint = remoteClient });
                             }
                         }
                         sbClient.Clear();
                     }
                }

                await Task.Delay(_requestIntervalMs, _cts.Token);
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
