using System.Net.Sockets;
using System.Text;
using Spectre.Console;

namespace TestScanner;

class Program
{
    static async Task Main(string[] args)
    {
        AnsiConsole.MarkupLine("[green]╔═══════════════════════════════════════════════════════╗[/]");
        AnsiConsole.MarkupLine("[green]║       Test Scanner Emulator for ScanFetch            ║[/]");
        AnsiConsole.MarkupLine("[green]╚═══════════════════════════════════════════════════════╝[/]");
        AnsiConsole.WriteLine();

        // Параметры подключения
        string host = AnsiConsole.Ask<string>("Введите [cyan]IP адрес сервера[/]:", "127.0.0.1");
        int port = AnsiConsole.Ask<int>("Введите [cyan]порт сервера[/]:", 2002);
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[yellow]Режим работы:[/]");
        AnsiConsole.MarkupLine("[yellow]1.[/] Client mode (отправляем данные после подключения)");
        AnsiConsole.MarkupLine("[yellow]2.[/] Server mode (приложение подключается к нам)");
        
        var mode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Выберите [green]режим[/]:")
                .AddChoices("Client", "Server"));

        AnsiConsole.WriteLine();

        if (mode == "Client")
        {
            await RunClientModeAsync(host, port);
        }
        else
        {
            await RunServerModeAsync(port);
        }
    }

    static async Task RunClientModeAsync(string host, int port)
    {
        AnsiConsole.Status()
            .Start($"Подключение к {host}:{port}...", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                Thread.Sleep(500);
            });

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port);
            AnsiConsole.MarkupLine($"[green]✓[/] Подключено к {host}:{port}");
            AnsiConsole.WriteLine();

            var stream = client.GetStream();

            AnsiConsole.MarkupLine("[yellow]Введите коды для отправки (или 'exit' для выхода)[/]");
            AnsiConsole.MarkupLine("[dim]Формат: просто введите код и нажмите Enter[/]");
            AnsiConsole.WriteLine();

            while (true)
            {
                var code = AnsiConsole.Ask<string>("[cyan]Код[/]:");

                if (code.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(code))
                {
                    AnsiConsole.MarkupLine("[red]✗[/] Код не может быть пустым!");
                    continue;
                }

                // Отправляем код с \r\n
                var data = Encoding.UTF8.GetBytes(code + "\r\n");
                await stream.WriteAsync(data);
                
                AnsiConsole.MarkupLine($"[green]✓[/] Отправлено: [white]{code}[/] ({data.Length} байт)");
                
                // Небольшая задержка для имитации реального сканера
                await Task.Delay(100);
            }

            AnsiConsole.MarkupLine("[yellow]Отключение...[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Ошибка:[/] {ex.Message}");
        }
    }

    static async Task RunServerModeAsync(int port)
    {
        AnsiConsole.Status()
            .Start($"Запуск сервера на порту {port}...", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                Thread.Sleep(500);
            });

        try
        {
            var listener = new TcpListener(System.Net.IPAddress.Any, port);
            listener.Start();
            AnsiConsole.MarkupLine($"[green]✓[/] Сервер запущен на порту {port}");
            AnsiConsole.MarkupLine("[yellow]Ожидание подключения от ScanFetch...[/]");
            AnsiConsole.WriteLine();

            var client = await listener.AcceptTcpClientAsync();
            var remoteEp = client.Client.RemoteEndPoint;
            AnsiConsole.MarkupLine($"[green]✓[/] Подключение от {remoteEp}");
            AnsiConsole.WriteLine();

            var stream = client.GetStream();

            AnsiConsole.MarkupLine("[yellow]Введите коды для отправки (или 'exit' для выхода)[/]");
            AnsiConsole.MarkupLine("[dim]Формат: просто введите код и нажмите Enter[/]");
            AnsiConsole.WriteLine();

            // Запускаем фоновую задачу для чтения входящих данных (триггеры от ScanFetch)
            var cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                var buffer = new byte[1024];
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (stream.DataAvailable)
                        {
                            int bytesRead = await stream.ReadAsync(buffer, cts.Token);
                            if (bytesRead > 0)
                            {
                                var received = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                AnsiConsole.MarkupLine($"[dim]← Получено от сервера: {received.Replace("\r", "<CR>").Replace("\n", "<LF>")}[/]");
                            }
                        }
                        await Task.Delay(50, cts.Token);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Ошибка чтения:[/] {ex.Message}");
                }
            }, cts.Token);

            while (true)
            {
                var code = AnsiConsole.Ask<string>("[cyan]Код[/]:");

                if (code.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(code))
                {
                    AnsiConsole.MarkupLine("[red]✗[/] Код не может быть пустым!");
                    continue;
                }

                // Отправляем код с \r\n
                var data = Encoding.UTF8.GetBytes(code + "\r\n");
                await stream.WriteAsync(data);
                
                AnsiConsole.MarkupLine($"[green]✓[/] Отправлено: [white]{code}[/] ({data.Length} байт)");
                
                // Небольшая задержка для имитации реального сканера
                await Task.Delay(100);
            }

            cts.Cancel();
            AnsiConsole.MarkupLine("[yellow]Отключение...[/]");
            listener.Stop();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Ошибка:[/] {ex.Message}");
        }
    }
}
