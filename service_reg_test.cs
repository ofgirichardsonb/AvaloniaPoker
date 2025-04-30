using System;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;
using PokerGame.Core.Messaging;
using PokerGame.Core.Microservices;

// Simple test program to verify service registration message flow
public class ServiceRegistrationTest
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Service Registration Test");
        
        // Initialize the central message broker
        Console.WriteLine("Initializing central message broker...");
        var broker = CentralMessageBroker.Instance;
        broker.Initialize();
        broker.Start();
        
        Console.WriteLine("Central message broker started");
        
        // Create a test service that will publish service registration
        Console.WriteLine("Creating test publisher service...");
        var publisherContext = new MSA.Foundation.ServiceManagement.ExecutionContext();
        var publisher = new TestPublisherService(publisherContext);
        
        // Create a test service that will subscribe to service registration
        Console.WriteLine("Creating test subscriber service...");
        var subscriberContext = new MSA.Foundation.ServiceManagement.ExecutionContext();
        var subscriber = new TestSubscriberService(subscriberContext);
        
        // Start both services
        Console.WriteLine("Starting test services...");
        publisher.Start();
        subscriber.Start();
        
        // Wait for a moment to allow services to initialize
        await Task.Delay(1000);
        
        // Publisher sends service registration
        Console.WriteLine("Publishing service registration...");
        publisher.PublishRegistration();
        
        // Wait for a moment to allow message to be processed
        await Task.Delay(3000);
        
        // Check if subscriber received the registration
        bool success = subscriber.ReceivedRegistration;
        Console.WriteLine($"Registration received: {success}");
        
        // Clean up
        publisher.Stop();
        subscriber.Stop();
        broker.Stop();
        
        Console.WriteLine("Test completed");
        
        // Exit with success/failure code
        Environment.Exit(success ? 0 : 1);
    }
}

// Simple test service that publishes service registration
public class TestPublisherService : MicroserviceBase
{
    public TestPublisherService(MSA.Foundation.ServiceManagement.ExecutionContext executionContext)
        : base(ServiceConstants.ServiceTypes.GameEngine, "TestPublisher", new PokerGame.Core.Messaging.ExecutionContext())
    {
    }
    
    public void PublishRegistration()
    {
        // Publish service registration message
        var message = new NetworkMessage
        {
            Type = MessageType.ServiceRegistration,
            MessageId = Guid.NewGuid().ToString(),
            SenderId = ServiceId
        };
        
        // Add registration payload
        var payload = new ServiceRegistrationPayload
        {
            ServiceId = ServiceId,
            ServiceName = "Test Publisher",
            ServiceType = "TestService"
        };
        
        message.SetPayload(payload);
        
        // Log the message being sent
        Console.WriteLine($"Publishing registration message ID: {message.MessageId}");
        
        // Broadcast through broker
        Broadcast(message);
    }
}

// Simple test service that subscribes to service registration messages
public class TestSubscriberService : MicroserviceBase
{
    public bool ReceivedRegistration { get; private set; } = false;
    
    public TestSubscriberService(MSA.Foundation.ServiceManagement.ExecutionContext executionContext)
        : base(ServiceConstants.ServiceTypes.ConsoleUI, "TestSubscriber", new PokerGame.Core.Messaging.ExecutionContext())
    {
    }
    
    public override void Start()
    {
        base.Start();
        
        // Subscribe to service registration messages
        Console.WriteLine("Subscribing to ServiceRegistration messages");
        BrokerManager.Instance.Subscribe(MessageType.ServiceRegistration, HandleServiceRegistration);
    }
    
    private bool HandleServiceRegistration(NetworkMessage message)
    {
        if (message.Type == MessageType.ServiceRegistration)
        {
            var payload = message.GetPayload<ServiceRegistrationPayload>();
            if (payload != null)
            {
                Console.WriteLine($"Received service registration from: {payload.ServiceName} (ID: {payload.ServiceId})");
                ReceivedRegistration = true;
            }
        }
        return true;
    }
}