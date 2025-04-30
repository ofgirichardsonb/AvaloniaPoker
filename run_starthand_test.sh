#!/bin/bash

echo "Building StartHandTest project..."
cd /home/runner/workspace
dotnet build StartHandTest/StartHandTest.csproj

echo "Running StartHandTest..."
cd /home/runner/workspace
dotnet run --project StartHandTest/StartHandTest.csproj