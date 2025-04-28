using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using PokerGame.Core.Messaging;
using PokerGame.Core.Models;

// Suppress obsolete warnings for transition period
#pragma warning disable CS0619 // Type or member is obsolete

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// A simplified card deck service that handles card operations
    /// </summary>
    [Obsolete("This class has been replaced with CardDeckService and will be removed in a future release.")]
    public class SimpleCardDeckService : SimpleServiceBase
    {
        private readonly Dictionary<string, Deck> _decks = new Dictionary<string, Deck>();
        private readonly Random _random = new Random();
        private readonly Logger Logger;
        
        /// <summary>
        /// Creates a new simplified card deck service
        /// </summary>
        /// <param name="publisherPort">The port on which this service will publish messages</param>
        /// <param name="subscriberPort">The port on which this service will subscribe to messages</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        public SimpleCardDeckService(int publisherPort, int subscriberPort, bool verbose = false)
            : base("Card Deck Service", "CardDeck", publisherPort, subscriberPort, verbose)
        {
            // Create a logger instance
            Logger = new Logger("CardDeck", verbose);
        }
        
        /// <summary>
        /// Handles a received message
        /// </summary>
        /// <param name="message">The message to handle</param>
        protected override void HandleMessage(SimpleMessage message)
        {
            base.HandleMessage(message);
            
            switch (message.Type)
            {
                case SimpleMessageType.CardDeal:
                    HandleCardDeal(message);
                    break;
                    
                case SimpleMessageType.DeckShuffle:
                    HandleDeckShuffle(message);
                    break;
                    
                case SimpleMessageType.StartHand:
                    // Handle deck creation by extracting a deck ID from the message
                    HandleDeckCreate(message, $"deck-{Guid.NewGuid().ToString("N").Substring(0, 8)}");
                    break;
                
                // Add DeckCreate message type handling
                case SimpleMessageType.DeckCreate:
                    try
                    {
                        // First try to extract SimpleDeckCreatePayload
                        var simpleDeckCreatePayload = message.GetPayload<SimpleDeckCreatePayload>();
                        if (simpleDeckCreatePayload != null)
                        {
                            Logger.Log($"Handling DeckCreate message with SimpleDeckCreatePayload, DeckId: {simpleDeckCreatePayload.DeckId}");
                            HandleDeckCreate(message, simpleDeckCreatePayload.DeckId);
                        }
                        else
                        {
                            // Fallback to regular handling
                            HandleDeckCreate(message);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error extracting SimpleDeckCreatePayload: {ex.Message}", ex);
                        // Fallback to regular handling
                        HandleDeckCreate(message);
                    }
                    break;
                    
                default:
                    // Let the base class handle other message types
                    break;
            }
        }
        
        /// <summary>
        /// Handles a card deal message
        /// </summary>
        /// <param name="message">The message to handle</param>
        private void HandleCardDeal(SimpleMessage message)
        {
            try
            {
                var dealPayload = message.GetPayload<CardDealPayload>();
                if (dealPayload == null)
                {
                    SendErrorResponse(message, "Invalid card deal payload");
                    return;
                }
                
                // Check if the deck exists
                if (!_decks.TryGetValue(dealPayload.DeckId, out Deck deck))
                {
                    // Create a new deck if it doesn't exist
                    deck = CreateDeck(dealPayload.DeckId);
                    _decks[dealPayload.DeckId] = deck;
                    
                    Logger.Log($"Created new deck with ID: {dealPayload.DeckId}");
                }
                
                // Deal the requested number of cards
                var dealtCards = deck.DealCards(dealPayload.Count);
                
                // Create the response payload
                var responsePayload = new CardDealResponsePayload
                {
                    DeckId = dealPayload.DeckId,
                    Cards = dealtCards,
                    RemainingCards = deck.RemainingCards,
                    Success = true
                };
                
                // Send the response
                var response = SimpleMessage.CreateResponse(SimpleMessageType.CardDeal, message, responsePayload);
                PublishMessage(response);
                
                Logger.Log($"Dealt {dealtCards.Count} cards from deck {dealPayload.DeckId}. Remaining: {deck.RemainingCards}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling card deal message", ex);
                SendErrorResponse(message, $"Error dealing cards: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handles a deck shuffle message
        /// </summary>
        /// <param name="message">The message to handle</param>
        private void HandleDeckShuffle(SimpleMessage message)
        {
            try
            {
                var shufflePayload = message.GetPayload<DeckShufflePayload>();
                if (shufflePayload == null)
                {
                    SendErrorResponse(message, "Invalid deck shuffle payload");
                    return;
                }
                
                // Check if the deck exists
                if (!_decks.TryGetValue(shufflePayload.DeckId, out Deck deck))
                {
                    // Create a new deck if it doesn't exist
                    deck = CreateDeck(shufflePayload.DeckId);
                    _decks[shufflePayload.DeckId] = deck;
                    
                    Logger.Log($"Created new deck with ID: {shufflePayload.DeckId}");
                }
                
                // Shuffle the deck
                deck.Shuffle();
                
                // Create the response payload
                var responsePayload = new DeckShuffleResponsePayload
                {
                    DeckId = shufflePayload.DeckId,
                    Success = true,
                    RemainingCards = deck.RemainingCards
                };
                
                // Send the response
                var response = SimpleMessage.CreateResponse(SimpleMessageType.DeckShuffle, message, responsePayload);
                PublishMessage(response);
                
                Logger.Log($"Shuffled deck {shufflePayload.DeckId}. Remaining cards: {deck.RemainingCards}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling deck shuffle message", ex);
                SendErrorResponse(message, $"Error shuffling deck: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Creates a new deck with the specified ID
        /// </summary>
        /// <param name="deckId">The ID of the deck</param>
        /// <returns>The created deck</returns>
        private Deck CreateDeck(string deckId)
        {
            var deck = new Deck();
            deck.Initialize();
            deck.Shuffle();
            return deck;
        }
        
        /// <summary>
        /// Handles a deck create message
        /// </summary>
        /// <param name="message">The message to handle</param>
        /// <param name="deckId">The ID of the deck to create (or use from payload if available)</param>
        private void HandleDeckCreate(SimpleMessage message, string deckId = null)
        {
            try
            {
                Logger.Log($"Handling deck creation request with message ID: {message.MessageId}");
                
                // Try to extract deckId from payload if not provided
                if (string.IsNullOrEmpty(deckId))
                {
                    // Try to extract from various payload types
                    try {
                        // For direct DeckCreate messages
                        try 
                        {
                            var createPayload = message.GetPayload<SimpleDeckCreatePayload>();
                            if (createPayload != null && !string.IsNullOrEmpty(createPayload.DeckId))
                            {
                                deckId = createPayload.DeckId;
                                Logger.Log($"Extracted deck ID {deckId} from SimpleDeckCreatePayload");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Failed to extract payload as SimpleDeckCreatePayload", ex);
                            // Fallback to default deck ID
                            deckId = $"deck-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                            Logger.Log($"Using generated deck ID: {deckId}");
                        }
                    }
                    catch (Exception)
                    {
                        // Payload wasn't of the expected type, use a generated ID
                        deckId = $"deck-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                    }
                }
                
                // Create the deck
                if (_decks.ContainsKey(deckId))
                {
                    Logger.Log($"Deck with ID {deckId} already exists, resetting it");
                    _decks[deckId].Initialize();
                    _decks[deckId].Shuffle();
                }
                else
                {
                    var deck = CreateDeck(deckId);
                    _decks[deckId] = deck;
                    Logger.Log($"Created new deck with ID: {deckId}");
                }
                
                // Create a response payload
                var responsePayload = new SimpleDeckCreateResponsePayload
                {
                    DeckId = deckId,
                    Success = true,
                    RemainingCards = _decks[deckId].RemainingCards
                };
                
                // Send an acknowledgment for the original message
                var ackMessage = SimpleMessage.CreateAcknowledgment(message);
                ackMessage.SenderId = ServiceId;
                PublishMessage(ackMessage);
                Logger.Log($"Sent acknowledgment for message: {message.MessageId}");
                
                // Send a specific response - use DeckCreate response type
                var response = SimpleMessage.CreateResponse(SimpleMessageType.DeckCreate, message, responsePayload);
                response.SenderId = ServiceId;
                PublishMessage(response);
                Logger.Log($"Sent deck create response for deck: {deckId}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling deck create message", ex);
                SendErrorResponse(message, $"Error creating deck: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sends an error response for a message
        /// </summary>
        /// <param name="originalMessage">The original message</param>
        /// <param name="errorMessage">The error message</param>
        private void SendErrorResponse(SimpleMessage originalMessage, string errorMessage)
        {
            var errorResponse = SimpleMessage.CreateError(originalMessage, errorMessage);
            PublishMessage(errorResponse);
            
            Logger.LogError($"Sent error response: {errorMessage}", null);
        }
    }
    
    /// <summary>
    /// Payload for card deal messages
    /// </summary>
    public class CardDealPayload
    {
        /// <summary>
        /// Gets or sets the ID of the deck to deal from
        /// </summary>
        [JsonPropertyName("deckId")]
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the number of cards to deal
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; } = 1;
    }
    
    /// <summary>
    /// Payload for card deal response messages
    /// </summary>
    public class CardDealResponsePayload
    {
        /// <summary>
        /// Gets or sets the ID of the deck
        /// </summary>
        [JsonPropertyName("deckId")]
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the dealt cards
        /// </summary>
        [JsonPropertyName("cards")]
        public List<Card> Cards { get; set; } = new List<Card>();
        
        /// <summary>
        /// Gets or sets the number of remaining cards in the deck
        /// </summary>
        [JsonPropertyName("remainingCards")]
        public int RemainingCards { get; set; }
        
        /// <summary>
        /// Gets or sets whether the operation was successful
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }
    
    /// <summary>
    /// Payload for deck shuffle messages
    /// </summary>
    public class DeckShufflePayload
    {
        /// <summary>
        /// Gets or sets the ID of the deck to shuffle
        /// </summary>
        [JsonPropertyName("deckId")]
        public string DeckId { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Payload for deck shuffle response messages
    /// </summary>
    public class DeckShuffleResponsePayload
    {
        /// <summary>
        /// Gets or sets the ID of the deck
        /// </summary>
        [JsonPropertyName("deckId")]
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets whether the operation was successful
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        
        /// <summary>
        /// Gets or sets the number of remaining cards in the deck
        /// </summary>
        [JsonPropertyName("remainingCards")]
        public int RemainingCards { get; set; }
    }
    
    /// <summary>
    /// Payload for deck create messages in the simplified service architecture
    /// </summary>
    public class SimpleDeckCreatePayload
    {
        /// <summary>
        /// Gets or sets the ID of the deck to create
        /// </summary>
        [JsonPropertyName("deckId")]
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets whether to shuffle the deck after creation
        /// </summary>
        [JsonPropertyName("shuffle")]
        public bool Shuffle { get; set; } = true;
    }
    
    /// <summary>
    /// Payload for deck create response messages in the simplified service architecture
    /// </summary>
    public class SimpleDeckCreateResponsePayload
    {
        /// <summary>
        /// Gets or sets the ID of the deck
        /// </summary>
        [JsonPropertyName("deckId")]
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets whether the operation was successful
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        
        /// <summary>
        /// Gets or sets the number of remaining cards in the deck
        /// </summary>
        [JsonPropertyName("remainingCards")]
        public int RemainingCards { get; set; }
    }
}

// Restore warning level
#pragma warning restore CS0619 // Type or member is obsolete