#!/bin/bash

# Script to run the message monitor diagnostic tool
echo "Building Message Monitor..."
dotnet build MessageMonitor.csproj

if [ $? -eq 0 ]; then
    echo "Running Message Monitor..."
    MONITOR_PID=""
    
    # Capture PID when starting
    dotnet run --project MessageMonitor.csproj &
    MONITOR_PID=$!
    echo "Message monitor started with PID: $MONITOR_PID"
    echo "Press Ctrl+C to stop monitoring."
    
    # Set up trap to kill the monitor on Ctrl+C
    trap "echo 'Stopping message monitor...'; kill $MONITOR_PID 2>/dev/null; exit 0" INT
    
    # Wait for the process to finish or for Ctrl+C
    wait $MONITOR_PID
else
    echo "Failed to build message monitor."
    exit 1
fi