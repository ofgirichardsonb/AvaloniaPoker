using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using PokerGame.Core.Microservices;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// A reusable message broker component that adds reliable delivery and acknowledgment
    /// capabilities on top of NetMQ/ZeroMQ
    /// </summary>
    public class MessageBroker : IDisposable
    {
        // Unique identifier for this broker instance
        private readonly string _brokerId;
        
        // Network configuration
        private readonly string _publisherAddress;
        private readonly string _subscriberAddress;
        private PublisherSocket? _publisher;
        private SubscriberSocket? _subscriber;
        private NetMQPoller? _poller;
        
        // Message handling
        private readonly ConcurrentDictionary<string, PendingMessage> _pendingMessages = new();
        private readonly ConcurrentDictionary<string, MessageHandlerDelegate> _messageHandlers = new();
        private readonly ConcurrentDictionary<string, DateTime> _receivedAcks = new();
        
        // Cancellation support
        private CancellationTokenSource? _cancellationTokenSource;
        
        // Configuration
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(5);
        private readonly int _maxRetries = 3;
        
        /// <summary>
        /// Delegate for message handling functions
        /// </summary>
        /// <param name="message">The message to handle</param>
        /// <returns>Task representing the async operation</returns>
        public delegate Task MessageHandlerDelegate(MessageEnvelope message);
        
        /// <summary>
        /// Creates a new message broker
        /// </summary>
        /// <param name="brokerName">Descriptive name for this broker</param>
        /// <param name="publishPort">Port used for publishing messages</param>
        /// <param name="subscribePort">Port used for subscribing to messages</param>
        public MessageBroker(string brokerName, int publishPort, int subscribePort)
        {
            _brokerId = $"{brokerName}-{Guid.NewGuid().ToString().Substring(0, 8)}";
            _publisherAddress = $"tcp://localhost:{publishPort}";
            _subscriberAddress = $"tcp://localhost:{subscribePort}";
            
            Console.WriteLine($"Created MessageBroker {_brokerId} with publisher={publishPort}, subscriber={subscribePort}");
        }
        
        /// <summary>
        /// Starts the message broker
        /// </summary>
        public void Start()
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                
                // Create sockets
                _publisher = new PublisherSocket();
                _publisher.Options.SendHighWatermark = 1000;
                _publisher.Bind(_publisherAddress);
                
                _subscriber = new SubscriberSocket();
                _subscriber.Options.ReceiveHighWatermark = 1000;
                _subscriber.Connect(_subscriberAddress);
                
                // Subscribe to all messages initially
                _subscriber.SubscribeToAnyTopic();
                
                // Set up message handling
                _subscriber.ReceiveReady += OnMessageReceived;
                
                // Start the poller
                _poller = new NetMQPoller { _subscriber };
                _poller.RunAsync();
                
                // Start the message retry task
                Task.Run(RetryLoop, _cancellationTokenSource.Token);
                
                Console.WriteLine($"MessageBroker {_brokerId} started successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting MessageBroker: {ex.Message}");
                Dispose();
                throw;
            }
        }
        
        /// <summary>
        /// Stops the message broker
        /// </summary>
        public void Stop()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                
                if (_poller?.IsRunning == true)
                {
                    _poller.Stop();
                }
                
                Console.WriteLine($"MessageBroker {_brokerId} stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping MessageBroker: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Registers a handler for a specific message type
        /// </summary>
        /// <param name="messageType">The type of message to handle</param>
        /// <param name="handler">The handler function</param>
        public void RegisterHandler(string messageType, MessageHandlerDelegate handler)
        {
            _messageHandlers.TryAdd(messageType, handler);
            Console.WriteLine($"Registered handler for message type: {messageType}");
        }
        
        /// <summary>
        /// Unregisters a handler for a specific message type
        /// </summary>
        /// <param name="messageType">The type of message to stop handling</param>
        public void UnregisterHandler(string messageType)
        {
            _messageHandlers.TryRemove(messageType, out _);
            Console.WriteLine($"Unregistered handler for message type: {messageType}");
        }
        
        /// <summary>
        /// Sends a message with acknowledgment
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <returns>True if acknowledged, false if timed out</returns>
        public async Task<bool> SendWithAcknowledgmentAsync(MessageEnvelope message, int timeoutMs = 5000)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            
            // Ensure message has a unique ID
            if (string.IsNullOrEmpty(message.MessageId))
            {
                message.MessageId = Guid.NewGuid().ToString();
            }
            
            // Ensure message has sender ID set (crucial for acknowledgment routing)
            if (string.IsNullOrEmpty(message.SenderServiceId))
            {
                message.SenderServiceId = _brokerId;
                Console.WriteLine($"WARNING: Message {message.MessageId} had no sender ID. Setting to broker ID: {_brokerId}");
            }
            
            // Log before sending
            Console.WriteLine($"[{_brokerId}] Sending message {message.Type} from {message.SenderServiceId} to {message.TargetServiceId ?? "broadcast"} with acknowledgment (timeout: {timeoutMs}ms)");
            
            // Ensure reliable message delivery with retry logic
            try
            {
                // Check if message is already in pending messages (cleanup any old entries)
                if (_pendingMessages.TryGetValue(message.MessageId, out _))
                {
                    Console.WriteLine($"[{_brokerId}] WARNING: Message {message.MessageId} already exists in pending messages. Removing old entry.");
                    _pendingMessages.TryRemove(message.MessageId, out _);
                }
                
                // Add tracking for this message
                var pendingMessage = new PendingMessage
                {
                    Message = message,
                    CompletionSource = new TaskCompletionSource<bool>(),
                    SentTime = DateTime.UtcNow,
                    RetryCount = 0
                };
                
                _pendingMessages.TryAdd(message.MessageId, pendingMessage);
                
                // Send the message
                SendMessage(message);
                
                // Implement retry logic for reliability
                bool acknowledged = false;
                int retryCount = 0;
                int maxRetries = 3; // Maximum number of retries
                
                while (!acknowledged && retryCount <= maxRetries)
                {
                    // Wait for acknowledgment with timeout
                    var timeoutTask = Task.Delay(timeoutMs);
                    var completedTask = await Task.WhenAny(pendingMessage.CompletionSource.Task, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        // Timed out waiting for acknowledgment
                        retryCount++;
                        Console.WriteLine($"Message {message.MessageId} ({message.Type}) timed out waiting for acknowledgment. Retry {retryCount}/{maxRetries}");
                        
                        if (retryCount <= maxRetries)
                        {
                            // Resend the message
                            pendingMessage.CompletionSource = new TaskCompletionSource<bool>();
                            pendingMessage.RetryCount = retryCount;
                            pendingMessage.SentTime = DateTime.UtcNow;
                            
                            Console.WriteLine($"Retrying message {message.MessageId} ({message.Type})");
                            SendMessage(message);
                        }
                        else
                        {
                            // Maximum retries reached
                            Console.WriteLine($"Maximum retries reached for message {message.MessageId} ({message.Type})");
                            _pendingMessages.TryRemove(message.MessageId, out _);
                            return false;
                        }
                    }
                    else
                    {
                        // Acknowledgment received
                        acknowledged = true;
                        Console.WriteLine($"Message {message.MessageId} ({message.Type}) acknowledged successfully after {retryCount} retries");
                    }
                }
                
                // Cleanup
                _pendingMessages.TryRemove(message.MessageId, out _);
                return acknowledged;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in SendWithAcknowledgmentAsync: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                
                // Cleanup on error
                _pendingMessages.TryRemove(message.MessageId, out _);
                return false;
            }
        }
        
        /// <summary>
        /// Broadcasts a message to all listeners
        /// </summary>
        /// <param name="message">The message to broadcast</param>
        public void Broadcast(MessageEnvelope message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            
            // Ensure message has a unique ID
            if (string.IsNullOrEmpty(message.MessageId))
            {
                message.MessageId = Guid.NewGuid().ToString();
            }
            
            SendMessage(message);
        }
        
        /// <summary>
        /// Sends a direct message to a specific service
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="targetServiceId">The target service ID</param>
        public void SendTo(MessageEnvelope message, string targetServiceId)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrEmpty(targetServiceId)) throw new ArgumentException("Target service ID cannot be empty", nameof(targetServiceId));
            
            // Ensure message has a unique ID
            if (string.IsNullOrEmpty(message.MessageId))
            {
                message.MessageId = Guid.NewGuid().ToString();
            }
            
            // Set the target
            message.TargetServiceId = targetServiceId;
            
            SendMessage(message);
        }
        
        /// <summary>
        /// Low-level message sending
        /// </summary>
        /// <param name="message">The message to send</param>
        private void SendMessage(MessageEnvelope message)
        {
            if (_publisher == null)
            {
                throw new InvalidOperationException("Publisher socket is not initialized");
            }
            
            // Make sure the broker ID is set as the sender
            message.SenderServiceId = _brokerId;
            
            // Serialize and send
            var serialized = MessageEnvelope.Serialize(message);
            
            try
            {
                _publisher.SendFrame(serialized);
                Console.WriteLine($"Sent message {message.MessageId} of type {message.Type}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handles received messages
        /// </summary>
        private void OnMessageReceived(object? sender, NetMQSocketEventArgs e)
        {
            try
            {
                // Safely receive the message
                if (_subscriber == null)
                {
                    Console.WriteLine("WARNING: Subscriber is null in OnMessageReceived");
                    return;
                }
                
                var frame = _subscriber.ReceiveFrameString();
                if (string.IsNullOrEmpty(frame)) 
                {
                    Console.WriteLine("WARNING: Received empty frame");
                    return;
                }
                
                // Safely deserialize the message with detailed error logging
                MessageEnvelope? message;
                try 
                {
                    message = MessageEnvelope.Deserialize(frame);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Failed to deserialize message: {ex.Message}");
                    Console.WriteLine($"Failed message content: {frame}");
                    return;
                }
                
                if (message == null) 
                {
                    Console.WriteLine("WARNING: Received null message after deserialization");
                    return;
                }
                
                // Log all received messages for debugging
                Console.WriteLine($"RECEIVED RAW: Message ID: {message.MessageId}, Type: {message.Type}, From: {message.SenderServiceId}, To: {message.TargetServiceId ?? "broadcast"}");
                
                // Validate required message fields
                if (string.IsNullOrEmpty(message.MessageId))
                {
                    Console.WriteLine("WARNING: Received message with empty MessageId");
                    return;
                }
                
                // If this is an acknowledgment, process it
                if (message.Type == "Ack")
                {
                    string originalMsgId = message.Payload as string ?? "unknown";
                    Console.WriteLine($"Received ACK message with ID {message.MessageId}, for original message: {originalMsgId}");
                    ProcessAcknowledgment(message);
                    return;
                }
                
                // Check if we've already seen this message (to avoid duplicates)
                if (_receivedAcks.ContainsKey(message.MessageId))
                {
                    // We've already seen this message, just acknowledge it again
                    Console.WriteLine($"Received duplicate message {message.MessageId}, sending acknowledgment");
                    SendAcknowledgment(message);
                    return;
                }
                
                // Record that we've seen this message
                _receivedAcks.TryAdd(message.MessageId, DateTime.UtcNow);
                
                // Send acknowledgment
                SendAcknowledgment(message);
                
                // If the message is intended for someone else, ignore it
                if (!string.IsNullOrEmpty(message.TargetServiceId) && message.TargetServiceId != _brokerId)
                {
                    Console.WriteLine($"Message {message.MessageId} is intended for {message.TargetServiceId}, ignoring");
                    return;
                }
                
                // Process the message
                ProcessMessage(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing received message: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }
        
        /// <summary>
        /// Processes a received message
        /// </summary>
        /// <param name="message">The message to process</param>
        private void ProcessMessage(MessageEnvelope message)
        {
            // Check if we have a handler for this message type
            if (_messageHandlers.TryGetValue(message.Type, out var handler))
            {
                // Execute handler asynchronously
                Task.Run(async () =>
                {
                    try
                    {
                        await handler(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in message handler for {message.Type}: {ex.Message}");
                    }
                });
            }
            else
            {
                Console.WriteLine($"No handler registered for message type: {message.Type}");
            }
        }
        
        /// <summary>
        /// Sends an acknowledgment for a received message
        /// </summary>
        /// <param name="originalMessage">The message being acknowledged</param>
        private void SendAcknowledgment(MessageEnvelope originalMessage)
        {
            // Skip if sender ID is missing or invalid
            if (string.IsNullOrEmpty(originalMessage.SenderServiceId))
            {
                Console.WriteLine($"WARNING: Cannot send acknowledgment for message {originalMessage.MessageId} - sender ID is missing");
                return;
            }
            
            // Create acknowledgment message
            var ackMessage = new MessageEnvelope
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = "Ack",
                SenderServiceId = _brokerId,
                TargetServiceId = originalMessage.SenderServiceId,
                Payload = originalMessage.MessageId, // Reference to the original message
                Timestamp = DateTime.UtcNow
            };
            
            // Enhanced logging for ack messages
            Console.WriteLine($"Sending acknowledgment for message {originalMessage.MessageId} to {originalMessage.SenderServiceId}");
            
            SendMessage(ackMessage);
        }
        
        /// <summary>
        /// Processes an acknowledgment message
        /// </summary>
        /// <param name="ackMessage">The acknowledgment message</param>
        private void ProcessAcknowledgment(MessageEnvelope ackMessage)
        {
            // Extract the original message ID from the payload
            var originalMessageId = ackMessage.Payload as string;
            if (string.IsNullOrEmpty(originalMessageId)) 
            {
                Console.WriteLine($"Warning: Received acknowledgment with empty or null originalMessageId");
                return;
            }
            
            // Check if we're waiting for this acknowledgment
            if (_pendingMessages.TryGetValue(originalMessageId, out var pendingMessage))
            {
                // Complete the waiting task
                pendingMessage.CompletionSource.TrySetResult(true);
                
                // Remove the message from the pending collection after it's been acknowledged
                if (_pendingMessages.TryRemove(originalMessageId, out _))
                {
                    Console.WriteLine($"Received acknowledgment for message {originalMessageId}");
                }
                else
                {
                    Console.WriteLine($"Warning: Failed to remove acknowledged message {originalMessageId} from pending collection");
                }
            }
            else
            {
                Console.WriteLine($"Received acknowledgment for unknown message {originalMessageId}");
            }
        }
        
        /// <summary>
        /// Retry loop for unacknowledged messages
        /// </summary>
        private async Task RetryLoop()
        {
            try
            {
                while (!_cancellationTokenSource?.Token.IsCancellationRequested ?? false)
                {
                    // Check for expired messages
                    var now = DateTime.UtcNow;
                    var expiredKeys = new List<string>();
                    
                    foreach (var entry in _pendingMessages)
                    {
                        var messageId = entry.Key;
                        var pending = entry.Value;
                        
                        // Check if this message has timed out and needs to be retried
                        if (now - pending.SentTime > _defaultTimeout)
                        {
                            if (pending.RetryCount < _maxRetries)
                            {
                                // Retry sending
                                pending.RetryCount++;
                                pending.SentTime = now;
                                Console.WriteLine($"Retrying message {messageId} ({pending.RetryCount}/{_maxRetries})");
                                SendMessage(pending.Message);
                            }
                            else
                            {
                                // Maximum retries reached, fail the message
                                Console.WriteLine($"Message {messageId} failed after {_maxRetries} retries");
                                pending.CompletionSource.TrySetResult(false);
                                expiredKeys.Add(messageId);
                            }
                        }
                    }
                    
                    // Remove expired messages
                    foreach (var key in expiredKeys)
                    {
                        _pendingMessages.TryRemove(key, out _);
                    }
                    
                    // Clean up old acknowledgments
                    CleanupOldAcknowledgments();
                    
                    // Wait a bit before checking again
                    await Task.Delay(1000, _cancellationTokenSource?.Token ?? CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when shutting down
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in message retry loop: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Removes old acknowledgment records to prevent memory leaks
        /// </summary>
        private void CleanupOldAcknowledgments()
        {
            var now = DateTime.UtcNow;
            var expiredKeys = new List<string>();
            
            // Find acknowledgments older than 5 minutes
            foreach (var ack in _receivedAcks)
            {
                if (now - ack.Value > TimeSpan.FromMinutes(5))
                {
                    expiredKeys.Add(ack.Key);
                }
            }
            
            // Remove expired acknowledgments
            foreach (var key in expiredKeys)
            {
                _receivedAcks.TryRemove(key, out _);
            }
        }
        
        /// <summary>
        /// Represents a message that's waiting for acknowledgment
        /// </summary>
        private class PendingMessage
        {
            public MessageEnvelope Message { get; set; } = null!;
            public TaskCompletionSource<bool> CompletionSource { get; set; } = null!;
            public DateTime SentTime { get; set; }
            public int RetryCount { get; set; }
        }
        
        /// <summary>
        /// Disposes resources used by the message broker
        /// </summary>
        public void Dispose()
        {
            Stop();
            
            _publisher?.Dispose();
            _subscriber?.Dispose();
            _poller?.Dispose();
            _cancellationTokenSource?.Dispose();
            
            GC.SuppressFinalize(this);
        }
    }
}