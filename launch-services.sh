#!/bin/bash

# DEPRECATED: This script is maintained for backward compatibility
# Please use the new poker-services.sh script for improved process management
# This script follows the new services-first architecture where:
# 1. Services are launched separately in PokerGame.Services
# 2. The console UI is a client that connects to those services

echo "WARNING: This script is deprecated. Please use poker-services.sh instead."
echo "For equivalent functionality, use: poker-services.sh <command> [options]"
echo "Continuing with legacy script for compatibility..."
echo ""

CONSOLE_PORT_OFFSET=10 # Make sure each instance gets separate ports

# Function to launch a service host
launch_service_host() {
    local port_offset=$1
    local args=$2
    
    echo "Launching service host with port offset $port_offset..."
    
    # Launch the service host in the background
    dotnet run --project PokerGame.Services/PokerGame.Services.csproj -- \
        --all-services \
        --port-offset=$port_offset \
        $args &
    
    # Store the PID
    local pid=$!
    echo "Service host launched with PID $pid"
    echo $pid > ".services.pid"
    
    # Give it a moment to start up
    sleep 3
    
    echo "Services host should now be running. Check logs for details."
}

# Function to launch a UI client
launch_ui_client() {
    local service_type=$1
    local port_offset=$2
    local extra_args=$3
    
    echo "Launching $service_type client with port offset $port_offset..."
    
    # Launch the UI client in the background
    dotnet run --project PokerGame.Console/PokerGame.Console.csproj -- \
        --microservices \
        --service-type=$service_type \
        --port-offset=$port_offset \
        $extra_args &
    
    # Store the PID
    local pid=$!
    echo "$service_type client launched with PID $pid"
    echo $pid > ".$service_type.pid"
    
    # Give it a moment to start up
    sleep 2
}

# Function to stop a service
stop_service() {
    local service_type=$1
    
    if [ -f ".$service_type.pid" ]; then
        local pid=$(cat ".$service_type.pid")
        echo "Stopping $service_type service (PID $pid)..."
        kill $pid 2>/dev/null
        rm ".$service_type.pid"
    else
        echo "$service_type service not running"
    fi
}

# Stop the services host
stop_services_host() {
    if [ -f ".services.pid" ]; then
        local pid=$(cat ".services.pid")
        echo "Stopping services host (PID $pid)..."
        kill $pid 2>/dev/null
        rm ".services.pid"
    else
        echo "Services host not running"
    fi
}

# Stop any running services and clients
stop_all_services() {
    echo "Stopping all services and clients..."
    stop_service "consoleui"
    stop_services_host
}

# Start the services in the correct order with proper delays
start_all_services() {
    echo "Starting all services..."
    
    # First, stop any running services
    stop_all_services
    
    # Clear any existing PID files
    rm -f .gameengine.pid .carddeck.pid .consoleui.pid
    
    # Create a log file
    log_file="poker_standard.log"
    echo "Starting poker game in standard mode at $(date)" > $log_file
    
    # Generate a random port offset to avoid conflicts (between 100-999)
    random_offset=$((RANDOM % 900 + 100))
    
    # Use a single unified process for all services
    echo "Starting all services in a single command for better coordination..."
    echo "Using port offset $random_offset to avoid conflicts"
    echo "Game logs will be written to $log_file"
    
    # Run the process in the foreground
    echo ""
    echo "████████████████████████████████████████████████████████████"
    echo "█                                                          █"
    echo "█  TEXAS HOLD'EM POKER GAME (STANDARD MODE)                █"
    echo "█                                                          █"
    echo "█  Press Ctrl+C to stop the game when you're finished      █"
    echo "█                                                          █"
    echo "████████████████████████████████████████████████████████████"
    echo ""
    
    # Execute the command in the foreground with random port offset
    dotnet run --project PokerGame.Console/PokerGame.Console.csproj -- --microservices --enhanced-ui --port-offset=$random_offset
    
    # No background process, no PID tracking in this mode
    echo "Game has exited. Thanks for playing!"
}

# Start in verbose mode with more debugging information
start_verbose() {
    echo "Starting all services in verbose mode..."
    
    # First, stop any running services
    stop_all_services
    
    # Clear any existing PID files
    rm -f .gameengine.pid .carddeck.pid .consoleui.pid
    
    # Create a log file
    log_file="poker_verbose.log"
    echo "Starting poker game in verbose mode at $(date)" > $log_file
    
    # Generate a random port offset to avoid conflicts (between 1000-1999)
    random_offset=$((RANDOM % 1000 + 1000))
    
    # Use a single unified process for verbose mode
    echo "Starting all services in a single command with verbose logging..."
    echo "Using port offset $random_offset to avoid conflicts"
    echo "Game logs will be written to $log_file"
    
    # Run the process in the foreground
    echo ""
    echo "████████████████████████████████████████████████████████████"
    echo "█                                                          █"
    echo "█  TEXAS HOLD'EM POKER GAME (VERBOSE MODE)                 █"
    echo "█                                                          █"
    echo "█  Press Ctrl+C to stop the game when you're finished      █"
    echo "█                                                          █"
    echo "████████████████████████████████████████████████████████████"
    echo ""
    
    # Execute the command in the foreground with random port offset
    dotnet run --project PokerGame.Console/PokerGame.Console.csproj -- --microservices --enhanced-ui --verbose --port-offset=$random_offset
    
    # No background process, no PID tracking in this mode
    echo "Game has exited. Thanks for playing!"
}

# Start with emergency deck mode for better reliability
start_emergency() {
    echo "Starting in emergency deck mode (more reliable)..."
    
    # First, stop any running services
    stop_all_services
    
    # Clear any existing PID files
    rm -f .gameengine.pid .carddeck.pid .consoleui.pid
    
    # Create a log file
    log_file="poker_emergency.log"
    echo "Starting poker game in emergency mode at $(date)" > $log_file
    
    # Generate a random port offset to avoid conflicts (between 2000-2999)
    random_offset=$((RANDOM % 1000 + 2000))
    
    # Use a single unified process for emergency mode
    echo "Starting all services in a single command for better coordination..."
    echo "Using port offset $random_offset to avoid conflicts"
    echo "Game logs will be written to $log_file"
    
    # Run the process in the foreground instead of background
    echo ""
    echo "████████████████████████████████████████████████████████████"
    echo "█                                                          █"
    echo "█  TEXAS HOLD'EM POKER GAME (EMERGENCY MODE)               █"
    echo "█                                                          █"
    echo "█  Press Ctrl+C to stop the game when you're finished      █"
    echo "█                                                          █"
    echo "████████████████████████████████████████████████████████████"
    echo ""
    
    # Execute the command in the foreground with random port offset
    dotnet run --project PokerGame.Console/PokerGame.Console.csproj -- --microservices --emergency-deck --enhanced-ui --verbose --port-offset=$random_offset
    
    # Note: No background process, no PID tracking in this mode
    echo "Game has exited. Thanks for playing!"
}

# Main script execution
case "$1" in
    start)
        start_all_services
        ;;
    stop)
        stop_all_services
        ;;
    restart)
        stop_all_services
        sleep 2
        start_all_services
        ;;
    verbose)
        start_verbose
        ;;
    emergency)
        start_emergency
        ;;
    start-services)
        # Clear any existing services PID file
        rm -f .services.pid
        # Use port offset 200 for services
        launch_service_host 200 "--verbose"
        ;;
    start-ui)
        # Clear any existing PID file
        rm -f .consoleui.pid
        # Use port offset 200 for individual UI service
        launch_ui_client "consoleui" 200 "--enhanced-ui"
        ;;
    curses)
        # Clear any existing PID file
        rm -f .consoleui.pid
        # Start the Console UI service with curses interface
        launch_ui_client "consoleui" 200 "--curses"
        ;;
    services-and-ui)
        # Start both services and UI in sequence
        rm -f .services.pid .consoleui.pid
        # Start services first
        launch_service_host 200 "--verbose"
        # Then start the UI
        launch_ui_client "consoleui" 200 "--enhanced-ui"
        ;;
    services-and-curses)
        # Start both services and curses UI in sequence
        rm -f .services.pid .consoleui.pid
        # Start services first
        launch_service_host 200 "--verbose"
        # Then start the curses UI
        launch_ui_client "consoleui" 200 "--curses"
        ;;
    stop-services)
        stop_services_host
        ;;
    stop-ui)
        stop_service "consoleui"
        ;;
    *)
        echo "Usage: $0 {start|stop|restart|verbose|emergency|start-services|start-ui|curses|services-and-ui|services-and-curses|stop-services|stop-ui}"
        echo ""
        echo "  start              - Start all services normally (legacy mode)"
        echo "  stop               - Stop all running services and clients"
        echo "  restart            - Restart all services (legacy mode)"
        echo "  verbose            - Start with verbose logging (legacy mode)"
        echo "  emergency          - Start with emergency deck mode (legacy mode)"
        echo "  start-services     - Start the services host (new architecture)"
        echo "  start-ui           - Start only the console UI client"
        echo "  curses             - Start only the UI with NCurses interface"
        echo "  services-and-ui    - Start both services and console UI"
        echo "  services-and-curses - Start both services and curses UI"
        echo "  stop-services      - Stop only the services host"
        echo "  stop-ui            - Stop only the UI client"
        exit 1
        ;;
esac

echo "Done."