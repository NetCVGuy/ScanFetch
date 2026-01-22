namespace ScanFetch.Configuration;

/// <summary>
/// Конфигурация приложения
/// </summary>
public class AppSettings
{
    public SystemSettings System { get; set; } = new();
    public GoogleSheetsSettings GoogleSheets { get; set; } = new();
    public List<ScannerSettings> Scanners { get; set; } = new();
}

/// <summary>
/// Системные настройки
/// </summary>
public class SystemSettings
{
    public bool CancelOnAny { get; set; } = true;
    public int ScannerTimeoutSeconds { get; set; } = 20;
}

/// <summary>
/// Настройки Google Sheets
/// </summary>
public class GoogleSheetsSettings
{
    public string WebhookUrl { get; set; } = string.Empty;
    public double CacheRetentionSeconds { get; set; } = 180.0;
    // Flags to control where scans are written
    public bool EnableFileOutput { get; set; } = true;
    public bool EnableGoogleSheets { get; set; } = true;
    // If set, scans will be written to individual files in this directory
    public string OutputPath { get; set; } = string.Empty;
    // File content formatting when saving scans locally: prefix + code + suffix
    public string FilePrefix { get; set; } = string.Empty;
    public string FileSuffix { get; set; } = string.Empty;
    // Optional file format template. If set, it overrides prefix/suffix and supports placeholders: {code}, {timestamp}
    public string FileFormat { get; set; } = string.Empty;
    
}

/// <summary>
/// Настройки сканера
/// </summary>
public class ScannerSettings
{
    public string Name { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool Enabled { get; set; } = true;
    // Role: "Client" (default) or "Server". If Server, the app will listen on Ip:Port and accept incoming connections.
    public string Role { get; set; } = "Client";
    // Optional: name or IP of the local interface to bind when running in Server role.
    // If set, the server will bind to this interface non-interactively.
    public string? ListenInterface { get; set; }
    // Optional: custom delimiter string to split scans (e.g. "\r", ";"). 
    // If null/empty, defaults to auto-detecting \r or \n.
    public string? Delimiter { get; set; }
    public string? StartsWithFilter { get; set; }
    // Интервал запросов к серверу в миллисекундах (для режима Client)
    public int RequestIntervalMs { get; set; } = 50;
}