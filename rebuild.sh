#!/bin/bash

# Clean the solution
echo "Cleaning solution..."
dotnet clean PokerGame.sln

# Restore packages
echo "Restoring packages..."
dotnet restore PokerGame.sln

# Build the solution
echo "Building solution..."
dotnet build PokerGame.sln

echo "Build completed!"