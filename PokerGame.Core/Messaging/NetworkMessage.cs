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
        public string MessageId { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp when the message was created
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Gets or sets the ID of the service that sent the message
        /// </summary>
        [JsonPropertyName("sender")]
        public string SenderId { get; set; }
        
        /// <summary>
        /// Gets or sets the ID of the service that should receive the message
        /// If null or empty, the message is broadcast to all services
        /// </summary>
        [JsonPropertyName("receiver")]
        public string ReceiverId { get; set; }
        
        /// <summary>
        /// Gets or sets the ID of the message that this message is in response to
        /// </summary>
        [JsonPropertyName("inResponseTo")]
        public string InResponseTo { get; set; }
        
        /// <summary>
        /// Gets or sets the payload of the message
        /// </summary>
        [JsonPropertyName("payload")]
        public object Payload { get; set; }
        
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
            return new NetworkMessage
            {
                Type = type,
                Payload = payload
            };
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
                
            return new NetworkMessage
            {
                Type = type,
                InResponseTo = originalMessage.MessageId,
                ReceiverId = originalMessage.SenderId,
                Payload = payload
            };
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
        public T GetPayload<T>()
        {
            if (Payload == null)
                return default;
                
            if (Payload is JsonElement jsonElement)
            {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
            }
            
            if (Payload is T typedPayload)
            {
                return typedPayload;
            }
            
            // Convert using JSON serialization as a fallback
            string json = JsonSerializer.Serialize(Payload);
            return JsonSerializer.Deserialize<T>(json);
        }
        
        /// <summary>
        /// Gets the payload as a string
        /// </summary>
        /// <returns>The payload as a string</returns>
        public string? GetPayloadAsString()
        {
            if (Payload == null)
                return null;
                
            if (Payload is string stringPayload)
                return stringPayload;
                
            if (Payload is JsonElement jsonElement)
                return jsonElement.GetRawText();
                
            return JsonSerializer.Serialize(Payload);
        }
    }
    
    // Note: ServiceRegistrationPayload is currently defined in SimpleMessage.cs
    // In a future release, it will be moved here with the rest of the NetworkMessage components
    // Once the transition from SimpleMessage to NetworkMessage is complete
}