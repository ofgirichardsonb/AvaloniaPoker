# Centralized Message Broker for Poker Game

This project implements a centralized message broker system designed to solve service discovery and reliable messaging issues in the poker game's microservices architecture.

## Key Features

- **Centralized Message Routing**: All messages pass through a single broker, eliminating the need for direct socket connections between services
- **Service Discovery**: Services register with the broker, allowing other services to discover them by type or capability
- **Reliable Messaging**: Built-in acknowledgment system ensures messages are delivered reliably
- **Message Deduplication**: Prevents duplicate message processing
- **Robust Error Handling**: Comprehensive error handling and logging
- **Monitoring and Debugging**: Support for message inspection and service status monitoring

## Architecture

The broker uses a frontend-backend architecture:

- **Frontend Socket**: Used by clients to connect to the broker (port 5570 by default)
- **Backend Socket**: Used by services to connect to the broker (port 5571 by default)
- **Monitor Socket**: Provides a way to monitor message flow (port 5572 by default)

## Components

- **CentralMessageBroker**: The main broker service that routes messages between clients and services
- **BrokerClient**: A client library that services can use to connect to the broker
- **BrokerMessage**: The message format used for communication
- **BrokerLogger**: A thread-safe logging system
- **TestClient**: A simple client for testing the broker

## Running the Broker

```bash
# Start the broker with default settings
./launch-broker.sh

# Start the broker with custom ports
./launch-broker.sh --frontend-port 6000 --backend-port 6001 --monitor-port 6002
```

## Running a Test Client

```bash
# Start a test client with default settings
./launch-client.sh

# Start a test client with custom name and type
./launch-client.sh --name "MyClient" --type "CardDeck"
```

## Test Client Commands

The test client supports the following commands:

- **help**: Displays help information
- **discover [type] [capability]**: Discovers services of the specified type and capability
- **ping \<serviceId\>**: Pings the specified service
- **send \<serviceId\> \<message\>**: Sends a message to the specified service
- **broadcast \<message\>**: Broadcasts a message to all services
- **exit**: Exits the client

## Integration with Poker Game

There are two ways to integrate the message broker with the poker game:

### Option 1: Standalone Broker

1. Start the broker using the launch script
2. Modify the poker game services to use the BrokerClient library instead of direct socket connections
3. Each service should register with the broker on startup
4. Services can discover other services using the broker's discovery mechanism
5. Messages are sent through the broker instead of directly to other services

### Option 2: In-Process Broker (Recommended)

1. Use the BrokerManager singleton to start the broker in the same process as the game engine
2. Create clients using the BrokerManager's CreateClient method
3. The broker runs in the main thread, eliminating the need for a separate process
4. Services connect to the broker using the same port configuration as the standalone version

```csharp
// Start the broker in the current process
BrokerManager.Instance.Start();

// Create a client for a service
var client = BrokerManager.Instance.CreateClient("MyServiceId", "MyServiceType");

// Connect the client
await client.ConnectAsync();

// Register the service
await client.RegisterServiceAsync();

// Use the client to discover and communicate with other services
var services = await client.DiscoverServicesAsync();
```

## Benefits for Poker Game

- Simplifies service communication by eliminating direct socket connections
- Solves service discovery issues (ConsoleUI finding the game engine)
- Improves reliability with built-in acknowledgment and retry mechanisms
- Makes debugging easier with centralized message monitoring
- Reduces the number of ports needed (only the broker ports are required)