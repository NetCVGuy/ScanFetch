# ScanFetch .NET

[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey.svg)](README.md)

TCP barcode scanner bridge with Google Sheets integration and local file output.

**Описание проекта (русский / English)**

- **Русский:** Консольное .NET-приложение для приёма сканов по TCP от сканеров (Client/Server режимы) и автоматической отправки в Google Sheets и/или локальные файлы. Поддержка автоматического переподключения, интерактивной отладки, и работы с различными моделями сканеров (Hikrobot ID2000, и др.).
- **English:** Console .NET application that receives scans over TCP from barcode scanners (Client/Server modes) and automatically sends them to Google Sheets and/or local files. Features auto-reconnect, interactive debug mode, and support for various scanner models (Hikrobot ID2000, etc.).

## ✨ Features

- **Dual TCP Modes:** Client (connect to scanner) and Server (accept connections from scanner)
- **Google Sheets Integration:** Automatic keyword-based column routing (F2:Z2 even columns)
- **Auto-Retry System:** Configurable automatic reconnection on failure
- **Interactive Debug Mode:** CLI-based parameter configuration at runtime
- **Scanner Support:** Generic TCP scanners + optimized profiles (Hikrobot ID2000)
- **Configurable Timeout Flush:** Per-scanner buffer timeout for non-terminated streams
- **Deduplication Cache:** Prevents duplicate scans with configurable retention
- **Cross-Platform:** Windows, Linux, macOS builds available
- **VS Code Debugging:** Full debugging support with launch configurations
- **TestScanner Emulator:** Hardware-free testing tool

## 📋 Requirements

- **.NET SDK 10.0** (recommended) - [Download](https://dotnet.microsoft.com/download)
- Network access to scanners (IP:Port configured in settings)
- Internet access for Google Sheets webhook (if enabled)
- Google Sheets with configured webhook script (see [Google Sheets Setup](#-настройка-google-sheets--google-sheets-setup))

## 🚀 Quick Start

1. **Clone and build:**
```bash
git clone <repository-url>
cd ScanFetch
dotnet restore
dotnet build
```

2. **Configure scanner in [appsettings.json](appsettings.json):**
```json
{
  "System": {
    "DebugMode": true,           // Enable interactive CLI
    "AutoRetryEnabled": true     // Auto-reconnect on failure
  },
  "Scanners": [{
    "Name": "MainScanner",
    "Ip": "192.168.1.100",
    "Port": 2002,
    "Role": "Client",            // or "Server"
    "Enabled": true
  }]
}
```

3. **Run:**
```bash
dotnet run
```

4. **Or use Debug Mode for interactive configuration:**
   - Set `"DebugMode": true` in appsettings.json
   - Run and follow CLI prompts to configure parameters

## 📦 Pre-built Releases

Download platform-specific executables from `publish/` directory:
- **Windows:** `publish/win-x64/ScanFetch.exe`
- **Linux:** `publish/linux-x64/ScanFetch`

Run directly without .NET SDK installation (self-contained builds).

## 🔧 Key Files

- **[appsettings.json](appsettings.json)** — Main configuration file
- **[appsettings.testscanner.json](appsettings.testscanner.json)** — TestScanner example config (Client mode)
- **[appsettings.hikrobot.json](appsettings.hikrobot.json)** — Hikrobot ID2000 config example (Server mode)
- **[CHANGELOG.md](CHANGELOG.md)** — Complete version history and changes
- **[.github/copilot-instructions.md](.github/copilot-instructions.md)** — AI agent development guide

**Source Code:**
- **[Program.cs](Program.cs)** — Entry point, service initialization, auto-retry loop, debug mode
- **[Configuration/AppSettings.cs](Configuration/AppSettings.cs)** — Configuration model with all settings
- **[Scanners/TcpScanner.cs](Scanners/TcpScanner.cs)** — TCP scanner implementation (Client/Server modes)
- **[Services/GoogleSheetsWebhook.cs](Services/GoogleSheetsWebhook.cs)** — Google Sheets + file output logic
- **[Logging/](Logging/)** — Custom logging providers (Spectre.Console + File)

**Scripts:**
- **[scripts/google_sheets_script.js](scripts/google_sheets_script.js)** — Google Apps Script webhook code
- **[build-linux.sh](build-linux.sh)** — Linux build script
- **[build-windows.bat](build-windows.bat)** — Windows build script
- **[build-testscanner.sh](build-testscanner.sh)** — TestScanner build script

## ⚙️ Configuration

### System Settings

```json
"System": {
  "CancelOnAny": true,              // Abort all if any scanner fails
  "ScannerTimeoutSeconds": 20,      // Connection timeout
  "AutoRetryEnabled": true,         // Auto-reconnect on failure (NEW)
  "MaxRetryAttempts": 0,            // 0 = infinite retries (NEW)
  "RetryDelaySeconds": 5,           // Delay between retries (NEW)
  "DebugMode": true                 // Interactive CLI configuration (NEW)
}
```

### Google Sheets Settings

```json
"GoogleSheets": {
  "WebhookUrl": "https://script.google.com/.../exec",
  "CacheRetentionSeconds": 250.0,  // Deduplication window
  "EnableFileOutput": true,
  "EnableGoogleSheets": true,
  "OutputPath": "Scans",
  "FilePrefix": "",               // Legacy: use FileFormat instead
  "FileSuffix": "",               // Legacy: use FileFormat instead
  "FileFormat": "{timestamp} {code}"  // Template with placeholders (NEW)
}
```

**Placeholders:**
- `{code}` — Scanned barcode. Example: `ABC123`
- `{timestamp}` — ISO-8601 timestamp. Example: `2026-01-14T03:25:08.5580000Z`
- `{scanner}` — Scanner name from config. Example: `MainScanner`
- `{remote}` — Remote endpoint (Server mode). Example: `192.168.1.100:54321`

**Template Examples:**
- `"{timestamp} {code}"` → `2026-01-14T03:25:08.5580000Z ABC123`
- `"{scanner} - {code}"` → `MainScanner - ABC123`
- `"Scan from {remote}: {code}"` → `Scan from 192.168.1.100:54321: ABC123`

### Scanner Settings

```json
"Scanners": [{
  "Name": "MainScanner",
  "Ip": "192.168.1.100",          // Scanner IP (Client mode) or bind IP (Server mode)
  "Port": 2002,
  "Enabled": true,
  "Role": "Client",               // "Client" or "Server"
  "ListenInterface": "eth0",      // Server mode: bind to specific interface (optional)
  "Delimiter": "0x0D",            // Custom delimiter: hex (0x0D0A) or escaped text (\r\n)
  "StartsWithFilter": "]C1",      // Optional: prefix filter for scans
  "RequestIntervalMs": 100,       // Client mode: trigger interval (NEW)
  "TimeoutFlushMs": 50            // Buffer timeout for non-terminated streams (NEW)
}]
```

**Role Modes:**
- **Client:** ScanFetch connects to scanner's IP:Port (outbound connection)
- **Server:** ScanFetch listens on Port, scanner connects to ScanFetch (inbound connection)

**Delimiter Configuration:**
- Hex format: `"0x0D"` or `"0x0D0A"` → Parsed to binary bytes
- Text format: `"\\r\\n"` → Escape sequences (`\r`, `\n`, `\t`, `\0`) replaced
- If unset: Auto-detects `\r` or `\n` (whichever comes first)

**Hikrobot ID2000 Example:**
```json
{
  "Name": "Hikrobot_ID2000",
  "Ip": "",                        // Empty = bind to all interfaces
  "Port": 2002,
  "Role": "Server",
  "TimeoutFlushMs": 50,            // Essential for non-terminated streams
  "RequestIntervalMs": 100
}
```

## 🏗️ Building / Сборка проектов

### Quick Build Scripts

**Linux (CachyOS/Arch):**
```bash
chmod +x build-linux.sh build-testscanner.sh
./build-linux.sh           # ScanFetch Debug + Release for Linux x64
./build-testscanner.sh     # TestScanner emulator
```

**Windows:**
```cmd
build-windows.bat          # ScanFetch Debug + Release for Windows x64
```

### Manual Cross-Platform Build

```bash
# Linux x64
dotnet publish -c Release -r linux-x64 --self-contained true -o publish/linux-x64

# Windows x64
dotnet publish -c Release -r win-x64 --self-contained true -o publish/win-x64

# macOS ARM64
dotnet publish -c Release -r osx-arm64 --self-contained true -o publish/osx-arm64
```

**Output:** Self-contained single-file executables in `publish/<platform>/`

## 🐛 Debugging / Отладка

### VS Code Debugging

Project configured for VS Code debugging with [.vscode/launch.json](.vscode/launch.json):

- **F5** — Launch ScanFetch with debugger attached
- **Ctrl+Shift+P** → "Debug: Select and Start Debugging" → Choose "TestScanner" for emulator debugging

Breakpoints work in all `.cs` files. Debug console shows logs and execution flow.

### TestScanner Emulator

Hardware-free testing tool for development:

**Terminal 1 - TestScanner (Server mode, emulates scanner):**
```bash
dotnet run --project TestScanner/TestScanner.csproj
# Select: Server mode, Port: 2002
# Send test codes via interactive CLI
```

**Terminal 2 - ScanFetch (Client mode, connects to emulator):**
```bash
# Configure in appsettings.json:
# "Role": "Client", "Ip": "127.0.0.1", "Port": 2002
dotnet run
```

**Note:** TestScanner Server mode mimics real scanner behavior (e.g., Hikrobot ID2000) by accepting inbound connections.

See [TestScanner/README.md](TestScanner/README.md) for detailed usage.

## 🏃 Running / Запуск

### Production Mode

1. Configure [appsettings.json](appsettings.json) with production settings
2. Set `"DebugMode": false` and `"AutoRetryEnabled": true`
3. Run:
```bash
dotnet run
```

### Debug Mode (Interactive Configuration)

1. Set `"DebugMode": true` in [appsettings.json](appsettings.json)
2. Run and follow CLI prompts:
```bash
dotnet run
```

3. Choose configuration mode:
   - **Создать новую конфигурацию** → Interactive parameter entry
   - **Использовать конфигурацию** → Load from appsettings.json

4. Configure parameters via CLI:
   - Scanner role (Client/Server)
   - IP address and port
   - Delimiter (custom or auto)
   - Timeout flush (ms)
   - Request interval (Client mode)

### Logs and Output

- **Console:** Colored output via Spectre.Console
- **Log Files:** `Logs/<timestamp>.txt` (timestamped per session)
- **Scan Files:** `Scans/<timestamp>.txt` (if `EnableFileOutput: true`)

**Raw Data Debugging:** Look for `[RAW]` entries in logs showing hex bytes + ASCII representation.

## 📊 Настройка Google Sheets / Google Sheets Setup

### Script Installation

Google Apps Script code: [scripts/google_sheets_script.js](scripts/google_sheets_script.js)

**Installation Steps:**

1. Open your Google Sheet → **Extensions** → **Apps Script**
2. Delete default code and paste content from `scripts/google_sheets_script.js`
3. **Important:** Ensure your sheet is named **"TEST"** (script explicitly targets this sheet name)
4. Click **Deploy** → **New deployment** → **Web app**
5. Configure deployment:
   - **Execute as:** Me
   - **Who has access:** Anyone
6. Click **Deploy** and authorize the script
7. Copy the **Web app URL** (ends with `/exec`)
8. Paste URL into `WebhookUrl` in [appsettings.json](appsettings.json)

### Sheet Structure

```
    A       B          F         G      H         I      J         K      
1   -       -          -         -      -         -      -         -      
2   -       -          Keyword1  -      Keyword2  -      Keyword3  -      ← Keywords in EVEN columns only (F, H, J, L, N, P, R, T, V, X, Z)
3   Code1   Time1      Code2     Time2  Code3     Time3  -         -      ← Data starts at row 3
4   Code4   Time4      Code5     Time5  -         -      Code6     Time6
```

### How It Works

**Keyword Matching Logic:**
- Keywords placed in **row 2**, columns **F-Z** (only even columns: F, H, J, L, N, P, R, T, V, X, Z)
- Script searches F2:Z2 for case-insensitive substring matches in scan code
- If match found → writes to matched column (starting row 3)
- If no match → writes to **column A** (fallback, starting row 3)
- Timestamp always written in **adjacent column** (right of code)

**Example:**
- Scan code: `"ABC-WIDGET-123"`
- Keyword in F2: `"WIDGET"`
- Result: Code written to column F (first empty row from F3 down), timestamp in column G

**Request Format:**
```json
POST https://script.google.com/.../exec
{
  "code": "ABC123",
  "scanner": "MainScanner",
  "remote": "192.168.1.100:54321"
}
```

**Response Format:**
```json
{
  "result": "success",
  "row": 3,
  "col": 6,
  "code": "ABC123",
  "foundMatch": true,
  "message": "ABC123"
}
```

### Troubleshooting

**Data not appearing:**
1. Verify sheet is named **"TEST"** (case-sensitive)
2. Check Apps Script Executions (Extensions → Apps Script → Executions tab)
3. Verify deployment has "Who has access: Anyone" (not "Anyone with Google account")
4. Check ScanFetch logs for HTTP errors or timeout messages
5. Test webhook with curl:
```bash
curl -X POST "YOUR_WEBHOOK_URL" \
  -H "Content-Type: application/json" \
  -d '{"code":"TEST123","scanner":"Test","remote":"127.0.0.1:0"}'
```

**"Sheet TEST not found" error:**
- Create a sheet named "TEST" in your Google Sheets document
- Or modify script line 37 to use different sheet name: `getSheetByName("YOUR_SHEET_NAME")`

**Duplicate scans:**
- Increase `CacheRetentionSeconds` in appsettings.json (default 250s)
- Scanner may be sending repeated data (check with `[RAW]` logs)
## 🎨 GUI Editor (Optional)

Avalonia-based cross-platform GUI for editing `appsettings.json`:

**Run during development:**
```bash
dotnet restore
dotnet run --project Gui/ScanFetch.Gui
```

**Enable auto-launch:** Add to `appsettings.json`:
```json
"Gui": { "EnableGui": true }
```

**Build self-contained GUI:**
```bash
# Windows x64
dotnet publish Gui/ScanFetch.Gui -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/gui-win-x64

# Linux x64
dotnet publish Gui/ScanFetch.Gui -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/gui-linux-x64
```

**Note:** GUI project excluded from main console build; requires explicit build command.

## 📖 Documentation

- **[CHANGELOG.md](CHANGELOG.md)** — Complete version history with all changes since initial release
- **[.github/copilot-instructions.md](.github/copilot-instructions.md)** — Comprehensive AI agent guide covering:
  - Architecture overview with "hot reload" pattern
  - TCP scanner implementation details
  - Buffer fragmentation handling
  - Data processing flow
  - Google Sheets integration
  - Common pitfalls and troubleshooting
  - Development patterns

## 🛠️ Troubleshooting

### "No delimiter found" in logs
- Check scanner output with `[RAW]` logs (shows hex bytes)
- Verify `Delimiter` config matches actual bytes sent by scanner
- Try auto-detection: remove `Delimiter` setting

### Duplicate scans appearing
- Increase `CacheRetentionSeconds` (default 250s)
- Check if scanner sends repeated data (visible in `[RAW]` logs)
- Verify scanner configuration (may have auto-repeat enabled)

### Server mode won't bind
- **Multiple interfaces:** Set `ListenInterface` to specific interface name (e.g., `"eth0"`) or IP
- Check if port already in use: `netstat -tulpn | grep <PORT>`
- Verify firewall allows inbound connections on specified port

### Client mode connection refused
- Verify scanner is in Server mode (accepting connections)
- Check IP address and port configuration
- Ping scanner IP to verify network connectivity
- Verify scanner firewall allows connections on specified port

### Timeout flush fires prematurely
- Increase `TimeoutFlushMs` (default 50ms)
- Network latency may require 100-150ms for Hikrobot ID2000
- Check `[RAW]` logs to see actual data arrival timing

### Google Sheets 429 errors (rate limiting)
- Increase `CacheRetentionSeconds` to reduce duplicate requests
- Google Apps Script has execution quotas (check Apps Script dashboard)
- Consider batching if sending high volume

### Google Sheets data not appearing
- See [Google Sheets Setup](#-настройка-google-sheets--google-sheets-setup) troubleshooting section

## 🚀 Production Deployment

**Recommended Production Settings:**

```json
{
  "System": {
    "DebugMode": false,           // Disable interactive prompts
    "AutoRetryEnabled": true,     // Enable auto-reconnect
    "MaxRetryAttempts": 0,        // Infinite retries
    "RetryDelaySeconds": 5,
    "CancelOnAny": false          // Continue with working scanners
  },
  "GoogleSheets": {
    "CacheRetentionSeconds": 300.0,  // 5 minutes deduplication
    "EnableFileOutput": true,        // Backup to files
    "EnableGoogleSheets": true
  }
}
```

**Systemd Service (Linux):**

Create `/etc/systemd/system/scanfetch.service`:
```ini
[Unit]
Description=ScanFetch Barcode Scanner Bridge
After=network.target

[Service]
Type=simple
User=scanfetch
WorkingDirectory=/opt/scanfetch
ExecStart=/opt/scanfetch/ScanFetch
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl daemon-reload
sudo systemctl enable scanfetch
sudo systemctl start scanfetch
sudo systemctl status scanfetch
```

**Windows Service:**

Use NSSM (Non-Sucking Service Manager):
```cmd
nssm install ScanFetch "C:\ScanFetch\ScanFetch.exe"
nssm set ScanFetch AppDirectory "C:\ScanFetch"
nssm start ScanFetch
```

## 📜 License

This project is licensed under the MIT License.

## 🤝 Contributing

Contributions welcome! Please:
1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open Pull Request

## 📞 Support

For issues, questions, or feature requests:
- Open an issue on GitHub
- Check [CHANGELOG.md](CHANGELOG.md) for recent changes
- Review [.github/copilot-instructions.md](.github/copilot-instructions.md) for technical details

---

**Version:** 1.0 (2026-01-22)  
**Platform:** .NET 10.0  
**Status:** ✅ Production Ready
