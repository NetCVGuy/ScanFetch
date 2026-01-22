# ScanFetch Monitor - Android App

Android приложение для мониторинга состояния TCP сканеров ScanFetch в реальном времени.

## Возможности

- **Real-time мониторинг** - подключение к REST API ScanFetch через SSE (Server-Sent Events)
- **Статус сканеров** - отображение состояния всех настроенных сканеров (подключен/отключен)
- **Push-уведомления** - автоматические уведомления при ошибках и отключении сканеров
- **История событий** - просмотр последних ошибок и событий
- **Dark theme** - современный темный интерфейс

## Требования

- Android 8.0 (API 26) и выше
- Доступ к локальной сети с ScanFetch сервером
- Разрешение на отправку уведомлений (Android 13+)

## Сборка

### С помощью Gradle (рекомендуется)

```bash
# Debug сборка
./gradlew assembleDebug

# Release сборка
./gradlew assembleRelease

# Установка на подключенное устройство
./gradlew installDebug
```

### С помощью скриптов

```bash
# Собрать APK
./build.sh

# Собрать и установить на устройство
./install.sh
```

APK файлы будут в `app/build/outputs/apk/`

## Установка и настройка

1. Установите APK на Android устройство
2. При первом запросе разрешите отправку уведомлений
3. Введите IP адрес компьютера с запущенным ScanFetch (например, `192.168.1.100`)
4. Укажите порт API (по умолчанию `5000`)
5. Нажмите **Connect**

## Использование

### Основной экран

- **Верхняя карточка**: настройки подключения и индикатор статуса
- **Scanners Status**: список всех сканеров с их текущим состоянием
  - Зеленая иконка = подключен
  - Красная иконка = отключен
- **Recent Events**: последние ошибки и события

### Уведомления

Приложение автоматически показывает push-уведомления при:
- Ошибке работы сканера (`ScannerError`)
- Отключении сканера (`ScannerDisconnected`)

### Кнопки управления

- **Connect/Disconnect**: подключение/отключение от сервера
- **Refresh** (иконка в верхнем меню): обновить данные вручную

## Архитектура

### Технологии

- **Kotlin** - основной язык
- **Jetpack Compose** - современный UI фреймворк
- **Retrofit** - REST API клиент
- **OkHttp SSE** - Server-Sent Events для real-time обновлений
- **Coroutines** - асинхронная обработка
- **ViewModel** - управление состоянием

### Структура

```
app/src/main/java/com/scanfetch/monitor/
├── MainActivity.kt           # Главный экран с Compose UI
├── MonitorApplication.kt     # Инициализация приложения
├── MonitorService.kt         # Фоновый сервис для SSE
├── data/
│   ├── Models.kt            # Data классы (ScannerStatus, ScannerEvent)
│   ├── ScanFetchApi.kt      # Retrofit API интерфейс
│   └── ScanFetchRepository.kt # Репозиторий для работы с API
└── ui/
    └── MonitorViewModel.kt  # ViewModel для экрана
```

## API Endpoints

Приложение использует следующие endpoints ScanFetch:

- `GET /api/status` - получить статус всех сканеров
- `GET /api/errors?count=50` - получить историю ошибок
- `GET /api/history?count=50` - получить полную историю событий
- `GET /api/events` - SSE stream для real-time уведомлений

## Troubleshooting

**Приложение не подключается к серверу:**
- Проверьте, что ScanFetch запущен с `"MonitoringApi": { "Enabled": true }`
- Убедитесь, что устройство в той же локальной сети
- Проверьте firewall на компьютере с ScanFetch (порт 5000)
- Попробуйте указать IP явно: `http://192.168.1.100:5000`

**Уведомления не приходят:**
- Проверьте разрешение на уведомления в настройках Android
- Убедитесь, что приложение не в режиме энергосбережения
- Проверьте, что ScanFetch отправляет события (проверьте логи)

**События не обновляются:**
- Нажмите кнопку Refresh в верхнем меню
- Переподключитесь к серверу
- Проверьте стабильность сети

## Разработка

### Добавление новых типов событий

1. Обновите `EventType` enum в `Services/EventBus.cs` (ScanFetch)
2. Добавьте обработку в `MonitorService.onEvent()`
3. Обновите UI в `MainActivity.kt` для отображения

### Изменение интервала опроса

В `MonitorViewModel.kt` измените:
```kotlin
delay(3000) // Poll every 3 seconds
```

### Настройка уведомлений

В `MonitorService.kt` измените `showAlertNotification()`

## Лицензия

Часть проекта ScanFetch © 2026
