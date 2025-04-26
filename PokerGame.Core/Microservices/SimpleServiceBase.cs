using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PokerGame.Core.Messaging;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Base class for simplified microservices with improved error handling and reliability
    /// </summary>
    public abstract class SimpleServiceBase : IDisposable
    {
        private readonly string _serviceId;
        private readonly string _serviceName;
        private readonly string _serviceType;
        private readonly bool _verbose;
        private readonly PokerGame.Core.Messaging.ExecutionContext _executionContext;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private SimpleMessageBroker _messageBroker;
        private readonly Logger _logger;
        private Task _backgroundTask;
        private bool _disposed = false;
        private int _publisherPort;
        private int _subscriberPort;
        
        /// <summary>
        /// Gets the unique identifier of this service
        /// </summary>
        public string ServiceId => _serviceId;
        
        /// <summary>
        /// Gets the name of this service
        /// </summary>
        public string ServiceName => _serviceName;
        
        /// <summary>
        /// Gets the type of this service
        /// </summary>
        public string ServiceType => _serviceType;
        
        /// <summary>
        /// Gets the logger instance for this service
        /// </summary>
        protected Logger Logger => _logger;
        
        /// <summary>
        /// Creates a new simple service instance
        /// </summary>
        /// <param name="serviceName">The human-readable name of the service</param>
        /// <param name="serviceType">The type of the service (e.g., "GameEngine", "CardDeck")</param>
        /// <param name="publisherPort">The port on which this service will publish messages</param>
        /// <param name="subscriberPort">The port on which this service will subscribe to messages</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        protected SimpleServiceBase(string serviceName, string serviceType, int publisherPort, int subscriberPort, bool verbose = false)
            : this(serviceName, serviceType, publisherPort, subscriberPort, null, verbose)
        {
        }
        
        /// <summary>
        /// Creates a new simple service instance with an execution context
        /// </summary>
        /// <param name="serviceName">The human-readable name of the service</param>
        /// <param name="serviceType">The type of the service (e.g., "GameEngine", "CardDeck")</param>
        /// <param name="publisherPort">The port on which this service will publish messages</param>
        /// <param name="subscriberPort">The port on which this service will subscribe to messages</param>
        /// <param name="executionContext">The execution context for this service</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        protected SimpleServiceBase(string serviceName, string serviceType, int publisherPort, int subscriberPort, PokerGame.Core.Messaging.ExecutionContext? executionContext = null, bool verbose = false)
        {
            _serviceId = Guid.NewGuid().ToString();
            _serviceName = serviceName;
            _serviceType = serviceType;
            _publisherPort = publisherPort;
            _subscriberPort = subscriberPort;
            _verbose = verbose;
            _executionContext = executionContext ?? new PokerGame.Core.Messaging.ExecutionContext();
            _cancellationTokenSource = _executionContext.CancellationTokenSource ?? new CancellationTokenSource();
            _logger = new Logger($"{serviceType}_{_serviceId.Substring(0, 8)}", verbose);
            
            _logger.Log($"Created {_serviceType} service '{_serviceName}' with ID {_serviceId}");
        }
        
        /// <summary>
        /// Starts the service
        /// </summary>
        public virtual void Start()
        {
            try
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(SimpleServiceBase));
                }
                
                _logger.Log($"Starting {_serviceType} service '{_serviceName}'...");
                
                // Create and start the message broker
                _messageBroker = new SimpleMessageBroker(_serviceId, _publisherPort, _subscriberPort, _executionContext, _verbose);
                _messageBroker.MessageReceived += OnMessageReceived;
                _messageBroker.Start();
                
                // Start the background task using the execution context if available
                if (_executionContext.TaskScheduler != null)
                {
                    _backgroundTask = Task.Factory.StartNew(
                        () => BackgroundLoop(_cancellationTokenSource.Token),
                        _cancellationTokenSource.Token,
                        TaskCreationOptions.LongRunning,
                        _executionContext.TaskScheduler);
                }
                else
                {
                    _backgroundTask = Task.Run(() => BackgroundLoop(_cancellationTokenSource.Token));
                }
                
                // Register the service
                RegisterService();
                
                _logger.Log($"{_serviceType} service '{_serviceName}' started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting {_serviceType} service '{_serviceName}'", ex);
                throw;
            }
        }
        
        /// <summary>
        /// Stops the service
        /// </summary>
        public virtual void Stop()
        {
            if (_disposed)
                return;
                
            _logger.Log($"Stopping {_serviceType} service '{_serviceName}'...");
            
            try
            {
                // Cancel the background task
                _cancellationTokenSource.Cancel();
                
                // Wait for the background task to complete
                if (_backgroundTask != null)
                {
                    _backgroundTask.Wait(1000);
                }
                
                // Stop the message broker
                if (_messageBroker != null)
                {
                    _messageBroker.MessageReceived -= OnMessageReceived;
                    _messageBroker.Stop();
                }
                
                _logger.Log($"{_serviceType} service '{_serviceName}' stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping {_serviceType} service '{_serviceName}'", ex);
            }
        }
        
        /// <summary>
        /// Disposes the service
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
                
            _disposed = true;
            
            try
            {
                Stop();
                
                // Dispose the message broker
                _messageBroker?.Dispose();
                _messageBroker = null;
                
                // Dispose the cancellation token source
                _cancellationTokenSource.Dispose();
                
                _logger.Log($"{_serviceType} service '{_serviceName}' disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error disposing {_serviceType} service '{_serviceName}'", ex);
            }
        }
        
        /// <summary>
        /// Publishes a message
        /// </summary>
        /// <param name="message">The message to publish</param>
        protected void PublishMessage(SimpleMessage message)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SimpleServiceBase));
            }
            
            try
            {
                if (_messageBroker == null)
                {
                    _logger.LogError("Cannot publish message: message broker is null");
                    return;
                }
                
                // Set the sender ID
                message.SenderId = _serviceId;
                
                // Publish the message
                _messageBroker.Publish(message);
                
                if (_verbose)
                {
                    _logger.Log($"Published message of type {message.Type}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error publishing message", ex);
            }
        }
        
        /// <summary>
        /// Background loop for the service
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to stop the loop</param>
        private void BackgroundLoop(CancellationToken cancellationToken)
        {
            try
            {
                _logger.Log($"Background task started for {_serviceType} service '{_serviceName}'");
                
                // Initialize the heartbeat timer
                var lastHeartbeat = DateTime.UtcNow;
                var heartbeatInterval = TimeSpan.FromSeconds(5);
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Send heartbeat if needed
                        var now = DateTime.UtcNow;
                        if (now - lastHeartbeat >= heartbeatInterval)
                        {
                            SendHeartbeat();
                            lastHeartbeat = now;
                        }
                        
                        // Run service-specific background tasks
                        OnBackgroundTick();
                        
                        // Sleep to avoid busy waiting
                        Thread.Sleep(100);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error in background loop", ex);
                        Thread.Sleep(1000); // Sleep longer after an error
                    }
                }
                
                _logger.Log($"Background task stopped for {_serviceType} service '{_serviceName}'");
            }
            catch (Exception ex)
            {
                _logger.LogError("Background task terminated with error", ex);
            }
        }
        
        /// <summary>
        /// Sends a heartbeat message
        /// </summary>
        private void SendHeartbeat()
        {
            try
            {
                var heartbeatMessage = SimpleMessage.Create(SimpleMessageType.Heartbeat);
                PublishMessage(heartbeatMessage);
                
                if (_verbose)
                {
                    _logger.Log("Sent heartbeat message");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error sending heartbeat", ex);
            }
        }
        
        /// <summary>
        /// Registers this service with other services
        /// </summary>
        private void RegisterService()
        {
            try
            {
                // Create the registration payload
                var payload = new ServiceRegistrationPayload
                {
                    ServiceId = _serviceId,
                    ServiceName = _serviceName,
                    ServiceType = _serviceType,
                    PublisherPort = _publisherPort,
                    SubscriberPort = _subscriberPort
                };
                
                // Create and publish the registration message
                var registrationMessage = SimpleMessage.Create(SimpleMessageType.ServiceRegistration, payload);
                PublishMessage(registrationMessage);
                
                _logger.Log($"Registered {_serviceType} service '{_serviceName}'");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error registering service", ex);
            }
        }
        
        /// <summary>
        /// Handles received messages
        /// </summary>
        /// <param name="sender">The sender of the event</param>
        /// <param name="e">The event arguments</param>
        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                var message = e.Message;
                
                // Don't skip response messages that are addressed directly to us, even if we're the sender
                bool isResponseToUs = !string.IsNullOrEmpty(message.InResponseTo) && message.ReceiverId == _serviceId;
                
                // Skip our own messages that aren't responses to us
                if (message.SenderId == _serviceId && !isResponseToUs)
                {
                    if (_verbose)
                    {
                        _logger.Log($"Skipping own message: {message.MessageId}");
                    }
                    return;
                }
                
                // If the message has a specific recipient and it's not us, ignore it
                if (!string.IsNullOrEmpty(message.ReceiverId) && message.ReceiverId != _serviceId)
                {
                    return;
                }
                
                // Log the received message
                if (_verbose)
                {
                    _logger.Log($"Received message: Type={message.Type}, From={message.SenderId}, To={message.ReceiverId}, InResponseTo={message.InResponseTo}");
                }
                
                // Handle the message based on its type
                HandleMessage(message);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling received message", ex);
            }
        }
        
        /// <summary>
        /// Called once per tick of the background loop
        /// </summary>
        protected virtual void OnBackgroundTick()
        {
            // Base implementation does nothing
        }
        
        /// <summary>
        /// Handles a received message
        /// </summary>
        /// <param name="message">The message to handle</param>
        protected virtual void HandleMessage(SimpleMessage message)
        {
            switch (message.Type)
            {
                case SimpleMessageType.Heartbeat:
                    // Heartbeats don't need a response
                    break;
                    
                case SimpleMessageType.ServiceRegistration:
                    // Handle service registration
                    var registrationPayload = message.GetPayload<ServiceRegistrationPayload>();
                    if (registrationPayload != null)
                    {
                        HandleServiceRegistration(registrationPayload, message);
                    }
                    break;
                    
                case SimpleMessageType.Acknowledgment:
                    // Handle acknowledgment
                    HandleAcknowledgment(message);
                    break;
                    
                case SimpleMessageType.Error:
                    // Handle error
                    var errorPayload = message.GetPayload<ErrorPayload>();
                    if (errorPayload != null)
                    {
                        _logger.LogError($"Received error message: {errorPayload.ErrorMessage}");
                    }
                    break;
                    
                default:
                    // Handle other message types in derived classes
                    break;
            }
        }
        
        /// <summary>
        /// Handles a service registration message
        /// </summary>
        /// <param name="payload">The registration payload</param>
        /// <param name="message">The original message</param>
        protected virtual void HandleServiceRegistration(ServiceRegistrationPayload payload, SimpleMessage message)
        {
            _logger.Log($"Registered service: {payload.ServiceName} (ID: {payload.ServiceId}, Type: {payload.ServiceType})");
            
            // Send an acknowledgment
            var acknowledgment = SimpleMessage.CreateAcknowledgment(message);
            PublishMessage(acknowledgment);
        }
        
        /// <summary>
        /// Handles an acknowledgment message
        /// </summary>
        /// <param name="message">The acknowledgment message</param>
        protected virtual void HandleAcknowledgment(SimpleMessage message)
        {
            if (string.IsNullOrEmpty(message.InResponseTo))
            {
                _logger.LogWarning($"Received acknowledgment with empty InResponseTo field, messageId: {message.MessageId}");
                return;
            }
            
            // Always acknowledge the acknowledgment message itself to prevent retries
            var ackAck = SimpleMessage.CreateAcknowledgment(message);
            PublishMessage(ackAck);
            
            _logger.Log($"Received acknowledgment for message {message.InResponseTo} and sent ack response");
        }
    }
    
    // This class has been moved to MessageTypes.cs
}