using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.ServiceManagement;

namespace MSA.Foundation.Messaging
{
    /// <summary>
    /// Manages message transport instances and provides access to them
    /// </summary>
    public class MessageTransportManager : IShutdownParticipant, IDisposable
    {
        private static readonly Lazy<MessageTransportManager> _instance = 
            new Lazy<MessageTransportManager>(() => new MessageTransportManager());
            
        private readonly Dictionary<string, IMessageTransport> _transports = new Dictionary<string, IMessageTransport>();
        private readonly object _lock = new object();
        private bool _isDisposed = false;
        
        /// <summary>
        /// Gets the singleton instance of the MessageTransportManager
        /// </summary>
        public static MessageTransportManager Instance => _instance.Value;
        
        /// <summary>
        /// Gets the ID of this shutdown participant
        /// </summary>
        public string ParticipantId => "MessageTransportManager";
        
        /// <summary>
        /// Gets the priority of this participant in the shutdown sequence
        /// Message transports should be shut down early to prevent new messages from being sent
        /// </summary>
        public int ShutdownPriority => 100;
        
        /// <summary>
        /// Creates a new MessageTransportManager and registers it with the ShutdownCoordinator
        /// </summary>
        private MessageTransportManager()
        {
            // Register with the ShutdownCoordinator
            ShutdownCoordinator.Instance.RegisterParticipant(this);
            
            Console.WriteLine("MessageTransportManager initialized and registered with ShutdownCoordinator");
        }
        
        /// <summary>
        /// Registers a transport with the manager
        /// </summary>
        /// <param name="transportId">The unique identifier for the transport</param>
        /// <param name="transport">The transport to register</param>
        /// <exception cref="ArgumentException">Thrown if a transport with the same ID is already registered</exception>
        public void RegisterTransport(string transportId, IMessageTransport transport)
        {
            if (string.IsNullOrEmpty(transportId))
                throw new ArgumentException("Transport ID cannot be null or empty", nameof(transportId));
                
            if (transport == null)
                throw new ArgumentNullException(nameof(transport));
                
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(MessageTransportManager));
                    
                if (_transports.ContainsKey(transportId))
                    throw new ArgumentException($"A transport with ID '{transportId}' is already registered", nameof(transportId));
                    
                _transports.Add(transportId, transport);
                Console.WriteLine($"Registered message transport {transportId}");
            }
        }
        
        /// <summary>
        /// Gets a transport by ID
        /// </summary>
        /// <param name="transportId">The ID of the transport to get</param>
        /// <returns>The transport with the specified ID</returns>
        /// <exception cref="KeyNotFoundException">Thrown if a transport with the specified ID is not found</exception>
        public IMessageTransport GetTransport(string transportId)
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(MessageTransportManager));
                    
                if (!_transports.TryGetValue(transportId, out var transport))
                    throw new KeyNotFoundException($"No transport found with ID '{transportId}'");
                    
                return transport;
            }
        }
        
        /// <summary>
        /// Tries to get a transport by ID
        /// </summary>
        /// <param name="transportId">The ID of the transport to get</param>
        /// <param name="transport">The transport with the specified ID, if found</param>
        /// <returns>True if the transport was found; otherwise, false</returns>
        public bool TryGetTransport(string transportId, out IMessageTransport transport)
        {
            lock (_lock)
            {
                if (_isDisposed)
                {
                    transport = null!;
                    return false;
                }
                
                return _transports.TryGetValue(transportId, out transport!);
            }
        }
        
        /// <summary>
        /// Unregisters a transport by ID
        /// </summary>
        /// <param name="transportId">The ID of the transport to unregister</param>
        /// <returns>True if the transport was found and unregistered; otherwise, false</returns>
        public bool UnregisterTransport(string transportId)
        {
            lock (_lock)
            {
                if (_isDisposed)
                    return false;
                    
                return _transports.Remove(transportId);
            }
        }
        
        /// <summary>
        /// Gets all registered transports
        /// </summary>
        /// <returns>A collection of all registered transports</returns>
        public IReadOnlyCollection<IMessageTransport> GetAllTransports()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    return Array.Empty<IMessageTransport>();
                    
                return new List<IMessageTransport>(_transports.Values);
            }
        }
        
        /// <summary>
        /// Called by the ShutdownCoordinator to perform cleanup
        /// </summary>
        public async Task ShutdownAsync(CancellationToken token)
        {
            Console.WriteLine("MessageTransportManager: Performing coordinated shutdown of message transports");
            
            List<IMessageTransport> transports;
            lock (_lock)
            {
                transports = new List<IMessageTransport>(_transports.Values);
                _transports.Clear();
            }
            
            foreach (var transport in transports)
            {
                if (token.IsCancellationRequested)
                    break;
                    
                try
                {
                    Console.WriteLine($"MessageTransportManager: Stopping transport {transport.TransportId}");
                    await transport.StopAsync().ConfigureAwait(false);
                    
                    // Dispose the transport if it's not already disposed
                    if (transport is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MessageTransportManager: Error stopping transport {transport.TransportId}: {ex.Message}");
                }
            }
            
            Console.WriteLine("MessageTransportManager: All message transports stopped");
        }
        
        /// <summary>
        /// Disposes resources used by the MessageTransportManager
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;
                
            _isDisposed = true;
            
            List<IMessageTransport> transports;
            lock (_lock)
            {
                transports = new List<IMessageTransport>(_transports.Values);
                _transports.Clear();
            }
            
            foreach (var transport in transports)
            {
                try
                {
                    if (transport is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing transport {transport.TransportId}: {ex.Message}");
                }
            }
        }
    }
}