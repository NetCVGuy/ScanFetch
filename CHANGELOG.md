# ScanFetch Changelog

## 2026-01-22 - Major Release v1.0

### 🎉 Google Sheets Integration - WORKING!
**CRITICAL FIX:** Google Sheets script now explicitly targets sheet by name
- ✅ Changed from `getActiveSheet()` to `getSheetByName("TEST")`
- ✅ Added error handling for missing sheet
- ✅ Data now correctly appears in TEST sheet
- ✅ Verified end-to-end: TestScanner → ScanFetch → Google Sheets → DATA VISIBLE!

### 🚀 New Features

#### Auto-Retry System
- **AutoRetryEnabled**: Automatic reconnection on failure (no manual Enter key)
- **MaxRetryAttempts**: Configurable retry limit (0 = infinite)
- **RetryDelaySeconds**: Delay between retry attempts
- Eliminates need for manual intervention in production environments

#### Interactive Debug Mode
- **DebugMode flag**: Enable interactive CLI parameter configuration
- Real-time scanner configuration via Spectre.Console prompts
- Choose Client/Server mode at runtime
- Configure IP, Port, Delimiter, Timeout, Request Interval interactively
- No need to edit appsettings.json for testing

#### Configurable Timeout Flush
- **TimeoutFlushMs**: Per-scanner buffer timeout configuration
- Essential for scanners without CR/LF terminators (e.g., Hikrobot ID2000)
- Default 50ms, adjustable for network latency
- Works in both Client and Server modes

#### Hikrobot ID2000 Support
- Dedicated configuration example: `appsettings.hikrobot.json`
- Optimal settings for continuous stream scanning
- Server mode with timeout flush
- Documentation for network setup

### 🛠️ Development Tools

#### TestScanner Emulator
- Full-featured scanner emulator for testing without hardware
- Both Client and Server modes supported
- Interactive CLI for sending test codes
- Automatic trigger simulation in Client mode
- Build script: `build-testscanner.sh`

#### VS Code Debugging
- Complete debug configuration in `.vscode/launch.json`
- ScanFetch debug profile (F5)
- TestScanner debug profile (Ctrl+Shift+P → Debug)
- Tasks for build automation
- Breakpoint support in all .cs files

#### Cross-Platform Build Scripts
- **Linux:** `build-linux.sh` (Debug + Release)
- **Windows:** `build-windows.bat` (Debug + Release)
- **TestScanner:** `build-testscanner.sh`
- Single-file self-contained executables
- Publish directories: `publish/linux-x64`, `publish/win-x64`

### 📚 Documentation

#### Comprehensive AI Agent Instructions
- `.github/copilot-instructions.md` created
- Architecture overview with "hot reload" pattern
- TCP scanner implementation details
- Data processing flow documentation
- Google Sheets integration guide
- Common pitfalls and troubleshooting
- Development patterns and best practices

#### Example Configurations
- `appsettings.hikrobot.json` - Hikrobot ID2000 setup
- `appsettings.testscanner.json` - TestScanner client config
- Detailed inline comments for all settings

### 🐛 Bug Fixes

#### .NET 10 SDK Assembly Attributes
- Fixed duplicate assembly attribute errors
- Added `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>`
- Added `<GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>`
- Removed hardcoded `<RuntimeIdentifier>win-x64</RuntimeIdentifier>`

#### HTTP Timeout Issues
- Increased Google Sheets webhook timeout: 5s → 35s
- Matches Google Apps Script lock timeout (30s)
- Added retry logic: 2 attempts with 2s delay
- Specific handling for `TaskCanceledException`

#### Google Sheets Script Reliability
- `getNextEmptyRow()` rewritten with linear search
- Removed unreliable `getLastRow()` dependency
- 1000 row search limit for performance
- Blank cell detection: `cell.isBlank()`

#### Google Sheets Sheet Targeting
- **CRITICAL:** Script now explicitly targets "TEST" sheet
- Fixed unreliable `getActiveSheet()` behavior with webhooks
- Added null check and error message if sheet not found
- Prevents data from being lost or written to wrong location

### 📊 Google Sheets Script Updates

#### Keyword Search Logic
- Search range: F2:Z2 (even columns only: F, H, J, L, N, P, R, T, V, X, Z)
- Case-insensitive substring matching
- First match wins (stops after first keyword found)
- Fallback to column A if no match

#### Data Writing
- Matched keywords: Write to keyword column starting row 3
- No match: Write to column A starting row 3
- Timestamp in adjacent column (right of code)
- LockService prevents concurrent write conflicts (30s timeout)

#### Response Format
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

### ⚙️ Configuration Changes

#### System Settings
```json
"System": {
  "CancelOnAny": true,
  "ScannerTimeoutSeconds": 20,
  "AutoRetryEnabled": true,      // NEW
  "MaxRetryAttempts": 0,         // NEW
  "RetryDelaySeconds": 5,        // NEW
  "DebugMode": true              // NEW
}
```

#### Scanner Settings
```json
"Scanners": [
  {
    "TimeoutFlushMs": 50,        // NEW - configurable per-scanner
    "ListenInterface": "eth0",   // Server mode interface binding
    "RequestIntervalMs": 100     // Client mode trigger interval
  }
]
```

#### Google Sheets Settings
```json
"GoogleSheets": {
  "WebhookUrl": "https://script.google.com/...",
  "CacheRetentionSeconds": 250.0,
  "EnableFileOutput": true,
  "EnableGoogleSheets": true,
  "OutputPath": "Scans",
  "FileFormat": "{timestamp} {code}"  // Supports placeholders
}
```

### 🔧 Technical Improvements

#### Logging Enhancements
- Raw data visibility: `[RAW]` logs show hex bytes + ASCII
- Delimiter debugging: Shows exact bytes received
- Buffer state logging for timeout flush
- Execution details logged to both console and file

#### Error Handling
- Graceful scanner failure handling
- CancelOnAny flag for failure propagation control
- Detailed error messages with context
- HTTP retry with exponential backoff

#### Performance Optimization
- Parallel dispatch: File write + Google Sheets send
- Deduplication cache with configurable retention
- Periodic cache cleanup (runs when needed)
- Buffer timeout prevents indefinite wait

### 📝 Files Modified/Created

**New Files:**
- `.github/copilot-instructions.md` - AI agent documentation
- `.vscode/launch.json` - Debug configurations
- `.vscode/tasks.json` - Build tasks
- `TestScanner/Program.cs` - Scanner emulator
- `TestScanner/TestScanner.csproj` - Emulator project
- `build-linux.sh` - Linux build script
- `build-windows.bat` - Windows build script
- `build-testscanner.sh` - TestScanner build script
- `appsettings.hikrobot.json` - Hikrobot config example
- `appsettings.testscanner.json` - TestScanner config example
- `CHANGELOG.md` - This file

**Modified Files:**
- `Configuration/AppSettings.cs` - Added new settings
- `Scanners/TcpScanner.cs` - Configurable timeout flush
- `Services/GoogleSheetsWebhook.cs` - Extended timeout + retry
- `Program.cs` - Auto-retry + debug mode
- `ScanFetch.csproj` - Assembly info fix
- `scripts/google_sheets_script.js` - Explicit sheet targeting
- `appsettings.json` - Updated with new settings

### 🎯 Testing Status

**Verified Working:**
- ✅ TestScanner emulator (both Client and Server modes)
- ✅ ScanFetch auto-retry on failure
- ✅ Interactive debug mode parameter configuration
- ✅ Google Sheets webhook integration (data appears in TEST sheet)
- ✅ Keyword search in F2:Z2 even columns
- ✅ Fallback to column A for unmatched codes
- ✅ Timestamp writing in adjacent columns
- ✅ HTTP timeout handling (35s)
- ✅ Buffer timeout flush for non-terminated scanners
- ✅ Cross-platform builds (Linux x64, Windows x64)
- ✅ VS Code debugging (breakpoints working)

### 🚨 Known Issues

**Workarounds Applied:**
- .NET 10 SDK assembly attribute bug → `GenerateAssemblyInfo=false`
- TestScanner CS7022 warning → Cosmetic, does not affect functionality

### 📖 Next Steps

**For Production Deployment:**
1. Set `DebugMode: false` in appsettings.json
2. Configure scanner settings (IP, Port, Role)
3. Update Google Sheets webhook URL
4. Verify "TEST" sheet exists in Google Sheets
5. Set keywords in F2:Z2 even columns (F, H, J, L, N, P, R, T, V, X, Z)
6. Run: `dotnet run` or use published executable

**For Development:**
1. Enable `DebugMode: true` for interactive testing
2. Use TestScanner for hardware-free testing
3. Check `Logs/` directory for detailed execution logs
4. Monitor Google Apps Script Executions for webhook debugging
5. Use VS Code debugger (F5) for step-through debugging

### 💡 Key Learnings

1. **Google Apps Script Webhooks:** Always use `getSheetByName()` explicitly, never rely on `getActiveSheet()`
2. **TCP Stream Handling:** Buffer fragmentation requires proper delimiter logic and timeout flush
3. **HttpClient Timeouts:** Must exceed Google Apps Script lock timeout (30s)
4. **.NET 10 SDK:** Known bug with assembly info generation (workaround required)
5. **Interactive CLI:** Spectre.Console provides excellent UX for parameter configuration

---

## Previous Versions

### Initial Release (2026-01-06)
- Basic TCP scanner implementation (Client/Server modes)
- Google Sheets webhook integration
- File output with timestamps
- Spectre.Console logging
- Configuration via appsettings.json
