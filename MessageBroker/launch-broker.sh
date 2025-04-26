#!/bin/bash

# Build the MessageBroker project
echo "Building MessageBroker project..."
dotnet build

# Set default ports
FRONTEND_PORT=5570
BACKEND_PORT=5571
MONITOR_PORT=5572

# Check for command line arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --frontend-port)
      FRONTEND_PORT="$2"
      shift 2
      ;;
    --backend-port)
      BACKEND_PORT="$2"
      shift 2
      ;;
    --monitor-port)
      MONITOR_PORT="$2"
      shift 2
      ;;
    *)
      echo "Unknown option: $1"
      exit 1
      ;;
  esac
done

# Start the broker
echo "Starting MessageBroker on ports: frontend=$FRONTEND_PORT, backend=$BACKEND_PORT, monitor=$MONITOR_PORT"
dotnet run --project MessageBroker.csproj --property:StartupObject=MessageBroker.ProgramLauncher -- --frontend-port $FRONTEND_PORT --backend-port $BACKEND_PORT --monitor-port $MONITOR_PORT