using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// A client that connects to the central message broker
    /// </summary>
    public class BrokerClient : IDisposable
    {
        private readonly BrokerLogger _logger = BrokerLogger.Instance;
        private readonly TelemetryHelper _telemetry = TelemetryHelper.Instance;
        private readonly string _clientId;
        private readonly string _clientName;
        private readonly string _clientType;
        private readonly List<string> _capabilities;
        private readonly string _brokerAddress;
        private readonly int _brokerPort;
        private bool _telemetryEnabled = false;
        
        private DealerSocket? _socket;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task? _receiveTask;
        
        private readonly ConcurrentDictionary<string, TaskCompletionSource<BrokerMessage>> _pendingRequests = new ConcurrentDictionary<string, TaskCompletionSource<BrokerMessage>>();
        private readonly ConcurrentDictionary<string, ServiceRegistrationPayload> _knownServices = new ConcurrentDictionary<string, ServiceRegistrationPayload>();
        
        // Metrics for telemetry
        private long _messagesSent = 0;
        private long _messagesReceived = 0;
        private long _requestsSent = 0;
        private long _responseReceived = 0;
        // Tracking for acknowledgments (uncommented when implemented)
        // private long _acknowledgmentsSent = 0;
        private long _acknowledgementsReceived = 0;
        
        // Event for received messages
        public event EventHandler<BrokerMessage>? MessageReceived;
        
        /// <summary>
        /// Gets the unique identifier for this client
        /// </summary>
        public string ClientId => _clientId;
        
        /// <summary>
        /// Gets the name of this client
        /// </summary>
        public string ClientName => _clientName;
        
        /// <summary>
        /// Gets the type of this client
        /// </summary>
        public string ClientType => _clientType;
        
        /// <summary>
        /// Gets the capabilities of this client
        /// </summary>
        public IReadOnlyList<string> Capabilities => _capabilities.AsReadOnly();
        
        /// <summary>
        /// Gets the broker address that this client is connected to
        /// </summary>
        public string BrokerAddress => _brokerAddress;
        
        /// <summary>
        /// Gets the broker port that this client is connected to
        /// </summary>
        public int BrokerPort => _brokerPort;
        
        /// <summary>
        /// Gets a value indicating whether this client is connected to the broker
        /// </summary>
        public bool IsConnected => _socket != null && !_socket.IsDisposed;
        
        /// <summary>
        /// Creates a new instance of the broker client
        /// </summary>
        /// <param name="clientName">The name of the client</param>
        /// <param name="clientType">The type of the client</param>
        /// <param name="capabilities">The capabilities of the client</param>
        /// <param name="brokerAddress">The address of the broker</param>
        /// <param name="brokerPort">The port of the broker</param>
        public BrokerClient(
            string clientName,
            string clientType,
            List<string>? capabilities = null,
            string brokerAddress = "localhost",
            int brokerPort = 5570)
        {
            _clientId = $"{clientType}-{Guid.NewGuid()}";
            _clientName = clientName;
            _clientType = clientType;
            _capabilities = capabilities ?? new List<string>();
            _brokerAddress = brokerAddress;
            _brokerPort = brokerPort;
            
            _logger.Info("BrokerClient", $"Creating client with ID {_clientId} ({_clientName}, {_clientType})");
        }
        
        /// <summary>
        /// Initializes telemetry with the provided instrumentation key
        /// </summary>
        /// <param name="instrumentationKey">The Application Insights instrumentation key</param>
        public bool InitializeTelemetry(string? instrumentationKey = null)
        {
            try
            {
                if (string.IsNullOrEmpty(instrumentationKey))
                {
                    // Try to get from environment variable
                    instrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
                }
                
                if (!string.IsNullOrEmpty(instrumentationKey))
                {
                    _logger.Info("BrokerClient", "Initializing telemetry...");
                    if (_telemetry.Initialize(instrumentationKey))
                    {
                        _telemetryEnabled = true;
                        _logger.Info("BrokerClient", "Telemetry initialized successfully");
                        
                        // Track telemetry initialization
                        var props = new Dictionary<string, string>
                        {
                            { "ClientId", _clientId },
                            { "ClientName", _clientName },
                            { "ClientType", _clientType }
                        };
                        
                        _telemetry.TrackClientEvent(_clientId, "TelemetryInitialized", props);
                        return true;
                    }
                    else
                    {
                        _logger.Error("BrokerClient", "Failed to initialize telemetry");
                    }
                }
                else
                {
                    _logger.Warning("BrokerClient", "Application Insights instrumentation key not provided, telemetry disabled");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("BrokerClient", "Error initializing telemetry", ex);
            }
            
            _telemetryEnabled = false;
            return false;
        }
        
        /// <summary>
        /// Connects to the broker
        /// </summary>
        /// <param name="enableTelemetry">Whether to enable telemetry</param>
        /// <param name="instrumentationKey">The Application Insights instrumentation key (or null to use environment variable)</param>
        public void Connect(bool enableTelemetry = false, string? instrumentationKey = null)
        {
            try
            {
                _logger.Info("BrokerClient", $"Connecting to broker at {_brokerAddress}:{_brokerPort}");
                
                // Initialize telemetry if enabled
                if (enableTelemetry)
                {
                    InitializeTelemetry(instrumentationKey);
                }
                
                // Create and configure socket
                _socket = new DealerSocket();
                _socket.Options.Identity = System.Text.Encoding.UTF8.GetBytes(_clientId);
                _socket.Connect($"tcp://{_brokerAddress}:{_brokerPort}");
                
                // Track connection in telemetry
                if (_telemetryEnabled)
                {
                    var props = new Dictionary<string, string>
                    {
                        { "BrokerAddress", _brokerAddress },
                        { "BrokerPort", _brokerPort.ToString() }
                    };
                    
                    _telemetry.TrackClientEvent(_clientId, "ClientConnecting", props);
                }
                
                // Start receiving messages
                _receiveTask = Task.Run(ReceiveMessagesAsync);
                
                // Register with the broker
                RegisterWithBroker();
                
                _logger.Info("BrokerClient", "Connected to broker successfully");
                
                // Track successful connection in telemetry
                if (_telemetryEnabled)
                {
                    _telemetry.TrackClientEvent(_clientId, "ClientConnected");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("BrokerClient", "Error connecting to broker", ex);
                
                // Track connection failure in telemetry
                if (_telemetryEnabled)
                {
                    _telemetry.TrackException(ex, "BrokerClient");
                }
                
                throw;
            }
        }
        
        /// <summary>
        /// Connects to the broker
        /// </summary>
        public void Connect()
        {
            Connect(false);
        }
        
        /// <summary>
        /// Registers this client with the broker
        /// </summary>
        private void RegisterWithBroker()
        {
            try
            {
                var registration = new ServiceRegistrationPayload
                {
                    ServiceId = _clientId,
                    ServiceName = _clientName,
                    ServiceType = _clientType,
                    Capabilities = new List<string>(_capabilities)
                };
                
                var message = BrokerMessage.Create(BrokerMessageType.ServiceRegistration, registration);
                message.SenderId = _clientId;
                message.RequiresAcknowledgment = true;
                
                _logger.Info("BrokerClient", $"Registering with broker as {_clientId} ({_clientName}, {_clientType})");
                SendMessageAsync(message).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.Error("BrokerClient", "Error registering with broker", ex);
            }
        }
        
        /// <summary>
        /// Receives messages from the broker asynchronously
        /// </summary>
        private async Task ReceiveMessagesAsync()
        {
            try
            {
                _logger.Debug("BrokerClient", "Starting message receive loop");
                
                while (!_cancellationTokenSource.Token.IsCancellationRequested && _socket != null && !_socket.IsDisposed)
                {
                    try
                    {
                        // Try to receive a message with a short timeout
                        if (_socket.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out string? messageJson))
                        {
                            if (!string.IsNullOrEmpty(messageJson))
                            {
                                var message = BrokerMessage.FromJson(messageJson);
                                if (message != null)
                                {
                                    ProcessReceivedMessage(message);
                                }
                            }
                        }
                    }
                    catch (NetMQException ex)
                    {
                        _logger.Error("BrokerClient", "Error receiving message", ex);
                        await Task.Delay(1000, _cancellationTokenSource.Token);
                    }
                    
                    // Small delay to prevent CPU overuse
                    await Task.Delay(5, _cancellationTokenSource.Token);
                }
                
                _logger.Debug("BrokerClient", "Message receive loop terminated");
            }
            catch (TaskCanceledException)
            {
                // Normal during shutdown
            }
            catch (Exception ex)
            {
                _logger.Error("BrokerClient", "Error in receive loop", ex);
            }
        }
        
        /// <summary>
        /// Processes a received message
        /// </summary>
        /// <param name="message">The message to process</param>
        private void ProcessReceivedMessage(BrokerMessage message)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            
            try
            {
                _logger.Debug("BrokerClient", $"Received message: Type={message.Type}, ID={message.MessageId}, From={message.SenderId}, To={message.ReceiverId}");
                
                // Track message received in telemetry
                if (_telemetryEnabled)
                {
                    var props = new Dictionary<string, string>
                    {
                        { "MessageId", message.MessageId },
                        { "MessageType", message.Type.ToString() },
                        { "SenderId", message.SenderId ?? "unknown" }
                    };
                    
                    if (!string.IsNullOrEmpty(message.Topic))
                    {
                        props["Topic"] = message.Topic;
                    }
                    
                    _telemetry.TrackClientEvent(_clientId, "MessageReceived", props);
                    
                    if (message.Type == BrokerMessageType.Response)
                    {
                        Interlocked.Increment(ref _responseReceived);
                    }
                    else if (message.Type == BrokerMessageType.Acknowledgment)
                    {
                        Interlocked.Increment(ref _acknowledgementsReceived);
                    }
                    
                    Interlocked.Increment(ref _messagesReceived);
                }
                
                // Check if this is a response to a pending request
                if (!string.IsNullOrEmpty(message.InResponseTo) && _pendingRequests.TryRemove(message.InResponseTo, out var tcs))
                {
                    _logger.Debug("BrokerClient", $"Completing pending request: {message.InResponseTo}");
                    
                    // Track response received in telemetry if it's a response to our request
                    if (_telemetryEnabled)
                    {
                        var props = new Dictionary<string, string>
                        {
                            { "MessageId", message.MessageId },
                            { "RequestId", message.InResponseTo },
                            { "MessageType", message.Type.ToString() },
                            { "SenderId", message.SenderId ?? "unknown" }
                        };
                        
                        _telemetry.TrackClientEvent(_clientId, "ResponseReceived", props);
                    }
                    
                    tcs.SetResult(message);
                    return;
                }
                
                // Handle system messages
                if (HandleSystemMessage(message))
                {
                    return;
                }
                
                // Notify subscribers about the received message
                MessageReceived?.Invoke(this, message);
            }
            catch (Exception ex)
            {
                _logger.Error("BrokerClient", $"Error processing message: {message.MessageId}", ex);
                
                // Track processing error in telemetry
                if (_telemetryEnabled)
                {
                    var props = new Dictionary<string, string>
                    {
                        { "MessageId", message.MessageId },
                        { "MessageType", message.Type.ToString() },
                        { "SenderId", message.SenderId ?? "unknown" },
                        { "Duration", stopwatch.ElapsedMilliseconds.ToString() },
                        { "ErrorMessage", ex.Message }
                    };
                    
                    _telemetry.TrackClientEvent(_clientId, "MessageProcessingFailed", props);
                    _telemetry.TrackException(ex, "BrokerClient");
                }
            }
        }
        
        /// <summary>
        /// Handles system-level messages
        /// </summary>
        /// <param name="message">The message to handle</param>
        /// <returns>True if the message was handled, otherwise false</returns>
        private bool HandleSystemMessage(BrokerMessage message)
        {
            switch (message.Type)
            {
                case BrokerMessageType.Heartbeat:
                    // Respond to heartbeat with a heartbeat
                    var response = BrokerMessage.Create(BrokerMessageType.Heartbeat);
                    response.SenderId = _clientId;
                    response.ReceiverId = message.SenderId;
                    response.InResponseTo = message.MessageId;
                    
                    SendMessageAsync(response).ConfigureAwait(false);
                    return true;
                    
                case BrokerMessageType.ServiceRegistration:
                    // Store the service information
                    var registration = message.GetPayload<ServiceRegistrationPayload>();
                    if (registration != null)
                    {
                        _knownServices[registration.ServiceId] = registration;
                        _logger.Debug("BrokerClient", $"Registered service: {registration.ServiceId} ({registration.ServiceName}, {registration.ServiceType})");
                    }
                    return true;
                    
                case BrokerMessageType.Acknowledgment:
                    // Just log it
                    _logger.Debug("BrokerClient", $"Received acknowledgment for message: {message.InResponseTo}");
                    return true;
                    
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Sends a message to the broker asynchronously
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <returns>A task that completes when the message is sent</returns>
        public async Task SendMessageAsync(BrokerMessage message)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            
            try
            {
                if (_socket == null || _socket.IsDisposed)
                {
                    throw new InvalidOperationException("Not connected to broker");
                }
                
                // Ensure sender ID is set
                if (string.IsNullOrEmpty(message.SenderId))
                {
                    message.SenderId = _clientId;
                }
                
                var messageJson = message.ToJson();
                
                _logger.Debug("BrokerClient", $"Sending message: Type={message.Type}, ID={message.MessageId}, To={message.ReceiverId}");
                
                // Track message send in telemetry
                if (_telemetryEnabled)
                {
                    var props = new Dictionary<string, string>
                    {
                        { "MessageId", message.MessageId },
                        { "MessageType", message.Type.ToString() },
                        { "ReceiverId", message.ReceiverId ?? "broadcast" }
                    };
                    
                    if (!string.IsNullOrEmpty(message.Topic))
                    {
                        props["Topic"] = message.Topic;
                    }
                    
                    _telemetry.TrackClientEvent(_clientId, "MessageSending", props);
                    
                    if (message.Type == BrokerMessageType.Request)
                    {
                        Interlocked.Increment(ref _requestsSent);
                    }
                    
                    Interlocked.Increment(ref _messagesSent);
                }
                
                // Send the message
                _socket.SendFrame(messageJson);
                
                // Track successful message send in telemetry
                if (_telemetryEnabled)
                {
                    var props = new Dictionary<string, string>
                    {
                        { "MessageId", message.MessageId },
                        { "MessageType", message.Type.ToString() },
                        { "ReceiverId", message.ReceiverId ?? "broadcast" },
                        { "Duration", stopwatch.ElapsedMilliseconds.ToString() }
                    };
                    
                    _telemetry.TrackClientEvent(_clientId, "MessageSent", props);
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error("BrokerClient", $"Error sending message: {message.MessageId}", ex);
                
                // Track send failure in telemetry
                if (_telemetryEnabled)
                {
                    var props = new Dictionary<string, string>
                    {
                        { "MessageId", message.MessageId },
                        { "MessageType", message.Type.ToString() },
                        { "ReceiverId", message.ReceiverId ?? "broadcast" },
                        { "Duration", stopwatch.ElapsedMilliseconds.ToString() },
                        { "ErrorMessage", ex.Message }
                    };
                    
                    _telemetry.TrackClientEvent(_clientId, "MessageSendFailed", props);
                    _telemetry.TrackException(ex, "BrokerClient");
                }
                
                throw;
            }
            finally
            {
                stopwatch.Stop();
            }
        }
        
        /// <summary>
        /// Sends a message and waits for a response
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <param name="timeout">The timeout for the request</param>
        /// <returns>The response message</returns>
        public async Task<BrokerMessage> SendRequestAsync(BrokerMessage message, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(30);
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            
            try
            {
                // Track request sending in telemetry
                if (_telemetryEnabled)
                {
                    var props = new Dictionary<string, string>
                    {
                        { "MessageId", message.MessageId },
                        { "MessageType", message.Type.ToString() },
                        { "ReceiverId", message.ReceiverId ?? "broadcast" },
                        { "Timeout", timeout.Value.TotalMilliseconds.ToString() }
                    };
                    
                    if (!string.IsNullOrEmpty(message.Topic))
                    {
                        props["Topic"] = message.Topic;
                    }
                    
                    _telemetry.TrackClientEvent(_clientId, "RequestSending", props);
                }
                
                // Create a task completion source for the response
                var tcs = new TaskCompletionSource<BrokerMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingRequests[message.MessageId] = tcs;
                
                // Send the message
                await SendMessageAsync(message);
                
                // Wait for the response with timeout
                using var cts = new CancellationTokenSource(timeout.Value);
                using var registration = cts.Token.Register(() => 
                {
                    if (tcs.TrySetException(new TimeoutException($"Request timed out after {timeout.Value.TotalSeconds} seconds")))
                    {
                        // Track request timeout in telemetry
                        if (_telemetryEnabled)
                        {
                            var props = new Dictionary<string, string>
                            {
                                { "MessageId", message.MessageId },
                                { "MessageType", message.Type.ToString() },
                                { "ReceiverId", message.ReceiverId ?? "broadcast" },
                                { "Timeout", timeout.Value.TotalMilliseconds.ToString() },
                                { "ElapsedTime", stopwatch.ElapsedMilliseconds.ToString() }
                            };
                            
                            _telemetry.TrackClientEvent(_clientId, "RequestTimeout", props);
                        }
                    }
                });
                
                var response = await tcs.Task;
                
                // Track successful request in telemetry
                if (_telemetryEnabled)
                {
                    var props = new Dictionary<string, string>
                    {
                        { "MessageId", message.MessageId },
                        { "ResponseId", response.MessageId },
                        { "MessageType", message.Type.ToString() },
                        { "ReceiverId", message.ReceiverId ?? "broadcast" },
                        { "ResponseTime", stopwatch.ElapsedMilliseconds.ToString() }
                    };
                    
                    _telemetry.TrackClientEvent(_clientId, "RequestCompleted", props);
                }
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.Error("BrokerClient", $"Error sending request: {message.MessageId}", ex);
                
                // Track request failure in telemetry
                if (_telemetryEnabled)
                {
                    var props = new Dictionary<string, string>
                    {
                        { "MessageId", message.MessageId },
                        { "MessageType", message.Type.ToString() },
                        { "ReceiverId", message.ReceiverId ?? "broadcast" },
                        { "Duration", stopwatch.ElapsedMilliseconds.ToString() },
                        { "ErrorMessage", ex.Message },
                        { "ErrorType", ex.GetType().Name }
                    };
                    
                    _telemetry.TrackClientEvent(_clientId, "RequestFailed", props);
                    _telemetry.TrackException(ex, "BrokerClient");
                }
                
                throw;
            }
            finally
            {
                stopwatch.Stop();
                
                // Remove from pending requests if still there
                _pendingRequests.TryRemove(message.MessageId, out _);
            }
        }
        
        /// <summary>
        /// Disposes the broker client
        /// </summary>
        public void Dispose()
        {
            try
            {
                _logger.Info("BrokerClient", "Shutting down client...");
                
                // Track client shutdown in telemetry
                if (_telemetryEnabled)
                {
                    _telemetry.TrackClientEvent(_clientId, "ClientShuttingDown");
                }
                
                // Cancel the receive loop
                _cancellationTokenSource.Cancel();
                
                // Wait for the receive loop to finish
                if (_receiveTask != null && !_receiveTask.IsCompleted)
                {
                    try
                    {
                        _receiveTask.Wait(1000);
                    }
                    catch (AggregateException)
                    {
                        // Ignore TaskCanceledException
                    }
                }
                
                // Clean up resources
                _socket?.Dispose();
                _cancellationTokenSource.Dispose();
                
                // Complete any pending requests with an error
                var pendingCount = _pendingRequests.Count;
                foreach (var request in _pendingRequests)
                {
                    request.Value.TrySetException(new Exception("Client was disposed"));
                }
                
                _pendingRequests.Clear();
                
                _logger.Info("BrokerClient", "Client shut down successfully");
                
                // Track client shutdown complete in telemetry
                if (_telemetryEnabled)
                {
                    var props = new Dictionary<string, string>
                    {
                        { "CompletedPendingRequests", pendingCount.ToString() }
                    };
                    
                    _telemetry.TrackClientEvent(_clientId, "ClientShutDown", props);
                    _telemetry.Flush();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("BrokerClient", "Error during client shutdown", ex);
                
                // Track shutdown error in telemetry
                if (_telemetryEnabled)
                {
                    _telemetry.TrackException(ex, "BrokerClient");
                    _telemetry.Flush();
                }
            }
        }
    }
}