#!/bin/bash

# Simple script for managing poker game services using the new launcher
# This script uses the PokerGame.Launcher project which now has built-in service management
# Note: --enhanced-ui is deprecated; use --curses instead

# Function to build the launcher if needed
build_launcher() {
    echo "Building PokerGame.Launcher..."
    dotnet build PokerGame.Launcher/PokerGame.Launcher.csproj --configuration Release
    
    if [ $? -ne 0 ]; then
        echo "Error building launcher"
        exit 1
    fi
    
    echo "Launcher built successfully"
}

# Function to run the launcher with the given arguments
run_launcher() {
    local command="$1"
    shift  # Remove command from arguments
    
    # Build the launcher first
    build_launcher
    
    # Process additional arguments
    # Pass them directly to the launcher
    local args=("$command")
    
    # Process and map arguments
    for arg in "$@"; do
        # Map deprecated --enhanced-ui to --curses
        if [ "$arg" == "--enhanced-ui" ]; then
            echo "Note: --enhanced-ui is deprecated, using --curses instead"
            args+=("--curses")
        else
            args+=("$arg")
        fi
    done
    
    # Print the command to be executed
    echo "Executing: dotnet run --project PokerGame.Launcher/PokerGame.Launcher.csproj --no-build -- ${args[*]}"
    
    # Run the launcher with the specified command and processed arguments
    dotnet run --project PokerGame.Launcher/PokerGame.Launcher.csproj --no-build -- "${args[@]}"
    
    return $?
}

# Main script execution based on arguments
case "$1" in
    start)
        # Start all services
        shift
        run_launcher start-all "$@"
        ;;
    start-services)
        # Start only the services host
        shift
        run_launcher start-services "$@"
        ;;
    start-client)
        # Start only the console client
        shift
        run_launcher start-client "$@"
        ;;
    stop)
        # Stop all services
        run_launcher stop-all
        ;;
    stop-services)
        # Stop the services host
        run_launcher stop-services
        ;;
    stop-client)
        # Stop the console client
        run_launcher stop-client
        ;;
    status)
        # Show the status of all services
        run_launcher status
        ;;
    curses)
        # Start the console client with curses UI
        shift
        run_launcher start-client -c "$@"
        ;;
    services-and-curses)
        # Start both services and curses UI
        shift
        run_launcher start-all -c "$@"
        ;;
    *)
        echo "Usage: $0 {start|start-services|start-client|stop|stop-services|stop-client|status|curses|services-and-curses} [options]"
        echo ""
        echo "Commands:"
        echo "  start               - Start all services (services host and console client)"
        echo "  start-services      - Start only the services host"
        echo "  start-client        - Start only the console client"
        echo "  stop                - Stop all running services"
        echo "  stop-services       - Stop the services host"
        echo "  stop-client         - Stop the console client"
        echo "  status              - Show the status of all services"
        echo "  curses              - Start the console client with curses UI"
        echo "  services-and-curses - Start both services and curses UI"
        echo ""
        echo "Options:"
        echo "  --port-offset=N, -p N - Use port offset N for the services"
        echo "  --verbose, -v         - Enable verbose logging"
        echo "  --curses, -c          - Use enhanced UI (curses) for console client"
        echo "  --enhanced-ui         - Deprecated: Use --curses instead"
        exit 1
        ;;
esac

echo "Done."