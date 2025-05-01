#!/bin/bash

cd /home/runner/workspace

# Variables to track PIDs
MONITOR_PID=""

# Function to cleanup processes
cleanup() {
    echo "Cleaning up processes..."
    
    # Kill the monitor process if it's still running
    if [[ ! -z "$MONITOR_PID" && -e /proc/$MONITOR_PID ]]; then
        echo "Terminating message monitor process: $MONITOR_PID"
        kill -TERM $MONITOR_PID 2>/dev/null || true
        sleep 1
        kill -KILL $MONITOR_PID 2>/dev/null || true
    fi
    
    # Final check for any leftover processes
    echo "Checking for any remaining dotnet processes related to message monitor..."
    pkill -f "dotnet.*MessageMonitor" || true
    
    echo "Cleanup complete."
}

# Set up signal handlers
trap 'cleanup; exit 130' INT
trap 'cleanup; exit 143' TERM
trap 'cleanup; exit 0' EXIT

echo "Building Message Monitor..."
dotnet build MessageMonitor.csproj

if [ $? -ne 0 ]; then
    echo "The build failed. Fix the build errors and run again."
    exit 1
fi

echo "Running Message Monitor..."
dotnet run --project MessageMonitor.csproj &
MONITOR_PID=$!

echo "Message monitor started with PID: $MONITOR_PID"
echo "Press Ctrl+C to stop monitoring."

# Wait for the process to finish
wait $MONITOR_PID