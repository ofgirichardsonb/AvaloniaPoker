using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using PokerGame.Core.Telemetry;

// Suppress obsolete warnings for transition period
#pragma warning disable CS0619 // Type or member is obsolete

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// A central message broker that coordinates communication between services
    /// </summary>
    public class CentralMessageBroker : IDisposable
    {
        private readonly ExecutionContext _executionContext;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ConcurrentDictionary<string, Action<NetworkMessage>> _subscribers = new ConcurrentDictionary<string, Action<NetworkMessage>>();
        private readonly ConcurrentDictionary<string, ServiceInfo> _services = new ConcurrentDictionary<string, ServiceInfo>();
        private readonly ConcurrentQueue<NetworkMessage> _messageQueue = new ConcurrentQueue<NetworkMessage>();
        private Task? _processingTask;
        private bool _isStarted;
        private bool _isDisposed;
        private readonly Logger _logger;
        private readonly object _startLock = new object();
        private object? _telemetryHandler;
        
        private readonly int _port;
        private readonly bool _verbose;
        
        /// <summary>
        /// Gets the port used for publishing messages
        /// </summary>
        public int PublisherPort => _port;
        
        /// <summary>
        /// Gets the port used for subscribing to messages (publisher port + 1)
        /// </summary>
        public int SubscriberPort => _port + 1;
        
        /// <summary>
        /// Creates a new central message broker
        /// </summary>
        /// <param name="executionContext">The execution context to use</param>
        public CentralMessageBroker(ExecutionContext executionContext)
            : this(executionContext, 25555, false)
        {
        }
        
        /// <summary>
        /// Creates a new central message broker with specified port and verbosity
        /// </summary>
        /// <param name="executionContext">The execution context to use</param>
        /// <param name="port">The port number to use for communication</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        public CentralMessageBroker(ExecutionContext executionContext, int port, bool verbose = false)
        {
            _executionContext = executionContext ?? new ExecutionContext();
            _cancellationTokenSource = _executionContext.CancellationTokenSource ?? new CancellationTokenSource();
            _port = port;
            _verbose = verbose;
            _logger = new Logger("CentralMessageBroker", _verbose);
            _logger.Log($"Central message broker created on port {_port}");
        }
        
        /// <summary>
        /// Starts the message broker
        /// </summary>
        public void Start()
        {
            lock (_startLock)
            {
                if (_isStarted || _isDisposed)
                    return;
                
                _logger.Log("Starting central message broker");
                
                // Start the message processing task using the execution context if available
                if (_executionContext.TaskScheduler != null)
                {
                    _processingTask = Task.Factory.StartNew(
                        () => ProcessMessages(_cancellationTokenSource.Token),
                        _cancellationTokenSource.Token,
                        TaskCreationOptions.LongRunning,
                        _executionContext.TaskScheduler);
                }
                else
                {
                    _processingTask = Task.Run(() => ProcessMessages(_cancellationTokenSource.Token));
                }
                
                _isStarted = true;
                _logger.Log("Central message broker started");
            }
        }
        
        /// <summary>
        /// Stops the message broker
        /// </summary>
        public void Stop()
        {
            lock (_startLock)
            {
                if (!_isStarted || _isDisposed)
                    return;
                
                _logger.Log("Stopping central message broker");
                
                // We don't cancel the token here because it might be shared with other components
                // If we need to stop just this broker, we should create a linked token source
                
                _isStarted = false;
                _logger.Log("Central message broker stopped");
            }
        }
        
        /// <summary>
        /// Sets the telemetry handler for the broker
        /// </summary>
        /// <param name="telemetryHandler">The telemetry handler</param>
        public void SetTelemetryHandler(object telemetryHandler)
        {
            _telemetryHandler = telemetryHandler;
        }
        
        /// <summary>
        /// Subscribes to messages of a specific type
        /// </summary>
        /// <param name="messageType">The message type to subscribe to</param>
        /// <param name="handler">The handler to invoke when a message of this type is received</param>
        /// <returns>A subscription ID that can be used to unsubscribe</returns>
        public string Subscribe(MessageType messageType, Action<NetworkMessage> handler)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CentralMessageBroker));
                
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
                
            string subscriptionId = Guid.NewGuid().ToString();
            string key = $"{messageType}:{subscriptionId}";
            
            _subscribers.TryAdd(key, handler);
            _logger.Log($"Subscribed to message type {messageType} with ID {subscriptionId}");
            
            return subscriptionId;
        }
        
        /// <summary>
        /// Subscribes to messages of a specific type (legacy method)
        /// </summary>
        /// <param name="messageType">The message type to subscribe to</param>
        /// <param name="handler">The handler to invoke when a message of this type is received</param>
        /// <returns>A subscription ID that can be used to unsubscribe</returns>
        [Obsolete("Use Subscribe(MessageType, Action<NetworkMessage>) instead")]
        public string Subscribe(SimpleMessageType messageType, Action<SimpleMessage> handler)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CentralMessageBroker));
                
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
                
            // Convert the SimpleMessageType to MessageType
            var newMessageType = MessageAdapter.ToMessageType(messageType);
            
            // Create a wrapper that converts NetworkMessage to SimpleMessage before calling the handler
            Action<NetworkMessage> wrapperHandler = networkMessage => 
            {
                var simpleMessage = networkMessage.ToSimpleMessage();
                handler(simpleMessage);
            };
            
            return Subscribe(newMessageType, wrapperHandler);
        }
        
        /// <summary>
        /// Unsubscribes from messages
        /// </summary>
        /// <param name="subscriptionId">The subscription ID returned from Subscribe</param>
        /// <param name="messageType">The message type to unsubscribe from</param>
        public void Unsubscribe(string subscriptionId, MessageType messageType)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CentralMessageBroker));
                
            string key = $"{messageType}:{subscriptionId}";
            
            if (_subscribers.TryRemove(key, out _))
            {
                _logger.Log($"Unsubscribed from message type {messageType} with ID {subscriptionId}");
            }
        }
        
        /// <summary>
        /// Unsubscribes from messages (legacy method)
        /// </summary>
        /// <param name="subscriptionId">The subscription ID returned from Subscribe</param>
        /// <param name="messageType">The message type to unsubscribe from</param>
        [Obsolete("Use Unsubscribe(string, MessageType) instead")]
        public void Unsubscribe(string subscriptionId, SimpleMessageType messageType)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CentralMessageBroker));
                
            // Convert the SimpleMessageType to MessageType
            var newMessageType = MessageAdapter.ToMessageType(messageType);
            Unsubscribe(subscriptionId, newMessageType);
        }
        
        /// <summary>
        /// Publishes a message to all subscribers
        /// </summary>
        /// <param name="message">The message to publish</param>
        public void Publish(NetworkMessage message)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CentralMessageBroker));
                
            if (message == null)
                throw new ArgumentNullException(nameof(message));
                
            // Set the timestamp if it's not already set
            if (message.Timestamp == default)
            {
                message.Timestamp = DateTime.UtcNow;
            }
            
            // Enqueue the message for processing
            _messageQueue.Enqueue(message);
            _logger.Log($"Enqueued message of type {message.Type} from {message.SenderId} to {message.ReceiverId ?? "all"}");
        }
        
        /// <summary>
        /// Publishes a message to all subscribers (legacy method)
        /// </summary>
        /// <param name="message">The message to publish</param>
        [Obsolete("Use Publish(NetworkMessage) instead")]
        public void Publish(SimpleMessage message)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CentralMessageBroker));
                
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            
            // Convert the SimpleMessage to a NetworkMessage
            var networkMessage = message.ToNetworkMessage();
            Publish(networkMessage);
        }
        
        /// <summary>
        /// Registers a service with the broker
        /// </summary>
        /// <param name="serviceId">The unique ID of the service</param>
        /// <param name="serviceName">The human-readable name of the service</param>
        /// <param name="serviceType">The type of the service</param>
        public void RegisterService(string serviceId, string serviceName, string serviceType)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CentralMessageBroker));
                
            var info = new ServiceInfo
            {
                ServiceId = serviceId,
                ServiceName = serviceName,
                ServiceType = serviceType,
                LastSeen = DateTime.UtcNow
            };
            
            _services.AddOrUpdate(serviceId, info, (_, _) => info);
            _logger.Log($"Registered service {serviceName} ({serviceType}) with ID {serviceId}");
        }
        
        /// <summary>
        /// Gets information about a registered service
        /// </summary>
        /// <param name="serviceId">The unique ID of the service</param>
        /// <returns>The service information, or null if not found</returns>
        public ServiceInfo? GetServiceInfo(string serviceId)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CentralMessageBroker));
                
            if (_services.TryGetValue(serviceId, out var info))
            {
                return info;
            }
            
            return null;
        }
        
        /// <summary>
        /// Gets information about all registered services
        /// </summary>
        /// <returns>A list of all registered services</returns>
        public List<ServiceInfo> GetAllServiceInfo()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CentralMessageBroker));
                
            return new List<ServiceInfo>(_services.Values);
        }
        
        /// <summary>
        /// Processes messages in the queue
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to stop processing</param>
        private void ProcessMessages(CancellationToken cancellationToken)
        {
            _logger.Log("Message processing task started");
            
            try
            {
                while (!cancellationToken.IsCancellationRequested && !_isDisposed)
                {
                    // Process all messages in the queue
                    while (_messageQueue.TryDequeue(out var message))
                    {
                        try
                        {
                            // Update the last seen timestamp for the sender
                            if (!string.IsNullOrEmpty(message.SenderId) && 
                                _services.TryGetValue(message.SenderId, out var info))
                            {
                                info.LastSeen = DateTime.UtcNow;
                            }
                            
                            // Handle service registration messages specially
                            if (message.Type == MessageType.ServiceRegistration)
                            {
                                try
                                {
                                    var payload = message.GetPayload<ServiceRegistrationPayload>();
                                    if (payload != null)
                                    {
                                        RegisterService(
                                            payload.ServiceId, 
                                            payload.ServiceName, 
                                            payload.ServiceType);
                                            
                                        Console.WriteLine($"Successfully registered service {payload.ServiceName} ({payload.ServiceType}) with ID {payload.ServiceId}");
                                    }
                                    else
                                    {
                                        Console.WriteLine("WARNING: Service registration payload was null");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Try to extract service ID from message metadata if payload deserialization fails
                                    string serviceId = message.SenderId ?? "unknown";
                                    Console.WriteLine($"Error processing service registration from {serviceId}: {ex.Message}");
                                    
                                    // Try to register with limited information from message metadata
                                    if (!string.IsNullOrEmpty(serviceId))
                                    {
                                        RegisterService(serviceId, $"Service-{serviceId}", "Unknown");
                                        Console.WriteLine($"Registered service with limited metadata: {serviceId}");
                                    }
                                }
                            }
                            
                            // Find all subscribers for this message type
                            foreach (var subscriber in _subscribers)
                            {
                                // Parse the key to get the message type
                                string[] parts = subscriber.Key.Split(':');
                                if (parts.Length == 2 && Enum.TryParse<MessageType>(parts[0], out var type))
                                {
                                    // If the subscriber is for this message type, invoke the handler
                                    if (type == message.Type)
                                    {
                                        try
                                        {
                                            subscriber.Value(message);
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError($"Error in message handler: {ex.Message}", ex);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error processing message: {ex.Message}", ex);
                        }
                    }
                    
                    // Sleep to avoid busy waiting
                    Thread.Sleep(10);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Log("Message processing task cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in message processing task: {ex.Message}", ex);
            }
            
            _logger.Log("Message processing task stopped");
        }
        
        /// <summary>
        /// Disposes the message broker
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;
                
            _isDisposed = true;
            
            // Stop the broker
            Stop();
            
            // Clear all collections
            _subscribers.Clear();
            _services.Clear();
            
            while (_messageQueue.TryDequeue(out _))
            {
                // Empty the queue
            }
            
            _logger.Log("Central message broker disposed");
        }
    }
    
    /// <summary>
    /// Information about a registered service
    /// </summary>
    public class ServiceInfo
    {
        /// <summary>
        /// The unique ID of the service
        /// </summary>
        public string ServiceId { get; set; } = string.Empty;
        
        /// <summary>
        /// The human-readable name of the service
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;
        
        /// <summary>
        /// The type of the service
        /// </summary>
        public string ServiceType { get; set; } = string.Empty;
        
        /// <summary>
        /// When the service was last seen (sent a message)
        /// </summary>
        public DateTime LastSeen { get; set; }
    }
}