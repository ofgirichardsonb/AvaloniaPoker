# MSA.Foundation Architecture Overview

## Introduction

MSA.Foundation is a reusable architectural framework designed to simplify the development of microservice-based applications. It provides a set of core components for messaging, telemetry, and service management that can be shared across multiple projects.

## Key Components

```mermaid
graph TD
    subgraph Foundation["MSA.Foundation"]
        style Foundation fill:#e1f5fe,stroke:#0288d1,stroke-width:2px
        
        subgraph Messaging["Messaging Layer"]
            style Messaging fill:#e8f5e9,stroke:#388e3c,stroke-width:1px
            MB["MessageBroker"]
            SCA["SocketCommunicationAdapter"]
            MSG["Message"]
            MT["MessageType"]
        end
        
        subgraph Telemetry["Telemetry Layer"]
            style Telemetry fill:#fff8e1,stroke:#ffa000,stroke-width:1px
            TS["TelemetryService"]
            ITS["ITelemetryService"]
        end
        
        subgraph ServiceMgmt["Service Management"]
            style ServiceMgmt fill:#f3e5f5,stroke:#7b1fa2,stroke-width:1px
            EC["ExecutionContext"]
            SC["ServiceConstants"]
        end
    end
    
    subgraph Applications["Applications"]
        style Applications fill:#ffebee,stroke:#c62828,stroke-width:2px
        App1["Application 1"]
        App2["Application 2"]
        App3["Application 3"]
    end
    
    App1 --> MB
    App1 --> TS
    App1 --> EC
    
    App2 --> MB
    App2 --> TS
    App2 --> EC
    
    App3 --> MB
    App3 --> TS
    App3 --> EC
    
    MB --> SCA
    MB --- MSG
    MSG --- MT
    
    TS -.-> ITS
    
    classDef component fill:#f5f5f5,stroke:#333,stroke-width:1px
    class MB,SCA,MSG,MT,TS,ITS,EC,SC component
```

## Architectural Principles

### 1. Separation of Concerns

MSA.Foundation follows strict separation of concerns by dividing functionality into three main layers:

- **Messaging**: Handles all communication between services
- **Telemetry**: Provides monitoring, logging, and performance tracking
- **Service Management**: Manages service lifecycle and configuration

### 2. Dependency Inversion

The framework uses interfaces and dependency injection to ensure components are loosely coupled:

- `IMessageBroker` interface allows different messaging implementations
- `ITelemetryService` interface supports various telemetry providers
- Service components depend on abstractions, not concrete implementations

### 3. Cross-Platform Compatibility

All components are designed to work across different platforms:

- .NET 8.0 based for cross-platform support
- Platform-agnostic communication via ZeroMQ
- Cloud-friendly telemetry with Application Insights

## Communication Flow

```mermaid
sequenceDiagram
    participant Service1 as Service 1
    participant MsgBroker as Message Broker
    participant Service2 as Service 2
    participant TelSvc as Telemetry Service
    
    Service1->>MsgBroker: PublishMessage(command)
    MsgBroker->>Service2: OnMessageReceived(command)
    Service2->>TelSvc: TrackEvent("CommandReceived")
    Service2->>MsgBroker: PublishMessage(response)
    MsgBroker->>Service1: OnMessageReceived(response)
    Service1->>TelSvc: TrackEvent("ResponseReceived")
```

## Integration Patterns

MSA.Foundation supports several integration patterns:

1. **Request/Response**: For synchronous communication between services
2. **Publish/Subscribe**: For event-driven architectures
3. **Command**: For direct service control
4. **Event**: For loose coupling and scalability

## Deployment Model

The framework supports flexible deployment options:

- **In-Process**: Services running as threads within a single process
- **Cross-Process**: Services running as separate processes on a single machine
- **Distributed**: Services running across multiple machines or containers

## Configuration

Configuration is handled through a combination of:

- Environment variables for secrets and environment-specific settings
- ServiceConstants for static configuration
- JSON configuration files for application settings

## Extensibility

MSA.Foundation is designed for extensibility:

- Custom message types can be added to extend the messaging capabilities
- Telemetry can be extended with custom event tracking
- Service management can be customized for specific deployment scenarios

## Performance Considerations

- SocketCommunicationAdapter uses efficient binary serialization
- Telemetry batches events to minimize performance impact
- ExecutionContext manages thread lifecycle to prevent leaks

## Security

- Communication can be secured using transport-level encryption
- Telemetry includes safeguards for sensitive data
- Message validation prevents malformed messages

## Conclusion

MSA.Foundation provides a robust, extensible framework for building microservice-based applications with a focus on reliability, scalability, and maintainability. By abstracting common infrastructure concerns, it allows developers to focus on business logic rather than plumbing code.