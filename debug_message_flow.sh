#!/bin/bash
# Simple script to run the improved StartHand message flow test

echo "Building core components for message flow test..."
cd /home/runner/workspace
dotnet build MSA.Foundation/MSA.Foundation.csproj
dotnet build PokerGame.Abstractions/PokerGame.Abstractions.csproj
dotnet build PokerGame.Core/PokerGame.Core.csproj

echo "Creating a temporary project for the message flow test..."
mkdir -p /tmp/message_flow_test
cd /tmp/message_flow_test

# Create simple project file
cat > MessageFlowTest.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="MSA.Foundation">
      <HintPath>/home/runner/workspace/MSA.Foundation/bin/Debug/net8.0/MSA.Foundation.dll</HintPath>
    </Reference>
    <Reference Include="PokerGame.Abstractions">
      <HintPath>/home/runner/workspace/PokerGame.Abstractions/bin/Debug/net8.0/PokerGame.Abstractions.dll</HintPath>
    </Reference>
    <Reference Include="PokerGame.Core">
      <HintPath>/home/runner/workspace/PokerGame.Core/bin/Debug/net8.0/PokerGame.Core.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
EOF

# Copy the test file
cp /home/runner/workspace/improved_starthand_test.cs Program.cs

echo "Building and running the message flow test..."
dotnet build
dotnet run | tee /home/runner/workspace/starthand_test.log

echo "Test completed. Results are in starthand_test.log"