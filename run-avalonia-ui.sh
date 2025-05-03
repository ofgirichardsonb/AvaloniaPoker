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

# Configure environment for headless mode (no GUI in Replit environment)
# Note: In a real desktop environment, this would launch the graphical interface
export AVALONIA_HEADLESS=true
export AVALONIA_HEADLESS_FRAMEBUFFER=true

# Setup a simple status file for Replit to show we're running
mkdir -p /tmp/poker-ui
cat > /tmp/poker-ui/index.html << EOT
<!DOCTYPE html>
<html>
<head>
    <title>Poker Game Running</title>
    <style>
        body { font-family: Arial, sans-serif; background: #f5f5f5; margin: 0; padding: 20px; }
        .container { max-width: 800px; margin: 0 auto; background: white; padding: 20px; border-radius: 5px; box-shadow: 0 2px 5px rgba(0,0,0,0.1); }
        h1 { color: #333; margin-top: 0; }
        .status { background: #e8f5e9; border-left: 4px solid #4caf50; padding: 10px 15px; margin: 20px 0; }
        .command { background: #f5f5f5; padding: 10px; border-radius: 4px; font-family: monospace; margin: 10px 0; }
    </style>
</head>
<body>
    <div class="container">
        <h1>Poker Game UI Status</h1>
        <div class="status">
            <p><strong>Status:</strong> The Poker Game desktop application is currently running in headless mode.</p>
            <p>This is a desktop application built with Avalonia UI for Windows and macOS, so the actual interface is not visible in the Replit environment.</p>
        </div>
        <p>The services architecture is running in the background with the following components:</p>
        <ul>
            <li>Central Message Broker</li>
            <li>Game Engine Service</li>
            <li>Card Deck Service</li>
            <li>Lobby Service</li>
        </ul>
        <p>Check the console output for application logs and events.</p>
        <h2>Deployment</h2>
        <p>To create a standalone executable for distribution:</p>
        <div class="command">./build-standalone.sh</div>
        <p>This will create a self-contained executable in the ./publish directory that can be run without the dotnet runtime.</p>
    </div>
</body>
</html>
EOT

# Start the Avalonia UI in the background
dotnet run --project PokerGame.Avalonia/PokerGame.Avalonia.csproj --configuration Debug &
AVALONIA_PID=$!

# Wait for the Avalonia UI process to exit
echo "Poker Game is running. Press Ctrl+C to exit."
wait $AVALONIA_PID

# Cleanup happens automatically through the trap handler