# Reliable Messaging for Microservices

This module provides a reliable message broker system built on top of NetMQ/ZeroMQ that adds crucial features for robust microservice communication:

## Key Features

### 1. Acknowledgment System
- Automatically confirms message delivery between services
- Retries failed message deliveries
- Detects and reports timeouts for unacknowledged messages
- Provides both synchronous and asynchronous APIs

### 2. Message Tracking
- Unique identifiers for each message
- Timestamps for when messages are sent and acknowledged
- Service identity tracking for senders and receivers
- Prevention of duplicate message processing

### 3. Error Handling
- Graceful failure detection and reporting
- Automatic cleanup of resources
- Timeouts to prevent blocking operations

### 4. Integration Support
- Seamless integration with existing ZeroMQ-based microservices
- Extension methods for compatibility with legacy code
- Adapters for converting between message formats

## How to Use

### Basic Usage

```csharp
// Create a message broker with appropriate ports
var broker = new MessageBroker("ServiceName", publishPort, subscribePort);

// Register message handlers
broker.RegisterHandler("MessageType", async (message) => {
    // Process the message
    Console.WriteLine($"Received: {message.Type}");
    await Task.CompletedTask;
});

// Start the broker
broker.Start();

// Send a message with acknowledgment
var message = MessageEnvelope.Create("MessageType", new { Data = "payload" });
bool delivered = await broker.SendWithAcknowledgmentAsync(message);

if (delivered) {
    Console.WriteLine("Message was acknowledged!");
} else {
    Console.WriteLine("Message delivery timed out!");
}
```

### Integration with Existing Microservices

```csharp
// Create a specialized broker for an existing microservice
var microserviceBroker = new MicroserviceMessageBroker(
    existingService, 
    publishPort, 
    subscribePort);

// Register handlers for existing message types
microserviceBroker.RegisterMessageHandler(
    MessageType.ServiceRegistration, 
    existingService.HandleServiceRegistrationMessage);

// Start the broker
microserviceBroker.Start();

// Send a message with guaranteed delivery
await microserviceBroker.SendWithConfirmationAsync(
    Message.Create(MessageType.StartHand), 5000);
```

## Implementation Details

### Message Flow

1. Sender creates a message with a unique ID
2. Message is sent and tracked in a pending messages collection
3. Receiver processes the message and sends an acknowledgment
4. Sender receives acknowledgment and completes the send operation
5. If no acknowledgment is received within timeout, message is retried
6. After maximum retries, the message is marked as failed

### Components

- **MessageBroker**: Core broker handling message sending, receiving, and tracking
- **MessageEnvelope**: Container for message data with metadata for tracking
- **MicroserviceMessageBroker**: Specialized broker for existing microservices
- **MessageBrokerExtensions**: Integration utilities

## Design Considerations

This implementation prioritizes reliability over raw performance. For extremely high-throughput scenarios, you might want to adjust retry intervals and timeouts.

The system is designed to handle hundreds of messages per second with full reliability guarantees, making it suitable for most microservice applications.

## Future Enhancements

Potential future enhancements could include:
- Message prioritization
- Persistent message storage for surviving process restarts
- Load balancing for message distribution
- Message encryption
- Compression for large payloads