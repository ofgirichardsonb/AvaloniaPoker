#!/bin/bash

echo "Building StartHandTestMinimal..."
cd /home/runner/workspace
dotnet build StartHandTestMinimal/StartHandTestMinimal.csproj -c Debug

echo "Running test..."
cd /home/runner/workspace
dotnet run --project StartHandTestMinimal/StartHandTestMinimal.csproj --no-build