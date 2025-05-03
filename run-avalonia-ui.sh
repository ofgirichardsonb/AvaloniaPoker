#!/bin/bash

# Trap for proper cleanup on exit
cleanup() {
    echo "Performing cleanup..."
    # Kill any running dotnet processes started by this script
    if [ ! -z "$SERVICES_PID" ]; then
        echo "Stopping services (PID: $SERVICES_PID)..."
        kill $SERVICES_PID 2>/dev/null || true
    fi
    
    if [ ! -z "$AVALONIA_PID" ]; then
        echo "Stopping Avalonia UI (PID: $AVALONIA_PID)..."
        kill $AVALONIA_PID 2>/dev/null || true
    fi
    
    # Run the launcher with stop-all command to ensure all processes are shutdown
    echo "Executing stop-all command through launcher..."
    dotnet run --project PokerGame.Launcher/PokerGame.Launcher.csproj --configuration Debug -- stop-all 2>/dev/null || true
    
    # Final check for any remaining processes
    echo "Checking for any lingering processes..."
    pkill -f "PokerGame.Launcher" 2>/dev/null || true
    pkill -f "PokerGame.Services" 2>/dev/null || true
    
    echo "Cleanup complete."
    exit 0
}

# Set up trap to call cleanup function on script exit
trap cleanup EXIT INT TERM

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
# Note: These environment variables enable Avalonia UI to run in a headless environment like Replit
export AVALONIA_HEADLESS=true
export AVALONIA_HEADLESS_FRAMEBUFFER=true

# Run the Avalonia UI in headless mode
echo "Running Avalonia UI in headless mode (Replit environment)..."
echo "Note: In a real desktop environment, this would launch the graphical interface."

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
    </div>
</body>
</html>
EOT

# Start the Avalonia UI in the background
dotnet run --project PokerGame.Avalonia/PokerGame.Avalonia.csproj --configuration Debug &
AVALONIA_PID=$!

# Note: We've removed the HTTP server to avoid port conflicts

# Wait for the Avalonia UI process to exit
echo "Services running. Press Ctrl+C to exit..."
wait $AVALONIA_PID

# Cleanup happens automatically through the trap handler