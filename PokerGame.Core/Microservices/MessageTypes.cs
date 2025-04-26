using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokerGame.Core.Models;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Defines the types of messages that can be sent between microservices
    /// </summary>
    public enum MessageType
    {
        // System messages
        Heartbeat,
        ServiceRegistration,
        ServiceDiscovery,
        Notification,     // General notification/info message
        Acknowledgment,   // Generic acknowledgment for any message
        Ping,             // Ping message to check service availability
        
        // Game engine messages
        StartGame,        // Request to start a new game
        GameStarted,      // Confirmation that game was started
        GameState,        // Current state of the game
        EndGame,          // Request to end a game
        GameEnded,        // Confirmation that game ended
        StartHand,        // Request to start a new hand
        HandStarted,      // Confirmation that hand was started
        EndHand,          // Request to end current hand
        HandEnded,        // Confirmation that hand ended
        DealCards,
        PlayerAction,
        ActionResponse,
        RoundComplete,
        HandComplete,
        
        // Player management messages
        PlayerJoin,
        PlayerLeave,
        PlayerUpdate,
        
        // UI messages
        DisplayUpdate,
        UserInput,
        
        // Card deck messages
        DeckCreate,
        DeckCreated,      // Confirmation of deck creation
        DeckShuffle,
        DeckShuffled,     // Confirmation that deck was shuffled
        DeckDeal,
        DeckDealt,        // Confirmation that cards were dealt
        DeckDealResponse, // Response with dealt cards
        DeckBurn,
        DeckBurnResponse,
        DeckReset,
        DeckStatus,
        DeckStatusResponse, // Response to status request
        
        // Error messages
        Error
    }
    
    /// <summary>
    /// Base class for all messages exchanged between microservices
    /// </summary>
    public class Message
    {
        /// <summary>
        /// Gets or sets the type of the message
        /// </summary>
        public MessageType Type { get; set; }
        
        /// <summary>
        /// Gets or sets the unique identifier for this message
        /// </summary>
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Gets or sets the identifier of the sender microservice
        /// </summary>
        public string SenderId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the identifier of the intended recipient microservice (empty for broadcast)
        /// </summary>
        public string ReceiverId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the timestamp when the message was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets or sets the message ID that this message is responding to
        /// Used for acknowledgments and responses to link back to original requests
        /// </summary>
        public string InResponseTo { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the payload of the message as a JSON string
        /// </summary>
        public string Payload { get; set; } = string.Empty;
        
        /// <summary>
        /// Sets the payload from an object by serializing it to JSON
        /// </summary>
        /// <typeparam name="T">The type of the payload object</typeparam>
        /// <param name="payload">The payload object to serialize</param>
        public void SetPayload<T>(T payload)
        {
            Payload = JsonSerializer.Serialize(payload);
        }
        
        /// <summary>
        /// Gets the payload as an object by deserializing the JSON
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <returns>The deserialized object, or default value if payload is empty or deserialization fails</returns>
        public T? GetPayload<T>() where T : class
        {
            if (string.IsNullOrEmpty(Payload))
            {
                return default;
            }
            
            try
            {
                return JsonSerializer.Deserialize<T>(Payload);
            }
            catch
            {
                // If deserialization fails, return null
                return default;
            }
        }
        
        /// <summary>
        /// Creates a new message with the specified type
        /// </summary>
        /// <param name="type">The type of the message</param>
        /// <returns>A new message instance</returns>
        public static Message Create(MessageType type)
        {
            return new Message { Type = type };
        }
        
        /// <summary>
        /// Creates a new message with the specified type and payload
        /// </summary>
        /// <typeparam name="T">The type of the payload</typeparam>
        /// <param name="type">The type of the message</param>
        /// <param name="payload">The payload data</param>
        /// <returns>A new message with the serialized payload</returns>
        public static Message Create<T>(MessageType type, T payload)
        {
            var message = new Message { Type = type };
            message.SetPayload(payload);
            return message;
        }
        
        /// <summary>
        /// Serializes the message to a JSON string
        /// </summary>
        /// <returns>The JSON representation of the message</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }
        
        /// <summary>
        /// Deserializes a message from a JSON string
        /// </summary>
        /// <param name="json">The JSON string</param>
        /// <returns>The deserialized message</returns>
        /// <exception cref="JsonException">Thrown when JSON deserialization fails</exception>
        public static Message FromJson(string json)
        {
            var message = JsonSerializer.Deserialize<Message>(json);
            if (message == null)
            {
                throw new JsonException("Failed to deserialize message from JSON");
            }
            return message;
        }
    }
    
    #region Payload classes for specific message types
    
    /// <summary>
    /// Payload for service registration messages
    /// </summary>
    public class ServiceRegistrationPayload
    {
        /// <summary>
        /// Gets or sets the unique identifier of the service
        /// </summary>
        public string ServiceId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the name of the service
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the type of the service
        /// </summary>
        public string ServiceType { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the endpoint URI of the service
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the capabilities of the service
        /// </summary>
        public List<string> Capabilities { get; set; } = new List<string>();
    }
    
    /// <summary>
    /// Payload for game state messages
    /// </summary>
    public class GameStatePayload
    {
        /// <summary>
        /// Gets or sets the current state of the game
        /// </summary>
        public Game.GameState CurrentState { get; set; }
        
        /// <summary>
        /// Gets or sets the pot size
        /// </summary>
        public int Pot { get; set; }
        
        /// <summary>
        /// Gets or sets the current bet
        /// </summary>
        public int CurrentBet { get; set; }
        
        /// <summary>
        /// Gets or sets the community cards
        /// </summary>
        public List<Card> CommunityCards { get; set; } = new List<Card>();
        
        /// <summary>
        /// Gets or sets the players in the game
        /// </summary>
        public List<PlayerInfo> Players { get; set; } = new List<PlayerInfo>();
        
        /// <summary>
        /// Gets or sets the dealer position
        /// </summary>
        public int DealerPosition { get; set; }
        
        /// <summary>
        /// Gets or sets the index of the current player
        /// </summary>
        public int CurrentPlayerIndex { get; set; }
    }
    
    /// <summary>
    /// Simplified player information for transmission
    /// </summary>
    public class PlayerInfo
    {
        /// <summary>
        /// Gets or sets the player's unique identifier
        /// </summary>
        public string PlayerId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the player's name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the player's chip count
        /// </summary>
        public int Chips { get; set; }
        
        /// <summary>
        /// Gets or sets the player's current bet in this round
        /// </summary>
        public int CurrentBet { get; set; }
        
        /// <summary>
        /// Gets or sets whether the player has folded
        /// </summary>
        public bool HasFolded { get; set; }
        
        /// <summary>
        /// Gets or sets whether the player is all-in
        /// </summary>
        public bool IsAllIn { get; set; }
        
        /// <summary>
        /// Gets or sets the player's hole cards (only sent to the specific player)
        /// </summary>
        public List<Card> HoleCards { get; set; } = new List<Card>();
        
        /// <summary>
        /// Creates a PlayerInfo from a Player model
        /// </summary>
        /// <param name="player">The player model</param>
        /// <param name="includeHoleCards">Whether to include hole cards information</param>
        /// <returns>A new PlayerInfo instance</returns>
        public static PlayerInfo FromPlayer(Player player, bool includeHoleCards = false)
        {
            var info = new PlayerInfo
            {
                PlayerId = player.Id,
                Name = player.Name,
                Chips = player.Chips,
                CurrentBet = player.CurrentBet,
                HasFolded = player.HasFolded,
                IsAllIn = player.IsAllIn
            };
            
            if (includeHoleCards)
            {
                info.HoleCards = new List<Card>(player.HoleCards);
            }
            
            return info;
        }
    }
    
    /// <summary>
    /// Payload for player action messages
    /// </summary>
    public class PlayerActionPayload
    {
        /// <summary>
        /// Gets or sets the ID of the player taking the action
        /// </summary>
        public string PlayerId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the action type (fold, check, call, raise)
        /// </summary>
        public string ActionType { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the bet amount (for raise actions)
        /// </summary>
        public int BetAmount { get; set; }
    }
    
    /// <summary>
    /// Payload for error messages
    /// </summary>
    public class ErrorPayload
    {
        /// <summary>
        /// Gets or sets the error code
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the error message
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets additional error details
        /// </summary>
        public string Details { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Payload for action response messages
    /// </summary>
    public class ActionResponsePayload
    {
        /// <summary>
        /// Gets or sets whether the action was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Gets or sets the action type that was processed
        /// </summary>
        public string ActionType { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the response message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Payload for notification messages
    /// </summary>
    public class NotificationPayload
    {
        /// <summary>
        /// Gets or sets the notification message
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the notification level (info, warning, error)
        /// </summary>
        public string Level { get; set; } = "info";
        
        /// <summary>
        /// Gets or sets additional context data for the notification
        /// </summary>
        public string Context { get; set; } = string.Empty;
    }
    
    // Note: The DeckCreatePayload, DeckIdPayload, DeckDealPayload, DeckDealResponsePayload, 
    // DeckBurnPayload, DeckBurnResponsePayload, and DeckStatusPayload classes are 
    // defined in CardDeckService.cs
    
    #endregion
}