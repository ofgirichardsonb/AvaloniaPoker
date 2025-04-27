# MSA.Foundation

A reusable microservices architecture foundation with messaging, telemetry, and service management capabilities.

## Overview

MSA.Foundation is a robust library designed to provide the core infrastructure needed to build microservice-based applications. It was extracted from a larger project to be shared and reused across multiple applications requiring similar architectural patterns.

## Key Features

- **Messaging Infrastructure**: Reliable communication between microservices using NetMQ
- **Telemetry Services**: Application insights integration for monitoring and diagnostics
- **Service Management**: Tools for managing microservice execution and lifecycle
- **Execution Context**: Thread and cancellation management for reliable service operations
- **Message Acknowledgment**: Built-in reliability with acknowledgment protocols

## Getting Started

```csharp
// Initialize messaging
var messageBroker = new MessageBroker();
messageBroker.Start();

// Initialize telemetry
var telemetryService = TelemetryService.Instance;
telemetryService.Initialize(Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY"));

// Create execution context
using var executionContext = new ExecutionContext();

// Send a message
var message = new Message(MessageType.Event, "SenderService", "Hello World!");
messageBroker.PublishMessage(message);

// Track an event
telemetryService.TrackEvent("ServiceStarted");
```

## Architecture

The library is organized into three main namespaces:

1. `MSA.Foundation.Messaging`: Components for interservice communication
2. `MSA.Foundation.Telemetry`: Components for monitoring and diagnostics
3. `MSA.Foundation.ServiceManagement`: Components for service lifecycle management

## Versioning

This library follows semantic versioning. See the CHANGELOG.md file for version history.

## License

This project is licensed under the MIT License - see the LICENSE file for details.