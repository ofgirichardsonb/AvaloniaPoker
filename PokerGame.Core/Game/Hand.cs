using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using PokerGame.Core.Models;

namespace PokerGame.Core.Game
{
    /// <summary>
    /// Represents a poker hand, consisting of 5 cards and a rank
    /// </summary>
    public class Hand : IComparable<Hand>
    {
        /// <summary>
        /// Gets the 5 cards that make up this hand
        /// </summary>
        [JsonPropertyName("cards")]
        public List<Card> Cards { get; private set; } = new List<Card>();
        
        /// <summary>
        /// Gets the rank of this hand (e.g., Flush, Straight, etc.)
        /// </summary>
        [JsonPropertyName("rank")]
        public HandRank Rank { get; private set; }
        
        /// <summary>
        /// Gets an array of card ranks used for comparing equivalent ranked hands
        /// For example, in case of two pairs where the first pair and second pair are identical,
        /// we need the fifth kicker card to determine the winner
        /// </summary>
        [JsonPropertyName("tieBreakers")]
        public int[] TieBreakers { get; private set; } = Array.Empty<int>();
        
        /// <summary>
        /// Gets the player ID associated with this hand, if any
        /// </summary>
        [JsonPropertyName("playerId")]
        public string PlayerId { get; private set; } = string.Empty;
        
        /// <summary>
        /// Gets a description of this hand
        /// </summary>
        [JsonIgnore]
        public string Description => $"{Rank.GetDescription()} ({GetHandDescription()})";
        
        /// <summary>
        /// Creates a new hand with the specified cards, rank, and tie breaker values
        /// </summary>
        public Hand(List<Card> cards, HandRank rank, int[] tieBreakers, string playerId = "")
        {
            if (cards.Count != 5)
            {
                throw new ArgumentException("A poker hand must consist of exactly 5 cards", nameof(cards));
            }
            
            Cards = cards;
            Rank = rank;
            TieBreakers = tieBreakers;
            PlayerId = playerId;
        }
        
        /// <summary>
        /// Compares this hand to another hand based on rank and tie breaker values
        /// </summary>
        /// <returns>
        /// -1 if this hand is lower than the other hand
        /// 0 if this hand is equivalent to the other hand
        /// 1 if this hand is higher than the other hand
        /// </returns>
        public int CompareTo(Hand other)
        {
            // First compare by hand rank
            int rankComparison = Rank.CompareTo(other.Rank);
            if (rankComparison != 0)
            {
                return rankComparison;
            }
            
            // If ranks are the same, compare by tie breaker values
            // Iterate through the tie breaker values in order
            for (int i = 0; i < TieBreakers.Length && i < other.TieBreakers.Length; i++)
            {
                int tieBreakComparison = TieBreakers[i].CompareTo(other.TieBreakers[i]);
                if (tieBreakComparison != 0)
                {
                    return tieBreakComparison;
                }
            }
            
            // If we get here, the hands are equivalent
            return 0;
        }
        
        /// <summary>
        /// Gets a description of the cards in this hand
        /// </summary>
        private string GetHandDescription()
        {
            // Generate a description based on the hand type
            switch (Rank)
            {
                case HandRank.HighCard:
                    return $"{GetCardRankName(TieBreakers[0])} high";
                
                case HandRank.OnePair:
                    return $"Pair of {GetCardRankNamePlural(TieBreakers[0])}";
                
                case HandRank.TwoPair:
                    return $"Pair of {GetCardRankNamePlural(TieBreakers[0])} and {GetCardRankNamePlural(TieBreakers[1])}";
                
                case HandRank.ThreeOfAKind:
                    return $"Three {GetCardRankNamePlural(TieBreakers[0])}";
                
                case HandRank.Straight:
                    return $"{GetCardRankName(TieBreakers[0])} high";
                
                case HandRank.Flush:
                    return $"{Cards[0].Suit} with {GetCardRankName(TieBreakers[0])} high";
                
                case HandRank.FullHouse:
                    return $"{GetCardRankNamePlural(TieBreakers[0])} full of {GetCardRankNamePlural(TieBreakers[1])}";
                
                case HandRank.FourOfAKind:
                    return $"Four {GetCardRankNamePlural(TieBreakers[0])}";
                
                case HandRank.StraightFlush:
                    return $"{GetCardRankName(TieBreakers[0])} high";
                
                case HandRank.RoyalFlush:
                    return Cards[0].Suit.ToString();
                
                default:
                    return string.Join(", ", Cards.Select(c => c.ToString()));
            }
        }
        
        /// <summary>
        /// Gets the name of a card rank (e.g., "Ace", "King", etc.)
        /// </summary>
        private static string GetCardRankName(int rank)
        {
            return rank switch
            {
                14 => "Ace",
                13 => "King",
                12 => "Queen",
                11 => "Jack",
                10 => "Ten",
                9 => "Nine",
                8 => "Eight",
                7 => "Seven",
                6 => "Six",
                5 => "Five",
                4 => "Four",
                3 => "Three",
                2 => "Deuce",
                _ => rank.ToString()
            };
        }
        
        /// <summary>
        /// Gets the plural name of a card rank (e.g., "Aces", "Kings", etc.)
        /// </summary>
        private static string GetCardRankNamePlural(int rank)
        {
            string singular = GetCardRankName(rank);
            
            // Handle special cases
            if (singular == "Six")
            {
                return "Sixes";
            }
            
            // For most cases, just add "s"
            return singular + "s";
        }
        
        /// <summary>
        /// Returns a string that represents the current hand
        /// </summary>
        public override string ToString()
        {
            return Description;
        }
    }
}