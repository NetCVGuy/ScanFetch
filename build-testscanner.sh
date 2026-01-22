#!/bin/bash

# TestScanner Build Script for Linux

echo "╔═══════════════════════════════════════════════════════╗"
echo "║       Building TestScanner for Linux x64              ║"
echo "╚═══════════════════════════════════════════════════════╝"
echo ""

cd TestScanner

# Очистка предыдущих сборок
echo "[1/3] Cleaning previous builds..."
dotnet clean TestScanner.csproj -c Release

# Восстановление зависимостей
echo ""
echo "[2/3] Restoring dependencies..."
dotnet restore TestScanner.csproj

# Сборка Debug версии
echo ""
echo "[3/3] Building Debug version..."
dotnet build TestScanner.csproj -c Debug

cd ..

echo ""
echo "✓ Build completed!"
echo ""
echo "To run: dotnet run --project TestScanner/TestScanner.csproj"
echo "Or: ./TestScanner/bin/Debug/net10.0/TestScanner"
