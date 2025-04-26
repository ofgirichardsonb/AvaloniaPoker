using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PokerGame.Core.Microservices;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// A specialized message broker for microservices that adds reliable messaging to the existing architecture
    /// </summary>
    public class MicroserviceMessageBroker : IDisposable
    {
        private readonly MessageBroker _broker;
        private readonly MicroserviceBase _ownerService;
        private bool _isDisposed;
        
        /// <summary>
        /// Creates a new microservice message broker
        /// </summary>
        /// <param name="ownerService">The microservice that owns this broker</param>
        /// <param name="publishPort">Port for publishing messages</param>
        /// <param name="subscribePort">Port for subscribing to messages</param>
        public MicroserviceMessageBroker(MicroserviceBase ownerService, int publishPort, int subscribePort)
        {
            _ownerService = ownerService ?? throw new ArgumentNullException(nameof(ownerService));
            
            string serviceName = MicroserviceBaseExtensions.GetServiceName(ownerService);
            
            // Create the underlying broker with the service identity
            _broker = new MessageBroker(
                $"{serviceName}-Broker", 
                publishPort, 
                subscribePort);
                
            Console.WriteLine($"Created MicroserviceMessageBroker for {serviceName}");
        }
        
        /// <summary>
        /// Starts the message broker
        /// </summary>
        public void Start()
        {
            string serviceName = MicroserviceBaseExtensions.GetServiceName(_ownerService);
            
            // Register core message handlers
            RegisterBasicMessageHandlers();
            
            // Start the broker
            _broker.Start();
            
            // Broadcast service registration
            BroadcastServiceRegistration();
            
            Console.WriteLine($"Started MicroserviceMessageBroker for {serviceName}");
        }
        
        /// <summary>
        /// Stops the message broker
        /// </summary>
        public void Stop()
        {
            string serviceName = MicroserviceBaseExtensions.GetServiceName(_ownerService);
            _broker.Stop();
            Console.WriteLine($"Stopped MicroserviceMessageBroker for {serviceName}");
        }
        
        /// <summary>
        /// Registers a handler for a specific message type
        /// </summary>
        /// <param name="messageType">The message type to handle</param>
        /// <param name="handler">The handler delegate</param>
        public void RegisterMessageHandler(MessageType messageType, Func<Message, Task> handler)
        {
            _broker.RegisterHandler(
                messageType.ToString(), 
                MessageBrokerExtensions.AdaptLegacyHandler(handler));
                
            Console.WriteLine($"Registered handler for {messageType}");
        }
        
        /// <summary>
        /// Registers the basic message handlers for common functionality
        /// </summary>
        private void RegisterBasicMessageHandlers()
        {
            string serviceName = MicroserviceBaseExtensions.GetServiceName(_ownerService);
            Console.WriteLine($"Registering basic message handlers for {serviceName}");
            
            // Register handler for acknowledgments
            _broker.RegisterHandler(
                MessageType.Acknowledgment.ToString(),
                async (envelope) =>
                {
                    Console.WriteLine($"Received acknowledgment for message {envelope.GetMetadata("InResponseTo")}");
                    await Task.CompletedTask;
                });
                
            // Handle service discovery
            _broker.RegisterHandler(
                MessageType.ServiceDiscovery.ToString(),
                async (envelope) =>
                {
                    Console.WriteLine($"Received service discovery request from {envelope.GetMetadata("SenderId")}");
                    BroadcastServiceRegistration();
                    await Task.CompletedTask;
                });
                
            // Handle service registration
            _broker.RegisterHandler(
                MessageType.ServiceRegistration.ToString(),
                async (envelope) =>
                {
                    var payload = envelope.GetPayload<ServiceRegistrationPayload>();
                    if (payload != null)
                    {
                        Console.WriteLine($"Received service registration from {payload.ServiceName} ({payload.ServiceType})");
                        // Create a Message object from the envelope for compatibility with existing code
                        var message = new Message
                        {
                            Type = MessageType.ServiceRegistration,
                            SenderId = envelope.GetMetadata("SenderId")
                        };
                        message.SetPayload(payload);
                        await MicroserviceBaseExtensions.HandleServiceRegistrationAsync(_ownerService, message);
                    }
                    await Task.CompletedTask;
                });
        }
        
        /// <summary>
        /// Broadcasts a service registration message
        /// </summary>
        public void BroadcastServiceRegistration()
        {
            string serviceName = MicroserviceBaseExtensions.GetServiceName(_ownerService);
            
            var registrationPayload = new ServiceRegistrationPayload
            {
                ServiceId = _ownerService.ServiceId,
                ServiceName = serviceName,
                ServiceType = _ownerService.GetType().Name.Replace("Service", ""),
                Capabilities = new List<string>()
            };
            
            var envelope = MessageEnvelope.Create(
                MessageType.ServiceRegistration.ToString(), 
                registrationPayload);
                
            _broker.Broadcast(envelope);
            
            Console.WriteLine($"Broadcast service registration for {serviceName}");
        }
        
        /// <summary>
        /// Sends a message with guaranteed delivery
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="receiverId">The ID of the receiver</param>
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <returns>True if delivery was confirmed, false if timed out</returns>
        public async Task<bool> SendWithConfirmationAsync(Message message, string receiverId, int timeoutMs = 5000)
        {
            // Create MessageEnvelope from Message
            message.SenderId = _ownerService.ServiceId;
            
            if (string.IsNullOrEmpty(message.MessageId))
            {
                message.MessageId = Guid.NewGuid().ToString();
            }
            
            // Create envelope
            var envelope = MessageEnvelope.Create(
                message.Type.ToString(),
                message.Payload);
                
            // Add metadata
            envelope.Metadata["SenderId"] = message.SenderId;
            envelope.Metadata["MessageId"] = message.MessageId;
            envelope.Metadata["TimeStamp"] = DateTime.UtcNow.ToString("o");
            envelope.Metadata["TargetId"] = receiverId;
            envelope.Metadata["InResponseTo"] = message.InResponseTo ?? string.Empty;
                
            return await _broker.SendWithAcknowledgmentAsync(envelope, timeoutMs);
        }
        
        /// <summary>
        /// Sends a message to a specific service
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="receiverId">The service ID of the receiver</param>
        public void SendTo(Message message, string receiverId)
        {
            if (string.IsNullOrEmpty(receiverId))
            {
                Console.WriteLine("Cannot send message: receiverId is null or empty");
                return;
            }
            
            // Set sender information
            message.SenderId = _ownerService.ServiceId;
            
            // Create envelope from message
            var envelope = MessageEnvelope.Create(
                message.Type.ToString(),
                message.Payload);
                
            // Add metadata
            envelope.Metadata["SenderId"] = message.SenderId;
            envelope.Metadata["MessageId"] = message.MessageId ?? Guid.NewGuid().ToString();
            envelope.Metadata["TimeStamp"] = DateTime.UtcNow.ToString("o");
            envelope.Metadata["TargetId"] = receiverId;
            envelope.Metadata["InResponseTo"] = message.InResponseTo ?? string.Empty;
            
            // Send the message through the broker
            _broker.SendTo(envelope, receiverId);
        }
        
        /// <summary>
        /// Broadcasts a message to all services
        /// </summary>
        /// <param name="message">The message to broadcast</param>
        public void Broadcast(Message message)
        {
            // Set sender information
            message.SenderId = _ownerService.ServiceId;
            
            // Create envelope from message
            var envelope = MessageEnvelope.Create(
                message.Type.ToString(),
                message.Payload);
                
            // Add metadata
            envelope.Metadata["SenderId"] = message.SenderId;
            envelope.Metadata["MessageId"] = message.MessageId ?? Guid.NewGuid().ToString();
            envelope.Metadata["TimeStamp"] = DateTime.UtcNow.ToString("o");
            envelope.Metadata["InResponseTo"] = message.InResponseTo ?? string.Empty;
            
            // Broadcast the message through the broker
            _broker.Broadcast(envelope);
        }
        
        /// <summary>
        /// Disposes resources used by the broker
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            
            _broker.Dispose();
            _isDisposed = true;
            
            GC.SuppressFinalize(this);
        }
    }
}