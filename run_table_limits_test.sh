#!/bin/bash

# run_table_limits_test.sh - Test script for table limits implementation

echo "Building required projects..."
dotnet build MSA.Foundation/MSA.Foundation.csproj -c Debug
dotnet build PokerGame.Abstractions/PokerGame.Abstractions.csproj -c Debug
dotnet build PokerGame.Core/PokerGame.Core.csproj -c Debug

echo "Running table limits test..."
dotnet run --project PokerGame.Core/PokerGame.Core.csproj -c Debug /home/runner/workspace/test_table_limits.cs

echo "Test complete."