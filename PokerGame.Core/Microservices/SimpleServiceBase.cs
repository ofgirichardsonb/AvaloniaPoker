using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PokerGame.Core.Messaging;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Base class for simplified services that use SimpleMessage for communication
    /// </summary>
    [Obsolete("This class has been replaced with MicroserviceBase and will be removed in a future release.")]
    public abstract class SimpleServiceBase : IDisposable
    {
        private readonly string _serviceName;
        private readonly string _serviceType;
        private readonly string _serviceId;
        private readonly int _publisherPort;
        private readonly int _subscriberPort;
        private readonly bool _verbose;
        private readonly SocketCommunicationAdapter _socketAdapter;
        private readonly Dictionary<string, ServiceInfo> _serviceRegistry = new Dictionary<string, ServiceInfo>();
        private readonly List<string> _acknowledgedMessageIds = new List<string>();
        private readonly object _serviceRegistryLock = new object();
        private bool _isRunning;
        private bool _isDisposed;
        private readonly PokerGame.Core.Messaging.ExecutionContext? _executionContext;
        private Task? _heartbeatTask;
        private Task? _registrationTask;
        private CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// Gets the unique identifier of this service
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

        /// <summary>
        /// Gets a value indicating whether verbose logging is enabled
        /// </summary>
        protected bool Verbose => _verbose;

        /// <summary>
        /// Creates a new simple service base
        /// </summary>
        /// <param name="serviceName">The name of the service</param>
        /// <param name="serviceType">The type of the service</param>
        /// <param name="publisherPort">The port to publish messages on</param>
        /// <param name="subscriberPort">The port to subscribe to messages on</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        protected SimpleServiceBase(string serviceName, string serviceType, int publisherPort, int subscriberPort, bool verbose = false)
        {
            _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
            _serviceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
            _serviceId = Guid.NewGuid().ToString();
            _publisherPort = publisherPort;
            _subscriberPort = subscriberPort;
            _verbose = verbose;
            _socketAdapter = new SocketCommunicationAdapter(_serviceId, publisherPort, subscriberPort, verbose);
            _socketAdapter.MessageReceived += OnMessageReceived;
        }

        /// <summary>
        /// Creates a new simple service base with a provided execution context
        /// </summary>
        /// <param name="serviceName">The name of the service</param>
        /// <param name="serviceType">The type of the service</param>
        /// <param name="publisherPort">The port to publish messages on</param>
        /// <param name="subscriberPort">The port to subscribe to messages on</param>
        /// <param name="executionContext">The execution context to use</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        protected SimpleServiceBase(string serviceName, string serviceType, int publisherPort, int subscriberPort, PokerGame.Core.Messaging.ExecutionContext? executionContext = null, bool verbose = false)
            : this(serviceName, serviceType, publisherPort, subscriberPort, verbose)
        {
            _executionContext = executionContext;
        }

        /// <summary>
        /// Starts the service
        /// </summary>
        public virtual void Start()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(SimpleServiceBase));

            if (_isRunning)
                return;

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Start the socket adapter
            _socketAdapter.Start();
            
            // Start sending heartbeats
            _heartbeatTask = Task.Run(SendHeartbeatsAsync);
            
            // Start sending service registrations
            _registrationTask = Task.Run(SendRegistrationsAsync);
            
            Console.WriteLine($"Service {_serviceName} ({_serviceType}) started with ID {_serviceId}");
            
            if (_verbose)
            {
                Console.WriteLine($"  Publishing on port {_publisherPort}");
                Console.WriteLine($"  Subscribing on port {_subscriberPort}");
            }
        }

        /// <summary>
        /// Stops the service
        /// </summary>
        public virtual void Stop()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(SimpleServiceBase));

            if (!_isRunning)
                return;

            _isRunning = false;
            _cancellationTokenSource?.Cancel();
            
            // Stop the socket adapter
            _socketAdapter.Stop();
            
            Console.WriteLine($"Service {_serviceName} ({_serviceType}) stopped");
        }

        /// <summary>
        /// Disposes the service
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the service
        /// </summary>
        /// <param name="disposing">Whether this is being called from Dispose()</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                Stop();
                _socketAdapter.Dispose();
                _cancellationTokenSource?.Dispose();
            }

            _isDisposed = true;
        }

        /// <summary>
        /// Sends heartbeats periodically
        /// </summary>
        private async Task SendHeartbeatsAsync()
        {
            if (_cancellationTokenSource == null)
                return;
                
            try
            {
                while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var heartbeatMessage = SimpleMessage.Create(SimpleMessageType.Heartbeat);
                    heartbeatMessage.SenderId = _serviceId;
                    
                    // Convert to NetworkMessage and publish
                    PublishMessage(heartbeatMessage.ToNetworkMessage());
                    
                    await Task.Delay(5000, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending heartbeats: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends service registrations periodically
        /// </summary>
        private async Task SendRegistrationsAsync()
        {
            if (_cancellationTokenSource == null)
                return;
                
            try
            {
                while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var payload = new ServiceRegistrationPayload
                    {
                        ServiceId = _serviceId,
                        ServiceName = _serviceName,
                        ServiceType = _serviceType
                    };
                    
                    var registrationMessage = SimpleMessage.Create(SimpleMessageType.ServiceRegistration, payload);
                    registrationMessage.SenderId = _serviceId;
                    
                    // Convert to NetworkMessage and publish
                    PublishMessage(registrationMessage.ToNetworkMessage());
                    
                    await Task.Delay(10000, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending registrations: {ex.Message}");
            }
        }

        /// <summary>
        /// Publishes a message to all subscribers
        /// </summary>
        /// <param name="message">The message to publish</param>
        protected void PublishMessage(NetworkMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
                
            if (string.IsNullOrEmpty(message.SenderId))
                message.SenderId = _serviceId;
                
            _socketAdapter.Publish(message);
        }

        /// <summary>
        /// Handles the MessageReceived event from the socket adapter
        /// </summary>
        private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            if (e.Message != null)
            {
                // Convert NetworkMessage to SimpleMessage for backward compatibility
                SimpleMessage simpleMessage = e.Message.ToSimpleMessage();
                HandleMessage(simpleMessage);
            }
        }
        
        /// <summary>
        /// Handles a received message
        /// </summary>
        /// <param name="message">The message to handle</param>
        protected virtual void HandleMessage(SimpleMessage message)
        {
            if (message == null)
                return;
                
            if (_verbose)
            {
                Console.WriteLine($"Received message: {message.Type} from {message.SenderId}");
            }
            
            switch (message.Type)
            {
                case SimpleMessageType.Heartbeat:
                    // No need to do anything with heartbeats
                    break;
                case SimpleMessageType.ServiceRegistration:
                    var payload = message.GetPayload<ServiceRegistrationPayload>();
                    HandleServiceRegistration(payload, message);
                    break;
                case SimpleMessageType.Acknowledgment:
                    HandleAcknowledgment(message);
                    break;
                case SimpleMessageType.Error:
                    var errorMessage = message.GetPayloadAsString();
                    Console.WriteLine($"Error from {message.SenderId}: {errorMessage}");
                    break;
                default:
                    // Let derived classes handle other message types
                    break;
            }
        }

        /// <summary>
        /// Handles a service registration message
        /// </summary>
        /// <param name="payload">The service registration payload</param>
        /// <param name="message">The original message</param>
        protected virtual void HandleServiceRegistration(ServiceRegistrationPayload payload, SimpleMessage message)
        {
            if (payload == null || string.IsNullOrEmpty(payload.ServiceId))
                return;
                
            lock (_serviceRegistryLock)
            {
                if (!_serviceRegistry.TryGetValue(payload.ServiceId, out var serviceInfo))
                {
                    serviceInfo = new ServiceInfo
                    {
                        ServiceId = payload.ServiceId,
                        ServiceName = payload.ServiceName,
                        ServiceType = payload.ServiceType,
                        LastSeen = DateTime.UtcNow
                    };
                    
                    _serviceRegistry[payload.ServiceId] = serviceInfo;
                    
                    if (_verbose)
                    {
                        Console.WriteLine($"Registered service: {payload.ServiceName} ({payload.ServiceType}) with ID {payload.ServiceId}");
                    }
                }
                else
                {
                    serviceInfo.LastSeen = DateTime.UtcNow;
                    
                    if (_verbose)
                    {
                        Console.WriteLine($"Updated service: {payload.ServiceName} ({payload.ServiceType}) with ID {payload.ServiceId}");
                    }
                }
            }
            
            // Send acknowledgment
            var acknowledgment = SimpleMessage.CreateAcknowledgment(message);
            acknowledgment.SenderId = _serviceId;
            
            // Convert to NetworkMessage and publish
            PublishMessage(acknowledgment.ToNetworkMessage());
        }

        /// <summary>
        /// Handles an acknowledgment message
        /// </summary>
        /// <param name="message">The acknowledgment message</param>
        protected virtual void HandleAcknowledgment(SimpleMessage message)
        {
            if (string.IsNullOrEmpty(message.InResponseTo))
                return;
                
            lock (_acknowledgedMessageIds)
            {
                if (!_acknowledgedMessageIds.Contains(message.InResponseTo))
                {
                    _acknowledgedMessageIds.Add(message.InResponseTo);
                    
                    if (_verbose)
                    {
                        Console.WriteLine($"Message {message.InResponseTo} acknowledged by {message.SenderId}");
                    }
                }
            }
            
            // Send acknowledgment to the acknowledgment
            // This helps with service discovery
            var ackAck = SimpleMessage.CreateAcknowledgment(message);
            ackAck.SenderId = _serviceId;
            
            // Convert to NetworkMessage and publish
            PublishMessage(ackAck.ToNetworkMessage());
        }

        /// <summary>
        /// Gets a list of all registered services
        /// </summary>
        /// <returns>A list of service information</returns>
        protected IReadOnlyList<ServiceInfo> GetRegisteredServices()
        {
            lock (_serviceRegistryLock)
            {
                return _serviceRegistry.Values.ToList();
            }
        }

        /// <summary>
        /// Gets a service by its ID
        /// </summary>
        /// <param name="serviceId">The ID of the service to get</param>
        /// <returns>The service information, or null if not found</returns>
        protected ServiceInfo? GetServiceById(string serviceId)
        {
            lock (_serviceRegistryLock)
            {
                if (_serviceRegistry.TryGetValue(serviceId, out var serviceInfo))
                    return serviceInfo;
                    
                return null;
            }
        }

        /// <summary>
        /// Gets a service by its type
        /// </summary>
        /// <param name="serviceType">The type of the service to get</param>
        /// <returns>The first service of the specified type, or null if not found</returns>
        protected ServiceInfo? GetServiceByType(string serviceType)
        {
            lock (_serviceRegistryLock)
            {
                foreach (var serviceInfo in _serviceRegistry.Values)
                {
                    if (serviceInfo.ServiceType == serviceType)
                        return serviceInfo;
                }
                
                return null;
            }
        }

        /// <summary>
        /// Gets all services of a specific type
        /// </summary>
        /// <param name="serviceType">The type of services to get</param>
        /// <returns>A list of services of the specified type</returns>
        protected IReadOnlyList<ServiceInfo> GetServicesByType(string serviceType)
        {
            lock (_serviceRegistryLock)
            {
                return _serviceRegistry.Values
                    .Where(s => s.ServiceType == serviceType)
                    .ToList();
            }
        }
    }
    
    /// <summary>
    /// Information about a service
    /// </summary>
    [Obsolete("This class has been replaced with ServiceInfo in MessageTypes.cs and will be removed in a future release.")]
    public class ServiceInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier of the service
        /// </summary>
        public string ServiceId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the name of the service
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the type of the service
        /// </summary>
        public string ServiceType { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the timestamp when the service was last seen
        /// </summary>
        public DateTime LastSeen { get; set; }
    }
}