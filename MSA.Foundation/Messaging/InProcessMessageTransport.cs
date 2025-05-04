using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.ServiceManagement;

namespace MSA.Foundation.Messaging
{
    /// <summary>
    /// Implementation of IMessageTransport that handles in-process messaging
    /// </summary>
    public class InProcessMessageTransport : IMessageTransport, IShutdownParticipant, IDisposable
    {
        private static readonly ConcurrentDictionary<string, InProcessMessageTransport> _transports = 
            new ConcurrentDictionary<string, InProcessMessageTransport>();
            
        private readonly string _transportId;
        private readonly ConcurrentDictionary<string, Func<IMessage, Task>> _subscribers = 
            new ConcurrentDictionary<string, Func<IMessage, Task>>();
        private readonly ConcurrentDictionary<string, IMessage> _pendingAcknowledgements = 
            new ConcurrentDictionary<string, IMessage>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private MessageTransportConfiguration _configuration = new MessageTransportConfiguration();
        private bool _isRunning = false;
        private bool _isDisposed = false;
        
        /// <summary>
        /// Gets the unique identifier for this transport instance
        /// </summary>
        public string TransportId => _transportId;
        
        /// <summary>
        /// Gets whether this transport is running
        /// </summary>
        public bool IsRunning => _isRunning && !_isDisposed && !_cancellationTokenSource.IsCancellationRequested;
        
        /// <summary>
        /// Gets the priority of this participant in the shutdown sequence
        /// </summary>
        public string ParticipantId => $"InProcessMessageTransport-{_transportId}";
        
        /// <summary>
        /// Gets the priority of this participant in the shutdown sequence
        /// </summary>
        public int ShutdownPriority => 200;
        
        /// <summary>
        /// Initializes a new instance of the InProcessMessageTransport class
        /// </summary>
        /// <param name="transportId">The unique identifier for this transport instance</param>
        public InProcessMessageTransport(string transportId)
        {
            _transportId = transportId ?? throw new ArgumentNullException(nameof(transportId));
            
            // Register with the transport registry
            _transports.TryAdd(transportId, this);
            
            // Register with the shutdown coordinator
            ShutdownCoordinator.Instance.RegisterParticipant(this);
            
            Console.WriteLine($"InProcessMessageTransport {transportId} created");
        }
        
        /// <summary>
        /// Gets a transport instance by ID, creating it if it doesn't exist
        /// </summary>
        /// <param name="transportId">The ID of the transport to get</param>
        /// <returns>The transport instance</returns>
        public static InProcessMessageTransport GetOrCreate(string transportId)
        {
            return _transports.GetOrAdd(transportId, id => new InProcessMessageTransport(id));
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
                throw new ObjectDisposedException(nameof(InProcessMessageTransport));
                
            _configuration = configuration;
            
            Console.WriteLine($"InProcessMessageTransport {_transportId} initialized for service {configuration.ServiceId} ({configuration.ServiceName})");
            
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
                throw new ObjectDisposedException(nameof(InProcessMessageTransport));
                
            if (_isRunning)
                return Task.FromResult(true);
                
            _isRunning = true;
            
            Console.WriteLine($"InProcessMessageTransport {_transportId} started");
            
            return Task.FromResult(true);
        }
        
        /// <summary>
        /// Stops the transport
        /// </summary>
        /// <returns>True if the transport was stopped successfully; otherwise, false</returns>
        public Task<bool> StopAsync()
        {
            if (!_isRunning)
                return Task.FromResult(true);
                
            _isRunning = false;
            
            // Cancel any pending acknowledgements
            foreach (var pendingAck in _pendingAcknowledgements)
            {
                _pendingAcknowledgements.TryRemove(pendingAck.Key, out _);
            }
            
            Console.WriteLine($"InProcessMessageTransport {_transportId} stopped");
            
            return Task.FromResult(true);
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
                
            if (!_transports.TryGetValue(destination, out var targetTransport))
            {
                Console.WriteLine($"InProcessMessageTransport {_transportId}: No transport found for destination {destination}");
                return false;
            }
            
            bool requireAck = message.RequireAcknowledgement;
            
            try
            {
                // If acknowledgement is required, register the message for acknowledgement
                if (requireAck)
                {
                    _pendingAcknowledgements.TryAdd(message.MessageId, message);
                    
                    // Set up a timeout task
                    var timeoutTask = Task.Delay(_configuration.AcknowledgementTimeoutMs, cancellationToken);
                    
                    // Deliver the message to the target transport
                    await targetTransport.DeliverMessageAsync(message).ConfigureAwait(false);
                    
                    // Wait for acknowledgement or timeout
                    var completedTask = await Task.WhenAny(
                        WaitForAcknowledgementAsync(message.MessageId), 
                        timeoutTask
                    ).ConfigureAwait(false);
                    
                    // If the completed task is the timeout task, the acknowledgement timed out
                    if (completedTask == timeoutTask)
                    {
                        _pendingAcknowledgements.TryRemove(message.MessageId, out _);
                        Console.WriteLine($"InProcessMessageTransport {_transportId}: Acknowledgement timed out for message {message.MessageId}");
                        return false;
                    }
                    
                    return true;
                }
                else
                {
                    // If no acknowledgement is required, just deliver the message
                    await targetTransport.DeliverMessageAsync(message).ConfigureAwait(false);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InProcessMessageTransport {_transportId}: Error sending message to {destination}: {ex.Message}");
                
                if (requireAck)
                {
                    _pendingAcknowledgements.TryRemove(message.MessageId, out _);
                }
                
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
                // Broadcast to all transports except this one
                foreach (var transport in _transports.Values)
                {
                    if (transport != this && !transport._isDisposed && transport._isRunning)
                    {
                        // Check cancellation
                        if (cancellationToken.IsCancellationRequested)
                            return false;
                            
                        try
                        {
                            await transport.DeliverMessageAsync(message).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"InProcessMessageTransport {_transportId}: Error broadcasting message to {transport.TransportId}: {ex.Message}");
                        }
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InProcessMessageTransport {_transportId}: Error broadcasting message: {ex.Message}");
                return false;
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
                throw new ObjectDisposedException(nameof(InProcessMessageTransport));
                
            string subscriptionId = $"type:{messageType}:{Guid.NewGuid()}";
            _subscribers.TryAdd(subscriptionId, handler);
            
            Console.WriteLine($"InProcessMessageTransport {_transportId}: Subscribed to message type {messageType} with ID {subscriptionId}");
            
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
                throw new ObjectDisposedException(nameof(InProcessMessageTransport));
                
            string subscriptionId = $"source:{source}:{Guid.NewGuid()}";
            _subscribers.TryAdd(subscriptionId, handler);
            
            Console.WriteLine($"InProcessMessageTransport {_transportId}: Subscribed to source {source} with ID {subscriptionId}");
            
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
                throw new ObjectDisposedException(nameof(InProcessMessageTransport));
                
            string subscriptionId = $"all:{Guid.NewGuid()}";
            _subscribers.TryAdd(subscriptionId, handler);
            
            Console.WriteLine($"InProcessMessageTransport {_transportId}: Subscribed to all messages with ID {subscriptionId}");
            
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
                Console.WriteLine($"InProcessMessageTransport {_transportId}: Unsubscribed from {subscriptionId}");
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
                
            if (_isDisposed || !_isRunning)
                return Task.FromResult(false);
                
            // Find the transport that's waiting for this acknowledgement
            foreach (var transport in _transports.Values)
            {
                if (transport._pendingAcknowledgements.TryRemove(messageId, out _))
                {
                    Console.WriteLine($"InProcessMessageTransport {_transportId}: Acknowledged message {messageId} from {transport.TransportId}");
                    
                    // If an acknowledgement handler is registered for this message, invoke it
                    if (transport.TryGetAcknowledgementHandler(messageId, out var tcs))
                    {
                        if (success)
                        {
                            tcs.TrySetResult(true);
                        }
                        else
                        {
                            tcs.TrySetException(new Exception(error ?? "Message processing failed"));
                        }
                    }
                    
                    return Task.FromResult(true);
                }
            }
            
            Console.WriteLine($"InProcessMessageTransport {_transportId}: No pending acknowledgement found for message {messageId}");
            return Task.FromResult(false);
        }
        
        /// <summary>
        /// Delivers a message to this transport's subscribers
        /// </summary>
        /// <param name="message">The message to deliver</param>
        /// <returns>A task representing the asynchronous operation</returns>
        private async Task DeliverMessageAsync(IMessage message)
        {
            if (message == null || _isDisposed || !_isRunning)
                return;
                
            List<Task> deliveryTasks = new List<Task>();
            
            // Collect all matching subscribers
            foreach (var subscriber in _subscribers)
            {
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
                Console.WriteLine($"InProcessMessageTransport {_transportId}: Error in subscriber: {ex.Message}");
                
                // If the message requires acknowledgement, send a negative acknowledgement
                if (message.RequireAcknowledgement)
                {
                    await AcknowledgeAsync(message.MessageId, false, ex.Message).ConfigureAwait(false);
                }
            }
        }
        
        // Dictionary to track TaskCompletionSource instances for acknowledgements
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _acknowledgementHandlers = 
            new ConcurrentDictionary<string, TaskCompletionSource<bool>>();
        
        /// <summary>
        /// Gets the TaskCompletionSource for a message acknowledgement
        /// </summary>
        /// <param name="messageId">The message ID</param>
        /// <param name="tcs">The TaskCompletionSource, if found</param>
        /// <returns>True if the TaskCompletionSource was found; otherwise, false</returns>
        private bool TryGetAcknowledgementHandler(string messageId, out TaskCompletionSource<bool> tcs)
        {
            return _acknowledgementHandlers.TryRemove(messageId, out tcs!);
        }
        
        /// <summary>
        /// Creates a Task that will complete when a message is acknowledged
        /// </summary>
        /// <param name="messageId">The message ID</param>
        /// <returns>A task that completes when the message is acknowledged</returns>
        private Task<bool> WaitForAcknowledgementAsync(string messageId)
        {
            var tcs = new TaskCompletionSource<bool>();
            _acknowledgementHandlers.TryAdd(messageId, tcs);
            
            // Set up a cancellation registration
            _cancellationTokenSource.Token.Register(() =>
            {
                if (_acknowledgementHandlers.TryRemove(messageId, out var handler))
                {
                    handler.TrySetCanceled();
                }
            });
            
            return tcs.Task;
        }
        
        /// <summary>
        /// Called by the ShutdownCoordinator to perform cleanup
        /// </summary>
        public async Task ShutdownAsync(CancellationToken token)
        {
            Console.WriteLine($"InProcessMessageTransport {_transportId}: Performing coordinated shutdown");
            
            await StopAsync().ConfigureAwait(false);
            
            // Remove from the transport registry
            _transports.TryRemove(_transportId, out _);
            
            // Cancel any pending operations
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch
            {
                // Ignore cancellation errors
            }
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
            
            // Remove from the transport registry
            _transports.TryRemove(_transportId, out _);
            
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
            
            // Clear collections
            _subscribers.Clear();
            _pendingAcknowledgements.Clear();
            _acknowledgementHandlers.Clear();
            
            Console.WriteLine($"InProcessMessageTransport {_transportId} disposed");
        }
    }
}