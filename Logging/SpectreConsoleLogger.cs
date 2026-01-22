using Microsoft.Extensions.Logging;
using Spectre.Console;
using ScanFetch.Services;

namespace ScanFetch.Logging;

public class SpectreConsoleLoggerProvider : ILoggerProvider
{
    private readonly EventBus? _eventBus;
    
    public SpectreConsoleLoggerProvider(EventBus? eventBus = null)
    {
        _eventBus = eventBus;
    }
    
    public ILogger CreateLogger(string categoryName)
    {
        return new SpectreConsoleLogger(categoryName, _eventBus);
    }

    public void Dispose()
    {
    }
}

public class SpectreConsoleLogger : ILogger
{
    private readonly string _categoryName;
    private readonly EventBus? _eventBus;

    public SpectreConsoleLogger(string categoryName, EventBus? eventBus = null)
    {
        _categoryName = categoryName;
        _eventBus = eventBus;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        
        // Log level short codes
        var levelShort = logLevel switch
        {
            LogLevel.Trace => "[grey]ТРС[/]",
            LogLevel.Debug => "[grey]ДБГ[/]",
            LogLevel.Information => "[green]ИНФ[/]",
            LogLevel.Warning => "[yellow]ПРД[/]",
            LogLevel.Error => "[red]ОШБ[/]",
            LogLevel.Critical => "[red bold]КРТ[/]",
            _ => "[white]???[/]"
        };

        var time = DateTime.Now.ToString("HH:mm:ss");
        
        // Shorten category name (e.g., ScanFetch.Program -> Program)
        var categoryShort = _categoryName.Split('.').LastOrDefault() ?? _categoryName;
        
        // Format: [time] LEVEL Source: Message
        var logOutput = Markup.Escape(message);
        
        AnsiConsole.MarkupLine($"[grey][[[/]{time}[grey]]][/] {levelShort} [blue]{categoryShort}[/]: {logOutput}");
        
        // Publish to EventBus for web monitoring
        _eventBus?.Publish(new ScannerEvent
        {
            Type = EventType.LogMessage,
            ScannerName = categoryShort,
            Message = message,
            Timestamp = DateTime.UtcNow,
            LogLevel = logLevel.ToString().ToLower()
        });

        if (exception != null)
        {
            AnsiConsole.WriteException(exception);
        }
    }
}
