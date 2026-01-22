# TestScanner - Эмулятор сканера для ScanFetch

Консольное приложение для тестирования ScanFetch без физического сканера.

## Возможности

- **Client Mode:** Подключается к ScanFetch Server и отправляет коды
- **Server Mode:** Принимает подключения от ScanFetch Client
- Ручной ввод кодов через консоль
- Отображение входящих TCP сообщений (триггеры от ScanFetch)
- Автоматическое добавление CR/LF к отправляемым кодам
- Цветной интерфейс Spectre.Console

## Запуск

### Из корневой директории проекта:
```bash
dotnet run --project TestScanner/TestScanner.csproj
```

### Из директории TestScanner:
```bash
cd TestScanner
dotnet run
```

### Запуск скомпилированной версии:
```bash
./TestScanner/bin/Debug/net10.0/TestScanner
```

## Примеры использования

### Основной сценарий: TestScanner эмулирует реальный сканер (Server mode)

**Рекомендуемый режим для разработки:**

1. Запустите TestScanner в Server mode (порт 2002)
2. Настройте ScanFetch в Client mode: `"Role": "Client", "Ip": "127.0.0.1", "Port": 2002`
3. Запустите ScanFetch - он автоматически подключится к TestScanner
4. Вводите коды в TestScanner для отправки в ScanFetch
5. ScanFetch обработает коды и отправит в Google Sheets / файлы

**Почему Server mode?** Реальные сканеры (например, Hikrobot ID2000) работают как серверы, ожидая подключения от приложения. TestScanner эмулирует это поведение.

### Альтернативный сценарий: TestScanner в Client mode

Используется для тестирования ScanFetch в Server mode (когда реальный сканер должен подключаться):

1. Запустите ScanFetch с `"Role": "Server", "Port": 2002`
2. Запустите TestScanner, выберите "Client" mode
3. Введите IP: `127.0.0.1`, порт: `2002`
4. Вводите коды для отправки

**Примечание:** В Server mode ScanFetch потребует выбор сетевого интерфейса, если их несколько.

## Команды

- Введите любой код и нажмите Enter для отправки
- Введите `exit` для выхода из программы

## Формат данных

Все отправляемые коды автоматически дополняются терминаторами `\r\n` для совместимости с протоколом ScanFetch.
