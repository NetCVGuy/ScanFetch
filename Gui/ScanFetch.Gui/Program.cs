using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System;

namespace ScanFetch.Gui;

internal class Program
{
    // Avalonia entry point
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
