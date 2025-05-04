using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using CoreServiceConstants = PokerGame.Core.ServiceManagement.ServiceConstants;
using PokerGame.Core.Messaging;
using MSA.Foundation.ServiceManagement;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Base class for all microservices
    /// </summary>
    public abstract class MicroserviceBase : IDisposable
    {
        protected string _serviceId = string.Empty;
        protected string _serviceName = string.Empty;
        protected string _serviceType = string.Empty;
        
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
        protected MSA.Foundation.Messaging.IMessageTransport? _messageTransport;
        
        protected CancellationTokenSource? _cancellationTokenSource = new CancellationTokenSource();
        private Task? _processingTask;
        private Task? _heartbeatTask;
        
        private readonly ConcurrentDictionary<string, string> _serviceRegistry = new ConcurrentDictionary<string, string>();
        
        // Configuration options
        private int _heartbeatIntervalMs;
        protected int _publisherPort;
        protected int _subscriberPort;
        
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
            MSA.Foundation.ServiceManagement.ExecutionContext executionContext,
            int heartbeatIntervalMs = 5000)
        {
            // Assign static IDs based on service type for more reliable direct connections
            if (serviceType == CoreServiceConstants.ServiceTypes.GameEngine)
            {
                _serviceId = CoreServiceConstants.StaticServiceIds.GameEngine;
            }
            else if (serviceType == CoreServiceConstants.ServiceTypes.CardDeck)
            {
                _serviceId = CoreServiceConstants.StaticServiceIds.CardDeck;
            }
            else if (serviceType == CoreServiceConstants.ServiceTypes.ConsoleUI)
            {
                _serviceId = CoreServiceConstants.StaticServiceIds.ConsoleUI;
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
            // Create sockets using in-process communication
            try 
            {
                // Initialize publisher and subscriber sockets using in-process communication
                _publisherSocket = NetMQContextHelper.CreateServicePublisher();
                _subscriberSocket = NetMQContextHelper.CreateServiceSubscriber();
                
                Console.WriteLine($"Creating microservice {serviceName} ({serviceType}) with execution context");
                Console.WriteLine($"Using STATIC SERVICE ID: {_serviceId}");
                Console.WriteLine($"Using in-process communication via {NetMQContextHelper.InProcessBrokerAddress}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing in-process sockets: {ex.Message}");
                throw;
            }
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
            if (serviceType == CoreServiceConstants.ServiceTypes.GameEngine)
            {
                _serviceId = CoreServiceConstants.StaticServiceIds.GameEngine;
            }
            else if (serviceType == CoreServiceConstants.ServiceTypes.CardDeck)
            {
                _serviceId = CoreServiceConstants.StaticServiceIds.CardDeck;
            }
            else if (serviceType == CoreServiceConstants.ServiceTypes.ConsoleUI)
            {
                _serviceId = CoreServiceConstants.StaticServiceIds.ConsoleUI;
            }
            else
            {
                // Fallback to dynamic ID for any new service types
                _serviceId = Guid.NewGuid().ToString();
            }
            
            _serviceName = serviceName;
            _serviceType = serviceType;
            _heartbeatIntervalMs = heartbeatIntervalMs;
            // Store port values for logging, but don't use them directly
            // The CentralMessageBroker handles actual port assignments
            _publisherPort = publisherPort;
            _subscriberPort = subscriberPort;
            
            InitializeChannelMessagingAsync().GetAwaiter().GetResult();
        }

        private async Task InitializeChannelMessagingAsync()
        {
            // Initialize channel-based message transport
            try 
            {
                // Initialize message transport using channel-based communication
                _messageTransport = ChannelMessageHelper.CreateServiceTransport(_serviceId);
                await _messageTransport.StartAsync();
                
                Console.WriteLine($"[{_serviceType} {_serviceId}] Using channel-based communication via {ChannelMessageHelper.ChannelBrokerAddress}");
                Console.WriteLine($"[{_serviceType} {_serviceId}] Port parameters ignored in favor of channel-based communication");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_serviceType} {_serviceId}] Error initializing channel-based transport: {ex.Message}");
                throw;
            }
            
            Console.WriteLine($"{_serviceName} ({_serviceType}) initialized with STATIC ID: {_serviceId}");
        }
        
        /// <summary>
        /// Starts the microservice
        /// </summary>
        public virtual void Start()
        {
            Console.WriteLine($"====> [{_serviceType} {_serviceId}] Starting microservice: {_serviceName}");
            
            // Initialize and ensure BrokerManager is started
            Console.WriteLine($"====> [{_serviceType} {_serviceId}] Ensuring BrokerManager is started...");
            BrokerManager.Instance.Start();
            
            // Try to access the central broker with multiple attempts
            bool centralBrokerFound = false;
            CentralMessageBroker? centralBroker = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                centralBroker = BrokerManager.Instance.CentralBroker;
                if (centralBroker != null)
                {
                    centralBrokerFound = true;
                    Console.WriteLine($"====> [{_serviceType} {_serviceId}] Successfully connected to central broker");
                    Console.WriteLine($"====> [{_serviceType} {_serviceId}] Using in-process broker at {centralBroker.BrokerAddress}");
                    break;
                }
                
                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Central broker not found on attempt {attempt+1}/3, waiting...");
                Thread.Sleep(100);
            }
            
            if (!centralBrokerFound)
            {
                Console.WriteLine($"====> [{_serviceType} {_serviceId}] WARNING: Could not find central broker, services may not communicate properly");
            }
            else
            {
                // Our sockets are already initialized with in-process communication in constructor
                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Using in-process communication for messaging");
            }
            
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
            _ = Task.Run(async () => {
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
        /// Initializes or reinitializes sockets to work with the central broker architecture
        /// Uses in-process communication for improved reliability and simplicity
        /// </summary>
        /// <param name="centralBroker">The central message broker to connect to</param>
        private void InitializeCentralBrokerSockets(CentralMessageBroker centralBroker)
        {
            Console.WriteLine($"====> [{_serviceType} {_serviceId}] Initializing sockets using in-process communication");
            
            // Close and dispose existing sockets properly if they exist
            if (_publisherSocket != null)
            {
                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Closing existing publisher socket...");
                _publisherSocket.Close();
                _publisherSocket.Dispose();
                _publisherSocket = null;
            }
            
            if (_subscriberSocket != null)
            {
                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Closing existing subscriber socket...");
                _subscriberSocket.Close();
                _subscriberSocket.Dispose();
                _subscriberSocket = null;
            }
            
            try
            {
                // Use the shared context helper to create service-specific in-process sockets
                _publisherSocket = NetMQContextHelper.CreateServicePublisher();
                _subscriberSocket = NetMQContextHelper.CreateServiceSubscriber();
                
                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Using in-process communication via {NetMQContextHelper.InProcessBrokerAddress}");
                
                // Log the configuration
                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Sockets successfully initialized using in-process communication");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"====> [{_serviceType} {_serviceId}] ERROR initializing sockets: {ex.Message}");
                throw;
            }
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
        /// Sends a message to all microservices through the central message broker
        /// </summary>
        /// <param name="message">The message to send</param>
        protected internal virtual void Broadcast(Message message)
        {
            try
            {
                // Set the sender ID
                message.SenderId = _serviceId;
                
                // Add a unique message ID if not already present
                if (string.IsNullOrEmpty(message.MessageId))
                {
                    message.MessageId = Guid.NewGuid().ToString();
                }
                
                // Log the broadcast
                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Broadcasting message {message.Type} (ID: {message.MessageId}) through central broker");
                
                // Convert to NetworkMessage
                var networkMessage = message.ToNetworkMessage();
                
                // We need to be more persistent in finding the central broker
                // Try multiple times with a short delay
                CentralMessageBroker? centralBroker = null;
                int maxAttempts = 3;
                
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    centralBroker = BrokerManager.Instance.CentralBroker;
                    if (centralBroker != null)
                    {
                        break;
                    }
                    
                    // If not found on first attempt, try to initialize it
                    if (attempt == 0)
                    {
                        Console.WriteLine($"Central broker not found on first attempt, trying to initialize BrokerManager...");
                        try 
                        {
                            // Make sure BrokerManager is started
                            BrokerManager.Instance.Start();
                            
                            // Try to access the broker again
                            centralBroker = BrokerManager.Instance.CentralBroker;
                            if (centralBroker != null)
                            {
                                Console.WriteLine($"Successfully found central broker after initialization");
                                break;
                            }
                        }
                        catch (Exception brokerInitEx)
                        {
                            Console.WriteLine($"Error initializing broker manager: {brokerInitEx.Message}");
                        }
                    }
                    
                    // Wait before trying again
                    Console.WriteLine($"Central broker not found on attempt {attempt+1}, waiting before retry...");
                    Thread.Sleep(100);
                }
                
                // If we found the central broker, use it
                if (centralBroker != null)
                {
                    centralBroker.Publish(networkMessage);
                    return;
                }
                
                // No longer using fallback to direct socket if central broker isn't available
                Console.WriteLine($"ERROR: Central broker not available after {maxAttempts} attempts for message {message.MessageId} - message NOT sent");
                // Do not use direct socket as fallback - this creates socket conflicts
                // _publisherSocket?.SendFrame(message.ToJson());
            }
            catch (Exception ex)
            {
                // Log any errors
                Console.WriteLine($"Error broadcasting message: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        /// <summary>
        /// Sends a message to a specific microservice through the central message broker
        /// Uses in-process communication for improved reliability and simplicity
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
                
                Console.WriteLine($"Sending message type {message.Type} to {receiverId} through central broker");
                
                // Convert to NetworkMessage
                var networkMessage = message.ToNetworkMessage();
                
                // We need to be more persistent in finding the central broker
                // Try multiple times with a short delay
                CentralMessageBroker? centralBroker = null;
                int maxAttempts = 3;
                
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    centralBroker = BrokerManager.Instance.CentralBroker;
                    if (centralBroker != null)
                    {
                        break;
                    }
                    
                    // If not found on first attempt, try to initialize it
                    if (attempt == 0)
                    {
                        Console.WriteLine($"[SendTo] Central broker not found on first attempt, trying to initialize BrokerManager...");
                        try 
                        {
                            // Make sure BrokerManager is started
                            BrokerManager.Instance.Start();
                            
                            // Try to access the broker again
                            centralBroker = BrokerManager.Instance.CentralBroker;
                            if (centralBroker != null)
                            {
                                Console.WriteLine($"[SendTo] Successfully found central broker after initialization");
                                break;
                            }
                        }
                        catch (Exception brokerInitEx)
                        {
                            Console.WriteLine($"[SendTo] Error initializing broker manager: {brokerInitEx.Message}");
                        }
                    }
                    
                    // Wait before trying again
                    Console.WriteLine($"[SendTo] Central broker not found on attempt {attempt+1}, waiting before retry...");
                    Thread.Sleep(100);
                }
                
                // If we found the central broker, use it
                if (centralBroker != null)
                {
                    centralBroker.Publish(networkMessage);
                    return true;
                }
                
                // Don't fall back to direct socket - require central broker for all messaging with in-process communication
                Console.WriteLine($"ERROR: Central broker not available after {maxAttempts} attempts. Message {message.MessageId} could not be sent.");
                Console.WriteLine($"Using in-process communication requires the central broker to be initialized before messages can be sent");
                return false;
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
                Endpoint = NetMQContextHelper.InProcessBrokerAddress, // Using in-process endpoint for better reliability
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
                    Endpoint = NetMQContextHelper.InProcessBrokerAddress, // Using in-process endpoint for better reliability
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
                        Console.WriteLine("Socket(s) no longer available, attempting to reconnect with in-process communication...");
                        try
                        {
                            // Get broker from BrokerManager
                            var centralBroker = BrokerManager.Instance.CentralBroker;
                            if (centralBroker == null)
                            {
                                Console.WriteLine("Cannot recreate sockets: CentralMessageBroker not available");
                                await Task.Delay(500, token);
                                continue;
                            }
                            
                            Console.WriteLine($"Using in-process communication via {NetMQContextHelper.InProcessBrokerAddress}");
                            
                            // Create new in-process sockets using the shared context
                            if (_subscriberSocket == null)
                            {
                                Console.WriteLine($"Creating new subscriber socket with in-process communication...");
                                _subscriberSocket = NetMQContextHelper.CreateServiceSubscriber();
                                Console.WriteLine($"Subscriber socket connected successfully with in-process communication");
                            }
                            
                            // Create new in-process publisher socket
                            if (_publisherSocket == null)
                            {
                                Console.WriteLine($"Creating new publisher socket with in-process communication...");
                                _publisherSocket = NetMQContextHelper.CreateServicePublisher();
                                Console.WriteLine($"Publisher socket connected successfully with in-process communication");
                            }
                            
                            // Give the sockets a moment to fully initialize
                            await Task.Delay(100, token);
                            Console.WriteLine("Socket reconnection successful with in-process communication, continuing message loop");
                            
                            // Skip to the next iteration to immediately use the new sockets
                            continue;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to recreate sockets with in-process communication: {ex.Message}");
                            
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
                                
                                // Broadcast the acknowledgment through central broker
                                Console.WriteLine($"====> [{_serviceType} {_serviceId}] Publishing ACK through central broker");
                                var networkAck = ackMessage.ToNetworkMessage();
                                BrokerManager.Instance.CentralBroker?.Publish(networkAck);
                                
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
                                    // Method 1: Using CentralMessageBroker instead of direct socket
                                    Console.WriteLine($"====> [{_serviceType} {_serviceId}] CRITICAL ACK 1: CentralBroker for {message.Type} {message.MessageId}");
                                    var networkAck = ackMessage.ToNetworkMessage();
                                    BrokerManager.Instance.CentralBroker?.Publish(networkAck);
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
                    
                    // Special handling for NetMQ context termination errors
                    if (ex.Message.Contains("CheckContextTerminated") || ex.Message.Contains("Context was terminated"))
                    {
                        // This is a NetMQ internal error that happens during socket operations
                        // when the context is being terminated or has been terminated
                        Console.WriteLine("NetMQ context termination detected - attempting to recreate sockets");
                        
                        try
                        {
                            // Close and dispose sockets
                            _publisherSocket?.Close();
                            _subscriberSocket?.Close();
                            _publisherSocket?.Dispose();
                            _subscriberSocket?.Dispose();
                            _publisherSocket = null;
                            _subscriberSocket = null;
                            
                            // Allow some time for sockets to close and context to settle
                            await Task.Delay(200);
                            
                            // Attempt to reconnect in the next loop iteration
                            continue;
                        }
                        catch (Exception socketEx)
                        {
                            Console.WriteLine($"Error cleaning up sockets after context termination: {socketEx.Message}");
                        }
                    }
                    else
                    {
                        // Log other errors - try to continue unless this is a fatal or repeating error
                        Console.WriteLine($"Error in message processing loop: {ex.Message}");
                    }
                    
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
            int[] portOffsets = CoreServiceConstants.Discovery.StandardPortOffsets;
            
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
                            if (serviceType == CoreServiceConstants.ServiceTypes.GameEngine)
                            {
                                int potentialPort = CoreServiceConstants.Ports.GetGameEnginePublisherPort(offset);
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