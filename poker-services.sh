#!/bin/bash

# Advanced script for managing poker game services using the new launcher
# This script uses the PokerGame.Launcher project to manage services more reliably

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
    
    # Prepare arguments array
    local cmdArgs=("$command")
    
    # Process additional arguments
    # For System.CommandLine beta4, we need to use a different strategy
    # Try using explicit flag=value syntax for each argument
    
    # Check for common command-line flags
    local portValue=""
    local useVerbose=false
    local useCurses=false
    local nextIsPort=false
    
    for arg in "$@"; do
        case "$arg" in
            --port-offset=*|-p=*)
                # Extract the port offset value
                portValue="${arg#*=}"
                ;;
            --port-offset|-p)
                # The next argument is the port value
                nextIsPort=true
                ;;
            [0-9]*)
                # If this is just a number, assume it's a port value
                if [ "$nextIsPort" = true ]; then
                    portValue="$arg"
                    nextIsPort=false
                elif [ -z "$portValue" ]; then
                    # If no port value yet, assume it's a port offset
                    portValue="$arg"
                fi
                ;;
            --verbose|-v)
                useVerbose=true
                ;;
            --curses|-c)
                useCurses=true
                ;;
        esac
    done
    
    # Set up the command arguments
    cmdArgs=("$command")
    
    # Add the port offset if provided
    if [ -n "$portValue" ]; then
        # Use separate arguments for flag and value with System.CommandLine
        cmdArgs+=("--port-offset" "$portValue")
    fi
    
    # Add other flags
    if [ "$useVerbose" = true ]; then
        cmdArgs+=("--verbose")
    fi
    
    if [ "$useCurses" = true ]; then
        cmdArgs+=("--curses")
    fi
    
    # Print the command to be executed
    echo "Executing: dotnet run --project PokerGame.Launcher/PokerGame.Launcher.csproj --no-build -- ${cmdArgs[*]}"
    
    # Run the launcher with the specified command and processed arguments
    dotnet run --project PokerGame.Launcher/PokerGame.Launcher.csproj --no-build -- "${cmdArgs[@]}"
    
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
        exit 1
        ;;
esac

echo "Done."