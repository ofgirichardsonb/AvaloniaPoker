using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;
using MSA.Foundation.ServiceManagement;
using NetMQ;
using NetMQ.Sockets;
using PokerGame.Core.Microservices;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Implementation of IMessageTransport that uses NetMQ for communication
    /// </summary>
    public class NetMQMessageTransport : IMessageTransport, IShutdownParticipant, IDisposable
    {
        private readonly string _transportId;
        private readonly ConcurrentDictionary<string, Func<IMessage, Task>> _subscribers = 
            new ConcurrentDictionary<string, Func<IMessage, Task>>();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _acknowledgementHandlers = 
            new ConcurrentDictionary<string, TaskCompletionSource<bool>>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        
        private MessageTransportConfiguration _configuration = new MessageTransportConfiguration();
        private bool _isRunning = false;
        private bool _isDisposed = false;
        private Task _receiveTask = Task.CompletedTask;
        
        // NetMQ socket connections
        private PublisherSocket? _publisherSocket;
        private SubscriberSocket? _subscriberSocket;
        private string _publisherAddress = string.Empty;
        private string _subscriberAddress = string.Empty;
        
        /// <summary>
        /// Gets the unique identifier for this transport instance
        /// </summary>
        public string TransportId => _transportId;
        
        /// <summary>
        /// Gets whether this transport is running
        /// </summary>
        public bool IsRunning => _isRunning && !_isDisposed && !_cancellationTokenSource.IsCancellationRequested;
        
        /// <summary>
        /// Gets the ID for this shutdown participant
        /// </summary>
        public string ParticipantId => $"NetMQMessageTransport-{_transportId}";
        
        /// <summary>
        /// Gets the priority of this participant in the shutdown sequence
        /// Message transports should be shut down early to prevent new messages from being sent
        /// </summary>
        public int ShutdownPriority => 300;
        
        /// <summary>
        /// Initializes a new instance of the NetMQMessageTransport class
        /// </summary>
        /// <param name="transportId">The unique identifier for this transport instance</param>
        public NetMQMessageTransport(string transportId)
        {
            _transportId = transportId ?? throw new ArgumentNullException(nameof(transportId));
            
            // Register with the shutdown coordinator
            ShutdownCoordinator.Instance.RegisterParticipant(this);
            
            Console.WriteLine($"NetMQMessageTransport {transportId} created");
        }
        
        /// <summary>
        /// Initializes the transport with the specified configuration
        /// </summary>
        /// <param name="configuration">The transport configuration</param>
        /// <returns>True if initialization succeeded; otherwise, false</returns>
        public Task<bool> InitializeAsync(MessageTransportConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
                
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(NetMQMessageTransport));
                
            _configuration = configuration;
            
            // Parse connection string for NetMQ addresses
            ParseConnectionString(configuration.ConnectionString);
            
            Console.WriteLine($"NetMQMessageTransport {_transportId} initialized for service {configuration.ServiceId} ({configuration.ServiceName})");
            Console.WriteLine($"  Publisher Address: {_publisherAddress}");
            Console.WriteLine($"  Subscriber Address: {_subscriberAddress}");
            
            return Task.FromResult(true);
        }
        
        /// <summary>
        /// Parses the connection string to extract the publisher and subscriber addresses
        /// </summary>
        /// <param name="connectionString">The connection string</param>
        private void ParseConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                // Use default in-process addresses if no connection string is provided
                _publisherAddress = "inproc://central-broker";
                _subscriberAddress = "inproc://central-broker";
                return;
            }
            
            // Check if this is an in-process connection
            if (connectionString.StartsWith("inproc://", StringComparison.OrdinalIgnoreCase))
            {
                // Use the same address for both publisher and subscriber if it's in-process
                _publisherAddress = connectionString;
                _subscriberAddress = connectionString;
                return;
            }
            
            // Parse connection string for TCP or other connection types
            // Format: "tcp://publish_address;tcp://subscribe_address"
            string[] parts = connectionString.Split(';');
            
            if (parts.Length >= 2)
            {
                _publisherAddress = parts[0].Trim();
                _subscriberAddress = parts[1].Trim();
            }
            else if (parts.Length == 1)
            {
                // If only one address is provided, use it for both publisher and subscriber
                _publisherAddress = parts[0].Trim();
                _subscriberAddress = parts[0].Trim();
            }
        }
        
        /// <summary>
        /// Starts the transport
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>True if the transport was started successfully; otherwise, false</returns>
        public Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(NetMQMessageTransport));
                
            if (_isRunning)
                return Task.FromResult(true);
                
            try
            {
                // Create and initialize the sockets
                if (_publisherAddress.StartsWith("inproc://", StringComparison.OrdinalIgnoreCase))
                {
                    // Use shared sockets for in-process communication
                    Console.WriteLine($"NetMQMessageTransport {_transportId}: Using in-process communication");
                    _publisherSocket = NetMQContextHelperV2.CreateServicePublisher();
                    _subscriberSocket = NetMQContextHelperV2.CreateServiceSubscriber();
                }
                else
                {
                    // Create dedicated sockets for network communication
                    Console.WriteLine($"NetMQMessageTransport {_transportId}: Using network communication");
                    _publisherSocket = new PublisherSocket();
                    _subscriberSocket = new SubscriberSocket();
                    
                    // Connect the sockets
                    _publisherSocket.Connect(_publisherAddress);
                    _subscriberSocket.Connect(_subscriberAddress);
                    _subscriberSocket.SubscribeToAnyTopic();
                }
                
                // Register the sockets with the shutdown handler
                if (_publisherSocket != null)
                {
                    NetMQShutdownHandler.Instance.TrackResource(_publisherSocket);
                }
                
                if (_subscriberSocket != null)
                {
                    NetMQShutdownHandler.Instance.TrackResource(_subscriberSocket);
                }
                
                // Start the receive task
                _receiveTask = Task.Run(() => ReceiveMessagesAsync(_cancellationTokenSource.Token));
                
                _isRunning = true;
                
                Console.WriteLine($"NetMQMessageTransport {_transportId} started");
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NetMQMessageTransport {_transportId}: Error starting transport: {ex.Message}");
                
                // Clean up any resources that were created
                CleanupSockets();
                
                return Task.FromResult(false);
            }
        }
        
        /// <summary>
        /// Stops the transport
        /// </summary>
        /// <returns>True if the transport was stopped successfully; otherwise, false</returns>
        public async Task<bool> StopAsync()
        {
            if (!_isRunning)
                return true;
                
            Console.WriteLine($"NetMQMessageTransport {_transportId}: Stopping");
            
            _isRunning = false;
            
            try
            {
                // Cancel the receive task
                _cancellationTokenSource.Cancel();
                
                // Wait for the receive task to complete with a timeout
                if (_receiveTask != null && !_receiveTask.IsCompleted)
                {
                    await Task.WhenAny(_receiveTask, Task.Delay(500)).ConfigureAwait(false);
                }
                
                // Clean up any pending acknowledgements
                foreach (var handler in _acknowledgementHandlers)
                {
                    handler.Value.TrySetCanceled();
                }
                
                _acknowledgementHandlers.Clear();
                
                // Clean up the sockets
                CleanupSockets();
                
                Console.WriteLine($"NetMQMessageTransport {_transportId} stopped");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NetMQMessageTransport {_transportId}: Error stopping transport: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Cleans up the socket resources
        /// </summary>
        private void CleanupSockets()
        {
            try
            {
                if (_publisherSocket != null)
                {
                    NetMQShutdownHandler.Instance.UntrackResource(_publisherSocket);
                    _publisherSocket = null;
                }
                
                if (_subscriberSocket != null)
                {
                    NetMQShutdownHandler.Instance.UntrackResource(_subscriberSocket);
                    _subscriberSocket = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NetMQMessageTransport {_transportId}: Error cleaning up sockets: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sends a message to the specified destination
        /// </summary>
        /// <param name="destination">The destination to send the message to</param>
        /// <param name="message">The message to send</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>True if the message was sent successfully; otherwise, false</returns>
        public async Task<bool> SendAsync(string destination, IMessage message, CancellationToken cancellationToken = default)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
                
            if (_isDisposed || !_isRunning || _publisherSocket == null)
                return false;
                
            try
            {
                // Serialize the message to JSON
                string messageJson = SerializeMessage(message);
                
                // Create a frame with the destination as the topic and the message as the content
                bool requireAck = message.RequireAcknowledgement;
                
                // If acknowledgement is required, register the message for acknowledgement
                if (requireAck)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    _acknowledgementHandlers.TryAdd(message.MessageId, tcs);
                    
                    // Send the message
                    _publisherSocket.SendMoreFrame(destination).SendFrame(messageJson);
                    
                    // Set up a timeout task
                    var timeoutTask = Task.Delay(_configuration.AcknowledgementTimeoutMs, cancellationToken);
                    
                    // Wait for acknowledgement or timeout
                    var completedTask = await Task.WhenAny(tcs.Task, timeoutTask).ConfigureAwait(false);
                    
                    // If the completed task is the timeout task, the acknowledgement timed out
                    if (completedTask == timeoutTask)
                    {
                        _acknowledgementHandlers.TryRemove(message.MessageId, out _);
                        Console.WriteLine($"NetMQMessageTransport {_transportId}: Acknowledgement timed out for message {message.MessageId}");
                        return false;
                    }
                    
                    return true;
                }
                else
                {
                    // Send the message without waiting for acknowledgement
                    _publisherSocket.SendMoreFrame(destination).SendFrame(messageJson);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NetMQMessageTransport {_transportId}: Error sending message to {destination}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Broadcasts a message to all subscribers
        /// </summary>
        /// <param name="message">The message to broadcast</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>True if the message was broadcast successfully; otherwise, false</returns>
        public Task<bool> BroadcastAsync(IMessage message, CancellationToken cancellationToken = default)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
                
            if (_isDisposed || !_isRunning || _publisherSocket == null)
                return Task.FromResult(false);
                
            try
            {
                // Serialize the message to JSON
                string messageJson = SerializeMessage(message);
                
                // Broadcast to all by using an empty topic frame
                _publisherSocket.SendMoreFrame("").SendFrame(messageJson);
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NetMQMessageTransport {_transportId}: Error broadcasting message: {ex.Message}");
                return Task.FromResult(false);
            }
        }
        
        /// <summary>
        /// Subscribes to messages of the specified type
        /// </summary>
        /// <param name="messageType">The type of message to subscribe to</param>
        /// <param name="handler">The handler to invoke when a message of the specified type is received</param>
        /// <returns>A subscription identifier that can be used to unsubscribe</returns>
        public string Subscribe(string messageType, Func<IMessage, Task> handler)
        {
            if (string.IsNullOrEmpty(messageType))
                throw new ArgumentException("Message type cannot be null or empty", nameof(messageType));
                
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
                
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(NetMQMessageTransport));
                
            string subscriptionId = $"type:{messageType}:{Guid.NewGuid()}";
            _subscribers.TryAdd(subscriptionId, handler);
            
            Console.WriteLine($"NetMQMessageTransport {_transportId}: Subscribed to message type {messageType} with ID {subscriptionId}");
            
            return subscriptionId;
        }
        
        /// <summary>
        /// Subscribes to messages from the specified source
        /// </summary>
        /// <param name="source">The source to subscribe to</param>
        /// <param name="handler">The handler to invoke when a message from the specified source is received</param>
        /// <returns>A subscription identifier that can be used to unsubscribe</returns>
        public string SubscribeToSource(string source, Func<IMessage, Task> handler)
        {
            if (string.IsNullOrEmpty(source))
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
                
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
                
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(NetMQMessageTransport));
                
            string subscriptionId = $"source:{source}:{Guid.NewGuid()}";
            _subscribers.TryAdd(subscriptionId, handler);
            
            Console.WriteLine($"NetMQMessageTransport {_transportId}: Subscribed to source {source} with ID {subscriptionId}");
            
            return subscriptionId;
        }
        
        /// <summary>
        /// Subscribes to all messages
        /// </summary>
        /// <param name="handler">The handler to invoke when any message is received</param>
        /// <returns>A subscription identifier that can be used to unsubscribe</returns>
        public string SubscribeToAll(Func<IMessage, Task> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
                
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(NetMQMessageTransport));
                
            string subscriptionId = $"all:{Guid.NewGuid()}";
            _subscribers.TryAdd(subscriptionId, handler);
            
            Console.WriteLine($"NetMQMessageTransport {_transportId}: Subscribed to all messages with ID {subscriptionId}");
            
            return subscriptionId;
        }
        
        /// <summary>
        /// Unsubscribes from messages using the specified subscription identifier
        /// </summary>
        /// <param name="subscriptionId">The subscription identifier to unsubscribe</param>
        /// <returns>True if the subscription was found and removed; otherwise, false</returns>
        public bool Unsubscribe(string subscriptionId)
        {
            if (string.IsNullOrEmpty(subscriptionId))
                throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
                
            if (_isDisposed)
                return false;
                
            bool result = _subscribers.TryRemove(subscriptionId, out _);
            
            if (result)
            {
                Console.WriteLine($"NetMQMessageTransport {_transportId}: Unsubscribed from {subscriptionId}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Acknowledges receipt of a message
        /// </summary>
        /// <param name="messageId">The identifier of the message to acknowledge</param>
        /// <param name="success">Whether the message was processed successfully</param>
        /// <param name="error">Optional error information if the message was not processed successfully</param>
        /// <returns>True if the acknowledgement was sent successfully; otherwise, false</returns>
        public Task<bool> AcknowledgeAsync(string messageId, bool success, string? error = null)
        {
            if (string.IsNullOrEmpty(messageId))
                throw new ArgumentException("Message ID cannot be null or empty", nameof(messageId));
                
            if (_isDisposed || !_isRunning || _publisherSocket == null)
                return Task.FromResult(false);
                
            try
            {
                // Create an acknowledgement message
                var ackMessage = new ServiceMessage
                {
                    MessageType = "Acknowledgement",
                    SenderId = _configuration.ServiceId
                };
                
                // Add acknowledgement information to headers
                ackMessage.SetHeader("AcknowledgedMessageId", messageId);
                ackMessage.SetHeader("Success", success.ToString());
                
                if (!string.IsNullOrEmpty(error))
                {
                    ackMessage.SetHeader("Error", error);
                }
                
                // Serialize the acknowledgement message
                string ackJson = SerializeMessage(ackMessage);
                
                // Send the acknowledgement to the broadcast topic
                _publisherSocket.SendMoreFrame("ack").SendFrame(ackJson);
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NetMQMessageTransport {_transportId}: Error acknowledging message {messageId}: {ex.Message}");
                return Task.FromResult(false);
            }
        }
        
        /// <summary>
        /// Called by the ShutdownCoordinator to perform cleanup
        /// </summary>
        public async Task ShutdownAsync(CancellationToken token)
        {
            Console.WriteLine($"NetMQMessageTransport {_transportId}: Performing coordinated shutdown");
            
            await StopAsync().ConfigureAwait(false);
            
            // Cancel any pending operations
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch
            {
                // Ignore cancellation errors
            }
            
            // Clean up sockets
            CleanupSockets();
        }
        
        /// <summary>
        /// Disposes resources used by the transport
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;
                
            _isDisposed = true;
            
            // Stop the transport
            StopAsync().GetAwaiter().GetResult();
            
            // Cancel any pending operations
            try
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
            catch
            {
                // Ignore cancellation errors
            }
            
            // Clean up sockets
            CleanupSockets();
            
            // Clear collections
            _subscribers.Clear();
            _acknowledgementHandlers.Clear();
            
            Console.WriteLine($"NetMQMessageTransport {_transportId} disposed");
        }
        
        /// <summary>
        /// Runs a background task to receive messages from the subscriber socket
        /// </summary>
        private async Task ReceiveMessagesAsync(CancellationToken token)
        {
            if (_subscriberSocket == null)
                return;
                
            Console.WriteLine($"NetMQMessageTransport {_transportId}: Starting message receive task");
            
            while (!token.IsCancellationRequested && _isRunning && !_isDisposed)
            {
                try
                {
                    // Check if a message is available with a timeout to allow for cancellation
                    bool messageAvailable = _subscriberSocket.TryReceiveFrameString(TimeSpan.FromMilliseconds(50), out string topic);
                    
                    if (!messageAvailable)
                        continue;
                        
                    // If there's a topic, there should be a message body
                    if (!_subscriberSocket.TryReceiveFrameString(out string messageJson))
                        continue;
                        
                    // Check for cancellation again before processing
                    if (token.IsCancellationRequested || !_isRunning || _isDisposed)
                        break;
                        
                    // Process the message
                    IMessage? message = DeserializeMessage(messageJson);
                    
                    if (message == null)
                        continue;
                        
                    // Handle acknowledgements separately
                    if (message.MessageType == "Acknowledgement")
                    {
                        ProcessAcknowledgement(message);
                        continue;
                    }
                    
                    // Deliver the message to subscribers
                    await DeliverMessageToSubscribersAsync(message, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected during cancellation
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"NetMQMessageTransport {_transportId}: Error receiving message: {ex.Message}");
                    
                    // Back off a bit on errors
                    try
                    {
                        await Task.Delay(100, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            
            Console.WriteLine($"NetMQMessageTransport {_transportId}: Message receive task completed");
        }
        
        /// <summary>
        /// Processes an acknowledgement message
        /// </summary>
        /// <param name="message">The acknowledgement message</param>
        private void ProcessAcknowledgement(IMessage message)
        {
            string acknowledgedMessageId = message.GetHeader("AcknowledgedMessageId");
            
            if (string.IsNullOrEmpty(acknowledgedMessageId))
                return;
                
            bool success = bool.TryParse(message.GetHeader("Success"), out bool successValue) && successValue;
            string error = message.GetHeader("Error");
            
            if (_acknowledgementHandlers.TryRemove(acknowledgedMessageId, out var handler))
            {
                if (success)
                {
                    handler.TrySetResult(true);
                }
                else
                {
                    handler.TrySetException(new Exception(error));
                }
                
                Console.WriteLine($"NetMQMessageTransport {_transportId}: Processed acknowledgement for message {acknowledgedMessageId}");
            }
        }
        
        /// <summary>
        /// Delivers a message to all matching subscribers
        /// </summary>
        /// <param name="message">The message to deliver</param>
        /// <param name="token">Cancellation token</param>
        private async Task DeliverMessageToSubscribersAsync(IMessage message, CancellationToken token)
        {
            if (message == null || _isDisposed || !_isRunning)
                return;
                
            List<Task> deliveryTasks = new List<Task>();
            
            // Collect all matching subscribers
            foreach (var subscriber in _subscribers)
            {
                if (token.IsCancellationRequested)
                    break;
                    
                string key = subscriber.Key;
                
                if (key.StartsWith("all:"))
                {
                    // Always matches
                    deliveryTasks.Add(InvokeSubscriberAsync(subscriber.Value, message));
                }
                else if (key.StartsWith("type:"))
                {
                    // Extract the message type from the key
                    string[] parts = key.Split(':');
                    if (parts.Length >= 2 && parts[1] == message.MessageType)
                    {
                        deliveryTasks.Add(InvokeSubscriberAsync(subscriber.Value, message));
                    }
                }
                else if (key.StartsWith("source:"))
                {
                    // Extract the source from the key
                    string[] parts = key.Split(':');
                    if (parts.Length >= 2 && parts[1] == message.SenderId)
                    {
                        deliveryTasks.Add(InvokeSubscriberAsync(subscriber.Value, message));
                    }
                }
            }
            
            // Invoke all matching subscribers in parallel
            if (deliveryTasks.Count > 0)
            {
                await Task.WhenAll(deliveryTasks).ConfigureAwait(false);
            }
            
            // If the message requires acknowledgement, send the acknowledgement
            if (message.RequireAcknowledgement)
            {
                await AcknowledgeAsync(message.MessageId, true).ConfigureAwait(false);
            }
        }
        
        /// <summary>
        /// Invokes a subscriber with error handling
        /// </summary>
        /// <param name="subscriber">The subscriber to invoke</param>
        /// <param name="message">The message to deliver</param>
        /// <returns>A task representing the asynchronous operation</returns>
        private async Task InvokeSubscriberAsync(Func<IMessage, Task> subscriber, IMessage message)
        {
            try
            {
                await subscriber(message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NetMQMessageTransport {_transportId}: Error in subscriber: {ex.Message}");
                
                // If the message requires acknowledgement, send a negative acknowledgement
                if (message.RequireAcknowledgement)
                {
                    await AcknowledgeAsync(message.MessageId, false, ex.Message).ConfigureAwait(false);
                }
            }
        }
        
        /// <summary>
        /// Serializes a message to JSON
        /// </summary>
        /// <param name="message">The message to serialize</param>
        /// <returns>The JSON string</returns>
        private string SerializeMessage(IMessage message)
        {
            // For now, we'll use a simple JSON format with basic fields
            StringBuilder sb = new StringBuilder();
            sb.Append('{');
            
            // Add message ID
            sb.Append("\"messageId\":\"").Append(message.MessageId).Append("\",");
            
            // Add message type
            sb.Append("\"messageType\":\"").Append(message.MessageType).Append("\",");
            
            // Add sender ID
            sb.Append("\"senderId\":\"").Append(message.SenderId).Append("\",");
            
            // Add timestamp
            sb.Append("\"timestamp\":\"").Append(message.Timestamp.ToString("o")).Append("\",");
            
            // Add correlation ID if set
            if (!string.IsNullOrEmpty(message.CorrelationId))
            {
                sb.Append("\"correlationId\":\"").Append(message.CorrelationId).Append("\",");
            }
            
            // Add reply-to if set
            if (!string.IsNullOrEmpty(message.ReplyTo))
            {
                sb.Append("\"replyTo\":\"").Append(message.ReplyTo).Append("\",");
            }
            
            // Add require acknowledgement
            sb.Append("\"requireAcknowledgement\":").Append(message.RequireAcknowledgement.ToString().ToLowerInvariant()).Append(",");
            
            // Add content type
            sb.Append("\"contentType\":\"").Append(message.ContentType).Append("\",");
            
            // Add content if not empty
            if (message.Content != null && message.Content.Length > 0)
            {
                sb.Append("\"content\":\"");
                sb.Append(Convert.ToBase64String(message.Content));
                sb.Append("\",");
            }
            
            // Add headers
            sb.Append("\"headers\":{");
            bool firstHeader = true;
            
            foreach (var header in message.Headers)
            {
                if (!firstHeader)
                    sb.Append(',');
                    
                sb.Append('\"').Append(header.Key).Append("\":\"").Append(header.Value).Append('\"');
                firstHeader = false;
            }
            
            sb.Append('}');
            
            // Close the JSON object
            sb.Append('}');
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Deserializes a message from JSON
        /// </summary>
        /// <param name="json">The JSON string</param>
        /// <returns>The deserialized message, or null if deserialization failed</returns>
        private IMessage? DeserializeMessage(string json)
        {
            try
            {
                // For now, we'll use System.Text.Json for deserialization
                var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
                var root = jsonDoc.RootElement;
                
                var message = new ServiceMessage();
                
                // Extract message ID
                if (root.TryGetProperty("messageId", out var messageIdProp))
                {
                    SetMessageId(message, messageIdProp.GetString() ?? Guid.NewGuid().ToString());
                }
                
                // Extract message type
                if (root.TryGetProperty("messageType", out var messageTypeProp))
                {
                    SetMessageType(message, messageTypeProp.GetString() ?? "Unknown");
                }
                
                // Extract sender ID
                if (root.TryGetProperty("senderId", out var senderIdProp))
                {
                    SetSenderId(message, senderIdProp.GetString() ?? string.Empty);
                }
                
                // Extract correlation ID
                if (root.TryGetProperty("correlationId", out var correlationIdProp))
                {
                    SetCorrelationId(message, correlationIdProp.GetString() ?? string.Empty);
                }
                
                // Extract reply-to
                if (root.TryGetProperty("replyTo", out var replyToProp))
                {
                    SetReplyTo(message, replyToProp.GetString() ?? string.Empty);
                }
                
                // Extract require acknowledgement
                if (root.TryGetProperty("requireAcknowledgement", out var requireAckProp))
                {
                    SetRequireAcknowledgement(message, requireAckProp.GetBoolean());
                }
                
                // Extract content type
                if (root.TryGetProperty("contentType", out var contentTypeProp))
                {
                    SetContentType(message, contentTypeProp.GetString() ?? "application/json");
                }
                
                // Extract content
                if (root.TryGetProperty("content", out var contentProp))
                {
                    string? base64Content = contentProp.GetString();
                    if (!string.IsNullOrEmpty(base64Content))
                    {
                        SetContent(message, Convert.FromBase64String(base64Content));
                    }
                }
                
                // Extract headers
                if (root.TryGetProperty("headers", out var headersProp))
                {
                    foreach (var header in headersProp.EnumerateObject())
                    {
                        message.SetHeader(header.Name, header.Value.GetString() ?? string.Empty);
                    }
                }
                
                return message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NetMQMessageTransport {_transportId}: Error deserializing message: {ex.Message}");
                return null;
            }
        }
        
        // Helper methods for setting properties directly
        
        private void SetMessageId(ServiceMessage message, string value)
        {
            message.MessageId = value;
        }
        
        private void SetMessageType(ServiceMessage message, string value)
        {
            message.MessageType = value;
        }
        
        private void SetSenderId(ServiceMessage message, string value)
        {
            message.SenderId = value;
        }
        
        private void SetCorrelationId(ServiceMessage message, string value)
        {
            message.CorrelationId = value;
        }
        
        private void SetReplyTo(ServiceMessage message, string value)
        {
            message.ReplyTo = value;
        }
        
        private void SetRequireAcknowledgement(ServiceMessage message, bool value)
        {
            message.RequireAcknowledgement = value;
        }
        
        private void SetContentType(ServiceMessage message, string value)
        {
            message.ContentType = value;
        }
        
        private void SetContent(ServiceMessage message, byte[] value)
        {
            message.Content = value;
        }
    }
}