#!/bin/bash
# Build just what we need for the minimal message flow test

echo "Building core components in Release mode..."
cd /home/runner/workspace
dotnet build MSA.Foundation/MSA.Foundation.csproj -c Release
dotnet build PokerGame.Abstractions/PokerGame.Abstractions.csproj -c Release
dotnet build PokerGame.Core/PokerGame.Core.csproj -c Release

echo "Building minimal message flow test..."
cd /home/runner/workspace/MessageFlowTest
dotnet build -c Release

echo "Running minimal message flow test..."
dotnet run -c Release