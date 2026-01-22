# ScanFetch Monitor - Сборка Android приложения

## ⚠️ Важно: Проблема с Java 25

Kotlin в текущих версиях не поддерживает Java 25. Для сборки требуется Java 17.

## Быстрое решение (Рекомендуется)

```bash
# Автоматически установит Java 17 и соберёт проект
./build-fix.sh
```

После успешной сборки APK будет в: `app/build/outputs/apk/debug/app-debug.apk`

---

## Ручная установка Java 17

```bash
# Arch Linux / CachyOS
sudo pacman -S jdk17-openjdk

# Проверка
ls /usr/lib/jvm/java-17-openjdk
```

Затем собрать с указанием Java 17:
```bash
export JAVA_HOME=/usr/lib/jvm/java-17-openjdk
./gradlew assembleDebug
```

---

### Вариант 1: Android Studio (Рекомендуется)

1. Открыть проект в Android Studio:
   ```bash
   # Запустить Android Studio и выбрать "Open Project"
   # Указать путь: /run/media/zahar/moding/VSPRJ/ScanFetch/AndroidMonitor
   ```

2. Android Studio автоматически:
   - Скачает Gradle Wrapper
   - Синхронизирует зависимости
   - Предложит собрать проект

3. Собрать APK:
   - `Build → Build Bundle(s) / APK(s) → Build APK(s)`
   - APK будет в `app/build/outputs/apk/debug/app-debug.apk`

### Вариант 2: Установить Gradle локально

```bash
# Arch Linux / CachyOS
sudo pacman -S gradle

# После установки
cd /run/media/zahar/moding/VSPRJ/ScanFetch/AndroidMonitor
gradle wrapper  # Создаст gradle-wrapper.jar
./gradlew assembleDebug
```

### Вариант 3: Скачать gradle-wrapper.jar вручную

```bash
cd /run/media/zahar/moding/VSPRJ/ScanFetch/AndroidMonitor
mkdir -p gradle/wrapper
cd gradle/wrapper

# Скачать gradle-wrapper.jar (версия 8.4)
wget https://raw.githubusercontent.com/gradle/gradle/v8.4.0/gradle/wrapper/gradle-wrapper.jar

# Вернуться в корень проекта
cd ../..
./gradlew assembleDebug
```

### Вариант 4: Использовать системный Gradle (быстрый способ)

Если Gradle уже установлен:
```bash
cd /run/media/zahar/moding/VSPRJ/ScanFetch/AndroidMonitor
gradle assembleDebug
```

APK будет в: `app/build/outputs/apk/debug/app-debug.apk`

## После успешной сборки

Установить на устройство:
```bash
# Через ADB (подключить телефон по USB с включенной отладкой)
adb install app/build/outputs/apk/debug/app-debug.apk

# Или скопировать APK на телефон и установить вручную
```

## Проверка зависимостей

```bash
# Java должна быть установлена (JDK 17+)
java -version

# Android SDK (если нет Android Studio)
export ANDROID_HOME=/path/to/android/sdk
export PATH=$PATH:$ANDROID_HOME/tools:$ANDROID_HOME/platform-tools
```

## Troubleshooting

**"command not found: gradle"**
→ Установите Gradle: `sudo pacman -S gradle`

**"SDK location not found"**
→ Создайте файл `local.properties`:
```
sdk.dir=/home/zahar/Android/Sdk
```

**"Could not resolve dependencies"**
→ Проверьте интернет-соединение, Gradle скачает зависимости при первой сборке
