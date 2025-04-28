using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSA.Foundation.Messaging
{
    /// <summary>
    /// Message for the messaging infrastructure
    /// </summary>
    public class Message
    {
        /// <summary>
        /// Gets or sets the message ID
        /// </summary>
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Gets or sets the message type
        /// </summary>
        public MessageType MessageType { get; set; } = MessageType.Unknown;
        
        /// <summary>
        /// Gets or sets the sender ID
        /// </summary>
        public string SenderId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the receiver ID
        /// </summary>
        public string ReceiverId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets or sets the payload
        /// </summary>
        public string? Payload { get; set; }
        
        /// <summary>
        /// Gets or sets the headers
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// Gets or sets whether acknowledgment is required
        /// </summary>
        public bool RequireAcknowledgment { get; set; } = false;
        
        /// <summary>
        /// Gets or sets the acknowledgment ID
        /// </summary>
        public string? AcknowledgmentId { get; set; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Message"/> class
        /// </summary>
        public Message() { }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Message"/> class
        /// </summary>
        /// <param name="messageType">The message type</param>
        /// <param name="senderId">The sender ID</param>
        /// <param name="payload">The payload</param>
        public Message(MessageType messageType, string senderId, string? payload = null)
        {
            MessageId = Guid.NewGuid().ToString();
            MessageType = messageType;
            SenderId = senderId;
            Payload = payload;
            Timestamp = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Creates an acknowledgment message for this message
        /// </summary>
        /// <param name="receiverId">The receiver ID</param>
        /// <returns>The acknowledgment message</returns>
        public Message CreateAcknowledgment(string receiverId)
        {
            return new Message
            {
                MessageId = Guid.NewGuid().ToString(),
                MessageType = MessageType.Acknowledgment,
                SenderId = receiverId,
                ReceiverId = SenderId,
                Timestamp = DateTime.UtcNow,
                AcknowledgmentId = MessageId,
                Headers = new Dictionary<string, string>(Headers)
            };
        }
        
        /// <summary>
        /// Creates a response message for this message
        /// </summary>
        /// <param name="receiverId">The receiver ID</param>
        /// <param name="payload">The payload</param>
        /// <returns>The response message</returns>
        public Message CreateResponse(string receiverId, string? payload = null)
        {
            var responseHeaders = new Dictionary<string, string>(Headers);
            responseHeaders["OriginalMessageId"] = MessageId;
            
            return new Message
            {
                MessageId = Guid.NewGuid().ToString(),
                MessageType = MessageType.Response,
                SenderId = receiverId,
                ReceiverId = SenderId,
                Timestamp = DateTime.UtcNow,
                Payload = payload,
                Headers = responseHeaders
            };
        }
        
        /// <summary>
        /// Serializes the message to JSON
        /// </summary>
        /// <returns>The JSON string</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }
        
        /// <summary>
        /// Deserializes a message from JSON
        /// </summary>
        /// <param name="json">The JSON string</param>
        /// <returns>The message</returns>
        public static Message? FromJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<Message>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing message: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Sets a payload object by serializing it to JSON
        /// </summary>
        /// <typeparam name="T">The payload type</typeparam>
        /// <param name="payload">The payload object</param>
        public void SetPayload<T>(T payload)
        {
            try
            {
                Payload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error serializing payload: {ex.Message}");
                Payload = null;
            }
        }
        
        /// <summary>
        /// Gets a payload object by deserializing from JSON
        /// </summary>
        /// <typeparam name="T">The payload type</typeparam>
        /// <returns>The payload object</returns>
        public T? GetPayload<T>()
        {
            if (string.IsNullOrEmpty(Payload))
            {
                return default;
            }
            
            try
            {
                return JsonSerializer.Deserialize<T>(Payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing payload: {ex.Message}");
                return default;
            }
        }
    }
}