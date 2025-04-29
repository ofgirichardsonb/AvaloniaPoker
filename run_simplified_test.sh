#!/bin/bash
# Run simplified StartHand message flow test

set -e

echo "Building core components in Release mode..."
cd /home/runner/workspace
dotnet build MSA.Foundation/MSA.Foundation.csproj -c Release
dotnet build PokerGame.Abstractions/PokerGame.Abstractions.csproj -c Release
dotnet build PokerGame.Core/PokerGame.Core.csproj -c Release

echo "Creating simplified test project..."
mkdir -p SimplifiedTest
cd SimplifiedTest

# Create a project file for the test
cat > SimplifiedTest.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../PokerGame.Core/PokerGame.Core.csproj" />
    <ProjectReference Include="../PokerGame.Abstractions/PokerGame.Abstractions.csproj" />
    <ProjectReference Include="../MSA.Foundation/MSA.Foundation.csproj" />
  </ItemGroup>
</Project>
EOF

# Copy the test file
cp ../simplified_starthand_test.cs Program.cs

echo "Building simplified test..."
dotnet build -c Release

echo "Running simplified test..."
dotnet run -c Release | tee simplified_test.log

echo "Test completed. Results are in simplified_test.log"