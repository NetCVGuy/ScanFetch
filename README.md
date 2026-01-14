# ScanFetch .NET Migration

This project is a migration of `scan.py` to .NET 10.0.

**ScanFetch .NET**

Описание проекта (русский / English)

- **Русский:** Консольное .NET-приложение для приёма сканов по TCP от сканеров и записи/отправки их в файл и/или Google Sheets через Webhook.
- **English:** Console .NET application that receives scans over TCP and writes them to files and/or posts them to Google Sheets via a webhook.

**Требования / Requirements**

- .NET SDK 10.0 (recommended). Older SDKs (8/9) may work if project target changed.
- Network access to scanners (IP:Port configured in settings).
- Internet access for Google App Script webhook (if enabled).

**Ключевые файлы / Key files**

- [appsettings.json](appsettings.json) — конфигурация приложения.
- [Configuration/AppSettings.cs](Configuration/AppSettings.cs) — модель конфигурации.
- [Program.cs](Program.cs) — точка входа, инициализация сервисов и сканеров.
- [Services/GoogleSheetsWebhook.cs](Services/GoogleSheetsWebhook.cs) — логика отправки в Google Sheets и записи в файл (разделена на методы).
- [Logging/FileLogger.cs](Logging/FileLogger.cs) — провайдер логирования в файл.

**Конфигурация / Configuration**

Пример секции `GoogleSheets` в [appsettings.json](appsettings.json):

```json
"GoogleSheets": {
   "WebhookUrl": "https://.../exec",
   "CacheRetentionSeconds": 180.0,
   "EnableFileOutput": true,
   "EnableGoogleSheets": true,
   "OutputPath": "Scans",
   "FilePrefix": "",
   "FileSuffix": "",
   "FileFormat": "{timestamp} {code}"
}
```

- `WebhookUrl` — URL вашего Google Apps Script вебхука.
- `CacheRetentionSeconds` — время удержания сканов в локальном кеше (для дедупликации) в секундах.
- `EnableFileOutput` — включить запись сканов в файлы.
- `EnableGoogleSheets` — включить отправку в Google Sheets.
- `OutputPath` — папка для файлов сканов (при пустом значении файлы не будут создаваться).
- `FilePrefix` / `FileSuffix` — префикс/суффикс при записи в файл (используется если `FileFormat` пуст).
- `FileFormat` — шаблон содержимого файла; поддерживаемые плейсхолдеры: `{code}`, `{timestamp}`.

**Плейсхолдеры / Placeholders**

- `{code}` — считанный код/строка от сканера. Example: `ABC123` / Пример: `ABC123`.
- `{timestamp}` — ISO-8601 timestamp (DateTime.ToString("O")). Example: `2026-01-14T03:25:08.5580000Z` / Пример: `2026-01-14T03:25:08.5580000Z`.

Примеры использования плейсхолдеров:

- `"FileFormat": "{timestamp} - {code}"` → `2026-01-14T03:25:08.5580000Z - ABC123`
- `"FilePrefix": "SCAN-"` и `"FileSuffix": "-OK"` → `SCAN-ABC123-OK`

**Как работает логика записи и отправки**

- Сервис [Services/GoogleSheetsWebhook.cs](Services/GoogleSheetsWebhook.cs) имеет отдельные методы:
   - `SaveScanToFileAsync(string code, string? scannerName = null, string? remote = null)` — сохраняет скан в отдельный файл в `OutputPath`.
   - `SendToGoogleSheetsAsync(string code, string? scannerName = null, string? remote = null)` — отправляет JSON POST `{ "code": "...", "scanner": "...", "remote": "..." }` на `WebhookUrl`.
- Метод `ProcessScanAsync(string code, string? scannerName = null, string? remote = null)` выполняет дедупликацию (по тексту и по времени кеша) и затем, если включены соответствующие флаги, параллельно вызывает `SaveScanToFileAsync` и/или `SendToGoogleSheetsAsync`.

**Запуск / Running**

1. Убедитесь, что в системе установлен .NET SDK.
2. Отредактируйте [appsettings.json](appsettings.json) согласно вашей среде.
3. Запустите:

```bash
dotnet run
```

**Все возможные плейсхолдеры (сводка) / All supported placeholders**

- `{code}` — содержимое скана.
- `{timestamp}` — временная метка в формате ISO-8601.
 - `{scanner}` — имя сканера из конфигурации (`ScannerSettings.Name`). Example: `FrontDoor` / Пример: `FrontDoor`.
 - `{remote}` — строка remote endpoint (например, `192.168.1.5:1234`) откуда пришёл скан (для Server роль). Example: `10.0.0.5:54321` / Пример: `10.0.0.5:54321`.

**Предложения по улучшению / Suggested improvements**


**GUI Editor (cross-platform)**

There is a small Avalonia-based GUI editor at `Gui/ScanFetch.Gui`. It can be used to edit `appsettings.json` via a friendly form (scanners list + scanner fields + GoogleSheets/file output settings) and also exposes a raw JSON editor.

How to run the GUI during development:

```bash
dotnet restore
dotnet run --project Gui/ScanFetch.Gui
```

To enable the program to attempt launching the GUI when it starts, add this to `appsettings.json`:

```json
"Gui": { "EnableGui": true }
```

Packaging notes (cross-platform):

- Build a self-contained single-file for Windows x64:

```bash
dotnet publish Gui/ScanFetch.Gui -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o published/gui-win-x64
```

- Build for Linux x64 (self-contained):

```bash
dotnet publish Gui/ScanFetch.Gui -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o published/gui-linux-x64
```

Notes:
- Building self-contained GUI binaries pulls in native runtime components; ensure corresponding runtime identifiers (`-r`) are supported on your machine and SDK.
- The GUI project is excluded from the main console project's compile glob, so building the console app doesn't require Avalonia packages unless you build the GUI project explicitly.
# ScanFetch
