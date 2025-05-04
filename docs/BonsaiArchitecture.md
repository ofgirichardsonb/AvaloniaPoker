# Bonsai Architecture Pattern

## Overview

The Bonsai Architecture is a pattern for developing and deploying microservices where the structure of a distributed system is miniaturized into a single process (like a bonsai tree resembles a full-sized tree in miniature). This approach allows developers to leverage microservice architectural patterns while maintaining the simplicity of a monolithic deployment when needed.

## Core Principles

1. **Same Design, Different Scale**: Services follow the same design patterns regardless of whether they're deployed within a single process or across distributed processes.

2. **Incremental Distribution**: Services can be promoted from in-process to distributed deployment without changing their core design or interfaces.

3. **Uniform Communication**: Using a consistent messaging abstraction regardless of whether message destinations are in the same process or across the network.

4. **Coordinated Lifecycle**: Includes robust mechanisms for coordinating initialization, operation, and shutdown across both in-process and distributed services.

## Architecture Components

### Messaging Infrastructure

- **IMessage**: Core message contract that all services use for communication
- **IMessageTransport**: Abstraction of communication mechanism that can be implemented for both in-process and distributed scenarios
- **ServiceMessage**: Implementation of messages with appropriate headers and correlation identifiers
- **TransportFactory**: Factory for creating appropriate transport implementations based on deployment context

### Service Management

- **ExecutionContext**: Provides a consistent environment for service execution, manages service lifecycles
- **ShutdownCoordinator**: Coordinates orderly shutdown of all services in priority order
- **MessageTransportManager**: Tracks and manages all message transports for cleanup

### Common Implementations

1. **InProcessMessageTransport**: Efficient implementation for services in the same process
2. **NetMQMessageTransport**: Implementation for communication between processes using NetMQ (ZeroMQ for .NET)
3. **RabbitMQTransport**: (Future) Implementation for distributed deployment using RabbitMQ

## Deployment Models

### Model 1: In-Process (Bonsai Form)

All services are hosted within a single process for development or for simple deployments:

```
+------------------+
|    Process       |
|  +-----------+   |
|  | Service A |   |
|  +-----------+   |
|        |         |
|  +-----------+   |
|  | Service B |   |
|  +-----------+   |
|        |         |
|  +-----------+   |
|  | Service C |   |
|  +-----------+   |
+------------------+
```

### Model 2: Hybrid Deployment

Some services remain in-process while others are promoted to separate processes:

```
+------------------+         +------------------+
|   Process 1      |         |   Process 2      |
|  +-----------+   |         |  +-----------+   |
|  | Service A |<-------------->| Service C |   |
|  +-----------+   |         |  +-----------+   |
|        |         |         +------------------+
|  +-----------+   |
|  | Service B |   |
|  +-----------+   |
+------------------+
```

### Model 3: Fully Distributed

All services run in their own processes for maximum scalability and resilience:

```
+------------------+
|   Process 1      |
|  +-----------+   |
|  | Service A |<------+
|  +-----------+   |   |
+------------------+   |
                       |
+------------------+   |    +------------------+
|   Process 2      |   |    |   Process 3      |
|  +-----------+   |   |    |  +-----------+   |
|  | Service B |<--+----+-->| Service C |   |
|  +-----------+   |        |  +-----------+   |
+------------------+        +------------------+
```

## Benefits

1. **Developer Experience**: Simplified development and testing in a single process while maintaining the benefits of a microservice architecture
2. **Scalable Evolution**: Allows for incremental promotion of services to separate processes as demand grows
3. **Consistent Patterns**: Same design patterns and communication styles regardless of deployment model
4. **Path to Distributed**: Clear migration path from monolithic to distributed deployment without rewriting services
5. **Improved Resource Usage**: Better resource utilization when full distribution isn't necessary

## Challenges & Solutions

### Resource Management

**Challenge**: Proper cleanup of resources in single-process scenario
**Solution**: ShutdownCoordinator ensuring orderly cleanup with priorities

### Message Delivery 

**Challenge**: Ensuring consistent delivery semantics regardless of deployment
**Solution**: Transport abstraction with common acknowledgment patterns 

### Error Handling

**Challenge**: Consistent error propagation across transport boundaries
**Solution**: Standardized error reporting in message acknowledgments

## Implementation Guidelines

1. Always communicate through the messaging abstraction, even for in-process services
2. Register all services with the ShutdownCoordinator to ensure orderly cleanup
3. Follow a consistent message structure with appropriate correlation IDs
4. Use the TransportFactory to create the appropriate transport implementation
5. Design services to be stateless with respect to their transport mechanism
6. Implement exponential backoff and retries at the transport level
7. Use dependency injection to provide the appropriate transport implementation

## Conclusion

The Bonsai Architecture provides a flexible approach to microservice development that combines the benefits of microservice design patterns with the simplicity of monolithic deployment. By maintaining consistent interfaces and communication patterns, services can be seamlessly promoted from in-process to distributed deployment as requirements evolve.