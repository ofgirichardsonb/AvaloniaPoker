using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Represents a message type in the broker system
    /// </summary>
    public enum BrokerMessageType
    {
        // System messages
        Heartbeat = 0,
        ServiceRegistration = 1,
        ServiceDiscovery = 2,
        Acknowledgment = 3,
        Error = 4,
        Ping = 5,
        Request = 6,
        Response = 7,
        
        // Application-specific messages - can be extended as needed
        Custom = 100,
        
        // Reserved range for user-defined message types
        UserDefined = 1000
    }

    /// <summary>
    /// Represents a message in the broker system
    /// </summary>
    public class BrokerMessage
    {
        /// <summary>
        /// Gets or sets the type of message
        /// </summary>
        public BrokerMessageType Type { get; set; }
        
        /// <summary>
        /// Gets or sets the unique identifier for the message
        /// </summary>
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Gets or sets the identifier of the service that sent the message
        /// </summary>
        public string SenderId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the identifier of the service that should receive the message
        /// If empty, the message is broadcast to all services
        /// </summary>
        public string ReceiverId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the identifier of the message that this message is responding to
        /// </summary>
        public string? InResponseTo { get; set; }
        
        /// <summary>
        /// Gets or sets the serialized payload of the message
        /// </summary>
        public string? SerializedPayload { get; set; }
        
        /// <summary>
        /// Gets or sets the topic of the message for pub/sub patterns
        /// </summary>
        public string? Topic { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp when the message was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets or sets additional headers for the message
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// Gets or sets a flag indicating whether the message requires an acknowledgment
        /// </summary>
        public bool RequiresAcknowledgment { get; set; } = false;

        /// <summary>
        /// Creates a new instance of the Message class with the specified type and payload
        /// </summary>
        /// <param name="type">The type of message</param>
        /// <param name="payload">The payload of the message</param>
        /// <returns>A new Message instance</returns>
        public static BrokerMessage Create(BrokerMessageType type, object? payload = null)
        {
            var message = new BrokerMessage { Type = type };
            
            if (payload != null)
            {
                message.SerializedPayload = JsonConvert.SerializeObject(payload);
            }
            
            return message;
        }
        
        /// <summary>
        /// Deserializes the payload of the message to the specified type
        /// </summary>
        /// <typeparam name="T">The type to deserialize the payload to</typeparam>
        /// <returns>The deserialized payload, or default(T) if deserialization fails</returns>
        public T? GetPayload<T>()
        {
            if (string.IsNullOrEmpty(SerializedPayload))
                return default;
                
            try
            {
                return JsonConvert.DeserializeObject<T>(SerializedPayload);
            }
            catch
            {
                return default;
            }
        }
        
        /// <summary>
        /// Converts the message to a JSON string
        /// </summary>
        /// <returns>A JSON representation of the message</returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
        
        /// <summary>
        /// Creates a message from a JSON string
        /// </summary>
        /// <param name="json">The JSON string to parse</param>
        /// <returns>A Message instance, or null if parsing fails</returns>
        public static BrokerMessage? FromJson(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<BrokerMessage>(json);
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Creates an acknowledgment message for this message
        /// </summary>
        /// <returns>A new Message instance with type Acknowledgment</returns>
        public BrokerMessage CreateAcknowledgment()
        {
            var ack = new BrokerMessage
            {
                Type = BrokerMessageType.Acknowledgment,
                SenderId = ReceiverId,
                ReceiverId = SenderId,
                InResponseTo = MessageId
            };
            
            return ack;
        }
    }
    
    /// <summary>
    /// Payload for service registration messages
    /// </summary>
    public class ServiceRegistrationPayload
    {
        /// <summary>
        /// Gets or sets the unique identifier for the service
        /// </summary>
        public string ServiceId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the human-readable name of the service
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the type of the service
        /// </summary>
        public string ServiceType { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the capabilities of the service
        /// </summary>
        public List<string> Capabilities { get; set; } = new List<string>();
        
        /// <summary>
        /// Gets or sets the endpoint where the service can be reached
        /// </summary>
        public string? Endpoint { get; set; }
        
        /// <summary>
        /// Gets or sets the publisher port of the service
        /// </summary>
        public int PublisherPort { get; set; }
        
        /// <summary>
        /// Gets or sets the subscriber port of the service
        /// </summary>
        public int SubscriberPort { get; set; }
    }
    
    /// <summary>
    /// Payload for service discovery messages
    /// </summary>
    public class ServiceDiscoveryPayload
    {
        /// <summary>
        /// Gets or sets the type of service to discover
        /// </summary>
        public string? ServiceType { get; set; }
        
        /// <summary>
        /// Gets or sets the specific capability to look for
        /// </summary>
        public string? Capability { get; set; }
    }
    
    /// <summary>
    /// Payload for error messages
    /// </summary>
    public class BrokerErrorPayload
    {
        /// <summary>
        /// Gets or sets the error code
        /// </summary>
        public int ErrorCode { get; set; }
        
        /// <summary>
        /// Gets or sets the error message
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets additional details about the error
        /// </summary>
        public string? Details { get; set; }
    }
}