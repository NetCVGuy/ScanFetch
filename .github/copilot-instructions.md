# ScanFetch .NET Project Instructions

This project is a .NET 10.0 console application for bridging TCP barcode scanners to Google Sheets and local files.

## Architecture Overview

**Core Loop Pattern:** `Program.cs` runs an infinite `while(true)` loop that implements automatic retry/reconnect logic:
- Each iteration: `configuration.Reload()` → Re-bind `AppSettings` → Re-create `TcpScanner` list → Attempt connections
- On error: Auto-retry after configurable delay (if `AutoRetryEnabled=true`) or wait for Enter key
- This "hot reload" design allows changing scanner settings in `appsettings.json` without restarting the process

**Dependency Injection:** Uses `Microsoft.Extensions.DependencyInjection` for services:
- `ILoggerFactory` → Creates loggers for each component
- Custom providers: `SpectreConsoleLoggerProvider` (colored console) + `FileLoggerProvider` (timestamped log files in `Logs/`)
- `AppSettings` singleton bound from configuration

**Scanner Lifecycle (Critical):**
1. `ConnectAsync(timeoutSeconds)` — Client mode connects outbound; Server mode prepares listener
2. `StartListeningAsync()` — Both modes enter read loop; returns `Task` that completes on disconnect
3. `DisconnectAsync()` — Cleanup called in `finally` block to ensure resources released

## TCP Scanner Implementation (`Scanners/TcpScanner.cs`)

**Role-Based Architecture:**
- **Client Mode:** Connects to `Ip:Port`, sends `"TRG\r\n"` trigger every `RequestIntervalMs`, reads response
- **Server Mode:** Binds to local interface, accepts inbound connections from scanners, reads continuous stream

**Buffer Fragmentation Handling (Critical):**
- TCP is stream-based; never assume `ReadAsync` returns complete messages
- `StringBuilder` accumulates bytes until delimiter found
- **Delimiter logic:** Custom (`_delimiter`) or default (`\r` or `\n`), with `\r\n` sequence handling
- **Timeout Flush (configurable via `TimeoutFlushMs`):** If buffer has data but no delimiter and `DataAvailable==false`, flushes buffer as complete scan
  - Essential for scanners that don't send CR/LF terminators (e.g., Hikrobot ID2000)
  - Default 50ms, configurable per-scanner in `appsettings.json`
  - Implemented in both Client and Server modes

**Delimiter Configuration:**
- Hex format: `"0x0D"` or `"0x0D0A"` → Parsed to binary bytes → UTF-8 decoded
- Text format: `"\\r\\n"` → Escape sequences replaced with actual chars (`\r`, `\n`, `\t`, `\0`)
- If unset: Auto-detects `\r` or `\n` (whichever comes first)

**StartsWith Filter:** Optional `StartsWithFilter` config rejects scans not matching prefix (e.g., `"]C1"`)

## Data Processing Flow

**`Services/GoogleSheetsWebhook.cs`:**
1. `ProcessScanAsync()` entry point
2. Reject if contains `"NoRead"` or empty/whitespace
3. Check deduplication cache (`Dictionary<string, DateTime>`) with `CacheRetentionSeconds` retention
4. Parallel dispatch: `Task.WhenAll([SaveScanToFileAsync, SendToGoogleSheetsAsync])` if respective flags enabled
5. Periodic cache cleanup (runs when `now - _lastCleanupTime > retention window`)

**File Output:** Creates timestamped files in `OutputPath` (e.g., `2026-01-14_03-25-08-558.txt`)
- Supports placeholders: `{code}`, `{timestamp}`, `{scanner}`, `{remote}`
- Template precedence: `FileFormat` > `FilePrefix` + code + `FileSuffix`

**HTTP Output (Google Sheets Webhook):** POSTs JSON to `WebhookUrl`
```json
{
  "code": "ABC123",
  "scanner": "MainScanner",
  "remote": "192.168.1.100:54321"
}
```

**Expected Response:** JSON with `{"result": "success"}` or `{"result": "error", "message": "..."}`

## Google Sheets Integration (`scripts/google_sheets_script.js`)

**Webhook Behavior:**
- Receives POST requests with scan data: `{"code": "ABC123", "scanner": "Scanner1", "remote": "192.168.1.1:1234"}`
- Uses `LockService` to prevent concurrent write conflicts (30s timeout)
- Searches range **F2:Z2** for keyword matches in scan code (case-insensitive, **even columns only**: F, H, J, L, N, P, R, T, V, X, Z)
- If match found: writes scan to first empty row in matched column (starting from row 3)
- If no match: writes to column A starting from row 3 (fallback)
- Writes timestamp in adjacent column (right of code)
- Returns JSON: `{"result": "success", "row": 3, "col": 6}` or `{"result": "error", "message": "..."}`

**Column Layout:**
```
Row 2: [Keywords in even columns]  F2    H2    J2    L2    N2    P2    R2    T2    V2    X2    Z2
Row 3+: [Data rows]                Data  Time  Data  Time  Data  Time  ...
                                    ^     ^     ^     ^     
                                    Code  TS    Code  TS    
```

**Fallback Logic:**
- No keyword match → Column A (row 3+)
- Column B contains timestamp for fallback entries

**Setup:**
1. Open Google Sheet → Extensions → Apps Script
2. Paste `scripts/google_sheets_script.js` code
3. Deploy → New deployment → Web app → Set "Execute as: Me" and "Who has access: Anyone"
4. Copy Web app URL → Set as `WebhookUrl` in `appsettings.json`

## Key Configuration Patterns (`appsettings.json`)

```json
{
  "System": {
    "CancelOnAny": true,              // If true, any scanner failure aborts all; false = continue with working scanners
    "ScannerTimeoutSeconds": 20,      // ConnectAsync timeout
    "AutoRetryEnabled": true,         // Auto-retry on failure (no Enter key wait)
    "MaxRetryAttempts": 0,            // 0 = infinite retries
    "RetryDelaySeconds": 5            // Delay between retry attempts
  },
  "GoogleSheets": {
    "WebhookUrl": "https://...",
    "CacheRetentionSeconds": 250.0,   // Deduplication window
    "EnableFileOutput": true,
    "EnableGoogleSheets": true,
    "OutputPath": "Scans",
    "FileFormat": "{timestamp} {code}" // Or use FilePrefix/FileSuffix
  },
  "Scanners": [
    {
      "Name": "MainScanner",
      "Ip": "",                        // Empty or "0.0.0.0" for Server = bind to all interfaces
      "Port": 2002,
      "Enabled": true,
      "Role": "Server",                // "Client" or "Server"
      "ListenInterface": "eth0",       // Optional: bind to specific interface by name or IP
      "Delimiter": "0x0D",             // Custom delimiter (hex or escaped text)
      "StartsWithFilter": "]C1",       // Optional: prefix filter
      "RequestIntervalMs": 100,        // Client mode trigger interval
      "TimeoutFlushMs": 50             // Buffer timeout flush delay (default 50ms)
    }
  ]
}
```

## Hikrobot ID2000 Scanner Configuration

**Hardware Characteristics:**
- Sends continuous data stream at ~100ms intervals
- May not include standard CR/LF terminators
- Recommended Server mode configuration

**Optimal Settings:**
```json
{
  "Name": "Hikrobot_ID2000",
  "Ip": "",
  "Port": 2002,
  "Role": "Server",
  "TimeoutFlushMs": 50,
  "RequestIntervalMs": 100
}
```

**Network Setup:**
- Configure scanner to connect to PC's IP address
- Set scanner port to match `Port` in config
- If multiple network interfaces, use `ListenInterface` to specify binding

**Troubleshooting:**
- **"No delimiter found"** → Increase `TimeoutFlushMs` to 100-150ms for network latency
- **Multiple interfaces prompt** → Set `ListenInterface` to specific interface (e.g., `"eth0"` or `"192.168.1.10"`)
- **Duplicate scans** → Scanner may send repeated data; increase `CacheRetentionSeconds`

## Build & Debugging

**Run Locally:**
```bash
dotnet run
# Logs written to Logs/<timestamp>.txt and console
# Scans written to Scans/<timestamp>.txt (if OutputPath configured)
```

**Build Scripts:**
```bash
# Linux (CachyOS/Arch)
./build-linux.sh           # Debug + Release for Linux x64
./build-testscanner.sh     # Build scanner emulator

# Windows
build-windows.bat          # Debug + Release for Windows x64
```

**VS Code Debugging:**
- **F5** launches ScanFetch with debugger attached
- **Ctrl+Shift+P** → Debug → Select "TestScanner" to debug emulator
- Configuration in `.vscode/launch.json`
- Breakpoints work in all .cs files

**TestScanner Emulator:**
```bash
dotnet run --project TestScanner/TestScanner.csproj
# Interactive CLI for testing without physical scanner
# Recommended: Server mode (emulates real scanner behavior)
# Configure ScanFetch in Client mode to connect to TestScanner
```

**Publish Single-File Executable:**
```bash
dotnet publish ScanFetch.csproj -c Release -r linux-x64 --self-contained true -o publish/linux-x64
# Windows: -r win-x64
# macOS: -r osx-arm64
```

## Logging Conventions

- **LogInformation:** Operational events, connection status, scan events (including `[RAW]` data)
- **LogDebug:** Buffer state, cache diagnostics (low-priority details)
- **LogWarning:** Partial failures, timeout flushes, disconnects
- **LogError:** Fatal errors, unrecoverable scanner failures
- **LogTrace:** Filtered scans (not logged by default)

**Raw Data Visibility:** `[RAW]` logs show hex bytes + ASCII representation (e.g., `0D-0A | <CR><LF>`) to debug delimiter issues

## GUI Integration (`Gui/ScanFetch.Gui`)

Avalonia-based settings editor (separate project, excluded from console build):
```bash
dotnet run --project Gui/ScanFetch.Gui
```

Auto-launch: Set `"Gui": { "EnableGui": true }` in `appsettings.json` (requires pre-built GUI executable in `Gui/ScanFetch.Gui/bin/Debug/net10.0/`)

## Common Pitfalls

1. **"No delimiter found"** → Check scanner output with `[RAW]` logs; ensure `Delimiter` config matches actual bytes sent
2. **Duplicate scans** → Increase `CacheRetentionSeconds` or check timestamp logic
3. **Server won't bind (multiple interfaces)** → **BLOCKING OPERATION (Server mode only):** Console will prompt for interface selection; set `ListenInterface` in scanner config to avoid manual input
4. **Client trigger ignored** → Some scanners expect different command format (default: `"TRG\r\n"`)
5. **Timeout flush fires prematurely** → Increase `TimeoutFlushMs` if network latency high (Hikrobot ID2000: try 100-150ms)
6. **Auto-retry disabled** → Set `AutoRetryEnabled=true` to avoid manual Enter key presses in production
7. **Google Sheets 429 errors** → Webhook rate-limited; increase `CacheRetentionSeconds` to reduce duplicate requests

## Development Patterns

**Adding New Scanner Model Support:**
1. Test with `[RAW]` logs to identify delimiter pattern
2. Configure `Delimiter` in hex format if non-standard
3. Adjust `TimeoutFlushMs` based on scanner transmission speed
4. Use `StartsWithFilter` if scanner sends mixed data streams

**Modifying Data Flow:**
- Scan → `TcpScanner.OnDataReceived` → `GoogleSheetsWebhook.ProcessScanAsync` → Parallel dispatch
- Add custom processing in `GoogleSheetsWebhook` before/after `ProcessScanAsync`
- Extend `ScanDataEventArgs` model for additional metadata
