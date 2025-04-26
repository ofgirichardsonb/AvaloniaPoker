using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using PokerGame.Core.Messaging;
using PokerGame.Core.Models;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// A simplified card deck service that handles card operations
    /// </summary>
    public class SimpleCardDeckService : SimpleServiceBase
    {
        private readonly Dictionary<string, Deck> _decks = new Dictionary<string, Deck>();
        private readonly Random _random = new Random();
        
        /// <summary>
        /// Creates a new simplified card deck service
        /// </summary>
        /// <param name="publisherPort">The port on which this service will publish messages</param>
        /// <param name="subscriberPort">The port on which this service will subscribe to messages</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        public SimpleCardDeckService(int publisherPort, int subscriberPort, bool verbose = false)
            : base("Card Deck Service", "CardDeck", publisherPort, subscriberPort, verbose)
        {
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
                var response = SimpleMessage.CreateResponse(message, SimpleMessageType.CardDeal, responsePayload);
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
                var response = SimpleMessage.CreateResponse(message, SimpleMessageType.DeckShuffle, responsePayload);
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
        /// Sends an error response for a message
        /// </summary>
        /// <param name="originalMessage">The original message</param>
        /// <param name="errorMessage">The error message</param>
        private void SendErrorResponse(SimpleMessage originalMessage, string errorMessage)
        {
            var errorResponse = SimpleMessage.CreateError(originalMessage, errorMessage);
            PublishMessage(errorResponse);
            
            Logger.LogError($"Sent error response: {errorMessage}");
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
}