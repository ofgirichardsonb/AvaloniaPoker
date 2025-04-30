#!/bin/bash

# Set working directory
cd /home/runner/workspace

# Clean previous build artifacts
echo "Cleaning previous build artifacts..."
rm -f service_reg_test.dll

# Build the test
echo "Building service registration test..."
dotnet build MSA.Foundation/MSA.Foundation.csproj -c Debug
dotnet build PokerGame.Abstractions/PokerGame.Abstractions.csproj -c Debug
dotnet build PokerGame.Core/PokerGame.Core.csproj -c Debug

# Build the test executable
echo "Compiling test program..."
dotnet build /home/runner/workspace/MSA.Foundation/MSA.Foundation.csproj -c Debug
dotnet build /home/runner/workspace/PokerGame.Core/PokerGame.Core.csproj -c Debug

# Compile the test program
dotnet csc /reference:/home/runner/workspace/MSA.Foundation/bin/Debug/net8.0/MSA.Foundation.dll \
    /reference:/home/runner/workspace/PokerGame.Abstractions/bin/Debug/net8.0/PokerGame.Abstractions.dll \
    /reference:/home/runner/workspace/PokerGame.Core/bin/Debug/net8.0/PokerGame.Core.dll \
    /reference:System.dll \
    /reference:System.Core.dll \
    /reference:System.Data.dll \
    /reference:System.Net.Http.dll \
    /reference:System.Xml.dll \
    /reference:System.Xml.Linq.dll \
    /reference:netstandard.dll \
    /reference:/usr/share/dotnet/packs/Microsoft.NETCore.App.Ref/8.0.0/ref/net8.0/Microsoft.CSharp.dll \
    /reference:/usr/share/dotnet/packs/Microsoft.NETCore.App.Ref/8.0.0/ref/net8.0/System.Runtime.dll \
    /out:service_reg_test.dll \
    /target:exe \
    service_reg_test.cs

# Run the test
echo "Running service registration test..."
dotnet service_reg_test.dll