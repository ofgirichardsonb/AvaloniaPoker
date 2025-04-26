using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerGame.Core.Models
{
    /// <summary>
    /// Evaluates poker hands to determine their ranking
    /// </summary>
    public static class PokerHandEvaluator
    {
        /// <summary>
        /// Evaluates the best 5-card poker hand from the given cards
        /// </summary>
        /// <param name="cards">The cards to evaluate (usually 7 for Texas Hold'em)</param>
        /// <returns>The best 5-card poker hand</returns>
        public static Hand EvaluateBestHand(List<Card> cards)
        {
            if (cards.Count < 5)
                throw new ArgumentException("At least 5 cards are required to evaluate a poker hand", nameof(cards));
            
            // Generate all possible 5-card combinations
            var combinations = GetAllCombinations(cards, 5);
            
            // Evaluate each combination and return the best one
            Hand? bestHand = null;
            foreach (var combo in combinations)
            {
                var hand = EvaluateHand(combo);
                if (bestHand == null || hand.CompareTo(bestHand) > 0)
                {
                    bestHand = hand;
                }
            }
            
            return bestHand!; // We know there will be at least one combination
        }
        
        /// <summary>
        /// Evaluates a 5-card poker hand
        /// </summary>
        /// <param name="cards">The 5 cards to evaluate</param>
        /// <returns>The evaluated poker hand</returns>
        public static Hand EvaluateHand(List<Card> cards)
        {
            if (cards.Count != 5)
                throw new ArgumentException("Exactly 5 cards are required to evaluate a poker hand", nameof(cards));
            
            // Check for each hand rank from highest to lowest
            if (IsRoyalFlush(cards, out var royalCards, out var royalKickers))
                return new Hand(cards, PokerHandRank.RoyalFlush, royalCards, royalKickers);
                
            if (IsStraightFlush(cards, out var straightFlushCards, out var straightFlushKickers))
                return new Hand(cards, PokerHandRank.StraightFlush, straightFlushCards, straightFlushKickers);
                
            if (IsFourOfAKind(cards, out var fourOfAKindCards, out var fourOfAKindKickers))
                return new Hand(cards, PokerHandRank.FourOfAKind, fourOfAKindCards, fourOfAKindKickers);
                
            if (IsFullHouse(cards, out var fullHouseCards, out var fullHouseKickers))
                return new Hand(cards, PokerHandRank.FullHouse, fullHouseCards, fullHouseKickers);
                
            if (IsFlush(cards, out var flushCards, out var flushKickers))
                return new Hand(cards, PokerHandRank.Flush, flushCards, flushKickers);
                
            if (IsStraight(cards, out var straightCards, out var straightKickers))
                return new Hand(cards, PokerHandRank.Straight, straightCards, straightKickers);
                
            if (IsThreeOfAKind(cards, out var threeOfAKindCards, out var threeOfAKindKickers))
                return new Hand(cards, PokerHandRank.ThreeOfAKind, threeOfAKindCards, threeOfAKindKickers);
                
            if (IsTwoPair(cards, out var twoPairCards, out var twoPairKickers))
                return new Hand(cards, PokerHandRank.TwoPair, twoPairCards, twoPairKickers);
                
            if (IsPair(cards, out var pairCards, out var pairKickers))
                return new Hand(cards, PokerHandRank.Pair, pairCards, pairKickers);
            
            // Default to high card
            var sortedCards = cards.OrderByDescending(c => c.Rank).ToList();
            return new Hand(cards, PokerHandRank.HighCard, new List<Card> { sortedCards[0] }, sortedCards.Skip(1).ToList());
        }
        
        /// <summary>
        /// Checks if the hand is a royal flush
        /// </summary>
        private static bool IsRoyalFlush(List<Card> cards, out List<Card> rankCards, out List<Card> kickers)
        {
            if (IsStraightFlush(cards, out rankCards, out kickers) && 
                rankCards.Any(c => c.Rank == Rank.Ace) && 
                rankCards.Any(c => c.Rank == Rank.King))
            {
                rankCards = cards.OrderByDescending(c => c.Rank).ToList();
                kickers = new List<Card>();
                return true;
            }
            
            rankCards = new List<Card>();
            kickers = new List<Card>();
            return false;
        }
        
        /// <summary>
        /// Checks if the hand is a straight flush
        /// </summary>
        private static bool IsStraightFlush(List<Card> cards, out List<Card> rankCards, out List<Card> kickers)
        {
            if (IsFlush(cards, out _, out _) && IsStraight(cards, out rankCards, out _))
            {
                kickers = new List<Card>();
                return true;
            }
            
            rankCards = new List<Card>();
            kickers = new List<Card>();
            return false;
        }
        
        /// <summary>
        /// Checks if the hand is four of a kind
        /// </summary>
        private static bool IsFourOfAKind(List<Card> cards, out List<Card> rankCards, out List<Card> kickers)
        {
            var groups = cards.GroupBy(c => c.Rank).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
            
            if (groups.Count >= 2 && groups[0].Count() == 4)
            {
                rankCards = groups[0].ToList();
                kickers = groups.Skip(1).SelectMany(g => g).ToList();
                return true;
            }
            
            rankCards = new List<Card>();
            kickers = new List<Card>();
            return false;
        }
        
        /// <summary>
        /// Checks if the hand is a full house
        /// </summary>
        private static bool IsFullHouse(List<Card> cards, out List<Card> rankCards, out List<Card> kickers)
        {
            var groups = cards.GroupBy(c => c.Rank).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
            
            if (groups.Count >= 2 && groups[0].Count() == 3 && groups[1].Count() >= 2)
            {
                rankCards = groups[0].Concat(groups[1]).ToList();
                kickers = new List<Card>();
                return true;
            }
            
            rankCards = new List<Card>();
            kickers = new List<Card>();
            return false;
        }
        
        /// <summary>
        /// Checks if the hand is a flush
        /// </summary>
        private static bool IsFlush(List<Card> cards, out List<Card> rankCards, out List<Card> kickers)
        {
            bool isFlush = cards.Select(c => c.Suit).Distinct().Count() == 1;
            
            if (isFlush)
            {
                rankCards = cards.OrderByDescending(c => c.Rank).ToList();
                kickers = new List<Card>();
                return true;
            }
            
            rankCards = new List<Card>();
            kickers = new List<Card>();
            return false;
        }
        
        /// <summary>
        /// Checks if the hand is a straight
        /// </summary>
        private static bool IsStraight(List<Card> cards, out List<Card> rankCards, out List<Card> kickers)
        {
            var distinctRanks = cards.Select(c => c.Rank).Distinct().OrderByDescending(r => r).ToList();
            
            // Special case: A-5-4-3-2 straight (Ace acts as low)
            if (distinctRanks.Count == 5 && 
                distinctRanks.Contains(Rank.Ace) && 
                distinctRanks.Contains(Rank.Five) &&
                distinctRanks.Contains(Rank.Four) &&
                distinctRanks.Contains(Rank.Three) &&
                distinctRanks.Contains(Rank.Two))
            {
                // For A-5-4-3-2, the 5 is the high card
                var aceCard = cards.First(c => c.Rank == Rank.Ace);
                var orderedCards = cards.Where(c => c.Rank != Rank.Ace)
                                       .OrderByDescending(c => c.Rank)
                                       .ToList();
                orderedCards.Add(aceCard); // Add Ace at the end (low)
                
                rankCards = orderedCards;
                kickers = new List<Card>();
                return true;
            }
            
            // Normal straight check
            if (distinctRanks.Count == 5)
            {
                int highRank = (int)distinctRanks[0];
                int lowRank = (int)distinctRanks[4];
                
                if (highRank - lowRank == 4)
                {
                    rankCards = cards.OrderByDescending(c => c.Rank).ToList();
                    kickers = new List<Card>();
                    return true;
                }
            }
            
            rankCards = new List<Card>();
            kickers = new List<Card>();
            return false;
        }
        
        /// <summary>
        /// Checks if the hand is three of a kind
        /// </summary>
        private static bool IsThreeOfAKind(List<Card> cards, out List<Card> rankCards, out List<Card> kickers)
        {
            var groups = cards.GroupBy(c => c.Rank).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
            
            if (groups.Count >= 3 && groups[0].Count() == 3)
            {
                rankCards = groups[0].ToList();
                kickers = groups.Skip(1).SelectMany(g => g.OrderByDescending(c => c.Rank)).ToList();
                return true;
            }
            
            rankCards = new List<Card>();
            kickers = new List<Card>();
            return false;
        }
        
        /// <summary>
        /// Checks if the hand is two pair
        /// </summary>
        private static bool IsTwoPair(List<Card> cards, out List<Card> rankCards, out List<Card> kickers)
        {
            var groups = cards.GroupBy(c => c.Rank).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
            
            if (groups.Count >= 3 && groups[0].Count() == 2 && groups[1].Count() == 2)
            {
                rankCards = groups[0].Concat(groups[1]).ToList();
                kickers = groups.Skip(2).SelectMany(g => g).ToList();
                return true;
            }
            
            rankCards = new List<Card>();
            kickers = new List<Card>();
            return false;
        }
        
        /// <summary>
        /// Checks if the hand is a pair
        /// </summary>
        private static bool IsPair(List<Card> cards, out List<Card> rankCards, out List<Card> kickers)
        {
            var groups = cards.GroupBy(c => c.Rank).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
            
            if (groups.Count >= 4 && groups[0].Count() == 2)
            {
                rankCards = groups[0].ToList();
                kickers = groups.Skip(1).SelectMany(g => g.OrderByDescending(c => c.Rank)).ToList();
                return true;
            }
            
            rankCards = new List<Card>();
            kickers = new List<Card>();
            return false;
        }
        
        /// <summary>
        /// Generates all possible combinations of the specified size from the input list
        /// </summary>
        private static List<List<Card>> GetAllCombinations(List<Card> cards, int combinationSize)
        {
            var result = new List<List<Card>>();
            GenerateCombinations(cards, combinationSize, 0, new List<Card>(), result);
            return result;
        }
        
        /// <summary>
        /// Recursive helper for generating combinations
        /// </summary>
        private static void GenerateCombinations(List<Card> cards, int combinationSize, int startIndex, 
                                               List<Card> currentCombination, List<List<Card>> result)
        {
            if (currentCombination.Count == combinationSize)
            {
                result.Add(new List<Card>(currentCombination));
                return;
            }
            
            for (int i = startIndex; i < cards.Count; i++)
            {
                currentCombination.Add(cards[i]);
                GenerateCombinations(cards, combinationSize, i + 1, currentCombination, result);
                currentCombination.RemoveAt(currentCombination.Count - 1);
            }
        }
    }
}
