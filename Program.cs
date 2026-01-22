using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScanFetch.Configuration;
using ScanFetch.Logging;
using ScanFetch.Scanners;
using ScanFetch.Services;
using ScanFetch.Api;
using Spectre.Console;
using ScanFetch;

// Создаем директорию для логов и файл
var logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
Directory.CreateDirectory(logsDirectory);
var logFileName = Path.Combine(logsDirectory, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

// Выводим логотип в консоль и в файл
AnsiConsole.MarkupLine($"[green]{Logo.AsciiArt}[/]");
File.WriteAllText(logFileName, Logo.AsciiArt + Environment.NewLine);

// Настраиваем конфигурацию
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// Настраиваем DI контейнер
var services = new ServiceCollection();

// Добавляем логирование (меняем Console на Spectre)
services.AddLogging(builder =>
{
    builder.AddConfiguration(configuration.GetSection("Logging"));
    builder.ClearProviders(); // Убираем стандартные провайдеры
    builder.AddProvider(new SpectreConsoleLoggerProvider());
    builder.AddProvider(new FileLoggerProvider(logFileName));
});

// Регистрируем сервисы
var appSettings = configuration.Get<AppSettings>() ?? new AppSettings();
services.AddSingleton(appSettings);

// Cache system settings to avoid nullable-analysis warnings
var systemSettings = appSettings.System ?? new SystemSettings();

var serviceProvider = services.BuildServiceProvider();
var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger<Program>();

logger.LogInformation("Запуск ScanFetch...");

// Создаем EventBus для мониторинга событий
var eventBus = new EventBus();

// Публикуем событие запуска приложения
eventBus.Publish(new ScannerEvent
{
    Type = EventType.ApplicationStarted,
    Message = "ScanFetch запущен"
});

// Создаём Google Sheets сервис
var googleSheets = new GoogleSheetsWebhook(
    appSettings.GoogleSheets.WebhookUrl,
    appSettings.GoogleSheets.CacheRetentionSeconds,
    appSettings.GoogleSheets.OutputPath,
    appSettings.GoogleSheets.FilePrefix,
    appSettings.GoogleSheets.FileSuffix,
    appSettings.GoogleSheets.FileFormat,
    appSettings.GoogleSheets.EnableFileOutput,
    appSettings.GoogleSheets.EnableGoogleSheets,
    loggerFactory.CreateLogger<GoogleSheetsWebhook>()
);

// If GUI enabled in config, attempt to launch the bundled GUI editor (if available)
if (appSettings?.GoogleSheets is not null && appSettings?.Scanners is not null)
{
    // New GUI flag location: top-level Gui settings (if exists) or GoogleSheets.EnableGui fallback
}

// Try to read GUI flag (if present) and launch GUI project if enabled
try
{
    // Look for a flag in appsettings.json under "Gui": { "EnableGui": true }
    var cfgPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
    if (File.Exists(cfgPath))
    {
        using var fs = File.OpenRead(cfgPath);
        var doc = System.Text.Json.JsonDocument.Parse(fs);
        if (doc.RootElement.TryGetProperty("Gui", out var guiEl) && guiEl.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (guiEl.TryGetProperty("EnableGui", out var en) && en.ValueKind == System.Text.Json.JsonValueKind.True)
            {
                logger.LogInformation("GUI mode enabled — attempting to start GUI editor.");
                // Attempt to launch the GUI project (if compiled). Prefer a built executable.
                var guiExe = Path.Combine(Directory.GetCurrentDirectory(), "Gui", "ScanFetch.Gui", "bin", "Debug", "net10.0", "ScanFetch.Gui");
                if (Environment.OSVersion.Platform == System.PlatformID.Win32NT)
                    guiExe += ".exe";

                if (File.Exists(guiExe))
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = guiExe, UseShellExecute = true }); }
                    catch (Exception ex) { logger.LogWarning(ex, "Не удалось запустить GUI-приложение: {Msg}", ex.Message); }
                }
                else
                {
                    // Fallback: instruct user how to run the GUI project
                    logger.LogInformation("GUI executable not found. To run it, build the GUI project and run: dotnet run --project Gui/ScanFetch.Gui");
                }
            }
        }
    }
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Ошибка при попытке запуска GUI (игнорируется)");
}

while (true)
{
    // Re-bind configuration to support hot-reload of settings (including scanners)
    // We need to reload the section to get fresh values
    configuration.Reload(); 
    appSettings = configuration.Get<AppSettings>() ?? new AppSettings();
    systemSettings = appSettings.System ?? new SystemSettings();

    // Cоздаем список сканеров заново при каждой попытке
    var scanners = new List<TcpScanner>();
    var connectedTasks = new List<Task>();
    
    // Запускаем Monitoring API если включено
    MonitoringApi? monitoringApi = null;
    if (appSettings.MonitoringApi?.Enabled == true)
    {
        try
        {
            monitoringApi = new MonitoringApi(
                appSettings.MonitoringApi.Port,
                eventBus,
                scanners,
                loggerFactory
            );
            await monitoringApi.StartAsync();
            logger.LogInformation("Monitoring API запущен на порту {Port}", appSettings.MonitoringApi.Port);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось запустить Monitoring API");
        }
    }

    try
    {
        // Создаём сканеры из конфигурации
        var scannerList = appSettings?.Scanners ?? Enumerable.Empty<ScannerSettings>();
        
        // Debug mode: interactive configuration
        if (systemSettings.DebugMode)
        {
            AnsiConsole.MarkupLine("[yellow]═══ РЕЖИМ ОТЛАДКИ ВКЛЮЧЕН ═══[/]");
            var debugMode = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Выберите режим работы:[/]")
                    .AddChoices("Client - подключиться к сканеру", "Server - ждать подключения от сканера", "Использовать конфигурацию из appsettings.json")
            );

            if (debugMode != "Использовать конфигурацию из appsettings.json")
            {
                var role = debugMode.StartsWith("Client") ? "Client" : "Server";
                var ip = role == "Client" 
                    ? AnsiConsole.Ask<string>("[cyan]Введите IP адрес сканера:[/]", "127.0.0.1")
                    : AnsiConsole.Ask<string>("[cyan]Введите IP для прослушивания (пусто = все интерфейсы):[/]", "");
                var port = AnsiConsole.Ask<int>("[cyan]Введите порт:[/]", 2002);
                var delimiter = AnsiConsole.Ask<string>("[cyan]Разделитель (пусто = авто CR/LF):[/]", "");
                var timeoutFlush = AnsiConsole.Ask<int>("[cyan]Таймаут очистки буфера (мс):[/]", 50);
                var requestInterval = AnsiConsole.Ask<int>("[cyan]Интервал запросов для Client (мс):[/]", 100);

                // Override scanner list with debug config
                scannerList = new List<ScannerSettings>
                {
                    new ScannerSettings
                    {
                        Name = "DebugScanner",
                        Ip = ip,
                        Port = port,
                        Role = role,
                        Enabled = true,
                        Delimiter = delimiter,
                        TimeoutFlushMs = timeoutFlush,
                        RequestIntervalMs = requestInterval
                    }
                };
            }
            AnsiConsole.MarkupLine("[green]═══ ЗАПУСК С ВЫБРАННЫМИ ПАРАМЕТРАМИ ═══[/]");
        }
        
        foreach (var scannerConfig in scannerList)
        {
            if (scannerConfig is null || !scannerConfig.Enabled) continue;
            var cfg = scannerConfig;
            var scanner = new TcpScanner(
                cfg.Ip,
                cfg.Port,
                cfg.Role,
                loggerFactory.CreateLogger<TcpScanner>(),
                cfg.ListenInterface,
                cfg.Delimiter,
                cfg.StartsWithFilter,
                cfg.RequestIntervalMs,
                cfg.TimeoutFlushMs,
                eventBus
            );
            
            scanner.Name = cfg.Name;
            scanner.Enabled = cfg.Enabled;

            // Подписываемся на события
            scanner.OnDataReceived += async (sender, e) =>
            {
                var remote = e.RemoteEndPoint ?? (cfg.Ip + ":" + cfg.Port);
                logger.LogDebug("Событие от {Name} (Remote: {Remote}): {Code}", cfg.Name, remote, e.Code);
                await googleSheets.ProcessScanAsync(e.Code, cfg.Name, remote);
            };

            scanners.Add(scanner);
        }

        if (scanners.Count == 0)
        {
            logger.LogWarning("Нет активных сканеров в конфигурации!");
            return;
        }

        logger.LogInformation("Попытка подключения к {Count} сканерам...", scanners.Count);
        
        // Логика подключения
        var successfulClientConnections = 0; // actual outbound client connections
        var listenersStarted = 0; // server listeners that started
        var connectionErrors = false;

        foreach (var scanner in scanners)
        {
            try
            {
                await scanner.ConnectAsync(systemSettings.ScannerTimeoutSeconds);

                // Start listening task for both client and server modes
                connectedTasks.Add(scanner.StartListeningAsync());

                // Distinguish server/listener vs client-connected
                if (string.Equals(scanner.Role, "Server", StringComparison.OrdinalIgnoreCase))
                {
                    listenersStarted++;
                }
                else
                {
                    successfulClientConnections++;
                }
            }
            catch (Exception)
            {
                connectionErrors = true;
                // Если cancelOnAny = true, то нет смысла продолжать подключать остальные
                if (systemSettings.CancelOnAny)
                {
                    logger.LogError("Режим CancelOnAny включен. Прерываем подключение.");
                    break;
                }
            }
        }

        // Проверка результатов
        if (systemSettings.CancelOnAny && connectionErrors)
        {
            // Отключаем те, что успели подключиться
            foreach (var scanner in scanners) await scanner.DisconnectAsync();
            
            if (systemSettings.AutoRetryEnabled)
            {
                logger.LogError("Ошибка подключения одного из сканеров. Повтор через {Delay}с...", systemSettings.RetryDelaySeconds);
                await Task.Delay(systemSettings.RetryDelaySeconds * 1000);
            }
            else
            {
                logger.LogError("Ошибка подключения одного из сканеров. Нажмите Enter для повтора...");
                Console.ReadLine();
            }
            continue; // Повторяем цикл while(true)
        }

        if (!systemSettings.CancelOnAny && successfulClientConnections == 0 && listenersStarted == 0)
        {
            if (systemSettings.AutoRetryEnabled)
            {
                logger.LogError("Не удалось подключиться ни к одному сканеру и не запущено ни одного сервера. Повтор через {Delay}с...", systemSettings.RetryDelaySeconds);
                await Task.Delay(systemSettings.RetryDelaySeconds * 1000);
            }
            else
            {
                logger.LogError("Не удалось подключиться ни к одному сканеру и не запущено ни одного сервера. Нажмите Enter для повтора...");
                Console.ReadLine();
            }
             continue;
        }

        // Логирование состояния подключения
        if (connectionErrors)
        {
            logger.LogWarning("Часть сканеров не подключилась, но работаем (CancelOnAny=false).");
        }

        if (listenersStarted > 0 && successfulClientConnections == 0)
        {
            logger.LogInformation("{Listeners} сервер(ов) запущено и слушают, но пока нет активных клиентских подключений.", listenersStarted);
        }
        else if (listenersStarted > 0 && successfulClientConnections > 0)
        {
            logger.LogInformation("{Listeners} сервер(ов) слушают, {Clients} клиентских подключений активно.", listenersStarted, successfulClientConnections);
        }
        else
        {
            logger.LogInformation("Все сканеры успешно подключены и работают.");
        }

        // Ждём завершения задач (остановка программы через Ctrl+C)
        // connectedTasks могут завершиться если сканер отвалится. 
        // В идеале можно добавить логику переподключения тут, но для начала просто WaitAll
        await Task.WhenAll(connectedTasks);
        
        // Если задачи завершились (соединения разорваны), выходим из цикла или перезапускаем?
        // Скорее всего, если они завершились, значит соединение потеряно.
        logger.LogWarning("Соединения со сканерами потеряны. Перезапуск через {Delay}с...", systemSettings.RetryDelaySeconds);
        await Task.Delay(systemSettings.RetryDelaySeconds * 1000);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Критическая ошибка в главном цикле");
        if (systemSettings.AutoRetryEnabled)
        {
            logger.LogInformation("Перезапуск через {Delay}с...", systemSettings.RetryDelaySeconds);
            await Task.Delay(systemSettings.RetryDelaySeconds * 1000);
        }
        else
        {
            logger.LogInformation("Нажмите Enter для перезапуска...");
            Console.ReadLine();
        }
    }
    finally
    {
        // Очистка перед выходом или перезапуском
        foreach (var scanner in scanners)
        {
            await scanner.DisconnectAsync();
        }
        
        // Останавливаем Monitoring API
        if (monitoringApi != null)
        {
            await monitoringApi.StopAsync();
            logger.LogInformation("Monitoring API остановлен");
        }
    }
}

// Публикуем событие остановки приложения (никогда не достигается из-за while(true), но добавим для полноты)
eventBus.Publish(new ScannerEvent
{
    Type = EventType.ApplicationStopped,
    Message = "ScanFetch остановлен"
});
