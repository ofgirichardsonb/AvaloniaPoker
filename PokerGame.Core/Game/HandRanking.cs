using System;
using System.Collections.Generic;

namespace PokerGame.Core.Game
{
    /// <summary>
    /// Represents a hand ranking in poker
    /// </summary>
    public class HandRanking : IComparable<HandRanking>
    {
        /// <summary>
        /// Gets the rank of the hand
        /// </summary>
        public HandRank Rank { get; private set; }
        
        /// <summary>
        /// Gets the high card values for tie-breaking
        /// </summary>
        public List<int> HighCardValues { get; private set; }
        
        /// <summary>
        /// Creates a new hand ranking
        /// </summary>
        /// <param name="rank">The hand rank</param>
        /// <param name="highCardValues">The high card values for tie-breaking</param>
        public HandRanking(HandRank rank, List<int> highCardValues)
        {
            Rank = rank;
            HighCardValues = highCardValues ?? new List<int>();
        }
        
        /// <summary>
        /// Compares this hand ranking to another one
        /// </summary>
        /// <param name="other">The hand ranking to compare to</param>
        /// <returns>A value indicating the relative comparison</returns>
        public int CompareTo(HandRanking other)
        {
            if (other == null)
                return 1;
                
            // First compare by hand rank
            int rankComparison = Rank.CompareTo(other.Rank);
            if (rankComparison != 0)
                return rankComparison;
                
            // If ranks are the same, compare high card values in order
            for (int i = 0; i < Math.Min(HighCardValues.Count, other.HighCardValues.Count); i++)
            {
                int valueComparison = HighCardValues[i].CompareTo(other.HighCardValues[i]);
                if (valueComparison != 0)
                    return valueComparison;
            }
            
            // If all values compared so far are equal, the hand with more high card values wins
            return HighCardValues.Count.CompareTo(other.HighCardValues.Count);
        }
        
        /// <summary>
        /// Returns a string representation of the hand ranking
        /// </summary>
        /// <returns>A string describing the hand ranking</returns>
        public override string ToString()
        {
            return $"{Rank} (High: {string.Join(", ", HighCardValues)})";
        }
    }
    
    // HandRank enum is already defined in HandRank.cs
}