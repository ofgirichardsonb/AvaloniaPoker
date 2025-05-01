#!/bin/bash

cd /home/runner/workspace

echo "Building Message Monitor..."
dotnet build MessageMonitor.csproj

echo "Running Message Monitor..."
dotnet run --project MessageMonitor.csproj