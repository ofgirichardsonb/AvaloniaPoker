#!/bin/bash

# Script to launch the PokerGame.Services microservice host

# Function to display usage information
display_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --port-offset VALUE   Set port offset for all services (default: 0)"
    echo "  --verbose             Enable verbose logging"
    echo "  --game-engine         Run the game engine service"
    echo "  --card-deck           Run the card deck service"
    echo "  --all-services        Run all services"
    echo ""
    echo "Examples:"
    echo "  $0 --all-services                  Run all services with default settings"
    echo "  $0 --game-engine --card-deck       Run game engine and card deck services"
    echo "  $0 --game-engine --port-offset 500 Run game engine with port offset 500"
}

# Check if help is requested
if [[ "$1" == "--help" || "$1" == "-h" ]]; then
    display_usage
    exit 0
fi

# Check if no arguments are provided
if [ $# -eq 0 ]; then
    echo "Error: No arguments provided."
    display_usage
    exit 1
fi

# Build the project if needed
echo "Building PokerGame.Services..."
dotnet build PokerGame.Services/PokerGame.Services.csproj

# Check if build was successful
if [ $? -ne 0 ]; then
    echo "Build failed. Please fix the errors and try again."
    exit 1
fi

# Run the services host with the provided arguments
echo "Starting PokerGame.Services host with arguments: $@"
echo "----------------------------------------"
dotnet run --project PokerGame.Services/PokerGame.Services.csproj -- "$@"