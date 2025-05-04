using System;
using System.Collections.Generic;
using System.Text;

namespace MSA.Foundation.Messaging
{
    /// <summary>
    /// Builder class for creating and modifying ServiceMessage instances
    /// </summary>
    public class ServiceMessageBuilder
    {
        private readonly ServiceMessage _message;
        
        /// <summary>
        /// Creates a new ServiceMessageBuilder
        /// </summary>
        public ServiceMessageBuilder()
        {
            _message = new ServiceMessage();
        }
        
        /// <summary>
        /// Creates a new ServiceMessageBuilder with an existing message
        /// </summary>
        /// <param name="message">The message to build from</param>
        public ServiceMessageBuilder(ServiceMessage message)
        {
            _message = message ?? new ServiceMessage();
        }
        
        /// <summary>
        /// Sets the message ID
        /// </summary>
        /// <param name="messageId">The message ID</param>
        /// <returns>This builder for fluent chaining</returns>
        public ServiceMessageBuilder WithMessageId(string messageId)
        {
            typeof(ServiceMessage).GetProperty("MessageId")?.SetValue(_message, messageId);
            return this;
        }
        
        /// <summary>
        /// Sets the message type
        /// </summary>
        /// <param name="messageType">The message type</param>
        /// <returns>This builder for fluent chaining</returns>
        public ServiceMessageBuilder WithMessageType(string messageType)
        {
            typeof(ServiceMessage).GetProperty("MessageType")?.SetValue(_message, messageType);
            return this;
        }
        
        /// <summary>
        /// Sets the timestamp
        /// </summary>
        /// <param name="timestamp">The timestamp</param>
        /// <returns>This builder for fluent chaining</returns>
        public ServiceMessageBuilder WithTimestamp(DateTime timestamp)
        {
            typeof(ServiceMessage).GetProperty("Timestamp")?.SetValue(_message, timestamp);
            return this;
        }
        
        /// <summary>
        /// Sets the sender ID
        /// </summary>
        /// <param name="senderId">The sender ID</param>
        /// <returns>This builder for fluent chaining</returns>
        public ServiceMessageBuilder WithSenderId(string senderId)
        {
            typeof(ServiceMessage).GetProperty("SenderId")?.SetValue(_message, senderId);
            return this;
        }
        
        /// <summary>
        /// Sets the correlation ID
        /// </summary>
        /// <param name="correlationId">The correlation ID</param>
        /// <returns>This builder for fluent chaining</returns>
        public ServiceMessageBuilder WithCorrelationId(string correlationId)
        {
            typeof(ServiceMessage).GetProperty("CorrelationId")?.SetValue(_message, correlationId);
            return this;
        }
        
        /// <summary>
        /// Sets the reply-to address
        /// </summary>
        /// <param name="replyTo">The reply-to address</param>
        /// <returns>This builder for fluent chaining</returns>
        public ServiceMessageBuilder WithReplyTo(string replyTo)
        {
            typeof(ServiceMessage).GetProperty("ReplyTo")?.SetValue(_message, replyTo);
            return this;
        }
        
        /// <summary>
        /// Sets whether acknowledgement is required
        /// </summary>
        /// <param name="requireAcknowledgement">Whether acknowledgement is required</param>
        /// <returns>This builder for fluent chaining</returns>
        public ServiceMessageBuilder WithRequireAcknowledgement(bool requireAcknowledgement)
        {
            typeof(ServiceMessage).GetProperty("RequireAcknowledgement")?.SetValue(_message, requireAcknowledgement);
            return this;
        }
        
        /// <summary>
        /// Sets the content type
        /// </summary>
        /// <param name="contentType">The content type</param>
        /// <returns>This builder for fluent chaining</returns>
        public ServiceMessageBuilder WithContentType(string contentType)
        {
            typeof(ServiceMessage).GetProperty("ContentType")?.SetValue(_message, contentType);
            return this;
        }
        
        /// <summary>
        /// Sets the content
        /// </summary>
        /// <param name="content">The content</param>
        /// <returns>This builder for fluent chaining</returns>
        public ServiceMessageBuilder WithContent(byte[] content)
        {
            typeof(ServiceMessage).GetProperty("Content")?.SetValue(_message, content);
            return this;
        }
        
        /// <summary>
        /// Sets a header value
        /// </summary>
        /// <param name="key">The header key</param>
        /// <param name="value">The header value</param>
        /// <returns>This builder for fluent chaining</returns>
        public ServiceMessageBuilder WithHeader(string key, string value)
        {
            _message.SetHeader(key, value);
            return this;
        }
        
        /// <summary>
        /// Creates a new message from an existing message
        /// </summary>
        /// <param name="message">The message to clone</param>
        /// <returns>A new builder with the same values as the specified message</returns>
        public static ServiceMessageBuilder FromExisting(IMessage message)
        {
            var newMessage = new ServiceMessage();
            var builder = new ServiceMessageBuilder(newMessage);
            
            builder.WithMessageId(message.MessageId)
                   .WithMessageType(message.MessageType)
                   .WithTimestamp(message.Timestamp)
                   .WithSenderId(message.SenderId)
                   .WithCorrelationId(message.CorrelationId)
                   .WithReplyTo(message.ReplyTo)
                   .WithRequireAcknowledgement(message.RequireAcknowledgement)
                   .WithContentType(message.ContentType)
                   .WithContent(message.Content);
            
            foreach (var header in message.Headers)
            {
                newMessage.SetHeader(header.Key, header.Value);
            }
            
            return builder;
        }
        
        /// <summary>
        /// Builds the ServiceMessage
        /// </summary>
        /// <returns>The built ServiceMessage</returns>
        public ServiceMessage Build()
        {
            return _message;
        }
    }
}