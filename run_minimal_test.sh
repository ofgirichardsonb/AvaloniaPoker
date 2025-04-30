#!/bin/bash

# Set working directory
cd /home/runner/workspace

# Clean previous build artifacts
echo "Cleaning previous build artifacts..."
rm -f minimal_test.dll

# Compile the test program
echo "Compiling minimal test program..."
dotnet csc /reference:/home/runner/workspace/MSA.Foundation/bin/Debug/net8.0/MSA.Foundation.dll \
    /reference:System.dll \
    /reference:System.Core.dll \
    /reference:netstandard.dll \
    /reference:/usr/share/dotnet/packs/Microsoft.NETCore.App.Ref/8.0.0/ref/net8.0/Microsoft.CSharp.dll \
    /reference:/usr/share/dotnet/packs/Microsoft.NETCore.App.Ref/8.0.0/ref/net8.0/System.Runtime.dll \
    /out:minimal_test.dll \
    /target:exe \
    minimal_service_reg_test.cs

# Run the test
echo "Running minimal test..."
dotnet minimal_test.dll