using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// A socket-based communication adapter that handles low-level network communications
    /// between microservices and the central message broker
    /// </summary>
    public class SocketCommunicationAdapter : IDisposable
    {
        private readonly string _serviceId;
        private readonly int _publisherPort;
        private readonly int _subscriberPort;
        private readonly bool _verbose;
        private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
        private CancellationTokenSource _cancellationTokenSource;
        private Task _publisherTask;
        private Task _subscriberTask;
        private Task _processingTask;
        private PublisherSocket _publisherSocket;
        private SubscriberSocket _subscriberSocket;
        private bool _disposed = false;
        private readonly Logger _logger;
        private readonly PokerGame.Core.Messaging.ExecutionContext _executionContext;

        // Event raised when a new message is received
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        /// <summary>
        /// Creates a new socket communication adapter
        /// </summary>
        /// <param name="serviceId">The unique ID of the service using this adapter</param>
        /// <param name="publisherPort">The port on which this adapter will publish messages</param>
        /// <param name="subscriberPort">The port on which this adapter will subscribe to messages</param>
        /// <param name="verbose">Whether to log verbose messages</param>
        public SocketCommunicationAdapter(string serviceId, int publisherPort, int subscriberPort, bool verbose = false)
            : this(serviceId, publisherPort, subscriberPort, new PokerGame.Core.Messaging.ExecutionContext(), verbose)
        {
        }
        
        /// <summary>
        /// Creates a new socket communication adapter with an execution context
        /// </summary>
        /// <param name="serviceId">The unique ID of the service using this adapter</param>
        /// <param name="publisherPort">The port on which this adapter will publish messages</param>
        /// <param name="subscriberPort">The port on which this adapter will subscribe to messages</param>
        /// <param name="executionContext">The execution context for this adapter</param>
        /// <param name="verbose">Whether to log verbose messages</param>
        public SocketCommunicationAdapter(string serviceId, int publisherPort, int subscriberPort, PokerGame.Core.Messaging.ExecutionContext executionContext, bool verbose = false)
        {
            _serviceId = serviceId;
            _publisherPort = publisherPort;
            _subscriberPort = subscriberPort;
            _verbose = verbose;
            
            // If no execution context is provided, create one from the current thread
            _executionContext = executionContext ?? PokerGame.Core.Messaging.ExecutionContext.FromCurrentThread();
            
            // Use the cancellation token source from the execution context, or create a new one
            _cancellationTokenSource = _executionContext.CancellationTokenSource ?? new CancellationTokenSource();
            
            // Create a logger with a unique name based on the service ID
            _logger = new Logger($"{serviceId}_MessageBroker", verbose);
            
            _logger.Log($"Created socket communication adapter for service {serviceId} with publisher={publisherPort}, subscriber={subscriberPort}");
            _logger.Log($"Using execution context: ThreadId={_executionContext.ThreadId}, IsTest={_executionContext.IsTestContext}");
        }

        /// <summary>
        /// Starts the socket communication adapter
        /// </summary>
        public void Start()
        {
            try
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(SocketCommunicationAdapter));
                }
                
                _logger.Log("Starting socket communication adapter...");
                
                // Start publisher socket
                _publisherSocket = new PublisherSocket();
                _publisherSocket.Options.SendHighWatermark = 1000;
                _publisherSocket.Bind($"tcp://0.0.0.0:{_publisherPort}");
                _logger.Log($"Publisher socket bound to port {_publisherPort}");
                
                // Start subscriber socket
                _subscriberSocket = new SubscriberSocket();
                _subscriberSocket.Options.ReceiveHighWatermark = 1000;
                _subscriberSocket.Connect($"tcp://localhost:{_subscriberPort}");
                _subscriberSocket.SubscribeToAnyTopic();
                _logger.Log($"Subscriber socket connected to port {_subscriberPort}");
                
                // Start processing tasks based on the execution context
                StartTasks();
                
                _logger.Log("Socket communication adapter started successfully");
            }
            catch (Exception ex)
            {
                _logger.Log($"Error starting socket communication adapter: {ex.Message}");
                // Re-throw the exception to let the caller handle it
                throw;
            }
        }
        
        /// <summary>
        /// Starts the adapter's processing tasks using the execution context if available
        /// </summary>
        private void StartTasks()
        {
            var token = _cancellationTokenSource.Token;
            
            // If we have a TaskScheduler in our execution context, use it
            if (_executionContext.TaskScheduler != null)
            {
                _logger.Log("Starting adapter tasks using execution context's TaskScheduler");
                
                _publisherTask = Task.Factory.StartNew(
                    () => PublisherLoop(token),
                    token,
                    TaskCreationOptions.LongRunning,
                    _executionContext.TaskScheduler);
                    
                _subscriberTask = Task.Factory.StartNew(
                    () => SubscriberLoop(token),
                    token,
                    TaskCreationOptions.LongRunning,
                    _executionContext.TaskScheduler);
                    
                _processingTask = Task.Factory.StartNew(
                    () => MessageProcessingLoop(token),
                    token,
                    TaskCreationOptions.LongRunning,
                    _executionContext.TaskScheduler);
            }
            // Otherwise fall back to standard Task.Run
            else
            {
                _logger.Log("Starting adapter tasks using standard Task.Run");
                
                _publisherTask = Task.Run(() => PublisherLoop(token), token);
                _subscriberTask = Task.Run(() => SubscriberLoop(token), token);
                _processingTask = Task.Run(() => MessageProcessingLoop(token), token);
            }
        }

        /// <summary>
        /// Stops the socket communication adapter
        /// </summary>
        public void Stop()
        {
            try
            {
                if (!_disposed)
                {
                    _logger.Log("Stopping socket communication adapter...");
                    _cancellationTokenSource.Cancel();
                    
                    // Wait for all tasks to complete with a timeout
                    Task.WaitAll(new[] { _publisherTask, _subscriberTask, _processingTask }, 1000);
                    
                    _logger.Log("Socket communication adapter stopped");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error stopping socket communication adapter: {ex.Message}");
            }
        }

        /// <summary>
        /// Publishes a message
        /// </summary>
        /// <param name="message">The message to publish</param>
        public void Publish(SimpleMessage message)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SocketCommunicationAdapter));
            }
            
            try
            {
                // Make sure the message has the correct sender ID
                message.SenderId = _serviceId;
                
                // Serialize the message
                string json = JsonSerializer.Serialize(message);
                
                // Enqueue the message for publishing
                _messageQueue.Enqueue(json);
                
                if (_verbose)
                {
                    _logger.Log($"Enqueued message of type {message.Type} for publishing");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Error publishing message: {ex.Message}");
                // Don't rethrow - publishing errors shouldn't crash the application
            }
        }

        private void PublisherLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Try to dequeue a message
                        if (_messageQueue.TryDequeue(out string json))
                        {
                            // Publish the message
                            _publisherSocket.SendFrame(json);
                            
                            if (_verbose)
                            {
                                _logger.Log($"Published message: {json.Substring(0, Math.Min(json.Length, 50))}...");
                            }
                        }
                        else
                        {
                            // No messages to send, sleep for a short time
                            Thread.Sleep(10);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Error in publisher loop: {ex.Message}");
                        // Sleep a bit longer after an error
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Publisher loop terminated with error: {ex.Message}");
            }
        }

        private void SubscriberLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Check if there's a message available
                        if (_subscriberSocket.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out string json))
                        {
                            // We received a message
                            if (!string.IsNullOrEmpty(json))
                            {
                                try
                                {
                                    // Deserialize and process the message
                                    SimpleMessage message = JsonSerializer.Deserialize<SimpleMessage>(json);
                                    
                                    // Don't skip response messages that are addressed directly to us
                                    bool isResponseToUs = !string.IsNullOrEmpty(message.InResponseTo) && message.ReceiverId == _serviceId;
                                    
                                    // Skip our own messages that aren't responses to us
                                    if (message.SenderId == _serviceId && !isResponseToUs)
                                    {
                                        if (_verbose)
                                        {
                                            _logger.Log($"Skipping own message: {message.MessageId}");
                                        }
                                        continue;
                                    }
                                    
                                    // Raise the MessageReceived event
                                    MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message, json));
                                    
                                    if (_verbose)
                                    {
                                        _logger.Log($"Received message type: {message.Type}, from: {message.SenderId}, to: {message.ReceiverId}");
                                    }
                                }
                                catch (JsonException ex)
                                {
                                    _logger.Log($"Error deserializing message: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Error in subscriber loop: {ex.Message}");
                        // Sleep a bit longer after an error
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Subscriber loop terminated with error: {ex.Message}");
            }
        }

        private void MessageProcessingLoop(CancellationToken cancellationToken)
        {
            try
            {
                // This loop exists primarily to keep the NetMQ context alive
                // and could be expanded for more complex processing if needed
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Sleep to avoid busy waiting
                        Thread.Sleep(100);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Error in message processing loop: {ex.Message}");
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Message processing loop terminated with error: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes all resources used by the socket communication adapter
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
                
            _disposed = true;
            
            try
            {
                // Stop tasks
                Stop();
                
                // Dispose sockets
                _publisherSocket?.Dispose();
                _subscriberSocket?.Dispose();
                
                // Dispose cancellation token source
                _cancellationTokenSource.Dispose();
                
                _logger.Log("Socket communication adapter disposed");
            }
            catch (Exception ex)
            {
                _logger.Log($"Error disposing socket communication adapter: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Event arguments for the MessageReceived event
    /// </summary>
    public class MessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// The received message
        /// </summary>
        public SimpleMessage Message { get; }
        
        /// <summary>
        /// The raw JSON of the message
        /// </summary>
        public string RawJson { get; }
        
        public MessageReceivedEventArgs(SimpleMessage message, string rawJson)
        {
            Message = message;
            RawJson = rawJson;
        }
    }
}