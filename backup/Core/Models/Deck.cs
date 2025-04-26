using System;
using System.Collections.Generic;

namespace PokerGame.Core.Models
{
    /// <summary>
    /// Represents a standard 52-card deck
    /// </summary>
    public class Deck
    {
        private readonly List<Card> _cards;
        private readonly Random _random;

        /// <summary>
        /// Creates a new deck with all 52 cards in order
        /// </summary>
        public Deck()
        {
            _cards = new List<Card>(52);
            _random = new Random();
            
            // Create a standard deck of 52 cards
            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                foreach (Rank rank in Enum.GetValues<Rank>())
                {
                    _cards.Add(new Card(rank, suit));
                }
            }
        }

        /// <summary>
        /// Gets the number of cards remaining in the deck
        /// </summary>
        public int CardsRemaining => _cards.Count;

        /// <summary>
        /// Shuffles the deck using the Fisher-Yates algorithm
        /// </summary>
        public void Shuffle()
        {
            int n = _cards.Count;
            
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                Card temp = _cards[k];
                _cards[k] = _cards[n];
                _cards[n] = temp;
            }
        }

        /// <summary>
        /// Deals a card from the top of the deck
        /// </summary>
        /// <returns>The top card or null if the deck is empty</returns>
        public Card? DealCard()
        {
            if (_cards.Count == 0)
                return null;
                
            Card card = _cards[0];
            _cards.RemoveAt(0);
            return card;
        }

        /// <summary>
        /// Deals multiple cards from the top of the deck
        /// </summary>
        /// <param name="count">Number of cards to deal</param>
        /// <returns>A list of cards or an empty list if not enough cards</returns>
        public List<Card> DealCards(int count)
        {
            List<Card> dealtCards = new List<Card>(count);
            
            for (int i = 0; i < count; i++)
            {
                Card? card = DealCard();
                if (card != null)
                    dealtCards.Add(card);
                else
                    break;
            }
            
            return dealtCards;
        }

        /// <summary>
        /// Resets the deck to a complete set of 52 cards in order
        /// </summary>
        public void Reset()
        {
            _cards.Clear();
            
            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                foreach (Rank rank in Enum.GetValues<Rank>())
                {
                    _cards.Add(new Card(rank, suit));
                }
            }
        }
    }
}
