using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Message types for the networking system
    /// </summary>
    public enum MessageType
    {
        // System messages
        Heartbeat,
        ServiceRegistration,
        ServiceDiscovery,
        Acknowledgment,
        Error,
        Ping,
        Debug,
        
        // Game-related messages
        GameState,
        PlayerJoin,
        PlayerAction,
        CardDeal,
        DeckShuffle,
        DeckShuffled,
        DeckCreate,
        DeckCreated,
        DeckDeal,
        DeckDealt,
        DeckStatus,
        DeckStatusResponse,
        DeckBurn,
        DeckBurnResponse,
        DeckReset,
        StartHand,
        EndHand,
        StartGame,
        GameStarted,
        EndGame,
        GameEnded,
        HandStarted,
        HandEnded,
        RoundComplete,
        HandComplete,
        
        // Player management messages
        PlayerLeave,
        PlayerUpdate,
        
        // UI messages
        DisplayUpdate,
        UserInput,
        
        // Notification messages
        InfoMessage,
        DebugMessage,
        Notification
    }
    
    /// <summary>
    /// Represents a message sent between components over the network
    /// </summary>
    public class NetworkMessage
    {
        /// <summary>
        /// Gets or sets the type of the message
        /// </summary>
        [JsonPropertyName("type")]
        public MessageType Type { get; set; }
        
        /// <summary>
        /// Gets or sets the unique identifier of the message
        /// </summary>
        [JsonPropertyName("id")]
        public string MessageId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the timestamp when the message was created
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets or sets the ID of the service that sent the message
        /// </summary>
        [JsonPropertyName("sender")]
        public string SenderId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the ID of the service that should receive the message
        /// If null or empty, the message is broadcast to all services
        /// </summary>
        [JsonPropertyName("receiver")]
        public string ReceiverId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the ID of the message that this message is in response to
        /// </summary>
        [JsonPropertyName("inResponseTo")]
        public string InResponseTo { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the payload of the message
        /// </summary>
        [JsonPropertyName("payload")]
        public string Payload { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets additional headers/metadata for the message
        /// This is useful for including routing or processing information
        /// </summary>
        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// Creates a new message with a random ID and the current timestamp
        /// </summary>
        public NetworkMessage()
        {
            MessageId = Guid.NewGuid().ToString();
            Timestamp = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Creates a new message with the specified type and an optional payload
        /// </summary>
        /// <param name="type">The type of the message</param>
        /// <param name="payload">The payload of the message</param>
        /// <returns>A new message</returns>
        public static NetworkMessage Create(MessageType type, object? payload = null)
        {
            var message = new NetworkMessage { Type = type };
            
            if (payload != null)
            {
                message.Payload = payload is string str ? str : JsonSerializer.Serialize(payload);
            }
            
            return message;
        }
        
        /// <summary>
        /// Creates a new message in response to another message
        /// </summary>
        /// <param name="type">The type of the response message</param>
        /// <param name="originalMessage">The original message being responded to</param>
        /// <param name="payload">The payload of the response</param>
        /// <returns>A new response message</returns>
        public static NetworkMessage CreateResponse(MessageType type, NetworkMessage originalMessage, object? payload = null)
        {
            if (originalMessage == null)
                throw new ArgumentNullException(nameof(originalMessage));
            
            var message = new NetworkMessage
            {
                Type = type,
                InResponseTo = originalMessage.MessageId,
                ReceiverId = originalMessage.SenderId
            };
            
            if (payload != null)
            {
                message.Payload = payload is string str ? str : JsonSerializer.Serialize(payload);
            }
            
            return message;
        }
        
        /// <summary>
        /// Creates an acknowledgment message in response to another message
        /// </summary>
        /// <param name="originalMessage">The original message being acknowledged</param>
        /// <param name="payload">Optional payload data to include</param>
        /// <returns>A new acknowledgment message</returns>
        public static NetworkMessage CreateAcknowledgment(NetworkMessage originalMessage, object? payload = null)
        {
            return CreateResponse(MessageType.Acknowledgment, originalMessage, payload);
        }
        
        /// <summary>
        /// Creates an error message in response to another message
        /// </summary>
        /// <param name="originalMessage">The original message that caused the error</param>
        /// <param name="errorMessage">The error message</param>
        /// <returns>A new error message</returns>
        public static NetworkMessage CreateError(NetworkMessage originalMessage, string errorMessage)
        {
            return CreateResponse(MessageType.Error, originalMessage, errorMessage);
        }
        
        /// <summary>
        /// Gets the payload as a specific type
        /// </summary>
        /// <typeparam name="T">The type to convert the payload to</typeparam>
        /// <returns>The payload as the specified type</returns>
        public T? GetPayload<T>() where T : class
        {
            if (string.IsNullOrEmpty(Payload))
                return null;
                
            try
            {
                return JsonSerializer.Deserialize<T>(Payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing payload to {typeof(T).Name}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Gets the payload as a string
        /// </summary>
        /// <returns>The payload as a string</returns>
        public string GetPayloadAsString()
        {
            return Payload;
        }
        
        /// <summary>
        /// Sets the payload by serializing an object to JSON
        /// </summary>
        /// <typeparam name="T">The type of the payload</typeparam>
        /// <param name="payload">The payload to serialize</param>
        public void SetPayload<T>(T payload)
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
                Console.WriteLine($"Error serializing payload of type {typeof(T).Name}: {ex.Message}");
                Payload = string.Empty;
            }
        }
    }
    
    // Note: ServiceRegistrationPayload is currently defined in SimpleMessage.cs
    // In a future release, it will be moved here with the rest of the NetworkMessage components
    // Once the transition from SimpleMessage to NetworkMessage is complete
}