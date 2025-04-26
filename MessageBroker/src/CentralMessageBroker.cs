using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace MessageBroker
{
    /// <summary>
    /// A central message broker that handles routing messages between services
    /// </summary>
    public class CentralMessageBroker : IDisposable
    {
        private readonly BrokerLogger _logger = BrokerLogger.Instance;
        private readonly string _brokerId;
        private readonly int _frontendPort;
        private readonly int _backendPort;
        private readonly int _monitorPort;
        
        private RouterSocket? _frontendSocket;
        private RouterSocket? _backendSocket;
        private PublisherSocket? _monitorSocket;
        
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly Task? _brokerTask;
        private readonly Thread? _brokerThread;
        private readonly bool _ownThread;
        
        // Service registry for discovery
        private readonly ConcurrentDictionary<string, ServiceRegistrationPayload> _serviceRegistry = new ConcurrentDictionary<string, ServiceRegistrationPayload>();
        
        // Message deduplication and acknowledgment tracking
        private readonly ConcurrentDictionary<string, DateTime> _processedMessages = new ConcurrentDictionary<string, DateTime>();
        private readonly ConcurrentDictionary<string, (DateTime Timestamp, BrokerMessage Message, int RetryCount)> _pendingAcknowledgments = new ConcurrentDictionary<string, (DateTime, BrokerMessage, int)>();
        
        // Broker statistics
        private long _messageCount = 0;
        private long _errorCount = 0;
        
        /// <summary>
        /// Gets the unique identifier for this broker instance
        /// </summary>
        public string BrokerId => _brokerId;
        
        /// <summary>
        /// Gets the port that clients connect to
        /// </summary>
        public int FrontendPort => _frontendPort;
        
        /// <summary>
        /// Gets the port that services connect to
        /// </summary>
        public int BackendPort => _backendPort;
        
        /// <summary>
        /// Gets the port that provides monitoring information
        /// </summary>
        public int MonitorPort => _monitorPort;
        
        /// <summary>
        /// Gets the number of messages processed by the broker
        /// </summary>
        public long MessageCount => Interlocked.Read(ref _messageCount);
        
        /// <summary>
        /// Gets the number of errors encountered by the broker
        /// </summary>
        public long ErrorCount => Interlocked.Read(ref _errorCount);
        
        /// <summary>
        /// Gets the number of services registered with the broker
        /// </summary>
        public int ServiceCount => _serviceRegistry.Count;
        
        /// <summary>
        /// Creates a new instance of the central message broker
        /// </summary>
        /// <param name="frontendPort">The port that clients connect to</param>
        /// <param name="backendPort">The port that services connect to</param>
        /// <param name="monitorPort">The port that provides monitoring information</param>
        public CentralMessageBroker(int frontendPort = 5570, int backendPort = 5571, int monitorPort = 5572)
        {
            _brokerId = $"Broker-{Guid.NewGuid()}";
            _frontendPort = frontendPort;
            _backendPort = backendPort;
            _monitorPort = monitorPort;
            _ownThread = true;
            
            _logger.Info("Broker", $"Creating broker with ID {_brokerId}");
            _logger.Info("Broker", $"Frontend port: {_frontendPort}, Backend port: {_backendPort}, Monitor port: {_monitorPort}");
            _logger.Info("Broker", $"Using own thread");
            
            // Start the broker in a background task
            _brokerTask = Task.Run(RunBrokerAsync);
            
            // Start acknowledgment checker
            Task.Run(CheckPendingAcknowledgmentsAsync);
            
            // Start expired message cleaner
            Task.Run(CleanExpiredMessagesAsync);
        }
        
        /// <summary>
        /// Creates a new instance of the central message broker that attaches to an existing thread
        /// </summary>
        /// <param name="existingThread">An existing thread to attach to, or null to create a new thread</param>
        /// <param name="frontendPort">The port that clients connect to</param>
        /// <param name="backendPort">The port that services connect to</param>
        /// <param name="monitorPort">The port that provides monitoring information</param>
        public CentralMessageBroker(Thread? existingThread, int frontendPort = 5570, int backendPort = 5571, int monitorPort = 5572)
        {
            _brokerId = $"Broker-{Guid.NewGuid()}";
            _frontendPort = frontendPort;
            _backendPort = backendPort;
            _monitorPort = monitorPort;
            _ownThread = existingThread == null;
            
            _logger.Info("Broker", $"Creating broker with ID {_brokerId}");
            _logger.Info("Broker", $"Frontend port: {_frontendPort}, Backend port: {_backendPort}, Monitor port: {_monitorPort}");
            _logger.Info("Broker", $"Using {(_ownThread ? "own thread" : "existing thread")}");
            
            if (existingThread == null)
            {
                // Start the broker in a background task
                _brokerTask = Task.Run(RunBrokerAsync);
            }
            else
            {
                // Attach to existing thread
                _brokerThread = existingThread;
                
                // Start the broker in the existing thread's context
                ThreadPool.QueueUserWorkItem(async _ => 
                {
                    await RunBrokerAsync();
                });
            }
            
            // Start acknowledgment checker
            Task.Run(CheckPendingAcknowledgmentsAsync);
            
            // Start expired message cleaner
            Task.Run(CleanExpiredMessagesAsync);
        }
        
        /// <summary>
        /// Creates a new instance of the central message broker with a custom ID
        /// </summary>
        /// <param name="brokerId">The custom broker ID to use</param>
        /// <param name="frontendPort">The port that clients connect to</param>
        /// <param name="backendPort">The port that services connect to</param>
        /// <param name="monitorPort">The port that provides monitoring information</param>
        public CentralMessageBroker(string brokerId, int frontendPort = 5570, int backendPort = 5571, int monitorPort = 5572)
        {
            _brokerId = brokerId;
            _frontendPort = frontendPort;
            _backendPort = backendPort;
            _monitorPort = monitorPort;
            _ownThread = true;
            
            _logger.Info("Broker", $"Creating broker with ID {_brokerId}");
            _logger.Info("Broker", $"Frontend port: {_frontendPort}, Backend port: {_backendPort}, Monitor port: {_monitorPort}");
            
            // Start the broker in a background task
            _brokerTask = Task.Run(RunBrokerAsync);
            
            // Start acknowledgment checker
            Task.Run(CheckPendingAcknowledgmentsAsync);
            
            // Start expired message cleaner
            Task.Run(CleanExpiredMessagesAsync);
        }
        
        /// <summary>
        /// Runs the broker asynchronously
        /// </summary>
        private async Task RunBrokerAsync()
        {
            try
            {
                _logger.Info("Broker", "Starting broker...");
                
                using (_frontendSocket = new RouterSocket())
                using (_backendSocket = new RouterSocket())
                using (_monitorSocket = new PublisherSocket())
                using (var poller = new NetMQPoller())
                {
                    // Bind sockets
                    _frontendSocket.Bind($"tcp://0.0.0.0:{_frontendPort}");
                    _backendSocket.Bind($"tcp://0.0.0.0:{_backendPort}");
                    _monitorSocket.Bind($"tcp://0.0.0.0:{_monitorPort}");
                    
                    _logger.Info("Broker", "Sockets bound successfully");
                    
                    // Set up message handlers
                    _frontendSocket.ReceiveReady += OnFrontendReceiveReady;
                    _backendSocket.ReceiveReady += OnBackendReceiveReady;
                    
                    poller.Add(_frontendSocket);
                    poller.Add(_backendSocket);
                    
                    // Start the poller
                    _logger.Info("Broker", "Starting message processing loop");
                    poller.RunAsync();
                    
                    // Send initial heartbeat
                    await SendHeartbeatAsync();
                    
                    // Keep the broker running until cancellation is requested
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, _cancellationTokenSource.Token);
                        
                        // Send periodic heartbeat
                        await SendHeartbeatAsync();
                    }
                    
                    // Clean up
                    poller.Remove(_frontendSocket);
                    poller.Remove(_backendSocket);
                    poller.Stop();
                    
                    _logger.Info("Broker", "Broker stopped gracefully");
                }
            }
            catch (Exception ex)
            {
                _logger.Critical("Broker", "Error running broker", ex);
                Interlocked.Increment(ref _errorCount);
            }
        }
        
        /// <summary>
        /// Handles messages received on the frontend socket (from clients)
        /// </summary>
        private void OnFrontendReceiveReady(object? sender, NetMQSocketEventArgs e)
        {
            try
            {
                var messageFrames = new List<byte[]>();
                
                // Receive all message frames
                var more = true;
                while (more && _frontendSocket != null)
                {
                    var buffer = _frontendSocket.ReceiveFrameBytes(out more);
                    messageFrames.Add(buffer);
                }
                
                if (messageFrames.Count < 3)
                {
                    _logger.Warning("Broker", "Received invalid message from frontend (too few frames)");
                    return;
                }
                
                // First frame is the sender identity
                var senderIdentity = messageFrames[0];
                
                // Last frame is the message content
                var messageContent = messageFrames[^1];
                var messageString = System.Text.Encoding.UTF8.GetString(messageContent);
                
                var message = BrokerMessage.FromJson(messageString);
                if (message == null)
                {
                    _logger.Warning("Broker", $"Received invalid JSON message from frontend: {messageString}");
                    return;
                }
                
                _logger.Debug("Broker", $"Received message from frontend: Type={message.Type}, ID={message.MessageId}, From={message.SenderId}, To={message.ReceiverId}");
                
                // Check for duplicate message
                if (IsDuplicateMessage(message.MessageId))
                {
                    _logger.Debug("Broker", $"Ignoring duplicate message: {message.MessageId}");
                    return;
                }
                
                // Process the message
                ProcessMessage(message, senderIdentity, isFromBackend: false);
            }
            catch (Exception ex)
            {
                _logger.Error("Broker", "Error processing frontend message", ex);
                Interlocked.Increment(ref _errorCount);
            }
        }
        
        /// <summary>
        /// Handles messages received on the backend socket (from services)
        /// </summary>
        private void OnBackendReceiveReady(object? sender, NetMQSocketEventArgs e)
        {
            try
            {
                var messageFrames = new List<byte[]>();
                
                // Receive all message frames
                var more = true;
                while (more && _backendSocket != null)
                {
                    var buffer = _backendSocket.ReceiveFrameBytes(out more);
                    messageFrames.Add(buffer);
                }
                
                if (messageFrames.Count < 3)
                {
                    _logger.Warning("Broker", "Received invalid message from backend (too few frames)");
                    return;
                }
                
                // First frame is the sender identity
                var senderIdentity = messageFrames[0];
                
                // Last frame is the message content
                var messageContent = messageFrames[^1];
                var messageString = System.Text.Encoding.UTF8.GetString(messageContent);
                
                var message = BrokerMessage.FromJson(messageString);
                if (message == null)
                {
                    _logger.Warning("Broker", $"Received invalid JSON message from backend: {messageString}");
                    return;
                }
                
                _logger.Debug("Broker", $"Received message from backend: Type={message.Type}, ID={message.MessageId}, From={message.SenderId}, To={message.ReceiverId}");
                
                // Check for duplicate message
                if (IsDuplicateMessage(message.MessageId))
                {
                    _logger.Debug("Broker", $"Ignoring duplicate message: {message.MessageId}");
                    return;
                }
                
                // Process the message
                ProcessMessage(message, senderIdentity, isFromBackend: true);
            }
            catch (Exception ex)
            {
                _logger.Error("Broker", "Error processing backend message", ex);
                Interlocked.Increment(ref _errorCount);
            }
        }
        
        /// <summary>
        /// Processes a message and routes it to the appropriate destination
        /// </summary>
        /// <param name="message">The message to process</param>
        /// <param name="senderIdentity">The identity of the sender</param>
        /// <param name="isFromBackend">Whether the message came from the backend socket</param>
        private void ProcessMessage(BrokerMessage message, byte[] senderIdentity, bool isFromBackend)
        {
            try
            {
                // Mark message as processed to prevent duplicates
                MarkMessageAsProcessed(message.MessageId);
                
                // Increment message counter
                Interlocked.Increment(ref _messageCount);
                
                // Check if this is an acknowledgment for a pending message
                if (message.Type == BrokerMessageType.Acknowledgment && !string.IsNullOrEmpty(message.InResponseTo))
                {
                    if (_pendingAcknowledgments.TryRemove(message.InResponseTo, out _))
                    {
                        _logger.Debug("Broker", $"Received acknowledgment for message: {message.InResponseTo}");
                    }
                }
                
                // Handle system messages
                if (HandleSystemMessage(message, senderIdentity, isFromBackend))
                {
                    return;
                }
                
                // Route the message to its destination
                RouteMessage(message, senderIdentity, isFromBackend);
                
                // If message requires acknowledgment, track it
                if (message.RequiresAcknowledgment)
                {
                    TrackMessageForAcknowledgment(message);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Broker", $"Error processing message: {message.MessageId}", ex);
                Interlocked.Increment(ref _errorCount);
            }
        }
        
        /// <summary>
        /// Handles system messages (registration, discovery, etc.)
        /// </summary>
        /// <param name="message">The message to handle</param>
        /// <param name="senderIdentity">The identity of the sender</param>
        /// <param name="isFromBackend">Whether the message came from the backend socket</param>
        /// <returns>True if the message was handled, false otherwise</returns>
        private bool HandleSystemMessage(BrokerMessage message, byte[] senderIdentity, bool isFromBackend)
        {
            switch (message.Type)
            {
                case BrokerMessageType.ServiceRegistration:
                    var registration = message.GetPayload<ServiceRegistrationPayload>();
                    if (registration != null)
                    {
                        _logger.Info("Broker", $"Registering service: {registration.ServiceId} ({registration.ServiceName}, {registration.ServiceType})");
                        _serviceRegistry[registration.ServiceId] = registration;
                        
                        // Send acknowledgment
                        SendAcknowledgment(message, senderIdentity, isFromBackend);
                        
                        // Broadcast service registration to all clients
                        BroadcastServiceRegistration(registration);
                    }
                    return true;
                
                case BrokerMessageType.ServiceDiscovery:
                    var discoveryRequest = message.GetPayload<ServiceDiscoveryPayload>();
                    if (discoveryRequest != null)
                    {
                        _logger.Info("Broker", $"Processing service discovery request: {discoveryRequest.ServiceType}, {discoveryRequest.Capability}");
                        var matchingServices = DiscoverServices(discoveryRequest.ServiceType, discoveryRequest.Capability);
                        
                        // Send response with matching services
                        SendServiceDiscoveryResponse(message, matchingServices, senderIdentity, isFromBackend);
                    }
                    return true;
                
                case BrokerMessageType.Ping:
                    _logger.Debug("Broker", $"Received ping from {message.SenderId}");
                    SendAcknowledgment(message, senderIdentity, isFromBackend);
                    return true;
                
                case BrokerMessageType.Heartbeat:
                    // Just log heartbeats, no special handling needed
                    _logger.Trace("Broker", $"Received heartbeat from {message.SenderId}");
                    return false;
                
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Routes a message to its destination
        /// </summary>
        /// <param name="message">The message to route</param>
        /// <param name="senderIdentity">The identity of the sender</param>
        /// <param name="isFromBackend">Whether the message came from the backend socket</param>
        private void RouteMessage(BrokerMessage message, byte[] senderIdentity, bool isFromBackend)
        {
            try
            {
                // If the message has a specific receiver, route it there
                if (!string.IsNullOrEmpty(message.ReceiverId))
                {
                    if (_serviceRegistry.TryGetValue(message.ReceiverId, out var service))
                    {
                        _logger.Debug("Broker", $"Routing message {message.MessageId} to service: {message.ReceiverId}");
                        
                        // Send to the specific service
                        var messageJson = message.ToJson();
                        var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageJson);
                        
                        // Select the appropriate socket based on service type
                        var isBackendService = true; // For now, all services are backend services
                        
                        if (isBackendService && _backendSocket != null)
                        {
                            var serviceIdentity = System.Text.Encoding.UTF8.GetBytes(message.ReceiverId);
                            _backendSocket.SendMoreFrame(serviceIdentity).SendFrame(messageBytes);
                        }
                        else if (!isBackendService && _frontendSocket != null)
                        {
                            var clientIdentity = System.Text.Encoding.UTF8.GetBytes(message.ReceiverId);
                            _frontendSocket.SendMoreFrame(clientIdentity).SendFrame(messageBytes);
                        }
                    }
                    else
                    {
                        _logger.Warning("Broker", $"Could not route message to unknown service: {message.ReceiverId}");
                        
                        // Send error back to sender
                        SendErrorResponse(message, $"Unknown service: {message.ReceiverId}", senderIdentity, isFromBackend);
                    }
                }
                else
                {
                    // Broadcast to all services
                    _logger.Debug("Broker", $"Broadcasting message {message.MessageId} to all services");
                    BroadcastMessage(message);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Broker", $"Error routing message: {message.MessageId}", ex);
                Interlocked.Increment(ref _errorCount);
            }
        }
        
        /// <summary>
        /// Broadcasts a message to all services
        /// </summary>
        /// <param name="message">The message to broadcast</param>
        private void BroadcastMessage(BrokerMessage message)
        {
            var messageJson = message.ToJson();
            var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageJson);
            
            // Send to all backend services
            if (_backendSocket != null)
            {
                foreach (var serviceId in _serviceRegistry.Keys)
                {
                    var serviceIdentity = System.Text.Encoding.UTF8.GetBytes(serviceId);
                    _backendSocket.SendMoreFrame(serviceIdentity).SendFrame(messageBytes);
                }
            }
            
            // Also publish on the monitor socket for anyone who's listening
            _monitorSocket?.SendFrame(messageJson);
        }
        
        /// <summary>
        /// Broadcasts a service registration to all services
        /// </summary>
        /// <param name="registration">The service registration to broadcast</param>
        private void BroadcastServiceRegistration(ServiceRegistrationPayload registration)
        {
            var message = BrokerMessage.Create(BrokerMessageType.ServiceRegistration, registration);
            message.SenderId = _brokerId;
            
            BroadcastMessage(message);
        }
        
        /// <summary>
        /// Discovers services matching the specified criteria
        /// </summary>
        /// <param name="serviceType">The type of service to discover</param>
        /// <param name="capability">The capability to look for</param>
        /// <returns>A list of matching service registrations</returns>
        private List<ServiceRegistrationPayload> DiscoverServices(string? serviceType, string? capability)
        {
            var results = new List<ServiceRegistrationPayload>();
            
            foreach (var service in _serviceRegistry.Values)
            {
                var matchesType = string.IsNullOrEmpty(serviceType) || service.ServiceType.Equals(serviceType, StringComparison.OrdinalIgnoreCase);
                var matchesCapability = string.IsNullOrEmpty(capability) || service.Capabilities.Contains(capability);
                
                if (matchesType && matchesCapability)
                {
                    results.Add(service);
                }
            }
            
            return results;
        }
        
        /// <summary>
        /// Sends a service discovery response
        /// </summary>
        /// <param name="requestMessage">The original request message</param>
        /// <param name="services">The list of matching services</param>
        /// <param name="recipientIdentity">The identity of the recipient</param>
        /// <param name="sendToBackend">Whether to send to the backend socket</param>
        private void SendServiceDiscoveryResponse(BrokerMessage requestMessage, List<ServiceRegistrationPayload> services, byte[] recipientIdentity, bool sendToBackend)
        {
            var responseMessage = BrokerMessage.Create(BrokerMessageType.ServiceDiscovery, services);
            responseMessage.SenderId = _brokerId;
            responseMessage.ReceiverId = requestMessage.SenderId;
            responseMessage.InResponseTo = requestMessage.MessageId;
            
            var messageJson = responseMessage.ToJson();
            var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageJson);
            
            if (sendToBackend && _backendSocket != null)
            {
                _backendSocket.SendMoreFrame(recipientIdentity).SendFrame(messageBytes);
            }
            else if (!sendToBackend && _frontendSocket != null)
            {
                _frontendSocket.SendMoreFrame(recipientIdentity).SendFrame(messageBytes);
            }
        }
        
        /// <summary>
        /// Sends an acknowledgment for a message
        /// </summary>
        /// <param name="originalMessage">The original message</param>
        /// <param name="recipientIdentity">The identity of the recipient</param>
        /// <param name="sendToBackend">Whether to send to the backend socket</param>
        private void SendAcknowledgment(BrokerMessage originalMessage, byte[] recipientIdentity, bool sendToBackend)
        {
            var ackMessage = originalMessage.CreateAcknowledgment();
            ackMessage.SenderId = _brokerId;
            
            var messageJson = ackMessage.ToJson();
            var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageJson);
            
            if (sendToBackend && _backendSocket != null)
            {
                _backendSocket.SendMoreFrame(recipientIdentity).SendFrame(messageBytes);
            }
            else if (!sendToBackend && _frontendSocket != null)
            {
                _frontendSocket.SendMoreFrame(recipientIdentity).SendFrame(messageBytes);
            }
        }
        
        /// <summary>
        /// Sends an error response
        /// </summary>
        /// <param name="originalMessage">The original message</param>
        /// <param name="errorMessage">The error message</param>
        /// <param name="recipientIdentity">The identity of the recipient</param>
        /// <param name="sendToBackend">Whether to send to the backend socket</param>
        private void SendErrorResponse(BrokerMessage originalMessage, string errorMessage, byte[] recipientIdentity, bool sendToBackend)
        {
            var errorPayload = new ErrorPayload
            {
                ErrorCode = 404,
                Message = errorMessage
            };
            
            var errorResponseMessage = BrokerMessage.Create(BrokerMessageType.Error, errorPayload);
            errorResponseMessage.SenderId = _brokerId;
            errorResponseMessage.ReceiverId = originalMessage.SenderId;
            errorResponseMessage.InResponseTo = originalMessage.MessageId;
            
            var messageJson = errorResponseMessage.ToJson();
            var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageJson);
            
            if (sendToBackend && _backendSocket != null)
            {
                _backendSocket.SendMoreFrame(recipientIdentity).SendFrame(messageBytes);
            }
            else if (!sendToBackend && _frontendSocket != null)
            {
                _frontendSocket.SendMoreFrame(recipientIdentity).SendFrame(messageBytes);
            }
        }
        
        /// <summary>
        /// Sends a heartbeat message to all clients
        /// </summary>
        private async Task SendHeartbeatAsync()
        {
            try
            {
                var heartbeatMessage = BrokerMessage.Create(BrokerMessageType.Heartbeat, DateTime.UtcNow.ToString("o"));
                heartbeatMessage.SenderId = _brokerId;
                
                BroadcastMessage(heartbeatMessage);
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error("Broker", "Error sending heartbeat", ex);
                Interlocked.Increment(ref _errorCount);
            }
        }
        
        /// <summary>
        /// Checks for pending acknowledgments that have timed out
        /// </summary>
        private async Task CheckPendingAcknowledgmentsAsync()
        {
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var now = DateTime.UtcNow;
                    var messagesToRetry = new List<(string MessageId, BrokerMessage Message, int RetryCount)>();
                    
                    // Check for messages that have waited too long for acknowledgment
                    foreach (var kvp in _pendingAcknowledgments)
                    {
                        var messageId = kvp.Key;
                        var (timestamp, message, retryCount) = kvp.Value;
                        
                        // If message has been waiting more than 5 seconds, retry or remove
                        if ((now - timestamp).TotalSeconds > 5)
                        {
                            if (retryCount < 3) // Retry up to 3 times
                            {
                                messagesToRetry.Add((messageId, message, retryCount));
                            }
                            else
                            {
                                // Max retries reached, remove from pending
                                _logger.Warning("Broker", $"Message {messageId} failed to be acknowledged after {retryCount} retries");
                                _pendingAcknowledgments.TryRemove(messageId, out _);
                            }
                        }
                    }
                    
                    // Retry messages
                    foreach (var (messageId, message, retryCount) in messagesToRetry)
                    {
                        _logger.Debug("Broker", $"Retrying message {messageId} (attempt {retryCount + 1}/3)");
                        
                        // Update retry count and timestamp
                        _pendingAcknowledgments[messageId] = (DateTime.UtcNow, message, retryCount + 1);
                        
                        // Re-route the message
                        RouteMessage(message, Array.Empty<byte>(), false);
                    }
                    
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // Normal during shutdown
            }
            catch (Exception ex)
            {
                _logger.Error("Broker", "Error checking pending acknowledgments", ex);
                Interlocked.Increment(ref _errorCount);
            }
        }
        
        /// <summary>
        /// Periodically cleans up expired message records to prevent memory leaks
        /// </summary>
        private async Task CleanExpiredMessagesAsync()
        {
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var now = DateTime.UtcNow;
                    var keysToRemove = new List<string>();
                    
                    // Find expired message records (older than 10 minutes)
                    foreach (var kvp in _processedMessages)
                    {
                        if ((now - kvp.Value).TotalMinutes > 10)
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                    }
                    
                    // Remove expired records
                    foreach (var key in keysToRemove)
                    {
                        _processedMessages.TryRemove(key, out _);
                    }
                    
                    if (keysToRemove.Count > 0)
                    {
                        _logger.Debug("Broker", $"Cleaned up {keysToRemove.Count} expired message records");
                    }
                    
                    await Task.Delay(60000, _cancellationTokenSource.Token); // Run every minute
                }
            }
            catch (TaskCanceledException)
            {
                // Normal during shutdown
            }
            catch (Exception ex)
            {
                _logger.Error("Broker", "Error cleaning expired messages", ex);
                Interlocked.Increment(ref _errorCount);
            }
        }
        
        /// <summary>
        /// Checks if a message has already been processed (to prevent duplicates)
        /// </summary>
        /// <param name="messageId">The ID of the message to check</param>
        /// <returns>True if the message is a duplicate, false otherwise</returns>
        private bool IsDuplicateMessage(string messageId)
        {
            return _processedMessages.ContainsKey(messageId);
        }
        
        /// <summary>
        /// Marks a message as processed to prevent duplicates
        /// </summary>
        /// <param name="messageId">The ID of the message to mark</param>
        private void MarkMessageAsProcessed(string messageId)
        {
            _processedMessages[messageId] = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Tracks a message for acknowledgment
        /// </summary>
        /// <param name="message">The message to track</param>
        private void TrackMessageForAcknowledgment(BrokerMessage message)
        {
            _pendingAcknowledgments[message.MessageId] = (DateTime.UtcNow, message, 0);
        }
        
        /// <summary>
        /// Stops the broker gracefully
        /// </summary>
        public void Stop()
        {
            try
            {
                _logger.Info("Broker", "Stopping broker...");
                
                // Signal cancellation to stop all tasks
                _cancellationTokenSource.Cancel();
                
                // Wait for the broker task to complete (if it was started)
                if (_brokerTask != null && _ownThread)
                {
                    _logger.Debug("Broker", "Waiting for broker task to complete...");
                    _brokerTask.Wait(5000); // Wait up to 5 seconds
                }
                
                _logger.Info("Broker", "Broker stopped");
            }
            catch (Exception ex)
            {
                _logger.Error("Broker", "Error stopping broker", ex);
                Interlocked.Increment(ref _errorCount);
            }
        }
        
        /// <summary>
        /// Disposes the message broker and releases resources
        /// </summary>
        public void Dispose()
        {
            try
            {
                _logger.Info("Broker", "Shutting down broker...");
                
                // First stop the broker gracefully
                Stop();
                
                // Clean up resources
                _frontendSocket?.Dispose();
                _backendSocket?.Dispose();
                _monitorSocket?.Dispose();
                _cancellationTokenSource.Dispose();
                
                // Clear collections to help with garbage collection
                _serviceRegistry.Clear();
                _processedMessages.Clear();
                _pendingAcknowledgments.Clear();
                
                _logger.Info("Broker", "Broker shut down successfully");
                _logger.Shutdown();
            }
            catch (Exception ex)
            {
                // Log but don't rethrow from Dispose
                Console.Error.WriteLine($"Error during broker disposal: {ex.Message}");
            }
        }
    }
}