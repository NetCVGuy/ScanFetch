#!/bin/bash

# ScanFetch Build Script for Linux (CachyOS)

echo "╔═══════════════════════════════════════════════════════╗"
echo "║       Building ScanFetch for Linux x64                ║"
echo "╚═══════════════════════════════════════════════════════╝"
echo ""

# Очистка предыдущих сборок
echo "[1/4] Cleaning previous builds..."
dotnet clean ScanFetch.csproj -c Release

# Восстановление зависимостей
echo ""
echo "[2/4] Restoring dependencies..."
dotnet restore ScanFetch.csproj

# Сборка Debug версии
echo ""
echo "[3/4] Building Debug version..."
dotnet build ScanFetch.csproj -c Debug

# Публикация Release версии для Linux
echo ""
echo "[4/4] Publishing Release version for Linux x64..."
dotnet publish ScanFetch.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o publish/linux-x64

echo ""
echo "✓ Build completed!"
echo ""
echo "Debug build: bin/Debug/net10.0/ScanFetch"
echo "Release build: publish/linux-x64/ScanFetch"
echo ""
echo "To run Debug version: ./bin/Debug/net10.0/ScanFetch"
echo "To run Release version: ./publish/linux-x64/ScanFetch"
