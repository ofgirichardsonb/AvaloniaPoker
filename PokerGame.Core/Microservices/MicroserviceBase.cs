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
            _processingTask = Task.Run(ProcessMessagesAsync, _cancellationTokenSource?.Token ?? CancellationToken.None);
            
            // Start the heartbeat task
            _heartbeatTask = Task.Run(SendHeartbeatAsync, _cancellationTokenSource?.Token ?? CancellationToken.None);
            
            // Register this service with others
            RegisterService();
        }
        
        /// <summary>
        /// Stops the microservice
        /// </summary>
        public virtual void Stop()
        {
            _cancellationTokenSource?.Cancel();
            
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
                // Cancel all operations first
                if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    _cancellationTokenSource.Cancel();
                }
                
                // Wait for tasks to complete before disposing sockets
                try
                {
                    var tasks = new List<Task>();
                    if (_processingTask != null && !_processingTask.IsCompleted) tasks.Add(_processingTask);
                    if (_heartbeatTask != null && !_heartbeatTask.IsCompleted) tasks.Add(_heartbeatTask);
                    
                    if (tasks.Count > 0)
                    {
                        Console.WriteLine($"Waiting for {tasks.Count} tasks to complete...");
                        Task.WaitAll(tasks.ToArray(), 1000); // Shorter timeout to avoid hanging
                    }
                }
                catch (AggregateException)
                {
                    // Tasks may throw exceptions when canceled - ignore
                    Console.WriteLine("Some tasks threw exceptions during cancellation (expected)");
                }

                // Close sockets properly before disposing
                if (_publisherSocket != null)
                {
                    Console.WriteLine("Closing publisher socket...");
                    _publisherSocket.Close();
                }
                
                if (_subscriberSocket != null)
                {
                    Console.WriteLine("Closing subscriber socket...");
                    _subscriberSocket.Close();
                }
                
                // Short pause to let sockets close cleanly
                Thread.Sleep(100);
                
                // Now dispose resources
                _publisherSocket?.Dispose();
                _subscriberSocket?.Dispose();
                
                // Reset to null after disposal to prevent further use
                _publisherSocket = null;
                _subscriberSocket = null;
                
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                
                // Clear collections
                _messageQueue.Clear();
                _serviceRegistry.Clear();
                
                // Finally, force a NetMQ cleanup
                Console.WriteLine("Performing NetMQ cleanup...");
                NetMQConfig.Cleanup(false);
                
                Console.WriteLine("Microservice disposed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during microservice disposal: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                
                // Last resort cleanup
                try {
                    NetMQConfig.Cleanup(false);
                } catch {}
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
        /// <returns>True if the message was sent successfully</returns>
        protected internal virtual bool SendTo(Message message, string receiverId)
        {
            try
            {
                // Make sure both IDs are set
                if (string.IsNullOrEmpty(_serviceId))
                {
                    Console.WriteLine("Warning: Sender ID is not set.");
                    return false;
                }
                
                if (string.IsNullOrEmpty(receiverId))
                {
                    Console.WriteLine("Warning: Target service ID is not set.");
                    return false;
                }
                
                // Set the sender and target IDs
                message.SenderId = _serviceId;
                message.ReceiverId = receiverId;
                
                // Add a unique message ID if not already present
                if (string.IsNullOrEmpty(message.MessageId))
                {
                    message.MessageId = Guid.NewGuid().ToString();
                }
                
                string serialized = message.ToJson();
                Console.WriteLine($"Sending message type {message.Type} to {receiverId}");
                
                // Use the publisher socket to send the message
                _publisherSocket?.SendFrame(serialized);
                
                return true;
            }
            catch (Exception ex)
            {
                // Log any errors
                Console.WriteLine($"Error sending message: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return false;
            }
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
            
            CancellationToken token = _cancellationTokenSource?.Token ?? CancellationToken.None;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var heartbeatMessage = Message.Create(MessageType.Heartbeat);
                    Broadcast(heartbeatMessage);
                    
                    // Use a larger delay to reduce error messages during development
                    await Task.Delay(10000, _cancellationTokenSource?.Token ?? CancellationToken.None);
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
            Console.WriteLine("Message processing started");
            
            CancellationToken token = _cancellationTokenSource?.Token ?? CancellationToken.None;
            bool isShuttingDown = false;
            
            while (!token.IsCancellationRequested && !isShuttingDown)
            {
                try
                {
                    // Check for cancellation at the beginning of each loop
                    if (token.IsCancellationRequested)
                    {
                        Console.WriteLine("Message processing cancellation detected");
                        break;
                    }
                    
                    // Make sure we still have valid sockets before trying to use them
                    if (_subscriberSocket == null || _publisherSocket == null)
                    {
                        Console.WriteLine("Socket(s) no longer available, terminating message loop");
                        break;
                    }
                    
                    // Check for incoming messages with a very short timeout to avoid blocking
                    if (_subscriberSocket.TryReceiveFrameString(TimeSpan.FromMilliseconds(50), out string? messageJson) && !string.IsNullOrEmpty(messageJson))
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
                        catch (ObjectDisposedException)
                        {
                            // Socket was disposed during receive - break out of the loop
                            Console.WriteLine("Socket was disposed during message processing");
                            isShuttingDown = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            // Continue after message parsing errors
                            Console.WriteLine($"Error processing message: {ex.Message}");
                        }
                    }
                    
                    // Process a limited number of queued messages per iteration
                    int processedCount = 0;
                    const int maxMessagesPerIteration = 10;
                    
                    while (!token.IsCancellationRequested && 
                           processedCount < maxMessagesPerIteration && 
                           _messageQueue.TryDequeue(out Message? message) && 
                           message != null)
                    {
                        try
                        {
                            processedCount++;
                            
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
                        catch (Exception ex)
                        {
                            // Log the error but continue processing other messages
                            Console.WriteLine($"Error handling message type {message.Type}: {ex.Message}");
                        }
                    }
                    
                    // Allow the service to do its own processing if there's still time
                    if (!token.IsCancellationRequested)
                    {
                        try
                        {
                            await DoWorkAsync();
                        }
                        catch (Exception ex)
                        {
                            // Log service-specific errors
                            Console.WriteLine($"Error in service-specific work: {ex.Message}");
                        }
                    }
                    
                    // Very small delay to prevent CPU overuse but still be responsive
                    try
                    {
                        await Task.Delay(5, token);
                    }
                    catch (TaskCanceledException)
                    {
                        // Normal cancellation
                        Console.WriteLine("Message processing loop canceled via Task.Delay");
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation
                    Console.WriteLine("Message processing loop canceled via OperationCanceledException");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // Socket was disposed - break out of the loop
                    Console.WriteLine("Socket was disposed during processing");
                    break;
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested)
                    {
                        Console.WriteLine("Caught exception during shutdown, terminating loop");
                        break;
                    }
                    
                    // Log other errors - try to continue unless this is a fatal or repeating error
                    Console.WriteLine($"Error in message processing loop: {ex.Message}");
                    
                    // Brief pause to avoid tight error loop
                    await Task.Delay(100);
                }
            }
            
            Console.WriteLine("Message processing loop terminated");
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