# MSA.Foundation Integration Guide

This guide demonstrates how to integrate MSA.Foundation into your microservices-based application.

## Getting Started

### 1. Installation

Add the MSA.Foundation NuGet package to your project:

```bash
dotnet add package MSA.Foundation
```

Or add the following to your `.csproj` file:

```xml
<ItemGroup>
    <PackageReference Include="MSA.Foundation" Version="0.1.0" />
</ItemGroup>
```

### 2. Basic Usage Example

Here's a complete example showing how to use the main components of MSA.Foundation:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;
using MSA.Foundation.Telemetry;
using MSA.Foundation.ServiceManagement;

namespace YourApplication
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Initialize telemetry (optional but recommended)
            var telemetryService = TelemetryService.Instance;
            telemetryService.Initialize(Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY"));
            
            // Create message broker
            var messageBroker = new MessageBroker("127.0.0.1", ServiceConstants.Ports.GetCentralBrokerPort());
            messageBroker.Start();
            
            // Subscribe to messages
            messageBroker.SubscribeAll(message => {
                Console.WriteLine($"Received message: {message.MessageType} from {message.SenderId}");
                telemetryService.TrackEvent("MessageReceived", new Dictionary<string, string> {
                    { "MessageType", message.MessageType.ToString() },
                    { "SenderId", message.SenderId }
                });
            });
            
            // Register a static service ID (optional)
            ServiceConstants.ServiceTypes.RegisterStaticId("MyService", "static_my_service");
            
            // Create and send a message
            var message = new Message(MessageType.Event, "MyService")
            {
                RequireAcknowledgment = true
            };
            message.SetPayload(new { Content = "Hello World" });
            
            bool success = messageBroker.PublishMessage(message);
            Console.WriteLine($"Message sent: {success}");
            
            // Create an execution context for a service
            using (var executionContext = new ExecutionContext())
            {
                // Run a task in the execution context
                await executionContext.RunAsync(() => {
                    Console.WriteLine("Task running in execution context");
                    // Your service logic here
                });
                
                // Wait for user input before exiting
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            
            // Clean up
            messageBroker.Dispose();
            telemetryService.Dispose();
        }
    }
}
```

## Creating a Microservice

When building a microservice-based application, you can create service classes that utilize the MSA.Foundation components:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;
using MSA.Foundation.Telemetry;
using MSA.Foundation.ServiceManagement;

namespace YourApplication.Services
{
    public class MyService : IDisposable
    {
        private readonly IMessageBroker _messageBroker;
        private readonly ITelemetryService _telemetryService;
        private readonly ExecutionContext _executionContext;
        private readonly string _serviceId;
        private readonly CancellationTokenSource _cts;
        private Task? _serviceTask;
        
        public MyService(IMessageBroker messageBroker, ITelemetryService telemetryService)
        {
            _messageBroker = messageBroker;
            _telemetryService = telemetryService;
            _serviceId = ServiceConstants.ServiceTypes.GetStaticId("MyService");
            _cts = new CancellationTokenSource();
            _executionContext = new ExecutionContext();
        }
        
        public async Task StartAsync()
        {
            // Subscribe to messages
            _messageBroker.Subscribe(MessageType.Command, HandleCommand);
            
            // Announce service availability
            var registrationMessage = new Message(MessageType.ServiceRegistration, _serviceId)
            {
                RequireAcknowledgment = true
            };
            await _messageBroker.PublishMessageAsync(registrationMessage);
            
            // Start service processing
            _serviceTask = _executionContext.RunAsync(() => ServiceLoop(_cts.Token));
            
            _telemetryService.TrackEvent("ServiceStarted", new Dictionary<string, string> {
                { "ServiceId", _serviceId }
            });
        }
        
        private async Task ServiceLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Service processing logic here
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _telemetryService.TrackException(ex);
            }
        }
        
        private void HandleCommand(Message message)
        {
            try
            {
                _telemetryService.TrackEvent("CommandReceived");
                
                // Handle command message
                Console.WriteLine($"Handling command: {message.MessageId}");
                
                // Send response if needed
                var response = message.CreateResponse(_serviceId, "Command processed");
                _messageBroker.PublishMessage(response);
            }
            catch (Exception ex)
            {
                _telemetryService.TrackException(ex);
            }
        }
        
        public async Task StopAsync()
        {
            _cts.Cancel();
            
            if (_serviceTask != null)
            {
                await _serviceTask;
            }
            
            _telemetryService.TrackEvent("ServiceStopped");
        }
        
        public void Dispose()
        {
            _cts.Dispose();
            _executionContext.Dispose();
        }
    }
}
```

## Environment Variables Configuration

MSA.Foundation components look for these environment variables:

| Variable Name | Description |
|---------------|-------------|
| `APPINSIGHTS_INSTRUMENTATIONKEY` | The Application Insights instrumentation key for telemetry |

## Best Practices

1. **Use Static Service IDs**: Register static IDs for your services to ensure reliable communication.
2. **Implement Acknowledgments**: Enable message acknowledgments for critical operations.
3. **Handle Exceptions**: Always wrap message handling in try-catch blocks and track exceptions.
4. **Dispose Resources**: Properly dispose of message brokers, execution contexts, and other resources.
5. **Use Telemetry**: Leverage the telemetry service to monitor your application's health and performance.

## Troubleshooting

- **Messages not being received**: Check that the publisher and subscriber are using the same broker address and port.
- **Telemetry not working**: Verify that the Application Insights instrumentation key is correctly set.
- **Service discovery issues**: Ensure that service IDs are registered and that services are broadcasting their availability.