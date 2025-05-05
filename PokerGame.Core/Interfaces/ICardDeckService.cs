using System.Collections.Generic;
using PokerGame.Core.Models;

namespace PokerGame.Core.Interfaces
{
    /// <summary>
    /// Interface for a service that provides card deck functionality
    /// </summary>
    public interface ICardDeckService
    {
        /// <summary>
        /// Gets a shuffled deck of cards
        /// </summary>
        /// <returns>A shuffled deck of cards</returns>
        List<Card> GetShuffledDeck();
        
        /// <summary>
        /// Draws a card from the deck
        /// </summary>
        /// <returns>The drawn card</returns>
        Card DrawCard();
    }
}