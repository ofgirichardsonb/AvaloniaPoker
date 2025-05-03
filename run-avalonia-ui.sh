#!/bin/bash

# run-avalonia-ui.sh - Development script for running Avalonia UI with services
# This script runs the Avalonia UI in development mode, with services managed directly
# For standalone deployment, use the build-standalone.sh script

# Trap for proper cleanup on exit - super aggressive version
cleanup() {
    echo "Performing cleanup..."
    # First try gentle termination 
    if [ ! -z "$AVALONIA_PID" ]; then
        echo "Stopping Avalonia UI (PID: $AVALONIA_PID)..."
        kill $AVALONIA_PID 2>/dev/null || true
        
        # Give it a short time to exit cleanly
        sleep 0.5
        
        # If still running, use SIGKILL (cannot be caught or ignored)
        if kill -0 $AVALONIA_PID 2>/dev/null; then
            echo "Application still running - using SIGKILL..."
            kill -9 $AVALONIA_PID 2>/dev/null || true
        fi
    fi
    
    # Forcefully kill all dotnet processes related to our application
    echo "Terminating all related processes..."
    pkill -9 -f "PokerGame.Avalonia" 2>/dev/null || true
    pkill -9 -f "PokerGame.Core" 2>/dev/null || true
    pkill -9 -f "PokerGame.Foundation" 2>/dev/null || true
    
    # As a nuclear option, check if any netmq processes are hanging
    NETMQ_PIDS=$(ps aux | grep -i netmq | grep -v grep | awk '{print $2}')
    if [ ! -z "$NETMQ_PIDS" ]; then
        echo "Found potential hanging NetMQ processes, terminating..."
        for PID in $NETMQ_PIDS; do
            kill -9 $PID 2>/dev/null || true
        done
    fi
    
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
            <p><strong>Status:</strong> The Poker Game desktop application is currently running.</p>
            <p>This is a desktop application built with Avalonia UI for Windows and macOS.</p>
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

# Create a fifo for IPC to allow termination from another terminal
TERMINATION_FIFO="/tmp/poker_terminate_fifo"
[ -p "$TERMINATION_FIFO" ] || mkfifo "$TERMINATION_FIFO"

# Start background monitoring for termination commands
(
    while true; do
        if read -r command < "$TERMINATION_FIFO"; then
            if [ "$command" = "terminate" ]; then
                echo "Termination command received. Shutting down..."
                # Signal the main process
                kill -TERM $$
                break
            fi
        fi
    done
) &
MONITOR_PID=$!

# Create the termination helper script
TERM_SCRIPT="/tmp/poker-terminate.sh"
cat > "$TERM_SCRIPT" << 'EOF'
#!/bin/bash
echo "terminate" > /tmp/poker_terminate_fifo
echo "Termination command sent to Poker Game. Application should exit shortly."
EOF
chmod +x "$TERM_SCRIPT"

# Wait for the Avalonia UI process to exit
echo "Poker Game is running."
echo "Press Ctrl+C to exit OR run the following command from another terminal to cleanly terminate:"
echo "$ /tmp/poker-terminate.sh"

# Wait for the application to finish
wait $AVALONIA_PID

# Clean up the monitor process
kill $MONITOR_PID 2>/dev/null || true

# Remove the fifo and script
rm -f "$TERMINATION_FIFO" "$TERM_SCRIPT"

# Cleanup happens automatically through the trap handler