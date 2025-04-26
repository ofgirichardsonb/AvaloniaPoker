#!/bin/bash

# Build the MessageBroker project
echo "Building MessageBroker project..."
dotnet build

# Set default client parameters
CLIENT_NAME="TestClient"
CLIENT_TYPE="Test"
BROKER_ADDRESS="localhost"
BROKER_PORT=5570

# Check for command line arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --name)
      CLIENT_NAME="$2"
      shift 2
      ;;
    --type)
      CLIENT_TYPE="$2"
      shift 2
      ;;
    --broker)
      BROKER_ADDRESS="$2"
      shift 2
      ;;
    --port)
      BROKER_PORT="$2"
      shift 2
      ;;
    *)
      echo "Unknown option: $1"
      exit 1
      ;;
  esac
done

# Start the test client
echo "Starting test client: name=$CLIENT_NAME, type=$CLIENT_TYPE, broker=$BROKER_ADDRESS:$BROKER_PORT"
dotnet run --project MessageBroker.csproj --property:StartupObject=MessageBroker.TestClient -- --name "$CLIENT_NAME" --type "$CLIENT_TYPE" --broker "$BROKER_ADDRESS" --port "$BROKER_PORT"