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
        _app.Urls.Add($"http://0.0.0.0:{port}");
        _app.UseCors();
        
        // Enable static files
        _app.UseStaticFiles();

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

        // GET /api/logs/stream - SSE stream для онлайн логов
        _app.MapGet("/api/logs/stream", async (HttpContext context) =>
        {
            context.Response.Headers["Content-Type"] = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"] = "keep-alive";

            _logger.LogInformation("Новый SSE клиент подключился к логам");

            var reader = _eventBus.Subscribe();
            
            try
            {
                await foreach (var evt in reader.ReadAllAsync(context.RequestAborted))
                {
                    // Отправляем только логи
                    if (evt.Type == EventType.LogMessage)
                    {
                        var json = JsonSerializer.Serialize(new
                        {
                            level = evt.LogLevel,
                            source = evt.ScannerName,
                            message = evt.Message,
                            timestamp = evt.Timestamp
                        });

                        await context.Response.WriteAsync($"data: {json}\n\n");
                        await context.Response.Body.FlushAsync();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SSE клиент отключился от логов");
            }
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

        // GET / - redirect to index.html or serve API info
        _app.MapGet("/", () =>
        {
            return Results.Redirect("/index.html");
        });
        
        // GET /api/logs - get recent log entries from log files
        _app.MapGet("/api/logs", (int? count, string? level) =>
        {
            var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            var logs = new List<object>();
            
            try
            {
                if (Directory.Exists(logsDir))
                {
                    var logFiles = Directory.GetFiles(logsDir, "*.txt")
                        .OrderByDescending(f => File.GetLastWriteTime(f))
                        .Take(5);
                    
                    foreach (var file in logFiles)
                    {
                        var lines = File.ReadAllLines(file);
                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            
                            // Parse log level from line format: [HH:mm:ss] LEVEL ...
                            var logLevel = "info";
                            if (line.Contains(" ОШБ ")) logLevel = "error";
                            else if (line.Contains(" ПРД ")) logLevel = "warning";
                            else if (line.Contains(" ИНФ ")) logLevel = "info";
                            else if (line.Contains(" ОТЛ ")) logLevel = "debug";
                            
                            if (string.IsNullOrEmpty(level) || logLevel == level)
                            {
                                logs.Add(new { level = logLevel, message = line, file = Path.GetFileName(file) });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка чтения логов");
            }
            
            var result = logs.OrderByDescending(l => l).Take(count ?? 500).ToList();
            return Results.Ok(new { logs = result });
        });
        
        // POST /api/control/restart - restart application (placeholder)
        _app.MapPost("/api/control/restart", () =>
        {
            _logger.LogWarning("Перезапуск приложения запрошен через API");
            return Results.Ok(new { message = "Restart command not implemented yet" });
        });
        
        // POST /api/control/stop - stop application (placeholder)
        _app.MapPost("/api/control/stop", () =>
        {
            _logger.LogWarning("Остановка приложения запрошена через API");
            return Results.Ok(new { message = "Stop command not implemented yet" });
        });
        
        // POST /api/control/start - start application (placeholder)
        _app.MapPost("/api/control/start", () =>
        {
            _logger.LogInformation("Запуск приложения запрошен через API");
            return Results.Ok(new { message = "Start command not implemented yet" });
        });
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
