# MSA.Foundation Class Diagram

This document provides a Mermaid-based class diagram visualization of the core components in MSA.Foundation.

## Core Component Structure

```mermaid
classDiagram
    %% Messaging interfaces and classes
    class IMessageBroker {
        <<interface>>
        +Start()
        +Stop()
        +PublishMessage(Message) bool
        +PublishMessageAsync(Message) Task~bool~
        +Subscribe(MessageType, Action~Message~) string
        +SubscribeAsync(MessageType, Func~Message, Task~) string
        +SubscribeAll(Action~Message~) string
        +SubscribeAllAsync(Func~Message, Task~) string
        +Unsubscribe(string) bool
    }
    
    class MessageBroker {
        -SocketCommunicationAdapter _socketAdapter
        -ConcurrentDictionary~string, Subscription~ _subscriptions
        -string _brokerId
        -bool _isRunning
        +MessageBroker(string, int, bool)
        +Start()
        +Stop()
        +PublishMessage(Message) bool
        +PublishMessageAsync(Message) Task~bool~
        +Subscribe(MessageType, Action~Message~) string
        +SubscribeAsync(MessageType, Func~Message, Task~) string
        +SubscribeAll(Action~Message~) string
        +SubscribeAllAsync(Func~Message, Task~) string
        +Unsubscribe(string) bool
        -OnMessageReceived(string, string)
        -SendAcknowledgment(Message)
    }
    
    class Message {
        +string MessageId
        +MessageType MessageType
        +string SenderId
        +string ReceiverId
        +DateTime Timestamp
        +string? Payload
        +Dictionary~string, string~ Headers
        +bool RequireAcknowledgment
        +string? AcknowledgmentId
        +Message()
        +Message(MessageType, string, string?)
        +CreateAcknowledgment(string) Message
        +CreateResponse(string, string?) Message
        +ToJson() string
        +~static~ FromJson(string) Message?
        +SetPayload~T~(T)
        +GetPayload~T~() T?
    }
    
    class SocketCommunicationAdapter {
        -string _address
        -int _port
        -bool _verbose
        -PublisherSocket _publisherSocket
        -SubscriberSocket _subscriberSocket
        -Task _receiveTask
        -CancellationTokenSource _cancellationTokenSource
        -ConcurrentDictionary~string, Action~string, string~~ _subscribers
        +SocketCommunicationAdapter(string, int, bool)
        +Start()
        +Stop()
        +SendMessage(string, string) bool
        +Subscribe(string, Action~string, string~) string
        +SubscribeAll(Action~string, string~) string
        +Unsubscribe(string) bool
        -ReceiveMessages(CancellationToken)
        -LogVerbose(string)
    }
    
    class Subscription {
        +MessageType? MessageType
        +Action~Message~ Callback
        +Subscription(MessageType?, Action~Message~)
    }
    
    enum MessageType {
        Unknown
        ServiceRegistration
        ServiceDiscovery
        Command
        Event
        Acknowledgment
        Heartbeat
        Debug
        Error
        Data
        Request
        Response
    }
    
    %% Telemetry interfaces and classes
    class ITelemetryService {
        <<interface>>
        +Initialize(string) bool
        +TrackEvent(string, IDictionary~string, string~?)
        +TrackMetric(string, double, IDictionary~string, string~?)
        +TrackException(Exception, IDictionary~string, string~?)
        +TrackRequest(string, DateTimeOffset, TimeSpan, string, bool)
        +TrackDependency(string, string, string, string, DateTimeOffset, TimeSpan, bool)
        +TrackTrace(string, SeverityLevel, IDictionary~string, string~?)
        +Flush()
        +FlushAsync() Task
    }
    
    class TelemetryService {
        -~static~ Lazy~TelemetryService~ _instance
        +~static~ TelemetryService Instance
        -TelemetryClient _telemetryClient
        -DependencyTrackingTelemetryModule? _dependencyModule
        -bool _isInitialized
        -string? _instrumentationKey
        -TelemetryService()
        +Initialize(string) bool
        -GetAppVersion() string
        +TrackEvent(string, IDictionary~string, string~?)
        +TrackMetric(string, double, IDictionary~string, string~?)
        +TrackException(Exception, IDictionary~string, string~?)
        +TrackRequest(string, DateTimeOffset, TimeSpan, string, bool)
        +TrackDependency(string, string, string, string, DateTimeOffset, TimeSpan, bool)
        +TrackTrace(string, SeverityLevel, IDictionary~string, string~?)
        +Flush()
        +FlushAsync() Task
    }
    
    %% Service Management classes
    class ExecutionContext {
        -CancellationTokenSource _cts
        -Thread _thread
        -bool _isRunning
        +CancellationToken CancellationToken
        +int ThreadId
        +bool IsRunning
        +ExecutionContext()
        +ExecutionContext(Thread)
        +RunAsync(Action) Task
        +RunAsync~T~(Func~T~) Task~T~
        +Stop()
    }
    
    class ServiceConstants {
        +~static~ string StaticServiceIdPrefix
        +~static~ int BaseCentralBrokerPort
        +~static~ int DynamicPortRangeStart
        +~static~ int DynamicPortRangeEnd
        +~static~ GetCentralBrokerPort(int) int
        +~static~ NormalizePort(int, int) int
        +~static~ RegisterStaticId(string, string)
        +~static~ GetStaticId(string) string
    }
    
    %% Relationships
    IMessageBroker <|.. MessageBroker
    MessageBroker o-- SocketCommunicationAdapter
    MessageBroker o-- Message
    MessageBroker *-- Subscription
    Message ..> MessageType
    ITelemetryService <|.. TelemetryService
    ExecutionContext ..> ServiceConstants
```

## Notes on Class Relationships

- **IMessageBroker** is implemented by **MessageBroker**
- **MessageBroker** uses **SocketCommunicationAdapter** for network communication
- **MessageBroker** creates and processes **Message** objects
- **Message** uses **MessageType** enum to categorize the message purpose
- **ITelemetryService** is implemented by **TelemetryService** (singleton pattern)
- **ExecutionContext** manages thread lifecycle and uses **ServiceConstants** for configuration

## Component Responsibilities

### Messaging Components

- **IMessageBroker**: Core interface for messaging functionality
- **MessageBroker**: Implementation of the message broker with subscription handling
- **Message**: Data structure for message content with serialization support
- **SocketCommunicationAdapter**: Handles the low-level network communication
- **MessageType**: Defines standard message categories

### Telemetry Components

- **ITelemetryService**: Interface for tracking events, metrics and exceptions
- **TelemetryService**: Application Insights implementation of telemetry

### Service Management Components

- **ExecutionContext**: Manages service execution with thread and cancellation support
- **ServiceConstants**: Central location for configuration constants and service IDs