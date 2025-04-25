using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PokerGame.Core.Models;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// A microservice that manages card decks for card games
    /// </summary>
    public class CardDeckService : MicroserviceBase
    {
        private readonly Dictionary<string, Deck> _decks = new Dictionary<string, Deck>();
        private readonly Dictionary<string, List<Card>> _burnPiles = new Dictionary<string, List<Card>>();
        
        // Default publisher and subscriber ports
        public const int DefaultPublisherPort = 5559;
        public const int DefaultSubscriberPort = 5560;
        
        /// <summary>
        /// Creates a new card deck service
        /// </summary>
        /// <param name="publisherPort">The port to publish messages on</param>
        /// <param name="subscriberPort">The port to subscribe to messages on</param>
        public CardDeckService(
            int publisherPort = DefaultPublisherPort,
            int subscriberPort = DefaultSubscriberPort)
            : base("CardDeck", "Card Deck Service", publisherPort, subscriberPort)
        {
        }
        
        /// <summary>
        /// Handles messages received from other services
        /// </summary>
        /// <param name="message">The message to handle</param>
        /// <returns>A task representing the asynchronous operation</returns>
        protected internal override async Task HandleMessageAsync(Message message)
        {
            try
            {
                switch (message.Type)
                {
                    case MessageType.DeckCreate:
                        var createPayload = message.GetPayload<DeckCreatePayload>();
                        if (createPayload != null)
                        {
                            CreateDeck(createPayload.DeckId, createPayload.Shuffle);
                            BroadcastDeckStatus(createPayload.DeckId);
                        }
                        break;
                        
                    case MessageType.DeckShuffle:
                        var shufflePayload = message.GetPayload<DeckIdPayload>();
                        if (shufflePayload != null)
                        {
                            ShuffleDeck(shufflePayload.DeckId);
                            BroadcastDeckStatus(shufflePayload.DeckId);
                        }
                        break;
                        
                    case MessageType.DeckDeal:
                        var dealPayload = message.GetPayload<DeckDealPayload>();
                        if (dealPayload != null)
                        {
                            var cards = DealCards(dealPayload.DeckId, dealPayload.Count);
                            SendDealResponse(message.SenderId, dealPayload.DeckId, cards);
                            BroadcastDeckStatus(dealPayload.DeckId);
                        }
                        break;
                        
                    case MessageType.DeckBurn:
                        var burnPayload = message.GetPayload<DeckBurnPayload>();
                        if (burnPayload != null)
                        {
                            var burnedCard = BurnCard(burnPayload.DeckId, burnPayload.FaceUp);
                            SendBurnResponse(message.SenderId, burnPayload.DeckId, burnedCard, burnPayload.FaceUp);
                            BroadcastDeckStatus(burnPayload.DeckId);
                        }
                        break;
                        
                    case MessageType.DeckReset:
                        var resetPayload = message.GetPayload<DeckIdPayload>();
                        if (resetPayload != null)
                        {
                            ResetDeck(resetPayload.DeckId);
                            BroadcastDeckStatus(resetPayload.DeckId);
                        }
                        break;
                        
                    case MessageType.DeckStatus:
                        var statusPayload = message.GetPayload<DeckIdPayload>();
                        if (statusPayload != null)
                        {
                            BroadcastDeckStatus(statusPayload.DeckId);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling card deck message: {ex.Message}");
                // Send error response if needed
                var errorPayload = new ErrorPayload 
                { 
                    ErrorCode = "DECK_ERROR", 
                    ErrorMessage = ex.Message 
                };
                var errorMessage = Message.Create(MessageType.Error, errorPayload);
                SendTo(errorMessage, message.SenderId);
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// Creates a new deck with the specified ID
        /// </summary>
        /// <param name="deckId">The unique ID for the deck</param>
        /// <param name="shuffle">Whether to shuffle the deck after creation</param>
        private void CreateDeck(string deckId, bool shuffle)
        {
            var deck = new Deck();
            if (shuffle)
                deck.Shuffle();
                
            _decks[deckId] = deck;
            _burnPiles[deckId] = new List<Card>();
            
            Console.WriteLine($"Created deck: {deckId} (Shuffled: {shuffle})");
        }
        
        /// <summary>
        /// Shuffles the specified deck
        /// </summary>
        /// <param name="deckId">The ID of the deck to shuffle</param>
        private void ShuffleDeck(string deckId)
        {
            if (_decks.TryGetValue(deckId, out var deck))
            {
                deck.Shuffle();
                Console.WriteLine($"Shuffled deck: {deckId}");
            }
            else
            {
                throw new Exception($"Deck not found: {deckId}");
            }
        }
        
        /// <summary>
        /// Deals cards from the specified deck
        /// </summary>
        /// <param name="deckId">The ID of the deck to deal from</param>
        /// <param name="count">The number of cards to deal</param>
        /// <returns>A list of dealt cards</returns>
        private List<Card> DealCards(string deckId, int count)
        {
            if (_decks.TryGetValue(deckId, out var deck))
            {
                var cards = deck.DealCards(count);
                Console.WriteLine($"Dealt {cards.Count} cards from deck: {deckId}");
                return cards;
            }
            else
            {
                throw new Exception($"Deck not found: {deckId}");
            }
        }
        
        /// <summary>
        /// Burns a card from the specified deck
        /// </summary>
        /// <param name="deckId">The ID of the deck to burn from</param>
        /// <param name="faceUp">Whether the card is burned face up (visible)</param>
        /// <returns>The burned card, or null if the deck is empty</returns>
        private Card? BurnCard(string deckId, bool faceUp)
        {
            if (_decks.TryGetValue(deckId, out var deck))
            {
                var burnPile = _burnPiles[deckId];
                
                var card = deck.DealCard();
                if (card != null)
                {
                    burnPile.Add(card);
                    Console.WriteLine($"Burned card from deck: {deckId} (Face up: {faceUp}, Card: {(faceUp ? card.ToString() : "Hidden")})");
                }
                return card;
            }
            else
            {
                throw new Exception($"Deck not found: {deckId}");
            }
        }
        
        /// <summary>
        /// Resets the specified deck to a full, unshuffled state
        /// </summary>
        /// <param name="deckId">The ID of the deck to reset</param>
        private void ResetDeck(string deckId)
        {
            if (_decks.TryGetValue(deckId, out var deck))
            {
                deck.Reset();
                _burnPiles[deckId].Clear();
                Console.WriteLine($"Reset deck: {deckId}");
            }
            else
            {
                throw new Exception($"Deck not found: {deckId}");
            }
        }
        
        /// <summary>
        /// Broadcasts the status of a deck to all listeners
        /// </summary>
        /// <param name="deckId">The ID of the deck</param>
        private void BroadcastDeckStatus(string deckId)
        {
            if (_decks.TryGetValue(deckId, out var deck))
            {
                var burnPile = _burnPiles[deckId];
                
                var payload = new DeckStatusPayload
                {
                    DeckId = deckId,
                    RemainingCards = deck.CardsRemaining,
                    BurnedCardsCount = burnPile.Count
                };
                
                var message = Message.Create(MessageType.DeckStatus, payload);
                Broadcast(message);
            }
            else
            {
                throw new Exception($"Deck not found: {deckId}");
            }
        }
        
        /// <summary>
        /// Sends a response with the dealt cards to the requesting service
        /// </summary>
        /// <param name="requesterId">The ID of the requesting service</param>
        /// <param name="deckId">The ID of the deck</param>
        /// <param name="cards">The dealt cards</param>
        private void SendDealResponse(string requesterId, string deckId, List<Card> cards)
        {
            var payload = new DeckDealResponsePayload
            {
                DeckId = deckId,
                Cards = cards
            };
            
            var message = Message.Create(MessageType.DeckDealResponse, payload);
            SendTo(message, requesterId);
        }
        
        /// <summary>
        /// Sends a response with the burned card to the requesting service
        /// </summary>
        /// <param name="requesterId">The ID of the requesting service</param>
        /// <param name="deckId">The ID of the deck</param>
        /// <param name="card">The burned card</param>
        /// <param name="faceUp">Whether the card was burned face up</param>
        private void SendBurnResponse(string requesterId, string deckId, Card? card, bool faceUp)
        {
            var payload = new DeckBurnResponsePayload
            {
                DeckId = deckId,
                Card = card,
                FaceUp = faceUp
            };
            
            var message = Message.Create(MessageType.DeckBurnResponse, payload);
            SendTo(message, requesterId);
        }
    }
    
    #region Card Deck Payloads
    
    /// <summary>
    /// Payload for creating a new deck
    /// </summary>
    public class DeckCreatePayload
    {
        /// <summary>
        /// Gets or sets the unique ID for the deck
        /// </summary>
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets whether to shuffle the deck after creation
        /// </summary>
        public bool Shuffle { get; set; } = true;
    }
    
    /// <summary>
    /// Payload for referencing a deck by ID
    /// </summary>
    public class DeckIdPayload
    {
        /// <summary>
        /// Gets or sets the unique ID of the deck
        /// </summary>
        public string DeckId { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Payload for dealing cards from a deck
    /// </summary>
    public class DeckDealPayload
    {
        /// <summary>
        /// Gets or sets the unique ID of the deck
        /// </summary>
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the number of cards to deal
        /// </summary>
        public int Count { get; set; } = 1;
    }
    
    /// <summary>
    /// Payload for burning a card from a deck
    /// </summary>
    public class DeckBurnPayload
    {
        /// <summary>
        /// Gets or sets the unique ID of the deck
        /// </summary>
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets whether the card is burned face up (visible)
        /// </summary>
        public bool FaceUp { get; set; } = false;
    }
    
    /// <summary>
    /// Payload containing the status of a deck
    /// </summary>
    public class DeckStatusPayload
    {
        /// <summary>
        /// Gets or sets the unique ID of the deck
        /// </summary>
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the number of cards remaining in the deck
        /// </summary>
        public int RemainingCards { get; set; }
        
        /// <summary>
        /// Gets or sets the number of cards in the burn pile
        /// </summary>
        public int BurnedCardsCount { get; set; }
    }
    
    /// <summary>
    /// Payload containing cards dealt from a deck
    /// </summary>
    public class DeckDealResponsePayload
    {
        /// <summary>
        /// Gets or sets the unique ID of the deck
        /// </summary>
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the list of dealt cards
        /// </summary>
        public List<Card> Cards { get; set; } = new List<Card>();
    }
    
    /// <summary>
    /// Payload containing a burned card
    /// </summary>
    public class DeckBurnResponsePayload
    {
        /// <summary>
        /// Gets or sets the unique ID of the deck
        /// </summary>
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the burned card
        /// </summary>
        public Card? Card { get; set; }
        
        /// <summary>
        /// Gets or sets whether the card was burned face up
        /// </summary>
        public bool FaceUp { get; set; }
    }
    
    #endregion
}