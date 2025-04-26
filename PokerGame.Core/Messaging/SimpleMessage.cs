using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Message types for the simplified messaging system
    /// </summary>
    public enum SimpleMessageType
    {
        // System messages
        Heartbeat,
        ServiceRegistration,
        Acknowledgment,
        Error,
        
        // Game-related messages
        GameState,
        PlayerJoin,
        PlayerAction,
        CardDeal,
        DeckShuffle,
        DeckCreate,     // Added to support deck creation messages
        StartHand,
        EndHand,
        
        // Notification messages
        InfoMessage,
        DebugMessage
    }
    
    /// <summary>
    /// A simple message class with minimal required fields
    /// </summary>
    public class SimpleMessage
    {
        /// <summary>
        /// Gets or sets the type of the message
        /// </summary>
        [JsonPropertyName("type")]
        public SimpleMessageType Type { get; set; }
        
        /// <summary>
        /// Gets or sets the unique identifier for this message
        /// </summary>
        [JsonPropertyName("messageId")]
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Gets or sets the identifier of the sender service
        /// </summary>
        [JsonPropertyName("senderId")]
        public string SenderId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the identifier of the recipient service (empty for broadcast)
        /// </summary>
        [JsonPropertyName("receiverId")]
        public string ReceiverId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the message ID that this message is responding to
        /// </summary>
        [JsonPropertyName("inResponseTo")]
        public string InResponseTo { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the timestamp when the message was created
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets or sets the payload of the message
        /// </summary>
        [JsonPropertyName("payload")]
        public string Payload { get; set; } = string.Empty;
        
        /// <summary>
        /// Sets the payload from an object by serializing it to JSON
        /// </summary>
        /// <typeparam name="T">The type of the payload object</typeparam>
        /// <param name="payload">The payload object to serialize</param>
        public void SetPayload<T>(T payload) where T : class
        {
            if (payload == null)
            {
                Payload = string.Empty;
                return;
            }
            
            try
            {
                Payload = JsonSerializer.Serialize(payload);
            }
            catch (Exception ex)
            {
                // Log serialization error, but don't throw
                Console.WriteLine($"Error serializing payload: {ex.Message}");
                Payload = string.Empty;
            }
        }
        
        /// <summary>
        /// Gets the payload as an object by deserializing the JSON
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <returns>The deserialized object, or default value if payload is empty or deserialization fails</returns>
        public T GetPayload<T>() where T : class
        {
            if (string.IsNullOrEmpty(Payload))
            {
                return default;
            }
            
            try
            {
                return JsonSerializer.Deserialize<T>(Payload);
            }
            catch (Exception ex)
            {
                // Log deserialization error, but don't throw
                Console.WriteLine($"Error deserializing payload: {ex.Message}");
                return default;
            }
        }
        
        /// <summary>
        /// Creates a new message with the specified type
        /// </summary>
        /// <param name="type">The type of the message</param>
        /// <returns>A new message instance</returns>
        public static SimpleMessage Create(SimpleMessageType type)
        {
            return new SimpleMessage { Type = type };
        }
        
        /// <summary>
        /// Creates a new message with the specified type and payload
        /// </summary>
        /// <typeparam name="T">The type of the payload</typeparam>
        /// <param name="type">The type of the message</param>
        /// <param name="payload">The payload data</param>
        /// <returns>A new message with the serialized payload</returns>
        public static SimpleMessage Create<T>(SimpleMessageType type, T payload) where T : class
        {
            var message = new SimpleMessage { Type = type };
            message.SetPayload(payload);
            return message;
        }
        
        /// <summary>
        /// Creates a new message that is a response to another message
        /// </summary>
        /// <param name="originalMessage">The message being responded to</param>
        /// <param name="type">The type of the response message</param>
        /// <returns>A new message that is a response to the original message</returns>
        public static SimpleMessage CreateResponse(SimpleMessage originalMessage, SimpleMessageType type)
        {
            return new SimpleMessage
            {
                Type = type,
                InResponseTo = originalMessage.MessageId,
                ReceiverId = originalMessage.SenderId // Send back to original sender
            };
        }
        
        /// <summary>
        /// Creates a new message that is a response to another message with a payload
        /// </summary>
        /// <typeparam name="T">The type of the payload</typeparam>
        /// <param name="originalMessage">The message being responded to</param>
        /// <param name="type">The type of the response message</param>
        /// <param name="payload">The payload data</param>
        /// <returns>A new message that is a response to the original message with the specified payload</returns>
        public static SimpleMessage CreateResponse<T>(SimpleMessage originalMessage, SimpleMessageType type, T payload) where T : class
        {
            var message = CreateResponse(originalMessage, type);
            message.SetPayload(payload);
            return message;
        }
        
        /// <summary>
        /// Creates an acknowledgment message for the specified message
        /// </summary>
        /// <param name="originalMessage">The message to acknowledge</param>
        /// <returns>An acknowledgment message</returns>
        public static SimpleMessage CreateAcknowledgment(SimpleMessage originalMessage)
        {
            return CreateResponse(originalMessage, SimpleMessageType.Acknowledgment);
        }
        
        /// <summary>
        /// Creates an error message in response to another message
        /// </summary>
        /// <param name="originalMessage">The message that caused the error</param>
        /// <param name="errorMessage">The error message</param>
        /// <returns>An error message</returns>
        public static SimpleMessage CreateError(SimpleMessage originalMessage, string errorMessage)
        {
            var errorPayload = new ErrorPayload { ErrorMessage = errorMessage };
            return CreateResponse(originalMessage, SimpleMessageType.Error, errorPayload);
        }
    }
    
    /// <summary>
    /// Payload for error messages
    /// </summary>
    public class ErrorPayload
    {
        /// <summary>
        /// Gets or sets the error message
        /// </summary>
        [JsonPropertyName("errorMessage")]
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets additional error details
        /// </summary>
        [JsonPropertyName("details")]
        public string Details { get; set; } = string.Empty;
    }
}