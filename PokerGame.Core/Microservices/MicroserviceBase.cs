using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Base class for all microservices
    /// </summary>
    public abstract class MicroserviceBase : IDisposable
    {
        private readonly string _serviceId;
        private readonly string _serviceName;
        private readonly string _serviceType;
        
        /// <summary>
        /// Gets the unique ID of this service
        /// </summary>
        public string ServiceId => _serviceId;
        
        private PublisherSocket? _publisherSocket;
        private SubscriberSocket? _subscriberSocket;
        private readonly ConcurrentQueue<Message> _messageQueue = new ConcurrentQueue<Message>();
        
        private CancellationTokenSource? _cancellationTokenSource = new CancellationTokenSource();
        private Task? _processingTask;
        private Task? _heartbeatTask;
        
        private readonly ConcurrentDictionary<string, string> _serviceRegistry = new ConcurrentDictionary<string, string>();
        
        // Configuration options
        private readonly int _heartbeatIntervalMs;
        
        /// <summary>
        /// Creates a new microservice instance
        /// </summary>
        /// <param name="serviceType">The type of service</param>
        /// <param name="serviceName">The human-readable name of the service</param>
        /// <param name="publisherPort">The port to use for publishing messages</param>
        /// <param name="subscriberPort">The port to use for subscribing to messages</param>
        /// <param name="heartbeatIntervalMs">The interval between heartbeats in milliseconds</param>
        protected MicroserviceBase(
            string serviceType, 
            string serviceName, 
            int publisherPort, 
            int subscriberPort,
            int heartbeatIntervalMs = 5000)
        {
            _serviceId = Guid.NewGuid().ToString();
            _serviceName = serviceName;
            _serviceType = serviceType;
            _heartbeatIntervalMs = heartbeatIntervalMs;
            
            // Set up the publisher socket with retry logic
            int maxRetries = 3;
            int currentRetry = 0;
            bool publisherBound = false;
            
            while (!publisherBound && currentRetry < maxRetries)
            {
                try
                {
                    // Try to create and bind the publisher socket
                    _publisherSocket = new PublisherSocket();
                    _publisherSocket.Options.SendHighWatermark = 1000;
                    _publisherSocket.Bind($"tcp://127.0.0.1:{publisherPort}");
                    publisherBound = true;
                    Console.WriteLine($"Successfully bound publisher socket on port {publisherPort}");
                }
                catch (NetMQ.AddressAlreadyInUseException)
                {
                    currentRetry++;
                    
                    // Dispose of failed socket attempt
                    if (_publisherSocket != null)
                    {
                        _publisherSocket.Dispose();
                        _publisherSocket = null;
                    }
                    
                    if (currentRetry < maxRetries)
                    {
                        Console.WriteLine($"Port {publisherPort} already in use, retrying with port {publisherPort + currentRetry}");
                        publisherPort += currentRetry;
                        Thread.Sleep(500); // Give time for potential cleanup
                    }
                    else
                    {
                        Console.WriteLine($"Failed to bind publisher after {maxRetries} attempts");
                        throw; // Rethrow after max retries
                    }
                }
            }
            
            // Set up the subscriber socket (for receiving messages)
            _subscriberSocket = new SubscriberSocket();
            _subscriberSocket.Options.ReceiveHighWatermark = 1000;
            _subscriberSocket.Connect($"tcp://127.0.0.1:{subscriberPort}");
            
            // Subscribe to all messages
            _subscriberSocket.Subscribe("");
            
            Console.WriteLine($"{_serviceName} ({_serviceType}) started with ID: {_serviceId}");
        }
        
        /// <summary>
        /// Starts the microservice
        /// </summary>
        public virtual void Start()
        {
            // Start the message processing task
            _processingTask = Task.Run(ProcessMessagesAsync, _cancellationTokenSource.Token);
            
            // Start the heartbeat task
            _heartbeatTask = Task.Run(SendHeartbeatAsync, _cancellationTokenSource.Token);
            
            // Register this service with others
            RegisterService();
        }
        
        /// <summary>
        /// Stops the microservice
        /// </summary>
        public virtual void Stop()
        {
            _cancellationTokenSource.Cancel();
            
            try
            {
                var tasks = new List<Task>();
                if (_processingTask != null) tasks.Add(_processingTask);
                if (_heartbeatTask != null) tasks.Add(_heartbeatTask);
                
                if (tasks.Count > 0)
                {
                    Task.WaitAll(tasks.ToArray(), 5000);
                }
            }
            catch (AggregateException)
            {
                // Tasks may throw exceptions when canceled
            }
            
            Dispose();
        }
        
        /// <summary>
        /// Disposes of resources used by the microservice
        /// </summary>
        public virtual void Dispose()
        {
            try 
            {
                // Close sockets properly before disposing
                _publisherSocket?.Close();
                _subscriberSocket?.Close();
                
                // Give sockets time to close
                Thread.Sleep(100);
                
                // Now dispose resources
                _publisherSocket?.Dispose();
                _subscriberSocket?.Dispose();
                _cancellationTokenSource?.Dispose();
                
                // Force a cleanup to release ports
                NetMQConfig.Cleanup(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during microservice disposal: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sends a message to all microservices
        /// </summary>
        /// <param name="message">The message to send</param>
        protected internal virtual void Broadcast(Message message)
        {
            message.SenderId = _serviceId;
            _publisherSocket?.SendFrame(message.ToJson());
        }
        
        /// <summary>
        /// Sends a message to a specific microservice
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="receiverId">The ID of the receiving service</param>
        protected internal virtual void SendTo(Message message, string receiverId)
        {
            message.SenderId = _serviceId;
            message.ReceiverId = receiverId;
            _publisherSocket?.SendFrame(message.ToJson());
        }
        
        /// <summary>
        /// Registers this service with other services
        /// </summary>
        private void RegisterService()
        {
            PublishServiceRegistration();
        }
        
        /// <summary>
        /// Manually publish this service's registration to other services
        /// </summary>
        public void PublishServiceRegistration()
        {
            var payload = new ServiceRegistrationPayload
            {
                ServiceId = _serviceId,
                ServiceName = _serviceName,
                ServiceType = _serviceType,
                Endpoint = "tcp://127.0.0.1", // Base endpoint, actual port is in specific implementations
                Capabilities = GetServiceCapabilities()
            };
            
            var message = Message.Create(MessageType.ServiceRegistration, payload);
            Broadcast(message);
        }
        
        /// <summary>
        /// Sends heartbeat messages at regular intervals
        /// </summary>
        private async Task SendHeartbeatAsync()
        {
            // Give services time to initialize
            await Task.Delay(1000);
            
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var heartbeatMessage = Message.Create(MessageType.Heartbeat);
                    Broadcast(heartbeatMessage);
                    
                    // Use a larger delay to reduce error messages during development
                    await Task.Delay(10000, _cancellationTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    // Normal cancellation
                    break;
                }
                catch (Exception ex)
                {
                    // Log and continue, sleep a bit longer to avoid spamming logs
                    Console.WriteLine($"Error in heartbeat: {ex.Message}");
                    await Task.Delay(2000);
                }
            }
        }
        
        /// <summary>
        /// Continuously processes incoming messages
        /// </summary>
        private async Task ProcessMessagesAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // Check for incoming messages
                    if (_subscriberSocket != null && _subscriberSocket.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out string? messageJson) && messageJson != null)
                    {
                        try 
                        {
                            var message = Message.FromJson(messageJson);
                            
                            // Skip our own messages
                            if (message.SenderId == _serviceId)
                                continue;
                                
                            // Process messages addressed to everyone or specifically to us
                            if (string.IsNullOrEmpty(message.ReceiverId) || message.ReceiverId == _serviceId)
                            {
                                _messageQueue.Enqueue(message);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing message: {ex.Message}");
                        }
                    }
                    
                    // Process any queued messages
                    while (_messageQueue.TryDequeue(out Message? message) && message != null)
                    {
                        switch (message.Type)
                        {
                            case MessageType.ServiceRegistration:
                                ProcessServiceRegistration(message);
                                break;
                                
                            case MessageType.Heartbeat:
                                // Process heartbeat if needed
                                break;
                                
                            default:
                                // Let the derived class handle other message types
                                await HandleMessageAsync(message);
                                break;
                        }
                    }
                    
                    // Allow the service to do its own processing
                    await DoWorkAsync();
                    
                    // Small delay to prevent CPU overuse
                    await Task.Delay(10, _cancellationTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    // Normal cancellation
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing messages: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Processes a service registration message
        /// </summary>
        /// <param name="message">The registration message</param>
        private void ProcessServiceRegistration(Message message)
        {
            var payload = message.GetPayload<ServiceRegistrationPayload>();
            
            if (payload != null)
            {
                _serviceRegistry[payload.ServiceId] = payload.ServiceType;
                Console.WriteLine($"Registered service: {payload.ServiceName} ({payload.ServiceType})");
                
                // Let derived classes know about the registration
                OnServiceRegistered(payload);
            }
        }
        
        /// <summary>
        /// Gets a list of service types from the registry
        /// </summary>
        /// <param name="serviceType">The type of service to find</param>
        /// <returns>A list of service IDs of the specified type</returns>
        protected List<string> GetServicesOfType(string serviceType)
        {
            var result = new List<string>();
            
            foreach (var entry in _serviceRegistry)
            {
                if (entry.Value == serviceType)
                {
                    result.Add(entry.Key);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Gets the capabilities of this service
        /// </summary>
        /// <returns>A list of capability identifiers</returns>
        protected virtual List<string> GetServiceCapabilities()
        {
            return new List<string>();
        }
        
        /// <summary>
        /// Performs service-specific processing when a new service is registered
        /// </summary>
        /// <param name="registrationInfo">The registration information</param>
        protected virtual void OnServiceRegistered(ServiceRegistrationPayload registrationInfo)
        {
            // Base implementation does nothing
        }
        
        /// <summary>
        /// Handles a message received from another service
        /// </summary>
        /// <param name="message">The message to handle</param>
        protected internal virtual Task HandleMessageAsync(Message message)
        {
            // Base implementation does nothing
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Performs periodic work specific to this service
        /// </summary>
        protected virtual Task DoWorkAsync()
        {
            // Base implementation does nothing
            return Task.CompletedTask;
        }
    }
}