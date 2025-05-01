#!/bin/bash

echo "Starting web server for Avalonia UI..."
cd /home/runner/workspace

# Build the Avalonia application for browser
echo "Building Avalonia UI for browser..."
dotnet publish PokerGame.Avalonia/PokerGame.Avalonia.csproj -c Debug -r browser-wasm --self-contained

# Start the services in the background
echo "Starting backend services..."
dotnet run --project PokerGame.Launcher/PokerGame.Launcher.csproj --configuration Debug -- start-services --port-offset 0 --verbose &
SERVICES_PID=$!

# Give services time to start
sleep 3

# Set up the web server for Avalonia UI
echo "Setting up web server for Avalonia UI..."
cd /home/runner/workspace/PokerGame.Avalonia/bin/Debug/net8.0/browser-wasm/AppBundle
python3 -m http.server 5000 &
HTTP_SERVER_PID=$!

echo "Avalonia UI web server running at http://localhost:5000"
echo "Press Ctrl+C to stop all services"

# Wait for Ctrl+C
trap "echo 'Shutting down...'; kill $SERVICES_PID; kill $HTTP_SERVER_PID; exit 0" INT TERM

# Keep the script running
while true; do
    sleep 1
done