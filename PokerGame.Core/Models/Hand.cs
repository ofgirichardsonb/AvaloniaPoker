using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerGame.Core.Models
{
    /// <summary>
    /// Represents a 5-card poker hand
    /// </summary>
    public class Hand : IComparable<Hand>
    {
        /// <summary>
        /// The cards in the hand
        /// </summary>
        public List<Card> Cards { get; }
        
        /// <summary>
        /// The rank of the hand (e.g., Pair, Flush, etc.)
        /// </summary>
        public PokerHandRank Rank { get; }
        
        /// <summary>
        /// The cards that determine the hand's value, sorted by importance (e.g., the pair cards first for a pair)
        /// </summary>
        public List<Card> RankCards { get; }
        
        /// <summary>
        /// Cards used as kickers to break ties
        /// </summary>
        public List<Card> Kickers { get; }

        /// <summary>
        /// Creates a new Hand instance with the specified cards and evaluated rank
        /// </summary>
        /// <param name="cards">The 5 cards in the hand</param>
        /// <param name="rank">The poker hand ranking</param>
        /// <param name="rankCards">The cards that determine the hand's value</param>
        /// <param name="kickers">The kicker cards for tiebreaking</param>
        public Hand(List<Card> cards, PokerHandRank rank, List<Card> rankCards, List<Card> kickers)
        {
            if (cards.Count != 5)
                throw new ArgumentException("A poker hand must contain exactly 5 cards", nameof(cards));
                
            Cards = cards.OrderByDescending(c => c.Rank).ToList();
            Rank = rank;
            RankCards = rankCards;
            Kickers = kickers;
        }

        /// <summary>
        /// Compares this hand to another hand according to poker hand ranking rules
        /// </summary>
        /// <param name="other">The hand to compare to</param>
        /// <returns>A negative number if this hand is worse, 0 if equal, positive if better</returns>
        public int CompareTo(Hand? other)
        {
            if (other == null)
                return 1;
                
            // First compare by hand rank
            int rankComparison = Rank.CompareTo(other.Rank);
            if (rankComparison != 0)
                return rankComparison;
                
            // If same hand rank, compare rank cards
            for (int i = 0; i < Math.Min(RankCards.Count, other.RankCards.Count); i++)
            {
                int cardComparison = RankCards[i].Rank.CompareTo(other.RankCards[i].Rank);
                if (cardComparison != 0)
                    return cardComparison;
            }
            
            // If still tied, compare kickers
            for (int i = 0; i < Math.Min(Kickers.Count, other.Kickers.Count); i++)
            {
                int kickerComparison = Kickers[i].Rank.CompareTo(other.Kickers[i].Rank);
                if (kickerComparison != 0)
                    return kickerComparison;
            }
            
            // Completely equal hands
            return 0;
        }

        /// <summary>
        /// Returns a string description of the hand
        /// </summary>
        public override string ToString()
        {
            string description = Rank.ToString();
            
            if (Rank == PokerHandRank.HighCard)
                return $"{description}: {RankCards[0].Rank} high";
            else if (Rank == PokerHandRank.Pair)
                return $"{description}: {RankCards[0].Rank}s";
            else if (Rank == PokerHandRank.TwoPair)
                return $"{description}: {RankCards[0].Rank}s and {RankCards[2].Rank}s";
            else if (Rank == PokerHandRank.ThreeOfAKind)
                return $"{description}: {RankCards[0].Rank}s";
            else if (Rank == PokerHandRank.Straight)
                return $"{description}: {RankCards[0].Rank} high";
            else if (Rank == PokerHandRank.Flush)
                return $"{description}: {RankCards[0].Rank} high";
            else if (Rank == PokerHandRank.FullHouse)
                return $"{description}: {RankCards[0].Rank}s full of {RankCards[3].Rank}s";
            else if (Rank == PokerHandRank.FourOfAKind)
                return $"{description}: {RankCards[0].Rank}s";
            else if (Rank == PokerHandRank.StraightFlush || Rank == PokerHandRank.RoyalFlush)
                return description;
                
            return description;
        }
    }
}
