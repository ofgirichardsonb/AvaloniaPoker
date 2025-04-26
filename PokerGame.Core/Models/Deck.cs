using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerGame.Core.Models
{
    /// <summary>
    /// A deck of cards
    /// </summary>
    public class Deck
    {
        private List<Card> _cards = new List<Card>();
        private Random _random = new Random();
        
        /// <summary>
        /// Gets the number of remaining cards in the deck
        /// </summary>
        public int RemainingCards => _cards.Count;
        
        /// <summary>
        /// Gets the number of remaining cards in the deck (alias for RemainingCards for compatibility)
        /// </summary>
        public int CardsRemaining => RemainingCards;
        
        /// <summary>
        /// Creates a new deck
        /// </summary>
        public Deck()
        {
        }
        
        /// <summary>
        /// Initializes the deck with a standard 52-card deck
        /// </summary>
        public void Initialize()
        {
            _cards.Clear();
            
            // Create all 52 cards
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                {
                    _cards.Add(new Card(rank, suit));
                }
            }
        }
        
        /// <summary>
        /// Shuffles the deck
        /// </summary>
        public void Shuffle()
        {
            // Fisher-Yates shuffle algorithm
            int n = _cards.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                Card value = _cards[k];
                _cards[k] = _cards[n];
                _cards[n] = value;
            }
        }
        
        /// <summary>
        /// Deals the specified number of cards from the top of the deck
        /// </summary>
        /// <param name="count">The number of cards to deal</param>
        /// <returns>A list of dealt cards</returns>
        public List<Card> DealCards(int count)
        {
            if (count <= 0)
            {
                return new List<Card>();
            }
            
            if (count > _cards.Count)
            {
                throw new InvalidOperationException($"Cannot deal {count} cards. Only {_cards.Count} cards remaining.");
            }
            
            List<Card> dealtCards = new List<Card>();
            
            for (int i = 0; i < count; i++)
            {
                Card card = _cards[0];
                _cards.RemoveAt(0);
                dealtCards.Add(card);
            }
            
            return dealtCards;
        }
        
        /// <summary>
        /// Deals a single card from the top of the deck
        /// </summary>
        /// <returns>The dealt card</returns>
        public Card DealCard()
        {
            if (_cards.Count == 0)
            {
                throw new InvalidOperationException("Cannot deal a card. The deck is empty.");
            }
            
            Card card = _cards[0];
            _cards.RemoveAt(0);
            return card;
        }
        
        /// <summary>
        /// Adds a card to the bottom of the deck
        /// </summary>
        /// <param name="card">The card to add</param>
        public void AddCard(Card card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }
            
            _cards.Add(card);
        }
        
        /// <summary>
        /// Adds a list of cards to the bottom of the deck
        /// </summary>
        /// <param name="cards">The cards to add</param>
        public void AddCards(IEnumerable<Card> cards)
        {
            if (cards == null)
            {
                throw new ArgumentNullException(nameof(cards));
            }
            
            _cards.AddRange(cards);
        }
        
        /// <summary>
        /// Resets the deck by adding all 52 cards and shuffling
        /// </summary>
        public void Reset()
        {
            Initialize();
            Shuffle();
        }
        
        /// <summary>
        /// Gets all cards from the deck without removing them
        /// </summary>
        /// <returns>All cards in the deck</returns>
        public List<Card> GetAllCards()
        {
            return _cards.ToList();
        }
        
        /// <summary>
        /// Returns a string representation of the deck
        /// </summary>
        /// <returns>A string representation of the deck</returns>
        public override string ToString()
        {
            return $"Deck with {_cards.Count} cards";
        }
    }
}