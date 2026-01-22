# ScanFetch .NET Project Instructions

This project is a .NET 10.0 console application for bridging TCP barcode scanners to Google Sheets and local files.

## Architecture Overview

- **Core Loop:** `Program.cs` runs an infinite loop that reloads configuration (`appsettings.json`) each iteration to support "hot reload" of scanner settings without restarting the process.
- **Dependency Injection:** Uses `Microsoft.Extensions.DependencyInjection` for `ILogger`, `AppSettings`, and services.
- **Scanners:**
  - Logic resides in `Scanners/TcpScanner.cs`.
  - Supports **Client** (connects to scanner) and **Server** (listens for scanner) roles.
  - **Manual Buffer Handling:** Since TCP is stream-based, `TcpScanner` implements manual packet fragmentation handling using `StringBuilder` and configurable delimiters.
  - **Timeout Flush:** Implements a fallback mechanism to process data remaining in the buffer if no delimiter is received within 50ms (crucial for scanners that don't send CR/LF).
- **Data Processing:** `Services/GoogleSheetsWebhook.cs` handles deduplication (memory cache with retention policy) and dispatching to HTTP/File.

## Key Files & Patterns

- **`Program.cs`**:
  - Entry point.
  - Handles the "Hot Reload" loop: `configuration.Reload()` -> Re-bind `AppSettings` -> Re-init `TcpScanner` list.
  - Manages the lifecycle of `TcpScanner` instances (Connect -> Wait -> Disconnect).
- **`Scanners/TcpScanner.cs`**:
  - **Critical:** Contains complex logic for parsing incoming byte streams.
  - Supports `Delimiter` (Text or Hex `0x0D`) and `StartsWithFilter`.
  - **Pattern:** Reads bytes -> Appends to Buffer -> Checks for Delimiter -> Invokes `OnDataReceived` -> Checks for Timeout Flush.
- **`Configuration/AppSettings.cs`**:
  - Strongly-typed configuration model.
  - `ScannerSettings`: `Role` ("Client"/"Server"), `Delimiter`, `StartsWithFilter`.

## Build & Publish

The project is configured for **Single File** publication.

```powershell
# Publish for Windows x64 (Single File)
dotnet publish ScanFetch.csproj -c Release
```
Output: `bin/Release/net10.0/win-x64/publish/ScanFetch.exe`

## Configuration Guide (appsettings.json)

When proposing configuration changes, adhere to this structure for `Scanners`:

```json
"Scanners": [
  {
    "Name": "Scanner1",
    "Ip": "192.168.1.10",
    "Port": 2002,
    "Role": "Client",          // or "Server"
    "Delimiter": "0x0D",       // Supports Hex (0x..) or escaped text ("\\r\\n")
    "StartsWithFilter": "]C1"  // Optional prefix filter
  }
]
```

## Developer Notes

1.  **Logging:** Use `_logger.LogInformation` for operational events and `_logger.LogDebug` for high-volume raw data (e.g., `[RAW] ...`).
2.  **TCP Fragmentation:** Never assume `ReadAsync` returns a complete message. Always append to buffer and parse.
3.  **Deduplication:** Logic is in `GoogleSheetsWebhook.cs`. It uses a `Dictionary<string, DateTime>` to track recent scans and prevent spamming the webhook.
4.  **GUI:** There is a separate Avalonia project in `Gui/` which is launched as a subprocess if enabled. Focus primarily on the Console app logic unless asked about UI.
