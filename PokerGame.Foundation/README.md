# PokerGame.Foundation

A reusable application foundation with messaging, telemetry, service management, and configuration for building scalable microservice applications.

## Overview

This foundation library provides core infrastructure components extracted from the PokerGame project. It offers a robust and reusable set of tools for building microservice-based applications with reliable messaging, telemetry, and service management.

## Key Components

### Messaging Infrastructure

- Message brokers and adapters for reliable communication
- NetMQ-based socket communication
- Strong message typing and serialization
- Support for acknowledgments and message routing

### Telemetry Services

- Application Insights integration
- Event, metric, and exception tracking
- Configuration via environment variables or appsettings.json
- Dependency tracking and performance monitoring

### Service Management

- Service discovery and registration
- Microservice base implementations
- Execution context management
- Service constants and configuration

### Configuration

- Environment-aware configuration loading
- Support for appsettings.json and environment variables
- Consistent configuration access patterns

## Usage

### Initializing Telemetry

```csharp
// Get the singleton instance
var telemetry = TelemetryService.Instance;

// Initialize with Application Insights key
string? appInsightsKey = ConfigurationManager.Instance.GetApplicationInsightsKey();
if (!string.IsNullOrEmpty(appInsightsKey))
{
    telemetry.Initialize(appInsightsKey);
    telemetry.TrackEvent("ApplicationStarted");
}
```

### Setting Up Messaging

```csharp
// Create a socket adapter
var socketAdapter = new SocketCommunicationAdapter("127.0.0.1", 5555, verbose: true);
socketAdapter.Start();

// Subscribe to messages
string subscriptionId = socketAdapter.SubscribeAll((topic, message) => 
{
    Console.WriteLine($"Received message with topic '{topic}': {message}");
});

// Send a message
socketAdapter.SendMessage("status", "Service started");
```

### Using Execution Context

```csharp
// Create an execution context
using var context = new ExecutionContext();

// Run an action in the context
await context.RunAsync(() => 
{
    // Long-running operation
});
```

## Version History

- 0.1.0: Initial release with core functionality
  - Extracted from PokerGame v0.1.0
  - Includes messaging, telemetry, and service management

## License

This foundation is provided as part of the PokerGame project.