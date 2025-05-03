#!/bin/bash

# PokerGame standalone executable build script
# This script builds a self-contained executable for the current platform

echo "PokerGame Self-Contained Executable Builder"
echo "==========================================="

# Detect platform
PLATFORM="unknown"
if [[ "$OSTYPE" == "darwin"* ]]; then
    PLATFORM="osx-x64"
    echo "Detected macOS platform"
elif [[ "$OSTYPE" == "msys" || "$OSTYPE" == "win32" || "$OSTYPE" == "cygwin" ]]; then
    PLATFORM="win-x64"
    echo "Detected Windows platform"
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    # We officially only support Windows and macOS, but we'll allow Linux builds for testing
    PLATFORM="linux-x64"
    echo "Detected Linux platform (unofficial support)"
else
    echo "Unsupported platform: $OSTYPE"
    exit 1
fi

# Set build configuration
CONFIG="Release"
OUTPUT_DIR="./publish"

echo "Building for platform: $PLATFORM"
echo "Configuration: $CONFIG"
echo "Output directory: $OUTPUT_DIR"

# Clean previous build
echo "Cleaning previous build..."
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Build the foundation libraries first
echo "Building foundation libraries..."
dotnet build MSA.Foundation/MSA.Foundation.csproj -c "$CONFIG" || exit 1
dotnet build PokerGame.Abstractions/PokerGame.Abstractions.csproj -c "$CONFIG" || exit 1
dotnet build PokerGame.Core/PokerGame.Core.csproj -c "$CONFIG" || exit 1

# Build and publish self-contained application
echo "Building and publishing self-contained application..."
dotnet publish PokerGame.Avalonia/PokerGame.Avalonia.csproj \
    -c "$CONFIG" \
    -r "$PLATFORM" \
    --self-contained true \
    -o "$OUTPUT_DIR" \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true || exit 1

echo "Build completed successfully!"
echo "Executable can be found in: $OUTPUT_DIR"

# Create platform-specific instructions
if [[ "$PLATFORM" == "osx-x64" ]]; then
    echo ""
    echo "macOS Instructions:"
    echo "1. Open Terminal and navigate to the '$OUTPUT_DIR' directory"
    echo "2. Run 'chmod +x PokerGame'"
    echo "3. Run './PokerGame' to start the application"
    echo ""
    echo "Note: If you get security warnings, go to System Preferences > Security & Privacy"
    echo "and click 'Open Anyway'"
elif [[ "$PLATFORM" == "win-x64" ]]; then
    echo ""
    echo "Windows Instructions:"
    echo "1. Navigate to the '$OUTPUT_DIR' directory"
    echo "2. Double-click 'PokerGame.exe' to run the application"
fi

echo "Thank you for using PokerGame!"