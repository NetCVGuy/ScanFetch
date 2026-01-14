using System.Text;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ScanFetch.Services;

/// <summary>
/// Сервис для работы с Google Sheets вебхуком
/// Проверяет дубликаты, NoRead, пустые строки перед отправкой
/// </summary>
public class GoogleSheetsWebhook
{
    private readonly ILogger<GoogleSheetsWebhook> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;
    private readonly string _outputPath;
    private readonly string _filePrefix;
    private readonly string _fileSuffix;
    private readonly string _fileFormat;
    private readonly Dictionary<string, DateTime> _lastScans = new(); // Used for retention logic
    private readonly double _cacheRetentionSeconds;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly bool _enableFileOutput;
    private readonly bool _enableGoogleSheets;

    public GoogleSheetsWebhook(
        string webhookUrl,
        double cacheRetentionSeconds,
        string outputPath = "",
        string filePrefix = "",
        string fileSuffix = "",
        string fileFormat = "",
        bool enableFileOutput = true,
        bool enableGoogleSheets = true,
        ILogger<GoogleSheetsWebhook>? logger = null)
    {
        _webhookUrl = webhookUrl;
        _cacheRetentionSeconds = cacheRetentionSeconds;
        _outputPath = outputPath ?? string.Empty;
        _filePrefix = filePrefix ?? string.Empty;
        _fileSuffix = fileSuffix ?? string.Empty;
        _fileFormat = fileFormat ?? string.Empty;
        _enableFileOutput = enableFileOutput;
        _enableGoogleSheets = enableGoogleSheets;
        _logger = logger ?? NullLogger<GoogleSheetsWebhook>.Instance;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Обрабатывает скан: проверяет на дубликаты, NoRead, пустые строки
    /// и отправляет в Google Sheets если всё ок
    /// </summary>
    public async Task ProcessScanAsync(string code, string? scannerName = null, string? remote = null)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (code.Contains("NoRead") || string.IsNullOrWhiteSpace(code))
            {
                //_logger.LogDebug("Игнорирован пустой или NoRead скан: {Code}", code);
                return;
            }

            // Проверка на дубликат в кеше
            if (_lastScans.ContainsKey(code))
            {
                _logger.LogInformation("Дубликат (в кеше): {Code}", code);
                return;
            }

            // Добавляем текущую запись
            _lastScans[code] = DateTime.Now;

            // Выполняем операции записи независимо (файлы и/или Google Sheets)
            var tasks = new List<Task>();
            if (_enableFileOutput)
            {
                tasks.Add(SaveScanToFileAsync(code, scannerName, remote));
            }

            if (_enableGoogleSheets)
            {
                tasks.Add(SendToGoogleSheetsAsync(code, scannerName, remote));
            }

            await Task.WhenAll(tasks);

            // Дебаг вывод кеша
            _logger.LogDebug("Кеш сканов: {CacheCount} записей", _lastScans.Count);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Сохраняет скан в отдельный файл (если настроено OutputPath)
    /// </summary>
    public async Task SaveScanToFileAsync(string code, string? scannerName = null, string? remote = null)
    {
        if (string.IsNullOrWhiteSpace(_outputPath))
        {
            // Нет пути для записи — ничего не делаем
            return;
        }

        try
        {
            Directory.CreateDirectory(_outputPath);
            var fileName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + ".txt";
            var fullPath = Path.Combine(_outputPath, fileName);
            string content;
            if (!string.IsNullOrWhiteSpace(_fileFormat))
            {
                content = _fileFormat.Replace("{code}", code)
                                      .Replace("{timestamp}", DateTime.Now.ToString("O"))
                                      .Replace("{scanner}", scannerName ?? string.Empty)
                                      .Replace("{remote}", remote ?? string.Empty) + Environment.NewLine;
            }
            else
            {
                // Allow prefix/suffix to contain placeholders as well
                var prefix = _filePrefix.Replace("{scanner}", scannerName ?? string.Empty)
                                         .Replace("{remote}", remote ?? string.Empty)
                                         .Replace("{timestamp}", DateTime.Now.ToString("O"));
                var suffix = _fileSuffix.Replace("{scanner}", scannerName ?? string.Empty)
                                         .Replace("{remote}", remote ?? string.Empty)
                                         .Replace("{timestamp}", DateTime.Now.ToString("O"));

                content = $"{prefix}{code}{suffix}" + Environment.NewLine;
            }
            await File.WriteAllTextAsync(fullPath, content);
            _logger.LogInformation("Записан файл скана: {Path}", fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при записи файла скана");
        }
    }

    /// <summary>
    /// Отправляет скан в Google Sheets через webhook
    /// </summary>
    public async Task SendToGoogleSheetsAsync(string code, string? scannerName = null, string? remote = null)
    {
        var now = DateTime.Now;
        _logger.LogInformation("Скан: {Code} (Timestamp: {Timestamp}) (Scanner: {Scanner}, Remote: {Remote})", code, DateTimeOffset.Now.ToUnixTimeSeconds(), scannerName, remote);
        var payload = new { code = code, scanner = scannerName, remote = remote };

        try
        {
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload), 
                Encoding.UTF8, 
                "application/json");

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.PostAsync(_webhookUrl, jsonContent, cts.Token);
            var responseText = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Ответ от Google Таблицы: {Response}", responseText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке в Google Sheets");
        }
    }
}
