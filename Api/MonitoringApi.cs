using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ScanFetch.Services;
using ScanFetch.Scanners;

namespace ScanFetch.Api;

public class MonitoringApi
{
    private readonly WebApplication _app;
    private readonly EventBus _eventBus;
    private readonly ILogger<MonitoringApi> _logger;
    private readonly List<TcpScanner> _scanners;

    public MonitoringApi(
        int port,
        EventBus eventBus,
        List<TcpScanner> scanners,
        ILoggerFactory loggerFactory)
    {
        _eventBus = eventBus;
        _scanners = scanners;
        _logger = loggerFactory.CreateLogger<MonitoringApi>();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions());
        
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        builder.Logging.ClearProviders();

        _app = builder.Build();
        _app.Urls.Add($"http://*:{port}");
        _app.UseCors();

        ConfigureEndpoints();
    }

    private void ConfigureEndpoints()
    {
        // GET /api/status - получить статус всех сканеров
        _app.MapGet("/api/status", () =>
        {
            var status = _scanners.Select(scanner => new
            {
                name = scanner.Name,
                enabled = scanner.Enabled,
                connected = scanner.IsConnected,
                role = scanner.Role,
                ip = scanner.Ip,
                port = scanner.Port,
                remoteEndpoint = scanner.RemoteEndpoint
            });

            return Results.Ok(new
            {
                timestamp = DateTime.UtcNow,
                scanners = status
            });
        });

        // GET /api/errors - получить последние ошибки
        _app.MapGet("/api/errors", (int? count) =>
        {
            var errors = _eventBus.GetErrors(count ?? 50);
            return Results.Ok(new
            {
                timestamp = DateTime.UtcNow,
                errors = errors.Select(e => new
                {
                    type = e.Type.ToString(),
                    scanner = e.ScannerName,
                    message = e.Message,
                    timestamp = e.Timestamp,
                    remote = e.RemoteEndpoint,
                    details = e.ErrorDetails
                })
            });
        });

        // GET /api/history - получить историю событий
        _app.MapGet("/api/history", (int? count) =>
        {
            var history = _eventBus.GetHistory(count ?? 50);
            return Results.Ok(new
            {
                timestamp = DateTime.UtcNow,
                events = history.Select(e => new
                {
                    type = e.Type.ToString(),
                    scanner = e.ScannerName,
                    message = e.Message,
                    timestamp = e.Timestamp,
                    remote = e.RemoteEndpoint
                })
            });
        });

        // GET /api/events - SSE stream для real-time уведомлений
        _app.MapGet("/api/events", async (HttpContext context) =>
        {
            context.Response.Headers["Content-Type"] = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"] = "keep-alive";

            _logger.LogInformation("Новый SSE клиент подключился");

            var reader = _eventBus.Subscribe();
            
            try
            {
                await foreach (var evt in reader.ReadAllAsync(context.RequestAborted))
                {
                    var json = JsonSerializer.Serialize(new
                    {
                        type = evt.Type.ToString(),
                        scanner = evt.ScannerName,
                        message = evt.Message,
                        timestamp = evt.Timestamp,
                        remote = evt.RemoteEndpoint,
                        details = evt.ErrorDetails
                    });

                    await context.Response.WriteAsync($"data: {json}\n\n");
                    await context.Response.Body.FlushAsync();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SSE клиент отключился");
            }
        });

        // GET / - простая главная страница
        _app.MapGet("/", () => Results.Text(@"
<!DOCTYPE html>
<html>
<head>
    <title>ScanFetch Monitoring API</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px; }
        h1 { color: #333; }
        .endpoint { margin: 20px 0; padding: 15px; background: #f5f5f5; border-radius: 5px; }
        code { background: #e0e0e0; padding: 2px 6px; border-radius: 3px; }
    </style>
</head>
<body>
    <h1>🔍 ScanFetch Monitoring API</h1>
    <p>API для мониторинга сканеров штрих-кодов</p>
    
    <div class='endpoint'>
        <h3>GET /api/status</h3>
        <p>Получить текущий статус всех сканеров</p>
    </div>
    
    <div class='endpoint'>
        <h3>GET /api/errors?count=50</h3>
        <p>Получить последние ошибки (count - количество, по умолчанию 50)</p>
    </div>
    
    <div class='endpoint'>
        <h3>GET /api/history?count=50</h3>
        <p>Получить историю всех событий (count - количество, по умолчанию 50)</p>
    </div>
    
    <div class='endpoint'>
        <h3>GET /api/events</h3>
        <p>SSE stream для получения уведомлений в реальном времени</p>
    </div>
    
    <p style='margin-top: 40px; color: #666;'>
        💡 Для использования в Android приложении подключайтесь к <code>http://IP_АДРЕС_СЕРВЕРА:PORT</code>
    </p>
</body>
</html>", "text/html"));
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _app.StartAsync(cancellationToken);
        _logger.LogInformation("🌐 Monitoring API запущен на {Urls}", string.Join(", ", _app.Urls));
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _app.StopAsync(cancellationToken);
        _logger.LogInformation("Monitoring API остановлен");
    }
}
