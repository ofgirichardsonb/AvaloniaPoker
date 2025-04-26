using System;

namespace PokerGame.Core.Game
{
    /// <summary>
    /// Represents the different poker hand rankings in order from lowest to highest
    /// </summary>
    public enum HandRank
    {
        /// <summary>
        /// No matching cards, evaluated by highest card
        /// </summary>
        HighCard = 0,
        
        /// <summary>
        /// Two cards of the same rank
        /// </summary>
        OnePair = 1,
        
        /// <summary>
        /// Two different pairs
        /// </summary>
        TwoPair = 2,
        
        /// <summary>
        /// Three cards of the same rank
        /// </summary>
        ThreeOfAKind = 3,
        
        /// <summary>
        /// Five cards in sequence (aces can be high or low)
        /// </summary>
        Straight = 4,
        
        /// <summary>
        /// Five cards of the same suit
        /// </summary>
        Flush = 5,
        
        /// <summary>
        /// Three cards of one rank and two of another
        /// </summary>
        FullHouse = 6,
        
        /// <summary>
        /// Four cards of the same rank
        /// </summary>
        FourOfAKind = 7,
        
        /// <summary>
        /// Five cards in sequence, all of the same suit
        /// </summary>
        StraightFlush = 8,
        
        /// <summary>
        /// Royal flush (A, K, Q, J, 10, all the same suit)
        /// </summary>
        RoyalFlush = 9
    }
    
    /// <summary>
    /// Extension methods for the HandRank enum
    /// </summary>
    public static class HandRankExtensions
    {
        /// <summary>
        /// Gets a human-readable description of the hand rank
        /// </summary>
        public static string GetDescription(this HandRank rank)
        {
            return rank switch
            {
                HandRank.HighCard => "High Card",
                HandRank.OnePair => "One Pair",
                HandRank.TwoPair => "Two Pair",
                HandRank.ThreeOfAKind => "Three of a Kind",
                HandRank.Straight => "Straight",
                HandRank.Flush => "Flush",
                HandRank.FullHouse => "Full House",
                HandRank.FourOfAKind => "Four of a Kind",
                HandRank.StraightFlush => "Straight Flush",
                HandRank.RoyalFlush => "Royal Flush",
                _ => throw new ArgumentOutOfRangeException(nameof(rank))
            };
        }
    }
}