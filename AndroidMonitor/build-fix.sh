#!/usr/bin/env bash

# Quick Android build fix script for Java 25+ systems

echo "=== ScanFetch Android Build Fix ==="

# Check if Java 17 is installed
if [ ! -d "/usr/lib/jvm/java-17-openjdk" ]; then
    echo "Java 17 not found. Installing..."
    sudo pacman -S jdk17-openjdk --noconfirm
fi

# Update gradle.properties to use Java 17
cd "$(dirname "$0")"
if ! grep -q "org.gradle.java.home" gradle.properties; then
    echo "org.gradle.java.home=/usr/lib/jvm/java-17-openjdk" >> gradle.properties
    echo "✓ Updated gradle.properties"
fi

# Build with Java 17
echo "Building APK with Java 17..."
export JAVA_HOME=/usr/lib/jvm/java-17-openjdk
bash gradlew assembleDebug

if [ $? -eq 0 ]; then
    echo ""
    echo "✓ Build successful!"
    echo "APK location: app/build/outputs/apk/debug/app-debug.apk"
    echo ""
    echo "To install on device:"
    echo "  adb install app/build/outputs/apk/debug/app-debug.apk"
else
    echo ""
    echo "✗ Build failed!"
    echo ""
    echo "Alternative: Use Android Studio"
    echo "  1. Open Android Studio"
    echo "  2. Open project: $(pwd)"
    echo "  3. Build → Build APK"
fi
