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
    local args=""
    local port_offset=""
    local verbose=""
    local curses=""
    local enhanced_ui=""
    
    # Process additional arguments
    for arg in "$@"; do
        case "$arg" in
            --port-offset=*)
                # Extract the port offset value for the format --port-offset=1234
                port_offset="--port-offset ${arg#*=}"
                ;;
            --port-offset)
                # Skip this argument, but capture the next one as --port-offset VALUE
                port_offset="--port-offset"
                ;;
            -p=*)
                # Extract the port offset value for the format -p=1234
                port_offset="--port-offset ${arg#*=}"
                ;;
            -p)
                # Skip this argument, but next one will be the value
                port_offset="--port-offset"
                ;;
            [0-9]*)
                # If the previous argument was -p or --port-offset and this is a number
                if [[ "$port_offset" == "--port-offset" ]]; then
                    port_offset="--port-offset $arg"
                elif [[ -z "$port_offset" ]]; then
                    # If no port_offset flag yet, assume this is the port offset value after the -p flag
                    port_offset="--port-offset $arg"
                fi
                ;;
            --verbose)
                verbose="--verbose"
                ;;
            -v)
                verbose="--verbose"
                ;;
            --curses)
                curses="--curses"
                ;;
            -c)
                curses="--curses"
                ;;
            --enhanced-ui)
                enhanced_ui="--enhanced-ui"
                ;;
            -e)
                enhanced_ui="--enhanced-ui"
                ;;
            *)
                # Any other arguments are passed as-is
                args="$args $arg"
                ;;
        esac
    done
    
    # Build the launcher first
    build_launcher
    
    # Combine all the arguments
    final_args="$command $port_offset $verbose $curses $enhanced_ui $args"
    
    # Print the command to be executed
    echo "Executing: dotnet run --project PokerGame.Launcher/PokerGame.Launcher.csproj --no-build -- $final_args"
    
    # Run the launcher with the specified command and processed arguments
    dotnet run --project PokerGame.Launcher/PokerGame.Launcher.csproj --no-build -- $final_args
    
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
        echo "  --curses, -c          - Use curses UI for console client"
        echo "  --enhanced-ui, -e     - Use enhanced UI for console client (alternative to curses)"
        exit 1
        ;;
esac

echo "Done."