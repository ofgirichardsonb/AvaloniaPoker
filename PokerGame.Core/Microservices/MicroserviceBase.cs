using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using PokerGame.Core.ServiceManagement;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Base class for all microservices
    /// </summary>
    public abstract class MicroserviceBase : IDisposable
    {
        protected readonly string _serviceId;
        protected readonly string _serviceName;
        protected readonly string _serviceType;
        
        /// <summary>
        /// Gets the unique ID of this service
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
        
        protected PublisherSocket? _publisherSocket;
        protected SubscriberSocket? _subscriberSocket;
        protected readonly ConcurrentQueue<Message> _messageQueue = new ConcurrentQueue<Message>();
        
        protected CancellationTokenSource? _cancellationTokenSource = new CancellationTokenSource();
        private Task? _processingTask;
        private Task? _heartbeatTask;
        
        private readonly ConcurrentDictionary<string, string> _serviceRegistry = new ConcurrentDictionary<string, string>();
        
        // Configuration options
        private readonly int _heartbeatIntervalMs;
        protected readonly int _publisherPort;
        protected readonly int _subscriberPort;
        
        /// <summary>
        /// Gets the service registry showing service ID to service type mappings
        /// </summary>
        protected Dictionary<string, string> GetServiceRegistry()
        {
            return _serviceRegistry.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
                
        /// <summary>
        /// Creates a new microservice instance with an execution context
        /// </summary>
        /// <param name="serviceType">The type of service</param>
        /// <param name="serviceName">The human-readable name of the service</param>
        /// <param name="executionContext">The execution context to use</param>
        /// <param name="heartbeatIntervalMs">The interval between heartbeats in milliseconds</param>
        protected MicroserviceBase(
            string serviceType, 
            string serviceName, 
            Messaging.ExecutionContext executionContext,
            int heartbeatIntervalMs = 5000)
        {
            // Assign static IDs based on service type for more reliable direct connections
            if (serviceType == ServiceConstants.ServiceTypes.GameEngine)
            {
                _serviceId = ServiceConstants.StaticServiceIds.GameEngine;
            }
            else if (serviceType == ServiceConstants.ServiceTypes.CardDeck)
            {
                _serviceId = ServiceConstants.StaticServiceIds.CardDeck;
            }
            else if (serviceType == ServiceConstants.ServiceTypes.ConsoleUI)
            {
                _serviceId = ServiceConstants.StaticServiceIds.ConsoleUI;
            }
            else
            {
                // Fallback to dynamic ID for any new service types
                _serviceId = Guid.NewGuid().ToString();
            }
            
            _serviceName = serviceName;
            _serviceType = serviceType;
            _heartbeatIntervalMs = heartbeatIntervalMs;
            _publisherPort = 0; // Not used in this constructor but initialized for completeness
            _subscriberPort = 0; // Not used in this constructor but initialized for completeness
            
            // Use the provided execution context to set up messaging
            // The broker will handle the actual communication
            Console.WriteLine($"Creating microservice {serviceName} ({serviceType}) with execution context");
            Console.WriteLine($"Using STATIC SERVICE ID: {_serviceId}");
        }
        
        /// <summary>
        /// Creates a new microservice instance with ports for backward compatibility
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
            // Assign static IDs based on service type for more reliable direct connections
            if (serviceType == ServiceConstants.ServiceTypes.GameEngine)
            {
                _serviceId = ServiceConstants.StaticServiceIds.GameEngine;
            }
            else if (serviceType == ServiceConstants.ServiceTypes.CardDeck)
            {
                _serviceId = ServiceConstants.StaticServiceIds.CardDeck;
            }
            else if (serviceType == ServiceConstants.ServiceTypes.ConsoleUI)
            {
                _serviceId = ServiceConstants.StaticServiceIds.ConsoleUI;
            }
            else
            {
                // Fallback to dynamic ID for any new service types
                _serviceId = Guid.NewGuid().ToString();
            }
            
            _serviceName = serviceName;
            _serviceType = serviceType;
            _heartbeatIntervalMs = heartbeatIntervalMs;
            _publisherPort = publisherPort;
            _subscriberPort = subscriberPort;
            
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
            
            Console.WriteLine($"{_serviceName} ({_serviceType}) started with STATIC ID: {_serviceId}");
        }
        
        /// <summary>
        /// Starts the microservice
        /// </summary>
        public virtual void Start()
        {
            Console.WriteLine($"====> [{_serviceType} {_serviceId}] Starting microservice: {_serviceName}");
            
            // Start the message processing task
            _processingTask = Task.Run(ProcessMessagesAsync, _cancellationTokenSource?.Token ?? CancellationToken.None);
            
            // Give time for processing task to initialize
            Thread.Sleep(300);
            
            // Register this service with others - do this before starting heartbeat
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Sending service registration broadcast (attempt {i+1}/3)");
                RegisterService();
                Thread.Sleep(100); // Small delay between registration attempts
            }
            
            // Start the heartbeat task after registration to ensure service is visible
            _heartbeatTask = Task.Run(SendHeartbeatAsync, _cancellationTokenSource?.Token ?? CancellationToken.None);
            
            Console.WriteLine($"====> [{_serviceType} {_serviceId}] Microservice started successfully");
        }
        
        /// <summary>
        /// Starts the microservice asynchronously
        /// </summary>
        public virtual async Task StartAsync()
        {
            Console.WriteLine($"====> [{_serviceType} {_serviceId}] Starting microservice asynchronously: {_serviceName}");
            
            // Start using the synchronous method
            Start();
            
            // Give a longer delay to allow full initialization and multiple registration broadcasts
            await Task.Delay(500);
            
            // Send one more registration after everything is started
            RegisterService();
            
            // Send multiple registrations with increasing delays for improved reliability
            Console.WriteLine($"====> [{_serviceType} {_serviceId}] Enhanced service discovery: sending multiple registrations");
            Task.Run(async () => {
                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(100 * (i + 1));
                    PublishServiceRegistration();
                }
            });
            
            // Debug our sockets and connections
            Console.WriteLine($"====> [{_serviceType} {_serviceId}] Publisher port: {_publisherPort}, Subscriber port: {_subscriberPort}");
            
            // Broadcast port information to help with debugging
            var portInfoMessage = Message.Create(MessageType.Debug, 
                $"Service {_serviceName} ({_serviceType}) is using publisher port {_publisherPort} and subscriber port {_subscriberPort}");
            portInfoMessage.SenderId = _serviceId;
            Broadcast(portInfoMessage);
            
            Console.WriteLine($"====> [{_serviceType} {_serviceId}] Microservice async start completed");
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
        /// Stops the microservice asynchronously
        /// </summary>
        public virtual async Task StopAsync()
        {
            _cancellationTokenSource?.Cancel();
            
            try
            {
                var tasks = new List<Task>();
                if (_processingTask != null) tasks.Add(_processingTask);
                if (_heartbeatTask != null) tasks.Add(_heartbeatTask);
                
                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks.ToArray()).WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
            catch (AggregateException)
            {
                // Tasks may throw exceptions when canceled
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Warning: StopAsync timed out waiting for tasks to complete");
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
                
                // Schedule NetMQ cleanup instead of doing it directly
                Console.WriteLine("Scheduling NetMQ cleanup...");
                NetMQContextHelper.ScheduleCleanup(200);
                
                Console.WriteLine("Microservice disposed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during microservice disposal: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                
                // Last resort cleanup
                try {
                    // Schedule cleanup rather than doing it directly
                    NetMQContextHelper.ScheduleCleanup(100);
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
                // DEBUG INFO: Log the direct message sending details
                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Direct message send attempt: Type: {message.Type}, ID: {message.MessageId}, Sending to: {receiverId}");
                
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
                
                // Important fix: When sending a message to a specific service,
                // it needs to be broadcast so all services can see it
                // The receiver ID field is used to filter who should process it
                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Broadcasting message {message.Type} to {receiverId} (using broadcast instead of direct send)");
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
                Endpoint = $"tcp://127.0.0.1:{_publisherPort}", // Include the port for better visibility
                Capabilities = GetServiceCapabilities()
            };
            
            Console.WriteLine($"====> [{_serviceType} {_serviceId}] Publishing service registration: {_serviceName} ({_serviceType})");
            
            // Create and broadcast service registration
            var message = Message.Create(MessageType.ServiceRegistration, payload);
            
            // Set a unique message ID for better tracking
            message.MessageId = Guid.NewGuid().ToString();
            
            // Log the registration details
            Console.WriteLine($"====> [{_serviceType} {_serviceId}] Broadcasting registration message ID: {message.MessageId}");
            
            // Broadcast the message
            Broadcast(message);
            
            // Wait briefly to allow broadcast to complete before attempting other communications
            Thread.Sleep(100);
        }
        
        /// <summary>
        /// Sends a targeted service registration directly to a specific service
        /// </summary>
        /// <param name="targetServiceId">The ID of the service to send registration to</param>
        public void SendTargetedRegistrationTo(string targetServiceId)
        {
            try
            {
                if (string.IsNullOrEmpty(targetServiceId))
                {
                    Console.WriteLine("Cannot send targeted registration: target service ID is null or empty");
                    return;
                }
                
                var registrationInfo = new ServiceRegistrationPayload
                {
                    ServiceId = _serviceId,
                    ServiceName = _serviceName,
                    ServiceType = _serviceType,
                    Endpoint = $"tcp://127.0.0.1:{_publisherPort}",
                    Capabilities = GetServiceCapabilities()
                };
                
                var message = Message.Create(MessageType.ServiceRegistration, registrationInfo);
                message.SenderId = _serviceId;
                message.ReceiverId = targetServiceId; // Target specific service
                message.MessageId = Guid.NewGuid().ToString();
                
                // Log this targeted registration
                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Sending targeted registration to service {targetServiceId} with message ID {message.MessageId}");
                
                // Direct send to target - use SendTo instead of SendMessage
                SendTo(message, targetServiceId);
                
                // Wait briefly to allow message to be processed
                Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending targeted registration: {ex.Message}");
            }
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
                    
                    // Make sure we still have valid sockets before trying to use them - attempt to reconnect if needed
                    if (_subscriberSocket == null || _publisherSocket == null)
                    {
                        Console.WriteLine("Socket(s) no longer available, attempting to reconnect...");
                        try
                        {
                            // Try to recreate the publisher socket if necessary
                            if (_publisherSocket == null)
                            {
                                Console.WriteLine($"Attempting to recreate publisher socket on port {_publisherPort}...");
                                _publisherSocket = new PublisherSocket();
                                _publisherSocket.Options.SendHighWatermark = 1000;
                                int publisherPort = _publisherPort > 0 ? _publisherPort : 5559; // Default if not set
                                _publisherSocket.Bind($"tcp://127.0.0.1:{publisherPort}");
                                Console.WriteLine($"Publisher socket recreated successfully on port {publisherPort}");
                            }
                            
                            // Try to recreate the subscriber socket if necessary
                            if (_subscriberSocket == null)
                            {
                                Console.WriteLine($"Attempting to recreate subscriber socket on port {_subscriberPort}...");
                                _subscriberSocket = new SubscriberSocket();
                                _subscriberSocket.Options.ReceiveHighWatermark = 1000;
                                int subscriberPort = _subscriberPort > 0 ? _subscriberPort : 5560; // Default if not set
                                _subscriberSocket.Connect($"tcp://localhost:{subscriberPort}");
                                _subscriberSocket.SubscribeToAnyTopic();
                                Console.WriteLine($"Subscriber socket recreated successfully on port {subscriberPort}");
                            }
                            
                            // Give the sockets a moment to fully initialize
                            await Task.Delay(100, token);
                            Console.WriteLine("Socket reconnection successful, continuing message loop");
                            
                            // Skip to the next iteration to immediately use the new sockets
                            continue;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to recreate sockets: {ex.Message}");
                            
                            // Wait a bit longer before retrying to avoid rapid failures
                            await Task.Delay(1000, token);
                            continue;
                        }
                    }
                    
                    // Check for incoming messages with a very short timeout to avoid blocking
                    if (_subscriberSocket.TryReceiveFrameString(TimeSpan.FromMilliseconds(50), out string? messageJson) && !string.IsNullOrEmpty(messageJson))
                    {
                        try 
                        {
                            Console.WriteLine($"====> [{_serviceType} {_serviceId}] Received raw message: {messageJson.Substring(0, Math.Min(100, messageJson.Length))}...");
                            
                            // Enhanced logging for critical message types
                            if (messageJson.Contains("\"Type\":3") || messageJson.Contains("\"Type\":11"))
                            {
                                Console.WriteLine($"====> CRITICAL MESSAGE [{_serviceType} {_serviceId}] RECEIVED: {messageJson}");
                            }
                            
                            var message = Message.FromJson(messageJson);
                            Console.WriteLine($"====> [{_serviceType} {_serviceId}] Parsed message type: {message.Type}, from: {message.SenderId}, to: {message.ReceiverId ?? "broadcast"}");
                            
                            // Skip our own messages
                            if (message.SenderId == _serviceId)
                            {
                                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Skipping own message: {message.MessageId}");
                                continue;
                            }
                                
                            // VERY IMPORTANT: Special handling for Ping and DeckCreate when we are a CardDeck service
                            // These messages need priority handling
                            if (_serviceType == "CardDeck" && 
                                (message.Type == MessageType.Ping || message.Type == MessageType.DeckCreate) &&
                                (string.IsNullOrEmpty(message.ReceiverId) || message.ReceiverId == _serviceId))
                            {
                                Console.WriteLine($"====> [{_serviceType} {_serviceId}] PRIORITY HANDLING for {message.Type}, ID: {message.MessageId}");
                                
                                // Handle Ping messages immediately with direct acknowledgment
                                var ackMessage = Message.Create(MessageType.Acknowledgment, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                                ackMessage.InResponseTo = message.MessageId;
                                ackMessage.SenderId = _serviceId;
                                ackMessage.ReceiverId = message.SenderId;
                                
                                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Sending IMMEDIATE ACK for {message.Type}, ID: {message.MessageId} to {message.SenderId}");
                                
                                // Broadcast the acknowledgment
                                var serializedAck = ackMessage.ToJson();
                                Console.WriteLine($"====> [{_serviceType} {_serviceId}] SENDING RAW SOCKET FRAME FOR ACK: {serializedAck}");
                                _publisherSocket?.SendFrame(serializedAck);
                                
                                // Try sending duplicate ack with different approach for redundancy
                                try 
                                {
                                    Console.WriteLine($"====> [{_serviceType} {_serviceId}] DIRECT BROADCAST of ACK for {message.MessageId}");
                                    Broadcast(ackMessage);
                                    Console.WriteLine($"====> [{_serviceType} {_serviceId}] DIRECT BROADCAST of ACK COMPLETED");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"====> [{_serviceType} {_serviceId}] ERROR in direct broadcast ACK: {ex.Message}");
                                }
                                
                                // Still queue the message for normal processing
                                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Enqueueing priority message: {message.Type}, ID: {message.MessageId}");
                                _messageQueue.Enqueue(message);
                            }
                            // Process messages addressed to everyone or specifically to us
                            else if (string.IsNullOrEmpty(message.ReceiverId) || message.ReceiverId == _serviceId)
                            {
                                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Enqueueing message: {message.Type}, ID: {message.MessageId}");
                                _messageQueue.Enqueue(message);
                            }
                            else
                            {
                                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Ignoring message addressed to: {message.ReceiverId}");
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
                            
                            // CRITICAL FIX: Handle ping and DeckCreate with highest priority and guaranteed ack
                            if (message.Type == MessageType.Ping || message.Type == MessageType.DeckCreate)
                            {
                                Console.WriteLine($"====> MicroserviceBase for {_serviceType}: CRITICAL MESSAGE HANDLING for {message.Type}");
                                
                                // Always send acknowledgment first for critical messages
                                var ackMessage = Message.Create(MessageType.Acknowledgment, DateTime.UtcNow.ToString("o"));
                                ackMessage.InResponseTo = message.MessageId;
                                ackMessage.SenderId = _serviceId;
                                ackMessage.ReceiverId = message.SenderId;
                                
                                // Multiple redundant acknowledgment methods
                                try 
                                {
                                    // Method 1: Direct socket send
                                    var serializedAck = ackMessage.ToJson();
                                    Console.WriteLine($"====> [{_serviceType} {_serviceId}] CRITICAL ACK 1: Raw socket for {message.Type} {message.MessageId}");
                                    _publisherSocket?.SendFrame(serializedAck);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"====> [{_serviceType} {_serviceId}] ACK error 1: {ex.Message}");
                                }
                                
                                try
                                {
                                    // Method 2: Broadcast 
                                    Console.WriteLine($"====> [{_serviceType} {_serviceId}] CRITICAL ACK 2: Broadcast for {message.Type} {message.MessageId}");
                                    Broadcast(ackMessage);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"====> [{_serviceType} {_serviceId}] ACK error 2: {ex.Message}");
                                }
                            }
                            
                            switch (message.Type)
                            {
                                case MessageType.ServiceRegistration:
                                    ProcessServiceRegistration(message);
                                    break;
                                    
                                case MessageType.Heartbeat:
                                    // Process heartbeat if needed
                                    break;
                                    
                                default:
                                    // Log this message for debugging
                                    Console.WriteLine($"====> MicroserviceBase for {_serviceType}: Delegating message type {message.Type} (ID: {message.MessageId}) to derived class {this.GetType().Name}");
                                    
                                    // Let the derived class handle other message types
                                    await HandleMessageAsync(message);
                                    
                                    Console.WriteLine($"====> MicroserviceBase for {_serviceType}: Completed handling message {message.Type} (ID: {message.MessageId})");
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
                // Add or update the service registry - use ConcurrentDictionary's thread-safe methods
                _serviceRegistry.AddOrUpdate(
                    payload.ServiceId,
                    payload.ServiceType, // Add this value if the key doesn't exist
                    (key, oldValue) => payload.ServiceType // Update to this value if the key exists
                );
                
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
        public virtual Task HandleMessageAsync(Message message)
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
        
        /// <summary>
        /// Attempts to discover services of a specified type with multiple retry attempts
        /// </summary>
        /// <param name="serviceType">The service type to discover</param>
        /// <param name="maxAttempts">Maximum number of attempts to find the service</param>
        /// <param name="delayBetweenAttemptsMs">Delay between attempts in milliseconds</param>
        /// <returns>A list of found service IDs</returns>
        protected async Task<List<string>> DiscoverServicesWithRetryAsync(string serviceType, int maxAttempts = 10, int delayBetweenAttemptsMs = 300)
        {
            Console.WriteLine($"Starting ENHANCED service discovery for {serviceType} with {maxAttempts} max attempts");
            
            // Try all standard port offsets to find services
            int[] portOffsets = ServiceConstants.Discovery.StandardPortOffsets;
            
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Check if we have any services of this type already registered
                var serviceIds = GetServicesOfType(serviceType);
                if (serviceIds.Count > 0)
                {
                    Console.WriteLine($"==> Found {serviceIds.Count} {serviceType} services on attempt {attempt}/{maxAttempts}");
                    return serviceIds;
                }
                
                // Send discovery message on every attempt for more aggressive discovery
                Console.WriteLine($"Broadcasting service discovery message (attempt {attempt}/{maxAttempts})");
                
                // Send general service discovery first
                var discoveryMsg = Message.Create(MessageType.ServiceDiscovery);
                discoveryMsg.SenderId = _serviceId;
                discoveryMsg.MessageId = Guid.NewGuid().ToString();
                Console.WriteLine($"Broadcasting discovery message with ID {discoveryMsg.MessageId}");
                Broadcast(discoveryMsg);
                
                // Then send our own registration in case other services are looking for us
                PublishServiceRegistration();
                
                // On every 3rd attempt, try multi-port discovery to find services on different port offsets
                if (attempt % 3 == 0)
                {
                    Console.WriteLine("*** ATTEMPTING MULTI-PORT DISCOVERY ***");
                    
                    foreach (int offset in portOffsets)
                    {
                        try
                        {
                            // For GameEngine service type, try different port combinations
                            if (serviceType == ServiceConstants.ServiceTypes.GameEngine)
                            {
                                int potentialPort = ServiceConstants.Ports.GetGameEnginePublisherPort(offset);
                                Console.WriteLine($"Trying to contact {serviceType} on port {potentialPort} (offset {offset})");
                                
                                // Create a targeted message for this offset with Debug type for visibility
                                var debugMsg = Message.Create(MessageType.Debug,
                                    $"{_serviceType} [{_serviceId}] multi-port discovery attempt for {serviceType} on offset {offset}");
                                debugMsg.SenderId = _serviceId;
                                debugMsg.MessageId = Guid.NewGuid().ToString();
                                Broadcast(debugMsg);
                                
                                // Send additional discovery message specifically for this port
                                var portSpecificMsg = Message.Create(MessageType.ServiceDiscovery);
                                portSpecificMsg.SenderId = _serviceId;
                                portSpecificMsg.MessageId = Guid.NewGuid().ToString();
                                Broadcast(portSpecificMsg);
                                
                                // Wait briefly to avoid overwhelming the network
                                await Task.Delay(50);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error in multi-port discovery for offset {offset}: {ex.Message}");
                        }
                    }
                    
                    // Check again if we've found any services after the multi-port attempt
                    serviceIds = GetServicesOfType(serviceType);
                    if (serviceIds.Count > 0)
                    {
                        Console.WriteLine($"==> Found {serviceIds.Count} {serviceType} services after multi-port discovery");
                        return serviceIds;
                    }
                }
                
                // Wait before the next attempt
                await Task.Delay(delayBetweenAttemptsMs);
            }
            
            // If we've exhausted all attempts, return an empty list
            Console.WriteLine($"Failed to discover any {serviceType} services after {maxAttempts} attempts");
            return new List<string>();
        }
    }
}