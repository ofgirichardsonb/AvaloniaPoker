using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MSA.Foundation.Messaging
{
    /// <summary>
    /// Factory for creating message transports
    /// </summary>
    public static class TransportFactory
    {
        private static readonly Dictionary<string, Func<string, IMessageTransport>> _transportFactories = 
            new Dictionary<string, Func<string, IMessageTransport>>();
        
        /// <summary>
        /// Static constructor to register built-in transport types
        /// </summary>
        static TransportFactory()
        {
            // Register the in-process transport
            RegisterTransportType("inproc", transportId => new InProcessMessageTransport(transportId));
        }
        
        /// <summary>
        /// Registers a transport type with the factory
        /// </summary>
        /// <param name="transportType">The transport type (e.g., "inproc", "netmq", "rabbitmq")</param>
        /// <param name="factory">A factory function that creates a transport instance</param>
        public static void RegisterTransportType(string transportType, Func<string, IMessageTransport> factory)
        {
            if (string.IsNullOrEmpty(transportType))
                throw new ArgumentException("Transport type cannot be null or empty", nameof(transportType));
                
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
                
            _transportFactories[transportType.ToLowerInvariant()] = factory;
            
            Console.WriteLine($"Registered transport type: {transportType}");
        }
        
        /// <summary>
        /// Creates a transport of the specified type
        /// </summary>
        /// <param name="transportType">The transport type (e.g., "inproc", "netmq", "rabbitmq")</param>
        /// <param name="transportId">The transport ID</param>
        /// <returns>A new transport instance</returns>
        public static IMessageTransport CreateTransport(string transportType, string transportId)
        {
            if (string.IsNullOrEmpty(transportType))
                throw new ArgumentException("Transport type cannot be null or empty", nameof(transportType));
                
            if (string.IsNullOrEmpty(transportId))
                throw new ArgumentException("Transport ID cannot be null or empty", nameof(transportId));
                
            string normalizedType = transportType.ToLowerInvariant();
            
            if (!_transportFactories.TryGetValue(normalizedType, out var factory))
                throw new ArgumentException($"Transport type '{transportType}' is not registered", nameof(transportType));
                
            return factory(transportId);
        }
        
        /// <summary>
        /// Creates a transport of the specified type and initializes it with the specified configuration
        /// </summary>
        /// <param name="transportType">The transport type (e.g., "inproc", "netmq", "rabbitmq")</param>
        /// <param name="transportId">The transport ID</param>
        /// <param name="configuration">The transport configuration</param>
        /// <returns>The initialized transport</returns>
        public static async Task<IMessageTransport> CreateAndInitializeTransportAsync(
            string transportType, 
            string transportId, 
            MessageTransportConfiguration configuration)
        {
            var transport = CreateTransport(transportType, transportId);
            await transport.InitializeAsync(configuration).ConfigureAwait(false);
            return transport;
        }
        
        /// <summary>
        /// Determines the transport type from a connection string
        /// </summary>
        /// <param name="connectionString">The connection string</param>
        /// <returns>The transport type</returns>
        public static string GetTransportTypeFromConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
                
            // Parse the connection string to determine the transport type
            if (connectionString.StartsWith("inproc://", StringComparison.OrdinalIgnoreCase))
            {
                return "inproc";
            }
            else if (connectionString.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
            {
                return "netmq";
            }
            else if (connectionString.StartsWith("rabbitmq://", StringComparison.OrdinalIgnoreCase) || 
                     connectionString.StartsWith("amqp://", StringComparison.OrdinalIgnoreCase))
            {
                return "rabbitmq";
            }
            
            // Default to in-process
            return "inproc";
        }
    }
}