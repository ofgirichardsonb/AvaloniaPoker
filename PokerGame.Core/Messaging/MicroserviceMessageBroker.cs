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
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <returns>True if delivery was confirmed, false if timed out</returns>
        public async Task<bool> SendWithConfirmationAsync(Message message, int timeoutMs = 5000)
        {
            var envelope = message.ToEnvelope();
            return await _broker.SendWithAcknowledgmentAsync(envelope, timeoutMs);
        }
        
        /// <summary>
        /// Sends a direct message to a specific service
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="targetServiceId">The target service ID</param>
        public void SendTo(Message message, string targetServiceId)
        {
            var envelope = message.ToEnvelope();
            _broker.SendTo(envelope, targetServiceId);
        }
        
        /// <summary>
        /// Broadcasts a message to all services
        /// </summary>
        /// <param name="message">The message to broadcast</param>
        public void Broadcast(Message message)
        {
            var envelope = message.ToEnvelope();
            _broker.Broadcast(envelope);
        }
        
        /// <summary>
        /// Registers basic message handlers used by all microservices
        /// </summary>
        private void RegisterBasicMessageHandlers()
        {
            // Handle service discovery
            _broker.RegisterHandler(
                MessageType.ServiceDiscovery.ToString(),
                async (envelope) =>
                {
                    Console.WriteLine($"Received service discovery request from {envelope.SenderServiceId}");
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
                        await MicroserviceBaseExtensions.HandleServiceRegistrationAsync(_ownerService, payload);
                    }
                    await Task.CompletedTask;
                });
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