using System;
using MSA.Foundation.Messaging;
using MSA.Foundation.ServiceManagement;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Transport type options for message communication
    /// </summary>
    public enum TransportType
    {
        /// <summary>
        /// Uses native .NET Channels for in-process messaging
        /// </summary>
        Channel,
        
        /// <summary>
        /// Automatically selects the best transport based on the connection string (currently defaults to Channel)
        /// </summary>
        Auto
    }
    
    /// <summary>
    /// Factory class for creating message transport instances
    /// </summary>
    public static class MessageTransportFactory
    {
        private static TransportType _defaultTransportType = TransportType.Channel;
        
        /// <summary>
        /// Sets the default transport type to use when Auto is specified
        /// </summary>
        /// <param name="transportType">The transport type to use by default</param>
        public static void SetDefaultTransportType(TransportType transportType)
        {
            if (transportType == TransportType.Auto)
                throw new ArgumentException("Cannot set default transport type to Auto");
                
            _defaultTransportType = transportType;
            Console.WriteLine($"MessageTransportFactory: Default transport type set to {transportType}");
        }
        
        /// <summary>
        /// Creates a message transport with the specified parameters
        /// </summary>
        /// <param name="transportId">A unique identifier for the transport</param>
        /// <param name="transportType">The type of transport to create</param>
        /// <returns>A new message transport instance</returns>
        public static IMessageTransport CreateTransport(string transportId, TransportType transportType = TransportType.Auto)
        {
            if (string.IsNullOrEmpty(transportId))
                throw new ArgumentException("Transport ID cannot be null or empty", nameof(transportId));
                
            // If Auto is specified, use the default transport type
            if (transportType == TransportType.Auto)
            {
                transportType = _defaultTransportType;
            }
            
            Console.WriteLine($"MessageTransportFactory: Creating {transportType} transport with ID {transportId}");
            
            // Create the appropriate transport based on the type
            switch (transportType)
            {
                case TransportType.Channel:
                    var config = new MSA.Foundation.Messaging.MessageTransportConfiguration { ServiceId = transportId };
                    return new ChannelMessageTransport(config);
                    
                default:
                    throw new ArgumentException($"Unsupported transport type: {transportType}", nameof(transportType));
            }
        }
        
        /// <summary>
        /// Creates a message transport with the specified connection string and configuration
        /// </summary>
        /// <param name="transportType">The type of transport to create</param>
        /// <param name="connectionString">The connection string to use</param>
        /// <param name="configuration">Configuration options for the transport</param>
        /// <returns>A new message transport instance</returns>
        public static IMessageTransport Create(TransportType transportType, string connectionString, MSA.Foundation.Messaging.MessageTransportConfiguration configuration)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
                
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
                
            // If Auto is specified, use the default transport type
            if (transportType == TransportType.Auto)
            {
                transportType = _defaultTransportType;
            }
            
            Console.WriteLine($"MessageTransportFactory: Creating {transportType} transport for {configuration.ServiceId} with connection {connectionString}");
            
            // Create the appropriate transport based on the type
            switch (transportType)
            {
                case TransportType.Channel:
                    return new ChannelMessageTransport(configuration);
                    
                default:
                    throw new ArgumentException($"Unsupported transport type: {transportType}", nameof(transportType));
            }
        }
        
        /// <summary>
        /// Creates a connection string for the specified transport type
        /// </summary>
        /// <param name="transportType">The transport type</param>
        /// <param name="serviceName">Optional service name for logging purposes</param>
        /// <returns>A connection string suitable for the specified transport</returns>
        public static string CreateConnectionString(TransportType transportType = TransportType.Auto, string? serviceName = null)
        {
            // If Auto is specified, use the default transport type
            if (transportType == TransportType.Auto)
            {
                transportType = _defaultTransportType;
            }
            
            string connectionString;
            
            switch (transportType)
            {
                case TransportType.Channel:
                    connectionString = ChannelMessageHelper.ChannelBrokerAddress;
                    break;
                    
                default:
                    throw new ArgumentException($"Unsupported transport type: {transportType}", nameof(transportType));
            }
            
            Console.WriteLine($"MessageTransportFactory: Created connection string for {transportType} transport{(serviceName != null ? $" ({serviceName})" : "")}: {connectionString}");
            
            return connectionString;
        }
    }
}