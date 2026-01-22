@echo off
REM ScanFetch Build Script for Windows

echo ╔═══════════════════════════════════════════════════════╗
echo ║       Building ScanFetch for Windows x64              ║
echo ╚═══════════════════════════════════════════════════════╝
echo.

REM Очистка предыдущих сборок
echo [1/4] Cleaning previous builds...
dotnet clean ScanFetch.csproj -c Release

REM Восстановление зависимостей
echo.
echo [2/4] Restoring dependencies...
dotnet restore ScanFetch.csproj

REM Сборка Debug версии
echo.
echo [3/4] Building Debug version...
dotnet build ScanFetch.csproj -c Debug

REM Публикация Release версии для Windows
echo.
echo [4/4] Publishing Release version for Windows x64...
dotnet publish ScanFetch.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o publish\win-x64

echo.
echo ✓ Build completed!
echo.
echo Debug build: bin\Debug\net10.0\ScanFetch.exe
echo Release build: publish\win-x64\ScanFetch.exe
echo.
echo To run Debug version: .\bin\Debug\net10.0\ScanFetch.exe
echo To run Release version: .\publish\win-x64\ScanFetch.exe

pause
