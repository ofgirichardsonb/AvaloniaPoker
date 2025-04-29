#!/bin/bash
# Run basic message test

# Create a dedicated directory for our test
mkdir -p /home/runner/workspace/BasicMessageTest
cd /home/runner/workspace/BasicMessageTest

# Create a project file
cat > BasicMessageTest.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../PokerGame.Core/PokerGame.Core.csproj" />
  </ItemGroup>
</Project>
EOF

# Copy the test program
cp /home/runner/workspace/basic_message_test.cs Program.cs

# Build and run the test
echo "Building basic message test..."
dotnet build -c Release

echo "Running basic message test..."
dotnet run -c Release