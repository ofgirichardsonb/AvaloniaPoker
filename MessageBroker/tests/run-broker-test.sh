#!/bin/bash

# Change to the project directory
cd "$(dirname "$0")/.."

# Make sure we have the necessary build files
if [ ! -f "MessageBroker.csproj" ]; then
    echo "Error: MessageBroker.csproj not found"
    exit 1
fi

# Compile the project in Debug mode
echo "Compiling project..."
dotnet build -c Debug

# Create a test project file if it doesn't exist
if [ ! -f "tests/BrokerManagerTest.csproj" ]; then
    echo "Creating test project file..."
    cat > tests/BrokerManagerTest.csproj << EOF
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <RootNamespace>MessageBroker.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\MessageBroker.csproj" />
  </ItemGroup>

</Project>
EOF
fi

# Run the test
echo "Running broker manager test..."
dotnet run --project tests/BrokerManagerTest.csproj