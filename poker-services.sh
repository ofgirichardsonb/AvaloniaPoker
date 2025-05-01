#!/bin/bash

# Simple script for managing poker game services using the new launcher
# This script uses the PokerGame.Launcher project which now has built-in service management
# Note: --enhanced-ui is deprecated; use --curses instead

# Global variables to track PIDs
LAUNCHER_PID=""
CHILD_PIDS=()

# Function to cleanup all child processes
cleanup() {
    echo "Cleaning up processes..."
    
    # First, try to use the launcher's stop command if available
    if [[ "$1" == "start-all" || "$1" == "start-services" ]]; then
        echo "Attempting graceful shutdown via launcher..."
        dotnet run --project PokerGame.Launcher/PokerGame.Launcher.csproj --configuration Release -- stop-all
    fi
    
    # Then kill the launcher process if it's still running
    if [[ ! -z "$LAUNCHER_PID" && -e /proc/$LAUNCHER_PID ]]; then
        echo "Terminating launcher process: $LAUNCHER_PID"
        kill -TERM $LAUNCHER_PID 2>/dev/null || true
        sleep 1
        kill -KILL $LAUNCHER_PID 2>/dev/null || true
    fi
    
    # Kill any remaining child processes we've tracked
    for pid in "${CHILD_PIDS[@]}"; do
        if [[ -e /proc/$pid ]]; then
            echo "Terminating child process: $pid"
            kill -TERM $pid 2>/dev/null || true
            sleep 1
            kill -KILL $pid 2>/dev/null || true
        fi
    done
    
    # Final check for any leftover dotnet processes we might have started
    echo "Checking for any remaining dotnet processes that might belong to this app..."
    pkill -f "dotnet.*PokerGame" || true
    
    echo "Cleanup complete."
}

# Set up signal handlers
trap 'cleanup $COMMAND; exit 130' INT
trap 'cleanup $COMMAND; exit 143' TERM
trap 'cleanup $COMMAND; exit 0' EXIT

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
    COMMAND="$1"  # Set global command for trap handlers
    local command="$1"
    shift  # Remove command from arguments
    
    # Build the launcher first
    build_launcher
    
    # Process additional arguments
    # Pass them directly to the launcher
    local args=("$command")
    
    # Process and map arguments
    for arg in "$@"; do
        # Skip UI-related options
        if [ "$arg" == "--enhanced-ui" ] || [ "$arg" == "--curses" ]; then
            echo "Note: UI options are being phased out, focusing on service layer only"
            # Skip the argument, don't add to args
        else
            args+=("$arg")
        fi
    done
    
    # Print the command to be executed
    echo "Executing: dotnet run --project PokerGame.Launcher/PokerGame.Launcher.csproj --configuration Release --no-build -- ${args[*]}"
    
    # Run the launcher with the specified command and processed arguments in background
    dotnet run --project PokerGame.Launcher/PokerGame.Launcher.csproj --configuration Release --no-build -- "${args[@]}" &
    LAUNCHER_PID=$!
    
    echo "Launcher started with PID: $LAUNCHER_PID"
    
    # Wait for the launcher to finish if it's a one-time command
    if [[ "$command" == "status" || "$command" == "stop"* ]]; then
        wait $LAUNCHER_PID
        return $?
    fi
    
    # Otherwise, for long-running commands, find child processes and register them
    sleep 2 # Give it a moment to start child processes
    
    # Find child processes started by our launcher
    mapfile -t NEW_CHILD_PIDS < <(pgrep -P $LAUNCHER_PID)
    for pid in "${NEW_CHILD_PIDS[@]}"; do
        CHILD_PIDS+=($pid)
        echo "Registered child process: $pid"
    done
    
    # If this is a long-running command, wait for the launcher process
    if [[ "$command" == "start"* ]]; then
        echo "Services started. Press Ctrl+C to stop."
        wait $LAUNCHER_PID
        return $?
    fi
    
    return 0
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