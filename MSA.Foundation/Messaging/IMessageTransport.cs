using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MSA.Foundation.Messaging
{
    /// <summary>
    /// Defines the contract for a message transport implementation
    /// </summary>
    /// <remarks>
    /// A message transport is responsible for sending and receiving messages between services,
    /// regardless of whether those services are in the same process or distributed.
    /// </remarks>
    public interface IMessageTransport : IDisposable
    {
        /// <summary>
        /// Gets the unique identifier for this transport instance
        /// </summary>
        string TransportId { get; }
        
        /// <summary>
        /// Gets whether this transport is running
        /// </summary>
        bool IsRunning { get; }
        
        /// <summary>
        /// Initializes the transport with the specified configuration
        /// </summary>
        /// <param name="configuration">The transport configuration</param>
        /// <returns>True if initialization succeeded; otherwise, false</returns>
        Task<bool> InitializeAsync(MessageTransportConfiguration configuration);
        
        /// <summary>
        /// Starts the transport
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>True if the transport was started successfully; otherwise, false</returns>
        Task<bool> StartAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stops the transport
        /// </summary>
        /// <returns>True if the transport was stopped successfully; otherwise, false</returns>
        Task<bool> StopAsync();
        
        /// <summary>
        /// Sends a message to the specified destination
        /// </summary>
        /// <param name="destination">The destination to send the message to</param>
        /// <param name="message">The message to send</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>True if the message was sent successfully; otherwise, false</returns>
        Task<bool> SendAsync(string destination, IMessage message, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Broadcasts a message to all subscribers
        /// </summary>
        /// <param name="message">The message to broadcast</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>True if the message was broadcast successfully; otherwise, false</returns>
        Task<bool> BroadcastAsync(IMessage message, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Subscribes to messages of the specified type
        /// </summary>
        /// <param name="messageType">The type of message to subscribe to</param>
        /// <param name="handler">The handler to invoke when a message of the specified type is received</param>
        /// <returns>A subscription identifier that can be used to unsubscribe</returns>
        string Subscribe(string messageType, Func<IMessage, Task> handler);
        
        /// <summary>
        /// Subscribes to messages from the specified source
        /// </summary>
        /// <param name="source">The source to subscribe to</param>
        /// <param name="handler">The handler to invoke when a message from the specified source is received</param>
        /// <returns>A subscription identifier that can be used to unsubscribe</returns>
        string SubscribeToSource(string source, Func<IMessage, Task> handler);
        
        /// <summary>
        /// Subscribes to all messages
        /// </summary>
        /// <param name="handler">The handler to invoke when any message is received</param>
        /// <returns>A subscription identifier that can be used to unsubscribe</returns>
        string SubscribeToAll(Func<IMessage, Task> handler);
        
        /// <summary>
        /// Unsubscribes from messages using the specified subscription identifier
        /// </summary>
        /// <param name="subscriptionId">The subscription identifier to unsubscribe</param>
        /// <returns>True if the subscription was found and removed; otherwise, false</returns>
        bool Unsubscribe(string subscriptionId);
        
        /// <summary>
        /// Acknowledges receipt of a message
        /// </summary>
        /// <param name="messageId">The identifier of the message to acknowledge</param>
        /// <param name="success">Whether the message was processed successfully</param>
        /// <param name="error">Optional error information if the message was not processed successfully</param>
        /// <returns>True if the acknowledgement was sent successfully; otherwise, false</returns>
        Task<bool> AcknowledgeAsync(string messageId, bool success, string? error = null);
    }
    
    /// <summary>
    /// Configuration for a message transport
    /// </summary>
    public class MessageTransportConfiguration
    {
        /// <summary>
        /// Gets or sets the unique identifier for the service using this transport
        /// </summary>
        public string ServiceId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the user-friendly name of the service using this transport
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the type of the service using this transport
        /// </summary>
        public string ServiceType { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the connection string for the transport
        /// </summary>
        /// <remarks>
        /// For in-process transports, this could be an identifier for the in-process broker.
        /// For distributed transports, this could be a connection string for the message broker.
        /// </remarks>
        public string ConnectionString { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets additional transport-specific options
        /// </summary>
        public Dictionary<string, string> Options { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// Gets or sets whether acknowledgement is required for messages sent through this transport
        /// </summary>
        public bool RequireAcknowledgement { get; set; } = false;
        
        /// <summary>
        /// Gets or sets the timeout for message acknowledgement in milliseconds
        /// </summary>
        public int AcknowledgementTimeoutMs { get; set; } = 5000;
        
        /// <summary>
        /// Gets or sets the maximum number of retry attempts for message sending
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;
        
        /// <summary>
        /// Gets or sets the retry interval in milliseconds
        /// </summary>
        public int RetryIntervalMs { get; set; } = 1000;
        
        /// <summary>
        /// Gets or sets whether to log message content
        /// </summary>
        public bool LogMessageContent { get; set; } = false;
        
        /// <summary>
        /// Gets or sets whether to compress message content
        /// </summary>
        public bool CompressMessageContent { get; set; } = false;
        
        /// <summary>
        /// Gets or sets whether to encrypt message content
        /// </summary>
        public bool EncryptMessageContent { get; set; } = false;
        
        /// <summary>
        /// Gets an option value from the options dictionary
        /// </summary>
        /// <param name="key">The option key</param>
        /// <param name="defaultValue">The default value to return if the key is not found</param>
        /// <returns>The option value if found; otherwise, the default value</returns>
        public string GetOption(string key, string defaultValue = "")
        {
            if (Options.TryGetValue(key, out var value))
            {
                return value;
            }
            
            return defaultValue;
        }
        
        /// <summary>
        /// Gets an option value from the options dictionary and converts it to the specified type
        /// </summary>
        /// <typeparam name="T">The type to convert the option value to</typeparam>
        /// <param name="key">The option key</param>
        /// <param name="defaultValue">The default value to return if the key is not found or conversion fails</param>
        /// <returns>The converted option value if found and conversion succeeds; otherwise, the default value</returns>
        public T GetOption<T>(string key, T defaultValue)
        {
            if (Options.TryGetValue(key, out var value))
            {
                try
                {
                    if (typeof(T) == typeof(bool) && bool.TryParse(value, out var boolValue))
                    {
                        return (T)(object)boolValue;
                    }
                    else if (typeof(T) == typeof(int) && int.TryParse(value, out var intValue))
                    {
                        return (T)(object)intValue;
                    }
                    else if (typeof(T) == typeof(double) && double.TryParse(value, out var doubleValue))
                    {
                        return (T)(object)doubleValue;
                    }
                    else if (typeof(T) == typeof(TimeSpan) && TimeSpan.TryParse(value, out var timeSpanValue))
                    {
                        return (T)(object)timeSpanValue;
                    }
                    else if (typeof(T) == typeof(DateTime) && DateTime.TryParse(value, out var dateTimeValue))
                    {
                        return (T)(object)dateTimeValue;
                    }
                    else if (typeof(T) == typeof(Guid) && Guid.TryParse(value, out var guidValue))
                    {
                        return (T)(object)guidValue;
                    }
                }
                catch
                {
                    // Conversion failed, return default value
                }
            }
            
            return defaultValue;
        }
    }
}