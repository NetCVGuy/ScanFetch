using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace ScanFetch.Logging;

public class SpectreConsoleLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new SpectreConsoleLogger(categoryName);
    }

    public void Dispose()
    {
    }
}

public class SpectreConsoleLogger : ILogger
{
    private readonly string _categoryName;

    public SpectreConsoleLogger(string categoryName)
    {
        _categoryName = categoryName;
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

        if (exception != null)
        {
            AnsiConsole.WriteException(exception);
        }
    }
}
