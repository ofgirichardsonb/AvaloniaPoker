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