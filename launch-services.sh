#!/bin/bash

# Advanced script to launch the poker game services in separate processes
# This is more reliable than managing everything in a single .NET async process

CONSOLE_PORT_OFFSET=10 # Make sure each instance gets separate ports

# Function to launch a service
launch_service() {
    local service_type=$1
    local port_offset=$2
    local extra_args=$3
    
    echo "Launching $service_type service with port offset $port_offset..."
    
    # Launch the service in the background
    dotnet run --project PokerGame.Console/PokerGame.Console.csproj -- \
        --microservices \
        --service-type=$service_type \
        --port-offset=$port_offset \
        $extra_args &
    
    # Store the PID
    local pid=$!
    echo "$service_type service launched with PID $pid"
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

# Stop any running services
stop_all_services() {
    echo "Stopping all services..."
    stop_service "gameengine"
    stop_service "carddeck" 
    stop_service "consoleui"
}

# Start the services in the correct order with proper delays
start_all_services() {
    echo "Starting all services..."
    
    # First, clear any existing PID files
    rm -f .gameengine.pid .carddeck.pid .consoleui.pid
    
    # Use a consistent offset for all services to ensure proper communication
    local offset=100
    
    # Start the Game Engine service first
    launch_service "gameengine" $offset ""
    echo "Waiting for game engine to initialize..."
    sleep 3
    
    # Start the Card Deck service second
    launch_service "carddeck" $offset ""
    echo "Waiting for card deck service to initialize..."
    sleep 3
    
    # Start the Console UI service last
    launch_service "consoleui" $offset "--enhanced-ui"
    
    echo "All services started. Poker game should be running."
    echo "Use stop command to terminate all services when done."
}

# Start in verbose mode with more debugging information
start_verbose() {
    echo "Starting all services in verbose mode..."
    
    # First, clear any existing PID files
    rm -f .gameengine.pid .carddeck.pid .consoleui.pid
    
    # Use a consistent offset for all verbose mode services
    local verbose_offset=150
    
    # Start the Game Engine service first with verbose logging
    launch_service "gameengine" $verbose_offset "--verbose"
    echo "Waiting for game engine to initialize..."
    sleep 3
    
    # Start the Card Deck service with verbose logging
    launch_service "carddeck" $verbose_offset "--verbose"
    echo "Waiting for card deck service to initialize..."
    sleep 3
    
    # Start the Console UI service with enhanced UI and verbose logging
    launch_service "consoleui" $verbose_offset "--enhanced-ui --verbose"
    
    echo "All services started in verbose mode."
    echo "Check logs for detailed information about service operation."
}

# Start with emergency deck mode for better reliability
start_emergency() {
    echo "Starting in emergency deck mode (more reliable)..."
    
    # First, clear any existing PID files
    rm -f .gameengine.pid .carddeck.pid .consoleui.pid
    
    # Use a fixed port offset for all emergency mode services to ensure they can communicate
    local emergency_offset=50
    
    # Start the Game Engine service first
    launch_service "gameengine" $emergency_offset ""
    echo "Waiting for game engine to initialize..."
    sleep 3
    
    # Start the Card Deck service with emergency deck mode
    launch_service "carddeck" $emergency_offset "--emergency-deck"
    echo "Waiting for card deck service to initialize..."
    sleep 3
    
    # Start the Console UI service last, with enhanced UI
    launch_service "consoleui" $emergency_offset "--enhanced-ui"
    
    echo "All services should now be running in emergency mode"
    echo "Game should be accessible through the console UI"
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
    start-engine)
        # Clear any existing PID file
        rm -f .gameengine.pid
        # Use port offset 200 for individual engine service
        launch_service "gameengine" 200 ""
        ;;
    start-deck)
        # Clear any existing PID file
        rm -f .carddeck.pid
        # Use port offset 200 for individual card deck service
        launch_service "carddeck" 200 ""
        ;;
    start-ui)
        # Clear any existing PID file
        rm -f .consoleui.pid
        # Use port offset 200 for individual UI service
        launch_service "consoleui" 200 "--enhanced-ui"
        ;;
    curses)
        # Clear any existing PID file
        rm -f .consoleui.pid
        # Start the Console UI service with curses interface
        launch_service "consoleui" 200 "--curses"
        ;;
    *)
        echo "Usage: $0 {start|stop|restart|verbose|emergency|start-engine|start-deck|start-ui|curses}"
        echo ""
        echo "  start       - Start all services normally"
        echo "  stop        - Stop all running services"
        echo "  restart     - Restart all services"
        echo "  verbose     - Start with verbose logging"
        echo "  emergency   - Start with emergency deck mode (more reliable)"
        echo "  start-engine - Start only the game engine service"
        echo "  start-deck  - Start only the card deck service"
        echo "  start-ui    - Start only the console UI service"
        echo "  curses      - Start only the UI with NCurses interface"
        exit 1
        ;;
esac

echo "Done."