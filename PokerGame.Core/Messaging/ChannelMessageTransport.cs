using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using MSA.Foundation.Messaging;
using MSA.Foundation.ServiceManagement;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// A native .NET implementation of IMessageTransport using System.Threading.Channels
    /// for efficient in-process messaging without external dependencies.
    /// </summary>
    public class ChannelMessageTransport : IMessageTransport, IShutdownParticipant, IDisposable
    {
        private static readonly ConcurrentDictionary<string, Channel<IMessage>> _channels = 
            new ConcurrentDictionary<string, Channel<IMessage>>();
            
        private static readonly Channel<IMessage> _broadcastChannel = 
            Channel.CreateUnbounded<IMessage>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
            
        private readonly string _transportId;
        private readonly ConcurrentDictionary<string, Func<IMessage, Task>> _subscribers = 
            new ConcurrentDictionary<string, Func<IMessage, Task>>();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _acknowledgementHandlers = 
            new ConcurrentDictionary<string, TaskCompletionSource<bool>>();
        
        private MessageTransportConfiguration _configuration = new MessageTransportConfiguration();
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private bool _isRunning = false;
        private bool _isDisposed = false;
        private Channel<IMessage> _serviceChannel;
        private Task _receiveTask = Task.CompletedTask;
        private Task _broadcastReceiveTask = Task.CompletedTask;
        
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
        public string ParticipantId => $"ChannelMessageTransport-{_transportId}";
        
        /// <summary>
        /// Gets the priority of this participant in the shutdown sequence
        /// </summary>
        public int ShutdownPriority => 300;
        
        /// <summary>
        /// Static address to use for in-process communication
        /// </summary>
        public static string InProcessAddress => "inproc://message-channel";
        
        /// <summary>
        /// Initializes a new instance of the ChannelMessageTransport class
        /// </summary>
        /// <param name="transportId">The unique identifier for this transport instance</param>
        public ChannelMessageTransport(string transportId)
        {
            _transportId = transportId ?? throw new ArgumentNullException(nameof(transportId));
            
            // Register with the shutdown coordinator
            ShutdownCoordinator.Instance.RegisterParticipant(this);
            
            // Create a channel for this service if it doesn't exist
            _serviceChannel = _channels.GetOrAdd(transportId, _ => 
                Channel.CreateUnbounded<IMessage>(new UnboundedChannelOptions 
                { 
                    SingleReader = true, 
                    SingleWriter = false 
                }));
                
            Console.WriteLine($"ChannelMessageTransport {transportId} created");
        }
        
        /// <summary>
        /// Initializes a new instance of the ChannelMessageTransport class with the specified configuration
        /// </summary>
        /// <param name="configuration">The transport configuration</param>
        public ChannelMessageTransport(MessageTransportConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
                
            if (string.IsNullOrEmpty(configuration.ServiceId))
                throw new ArgumentException("ServiceId is required in the configuration", nameof(configuration));
                
            _transportId = configuration.ServiceId;
            _configuration = configuration;
            
            // Register with the shutdown coordinator
            ShutdownCoordinator.Instance.RegisterParticipant(this);
            
            // Create a channel for this service if it doesn't exist
            _serviceChannel = _channels.GetOrAdd(_transportId, _ => 
                Channel.CreateUnbounded<IMessage>(new UnboundedChannelOptions 
                { 
                    SingleReader = true, 
                    SingleWriter = false 
                }));
                
            Console.WriteLine($"ChannelMessageTransport {_transportId} created with configuration");
            
            // Automatically initialize
            InitializeAsync(configuration).GetAwaiter().GetResult();
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
                throw new ObjectDisposedException(nameof(ChannelMessageTransport));
                
            _configuration = configuration;
            
            // For channel transport, we don't need to parse connection strings
            // But we'll keep the API compatible with other transports
            
            Console.WriteLine($"ChannelMessageTransport {_transportId} initialized for service {configuration.ServiceId} ({configuration.ServiceName})");
            Console.WriteLine($"  Using in-process channel communication");
            
            return Task.FromResult(true);
        }
        
        /// <summary>
        /// Starts the transport
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>True if the transport was started successfully; otherwise, false</returns>
        public Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ChannelMessageTransport));
                
            if (_isRunning)
                return Task.FromResult(true);
                
            try
            {
                // Create a linked cancellation token source
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                
                // Start the receive tasks
                _receiveTask = Task.Run(() => ReceiveMessagesAsync(_cancellationTokenSource.Token));
                _broadcastReceiveTask = Task.Run(() => ReceiveBroadcastMessagesAsync(_cancellationTokenSource.Token));
                
                _isRunning = true;
                
                Console.WriteLine($"ChannelMessageTransport {_transportId} started");
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChannelMessageTransport {_transportId}: Error starting transport: {ex.Message}");
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
                
            Console.WriteLine($"ChannelMessageTransport {_transportId}: Stopping");
            
            _isRunning = false;
            
            try
            {
                // Cancel the receive tasks
                _cancellationTokenSource.Cancel();
                
                // Wait for the receive tasks to complete with a timeout
                var timeoutTask = Task.Delay(500);
                await Task.WhenAny(_receiveTask, timeoutTask);
                await Task.WhenAny(_broadcastReceiveTask, timeoutTask);
                
                // Clean up any pending acknowledgements
                foreach (var handler in _acknowledgementHandlers)
                {
                    handler.Value.TrySetCanceled();
                }
                
                _acknowledgementHandlers.Clear();
                
                Console.WriteLine($"ChannelMessageTransport {_transportId} stopped");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChannelMessageTransport {_transportId}: Error stopping transport: {ex.Message}");
                return false;
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
                
            if (_isDisposed || !_isRunning)
                return false;
                
            try
            {
                // Get or create the destination channel
                var channel = _channels.GetOrAdd(destination, _ => 
                    Channel.CreateUnbounded<IMessage>(new UnboundedChannelOptions 
                    { 
                        SingleReader = true, 
                        SingleWriter = false 
                    }));
                
                // If acknowledgement is required, register the message for acknowledgement
                if (message.RequireAcknowledgement)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    _acknowledgementHandlers.TryAdd(message.MessageId, tcs);
                    
                    // Send the message
                    await channel.Writer.WriteAsync(message, cancellationToken);
                    
                    // Set up a timeout task
                    var timeoutTask = Task.Delay(_configuration.AcknowledgementTimeoutMs, cancellationToken);
                    
                    // Wait for acknowledgement or timeout
                    var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                    
                    // If the completed task is the timeout task, the acknowledgement timed out
                    if (completedTask == timeoutTask)
                    {
                        _acknowledgementHandlers.TryRemove(message.MessageId, out _);
                        Console.WriteLine($"ChannelMessageTransport {_transportId}: Acknowledgement timed out for message {message.MessageId}");
                        return false;
                    }
                    
                    return true;
                }
                else
                {
                    // Send the message without waiting for acknowledgement
                    await channel.Writer.WriteAsync(message, cancellationToken);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChannelMessageTransport {_transportId}: Error sending message to {destination}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Broadcasts a message to all subscribers
        /// </summary>
        /// <param name="message">The message to broadcast</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>True if the message was broadcast successfully; otherwise, false</returns>
        public async Task<bool> BroadcastAsync(IMessage message, CancellationToken cancellationToken = default)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
                
            if (_isDisposed || !_isRunning)
                return false;
                
            try
            {
                // If the message requires acknowledgment, use SendAsync instead since broadcast
                // doesn't have a specific destination for acknowledgment handling
                if (message.RequireAcknowledgement)
                {
                    Console.WriteLine($"ChannelMessageTransport {_transportId}: Cannot broadcast a message that requires acknowledgement. Use SendAsync instead.");
                    return false;
                }
                
                // Send to the broadcast channel
                await _broadcastChannel.Writer.WriteAsync(message, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChannelMessageTransport {_transportId}: Error broadcasting message: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Subscribes to messages of the specified type
        /// </summary>
        /// <param name="messageType">The type of message to subscribe to</param>
        /// <param name="handler">The handler to invoke when a message of the specified type is received</param>
        /// <returns>A subscription ID that can be used to unsubscribe</returns>
        public string Subscribe(string messageType, Func<IMessage, Task> handler)
        {
            if (string.IsNullOrEmpty(messageType))
                throw new ArgumentException("Message type cannot be null or empty", nameof(messageType));
                
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
                
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ChannelMessageTransport));
                
            string subscriptionId = Guid.NewGuid().ToString();
            string key = $"{messageType}:{subscriptionId}";
            
            _subscribers.TryAdd(key, handler);
            
            Console.WriteLine($"ChannelMessageTransport {_transportId}: Subscribed to message type {messageType} with ID {subscriptionId}");
            
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
                throw new ObjectDisposedException(nameof(ChannelMessageTransport));
                
            string subscriptionId = Guid.NewGuid().ToString();
            string key = $"SOURCE:{source}:{subscriptionId}";
            
            _subscribers.TryAdd(key, handler);
            
            Console.WriteLine($"ChannelMessageTransport {_transportId}: Subscribed to source {source} with ID {subscriptionId}");
            
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
                throw new ObjectDisposedException(nameof(ChannelMessageTransport));
                
            string subscriptionId = Guid.NewGuid().ToString();
            string key = $"ALL:{subscriptionId}";
            
            _subscribers.TryAdd(key, handler);
            
            Console.WriteLine($"ChannelMessageTransport {_transportId}: Subscribed to all messages with ID {subscriptionId}");
            
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
                throw new ObjectDisposedException(nameof(ChannelMessageTransport));
             
            bool removed = false;
            
            // Find all keys containing this subscription ID
            var keysToRemove = _subscribers.Keys
                .Where(k => k.Contains($":{subscriptionId}"))
                .ToList();
                
            // Remove each matching key
            foreach (var key in keysToRemove)
            {
                if (_subscribers.TryRemove(key, out _))
                {
                    removed = true;
                    Console.WriteLine($"ChannelMessageTransport {_transportId}: Unsubscribed from {key}");
                }
            }
            
            return removed;
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
                return Task.FromResult(false);
                
            if (_isDisposed)
                return Task.FromResult(false);
                
            // For in-process channel transport, acknowledgement is handled automatically
            // during message processing in the ProcessMessageAsync method
            
            // Add diagnostic information if an error occurred
            if (!success && !string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"ChannelMessageTransport {_transportId}: Message {messageId} processing failed with error: {error}");
            }
            
            return Task.FromResult(true);
        }
        
        /// <summary>
        /// Processes messages from the service channel
        /// </summary>
        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"ChannelMessageTransport {_transportId}: Started receiving messages");
            
            try
            {
                while (!cancellationToken.IsCancellationRequested && !_isDisposed)
                {
                    // Try to read from the channel with cancellation
                    IMessage message;
                    try
                    {
                        message = await _serviceChannel.Reader.ReadAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // This is expected when cancellation is requested
                        break;
                    }
                    catch (ChannelClosedException)
                    {
                        // Channel was closed, exit the loop
                        break;
                    }
                    
                    // Process the message
                    await ProcessMessageAsync(message, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChannelMessageTransport {_transportId}: Error in receive loop: {ex.Message}");
            }
            
            Console.WriteLine($"ChannelMessageTransport {_transportId}: Stopped receiving messages");
        }
        
        /// <summary>
        /// Processes messages from the broadcast channel
        /// </summary>
        private async Task ReceiveBroadcastMessagesAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"ChannelMessageTransport {_transportId}: Started receiving broadcast messages");
            
            try
            {
                while (!cancellationToken.IsCancellationRequested && !_isDisposed)
                {
                    // Try to read from the broadcast channel with cancellation
                    IMessage message;
                    try
                    {
                        message = await _broadcastChannel.Reader.ReadAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // This is expected when cancellation is requested
                        break;
                    }
                    catch (ChannelClosedException)
                    {
                        // Channel was closed, exit the loop
                        break;
                    }
                    
                    // Process the message if it wasn't sent by this service
                    if (message.SenderId != _configuration.ServiceId)
                    {
                        await ProcessMessageAsync(message, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChannelMessageTransport {_transportId}: Error in broadcast receive loop: {ex.Message}");
            }
            
            Console.WriteLine($"ChannelMessageTransport {_transportId}: Stopped receiving broadcast messages");
        }
        
        /// <summary>
        /// Processes a received message
        /// </summary>
        /// <param name="message">The message to process</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        private async Task ProcessMessageAsync(IMessage message, CancellationToken cancellationToken)
        {
            if (message == null)
                return;
                
            try
            {
                // Check if this is an acknowledgment message
                if (message is ServiceMessage serviceMessage && 
                    serviceMessage.IsAcknowledgement && 
                    !string.IsNullOrEmpty(serviceMessage.AcknowledgedMessageId))
                {
                    if (_acknowledgementHandlers.TryRemove(serviceMessage.AcknowledgedMessageId, out var tcs))
                    {
                        tcs.TrySetResult(true);
                    }
                    return;
                }
                
                // Get the message type and sender ID
                string messageType = message.MessageType;
                string senderId = message.SenderId;
                
                // Find all subscribers for this message
                var subscribers = new List<Func<IMessage, Task>>();
                
                // Add subscribers for this message type
                foreach (var kvp in _subscribers)
                {
                    string key = kvp.Key;
                    if (key.StartsWith($"{messageType}:") ||
                        key.StartsWith($"SOURCE:{senderId}:") ||
                        key.StartsWith("ALL:"))
                    {
                        subscribers.Add(kvp.Value);
                    }
                }
                
                // Invoke the handlers
                if (subscribers.Count > 0)
                {
                    foreach (var handler in subscribers)
                    {
                        try
                        {
                            await handler(message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"ChannelMessageTransport {_transportId}: Error in message handler: {ex.Message}");
                        }
                    }
                    
                    // Send acknowledgement if required
                    if (message.RequireAcknowledgement)
                    {
                        // Create an acknowledgement message using the helper method
                        var ack = ServiceMessage.CreateAcknowledgment(message)
                            .FromSender(_configuration.ServiceId);
                        
                        // Send the acknowledgement to the original sender
                        await SendAsync(message.SenderId, ack, cancellationToken);
                    }
                }
                else
                {
                    // No handlers found, but still send acknowledgement if required
                    if (message.RequireAcknowledgement)
                    {
                        // Create an acknowledgement message using the helper method
                        var ack = ServiceMessage.CreateAcknowledgment(message)
                            .FromSender(_configuration.ServiceId);
                        
                        // Send the acknowledgement to the original sender
                        await SendAsync(message.SenderId, ack, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChannelMessageTransport {_transportId}: Error processing message: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Performs cleanup during application shutdown
        /// </summary>
        /// <param name="token">A token to monitor for cancellation requests</param>
        public async Task ShutdownAsync(CancellationToken token)
        {
            Console.WriteLine($"ChannelMessageTransport {_transportId}: Shutting down");
            
            // Stop the transport if it's running
            if (_isRunning)
            {
                await StopAsync();
            }
            
            // Dispose resources
            Dispose();
        }
        
        /// <summary>
        /// Disposes resources used by the transport
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;
                
            _isDisposed = true;
            
            try
            {
                // Stop the transport if it's running
                if (_isRunning)
                {
                    _isRunning = false;
                    _cancellationTokenSource.Cancel();
                }
                
                // Clean up resources
                _cancellationTokenSource.Dispose();
                
                // Remove the service channel from the static dictionary
                _channels.TryRemove(_transportId, out _);
                
                Console.WriteLine($"ChannelMessageTransport {_transportId}: Disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ChannelMessageTransport {_transportId}: Error during disposal: {ex.Message}");
            }
        }
    }
}