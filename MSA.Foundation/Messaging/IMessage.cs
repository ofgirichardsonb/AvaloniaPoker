using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MSA.Foundation.Messaging
{
    /// <summary>
    /// Defines the contract for a message that can be sent through a message transport
    /// </summary>
    public interface IMessage
    {
        /// <summary>
        /// Gets the unique identifier for the message
        /// </summary>
        string MessageId { get; }
        
        /// <summary>
        /// Gets the type of the message
        /// </summary>
        string MessageType { get; }
        
        /// <summary>
        /// Gets the timestamp when the message was created
        /// </summary>
        DateTime Timestamp { get; }
        
        /// <summary>
        /// Gets the sender's identifier
        /// </summary>
        string SenderId { get; }
        
        /// <summary>
        /// Gets the correlation identifier for related messages
        /// </summary>
        string CorrelationId { get; }
        
        /// <summary>
        /// Gets the reply-to address for response messages
        /// </summary>
        string ReplyTo { get; }
        
        /// <summary>
        /// Gets whether acknowledgement is required for this message
        /// </summary>
        bool RequireAcknowledgement { get; }
        
        /// <summary>
        /// Gets the content type of the message
        /// </summary>
        string ContentType { get; }
        
        /// <summary>
        /// Gets the message content as a byte array
        /// </summary>
        byte[] Content { get; }
        
        /// <summary>
        /// Gets the message headers
        /// </summary>
        IReadOnlyDictionary<string, string> Headers { get; }
        
        /// <summary>
        /// Gets the content of the message as a strongly-typed object
        /// </summary>
        /// <typeparam name="T">The type to deserialize the content to</typeparam>
        /// <returns>The deserialized content</returns>
        T? GetContent<T>();
        
        /// <summary>
        /// Gets a header value by key
        /// </summary>
        /// <param name="key">The header key</param>
        /// <param name="defaultValue">The default value to return if the header is not found</param>
        /// <returns>The header value if found; otherwise, the default value</returns>
        string GetHeader(string key, string defaultValue = "");
    }
    
    /// <summary>
    /// Base implementation of IMessage with common functionality
    /// </summary>
    public class ServiceMessage : IMessage
    {
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>();
        
        /// <summary>
        /// Gets or sets the unique identifier for the message
        /// </summary>
        public string MessageId { get; set; }
        
        /// <summary>
        /// Gets or sets the type of the message
        /// </summary>
        public string MessageType { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp when the message was created
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Gets or sets the sender's identifier
        /// </summary>
        public string SenderId { get; set; }
        
        /// <summary>
        /// Gets or sets the correlation identifier for related messages
        /// </summary>
        public string CorrelationId { get; set; }
        
        /// <summary>
        /// Gets or sets the reply-to address for response messages
        /// </summary>
        public string ReplyTo { get; set; }
        
        /// <summary>
        /// Gets or sets whether acknowledgement is required for this message
        /// </summary>
        public bool RequireAcknowledgement { get; set; }
        
        /// <summary>
        /// Gets or sets the content type of the message
        /// </summary>
        public string ContentType { get; set; }
        
        /// <summary>
        /// Gets or sets the message content as a byte array
        /// </summary>
        public byte[] Content { get; set; }
        
        /// <summary>
        /// Gets the message headers
        /// </summary>
        public IReadOnlyDictionary<string, string> Headers => _headers;
        
        /// <summary>
        /// Creates a new message
        /// </summary>
        public ServiceMessage()
        {
            MessageId = Guid.NewGuid().ToString();
            MessageType = GetType().Name;
            Timestamp = DateTime.UtcNow;
            SenderId = string.Empty;
            CorrelationId = string.Empty;
            ReplyTo = string.Empty;
            RequireAcknowledgement = false;
            ContentType = "application/json";
            Content = Array.Empty<byte>();
        }
        
        /// <summary>
        /// Creates a new message with the specified content
        /// </summary>
        /// <param name="content">The content to include in the message</param>
        /// <typeparam name="T">The type of the content</typeparam>
        /// <returns>A new message with the specified content</returns>
        public static ServiceMessage Create<T>(T content)
        {
            var message = new ServiceMessage
            {
                MessageType = typeof(T).Name,
                ContentType = "application/json"
            };
            
            message.SetContent(content);
            return message;
        }
        
        /// <summary>
        /// Creates a new message of the specified type
        /// </summary>
        /// <param name="messageType">The type of the message</param>
        /// <returns>A new message of the specified type</returns>
        public static ServiceMessage Create(string messageType)
        {
            return new ServiceMessage
            {
                MessageType = messageType
            };
        }
        
        /// <summary>
        /// Creates a new message from the specified sender
        /// </summary>
        /// <param name="senderId">The sender's identifier</param>
        /// <param name="messageType">The type of the message</param>
        /// <returns>A new message from the specified sender</returns>
        public static ServiceMessage CreateFrom(string senderId, string messageType)
        {
            return new ServiceMessage
            {
                SenderId = senderId,
                MessageType = messageType
            };
        }
        
        /// <summary>
        /// Creates a new message as a reply to another message
        /// </summary>
        /// <param name="originalMessage">The message to reply to</param>
        /// <param name="replyMessageType">The type of the reply message</param>
        /// <returns>A new reply message</returns>
        public static ServiceMessage CreateReply(IMessage originalMessage, string replyMessageType)
        {
            return new ServiceMessage
            {
                MessageType = replyMessageType,
                CorrelationId = originalMessage.MessageId,
                ReplyTo = originalMessage.SenderId
            };
        }
        
        /// <summary>
        /// Sets the message content
        /// </summary>
        /// <param name="content">The content to set</param>
        /// <typeparam name="T">The type of the content</typeparam>
        public void SetContent<T>(T content)
        {
            if (content == null)
            {
                Content = Array.Empty<byte>();
                return;
            }
            
            string jsonString = JsonSerializer.Serialize(content);
            Content = System.Text.Encoding.UTF8.GetBytes(jsonString);
        }
        
        /// <summary>
        /// Gets the content of the message as a strongly-typed object
        /// </summary>
        /// <typeparam name="T">The type to deserialize the content to</typeparam>
        /// <returns>The deserialized content</returns>
        public T? GetContent<T>()
        {
            if (Content == null || Content.Length == 0)
            {
                return default;
            }
            
            try
            {
                string jsonString = System.Text.Encoding.UTF8.GetString(Content);
                return JsonSerializer.Deserialize<T>(jsonString);
            }
            catch (Exception)
            {
                return default;
            }
        }
        
        /// <summary>
        /// Sets a header value
        /// </summary>
        /// <param name="key">The header key</param>
        /// <param name="value">The header value</param>
        public void SetHeader(string key, string value)
        {
            _headers[key] = value;
        }
        
        /// <summary>
        /// Gets a header value by key
        /// </summary>
        /// <param name="key">The header key</param>
        /// <param name="defaultValue">The default value to return if the header is not found</param>
        /// <returns>The header value if found; otherwise, the default value</returns>
        public string GetHeader(string key, string defaultValue = "")
        {
            if (_headers.TryGetValue(key, out var value))
            {
                return value;
            }
            
            return defaultValue;
        }
        
        /// <summary>
        /// Sets whether acknowledgement is required for this message
        /// </summary>
        /// <param name="requireAcknowledgement">Whether acknowledgement is required</param>
        /// <returns>This message instance for fluent chaining</returns>
        public ServiceMessage WithAcknowledgement(bool requireAcknowledgement = true)
        {
            RequireAcknowledgement = requireAcknowledgement;
            return this;
        }
        
        /// <summary>
        /// Sets the correlation identifier for related messages
        /// </summary>
        /// <param name="correlationId">The correlation identifier</param>
        /// <returns>This message instance for fluent chaining</returns>
        public ServiceMessage WithCorrelationId(string correlationId)
        {
            CorrelationId = correlationId;
            return this;
        }
        
        /// <summary>
        /// Sets the sender's identifier
        /// </summary>
        /// <param name="senderId">The sender's identifier</param>
        /// <returns>This message instance for fluent chaining</returns>
        public ServiceMessage FromSender(string senderId)
        {
            SenderId = senderId;
            return this;
        }
        
        /// <summary>
        /// Sets the reply-to address for response messages
        /// </summary>
        /// <param name="replyTo">The reply-to address</param>
        /// <returns>This message instance for fluent chaining</returns>
        public ServiceMessage WithReplyTo(string replyTo)
        {
            ReplyTo = replyTo;
            return this;
        }
    }
}