#!/bin/bash

# run-avalonia-ui.sh - Development script for running Avalonia UI with services
# This script runs the Avalonia UI in development mode, with services managed directly
# For standalone deployment, use the build-standalone.sh script

# Trap for proper cleanup on exit
cleanup() {
    echo "Performing cleanup..."
    # Kill any running dotnet processes started by this script
    if [ ! -z "$AVALONIA_PID" ]; then
        echo "Stopping Avalonia UI (PID: $AVALONIA_PID)..."
        kill $AVALONIA_PID 2>/dev/null || true
    fi
    
    # Final check for any remaining processes
    echo "Checking for any lingering processes..."
    pkill -f "PokerGame.Avalonia" 2>/dev/null || true
    
    echo "Cleanup complete."
    exit 0
}

# Set up trap to call cleanup function on script exit
trap cleanup EXIT INT TERM

echo "Starting Avalonia UI application..."
cd /home/runner/workspace

# Make sure we have the latest builds
echo "Building core projects..."
dotnet build MSA.Foundation/MSA.Foundation.csproj --configuration Debug
dotnet build PokerGame.Abstractions/PokerGame.Abstractions.csproj --configuration Debug
dotnet build PokerGame.Core/PokerGame.Core.csproj --configuration Debug
dotnet build PokerGame.Avalonia/PokerGame.Avalonia.csproj --configuration Debug

# Note: We no longer need to start services separately as the Avalonia app now handles this internally

# Run in regular mode - no headless configuration
# (previously had headless mode configuration here, removed as requested)

# This is a desktop application - no web server needed

# Start the Avalonia UI in the background
dotnet run --project PokerGame.Avalonia/PokerGame.Avalonia.csproj --configuration Debug &
AVALONIA_PID=$!

# Wait for the Avalonia UI process to exit
echo "Poker Game is running. Press Ctrl+C to exit."
wait $AVALONIA_PID

# Cleanup happens automatically through the trap handler