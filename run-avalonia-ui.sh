#!/bin/bash

echo "Building Avalonia UI frontend..."
cd /home/runner/workspace

# Make sure we have the latest builds
echo "Building core projects..."
dotnet build MSA.Foundation/MSA.Foundation.csproj --configuration Debug
dotnet build PokerGame.Abstractions/PokerGame.Abstractions.csproj --configuration Debug
dotnet build PokerGame.Core/PokerGame.Core.csproj --configuration Debug
dotnet build PokerGame.Avalonia/PokerGame.Avalonia.csproj --configuration Debug

# First start the central broker and services
echo "Starting services..."
dotnet run --project PokerGame.Launcher/PokerGame.Launcher.csproj --configuration Debug -- start-services --port-offset 0 --verbose &
SERVICES_PID=$!

# Wait a moment for services to initialize
sleep 3

# Build and run the Avalonia UI
echo "Starting Avalonia UI..."
# Note: Replit's VNC support requires using port 5000 for access to the UI
export AVALONIA_HEADLESS_FRAMEBUFFER=true
export AVALONIA_HEADLESS=true
dotnet run --project PokerGame.Avalonia/PokerGame.Avalonia.csproj --configuration Debug

# Clean up background processes when this script exits
kill $SERVICES_PID
echo "Services shutdown complete."