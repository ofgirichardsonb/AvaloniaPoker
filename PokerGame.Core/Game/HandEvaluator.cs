using System;
using System.Collections.Generic;
using System.Linq;
using PokerGame.Core.Models;
using PokerGame.Core.Messaging;
using static PokerGame.Core.Messaging.Logger;

namespace PokerGame.Core.Game
{
    /// <summary>
    /// Evaluates poker hands and determines the winner
    /// </summary>
    public class HandEvaluator
    {
        /// <summary>
        /// Determines the best 5-card hand for a player given their hole cards and the community cards
        /// </summary>
        /// <param name="holeCards">The player's 2 hole cards</param>
        /// <param name="communityCards">The 3-5 community cards</param>
        /// <param name="playerId">Optional player ID to associate with the hand</param>
        /// <returns>The best 5-card hand for the player</returns>
        public static Hand EvaluateBestHand(List<Card> holeCards, List<Card> communityCards, string playerId = "")
        {
            // Combine the hole cards and community cards
            var allCards = new List<Card>();
            allCards.AddRange(holeCards);
            allCards.AddRange(communityCards);
            
            // Generate all possible 5-card combinations from the available cards
            var possibleHands = GenerateAllFiveCardCombinations(allCards);
            
            // Evaluate each possible hand and return the best one
            Hand bestHand = null;
            
            foreach (var cards in possibleHands)
            {
                var hand = EvaluateHand(cards, playerId);
                
                if (bestHand == null || hand.CompareTo(bestHand) > 0)
                {
                    bestHand = hand;
                }
            }
            
            Logger.Log($"Best hand for player {playerId}: {bestHand?.Description}");
            
            // Return the best hand (should never be null if inputs are valid)
            return bestHand!;
        }
        
        /// <summary>
        /// Evaluates a specific 5-card hand
        /// </summary>
        /// <param name="cards">The 5 cards to evaluate</param>
        /// <param name="playerId">Optional player ID to associate with the hand</param>
        /// <returns>The evaluated hand</returns>
        public static Hand EvaluateHand(List<Card> cards, string playerId = "")
        {
            if (cards.Count != 5)
            {
                throw new ArgumentException("Hand evaluation requires exactly 5 cards", nameof(cards));
            }
            
            // Check for each hand type from highest to lowest
            
            // Check for royal flush
            if (IsRoyalFlush(cards, out var royalFlushTieBreakers))
            {
                return new Hand(cards, HandRank.RoyalFlush, royalFlushTieBreakers, playerId);
            }
            
            // Check for straight flush
            if (IsStraightFlush(cards, out var straightFlushTieBreakers))
            {
                return new Hand(cards, HandRank.StraightFlush, straightFlushTieBreakers, playerId);
            }
            
            // Check for four of a kind
            if (IsFourOfAKind(cards, out var fourOfAKindTieBreakers))
            {
                return new Hand(cards, HandRank.FourOfAKind, fourOfAKindTieBreakers, playerId);
            }
            
            // Check for full house
            if (IsFullHouse(cards, out var fullHouseTieBreakers))
            {
                return new Hand(cards, HandRank.FullHouse, fullHouseTieBreakers, playerId);
            }
            
            // Check for flush
            if (IsFlush(cards, out var flushTieBreakers))
            {
                return new Hand(cards, HandRank.Flush, flushTieBreakers, playerId);
            }
            
            // Check for straight
            if (IsStraight(cards, out var straightTieBreakers))
            {
                return new Hand(cards, HandRank.Straight, straightTieBreakers, playerId);
            }
            
            // Check for three of a kind
            if (IsThreeOfAKind(cards, out var threeOfAKindTieBreakers))
            {
                return new Hand(cards, HandRank.ThreeOfAKind, threeOfAKindTieBreakers, playerId);
            }
            
            // Check for two pair
            if (IsTwoPair(cards, out var twoPairTieBreakers))
            {
                return new Hand(cards, HandRank.TwoPair, twoPairTieBreakers, playerId);
            }
            
            // Check for one pair
            if (IsOnePair(cards, out var onePairTieBreakers))
            {
                return new Hand(cards, HandRank.OnePair, onePairTieBreakers, playerId);
            }
            
            // If nothing else, it's a high card hand
            var highCardTieBreakers = GetRankCountMap(cards)
                .Select(kvp => kvp.Key)
                .OrderByDescending(rank => rank)
                .ToArray();
            
            return new Hand(cards, HandRank.HighCard, highCardTieBreakers, playerId);
        }
        
        /// <summary>
        /// Determines the winner(s) from a list of evaluated hands
        /// </summary>
        /// <param name="hands">The list of hands to compare</param>
        /// <returns>A list of hands that tie for the win</returns>
        public static List<Hand> DetermineWinners(List<Hand> hands)
        {
            if (hands == null || hands.Count == 0)
            {
                return new List<Hand>();
            }
            
            // Find the best hand
            var bestHand = hands[0];
            foreach (var hand in hands.Skip(1))
            {
                if (hand.CompareTo(bestHand) > 0)
                {
                    bestHand = hand;
                }
            }
            
            // Find all hands that tie with the best hand
            var winners = hands.Where(h => h.CompareTo(bestHand) == 0).ToList();
            
            // Return the winner(s)
            return winners;
        }
        
        #region Hand Type Checkers
        
        /// <summary>
        /// Checks if the hand is a royal flush (A, K, Q, J, 10 of the same suit)
        /// </summary>
        private static bool IsRoyalFlush(List<Card> cards, out int[] tieBreakers)
        {
            // A royal flush is a straight flush with an ace high
            if (IsStraightFlush(cards, out tieBreakers) && tieBreakers[0] == 14)
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Checks if the hand is a straight flush (five cards in sequence, all of the same suit)
        /// </summary>
        private static bool IsStraightFlush(List<Card> cards, out int[] tieBreakers)
        {
            if (IsFlush(cards, out _) && IsStraight(cards, out tieBreakers))
            {
                return true;
            }
            
            tieBreakers = Array.Empty<int>();
            return false;
        }
        
        /// <summary>
        /// Checks if the hand is four of a kind (four cards of the same rank)
        /// </summary>
        private static bool IsFourOfAKind(List<Card> cards, out int[] tieBreakers)
        {
            var rankCounts = GetRankCountMap(cards);
            
            var fourOfAKind = rankCounts.FirstOrDefault(kvp => kvp.Value == 4);
            if (fourOfAKind.Value == 4)
            {
                // The first tie breaker is the rank of the four of a kind
                // The second tie breaker is the rank of the kicker
                var kicker = rankCounts.FirstOrDefault(kvp => kvp.Value == 1);
                tieBreakers = new[] { fourOfAKind.Key, kicker.Key };
                return true;
            }
            
            tieBreakers = Array.Empty<int>();
            return false;
        }
        
        /// <summary>
        /// Checks if the hand is a full house (three cards of one rank, two cards of another rank)
        /// </summary>
        private static bool IsFullHouse(List<Card> cards, out int[] tieBreakers)
        {
            var rankCounts = GetRankCountMap(cards);
            
            var threeOfAKind = rankCounts.FirstOrDefault(kvp => kvp.Value == 3);
            var pair = rankCounts.FirstOrDefault(kvp => kvp.Value == 2);
            
            if (threeOfAKind.Value == 3 && pair.Value == 2)
            {
                // The first tie breaker is the rank of the three of a kind
                // The second tie breaker is the rank of the pair
                tieBreakers = new[] { threeOfAKind.Key, pair.Key };
                return true;
            }
            
            tieBreakers = Array.Empty<int>();
            return false;
        }
        
        /// <summary>
        /// Checks if the hand is a flush (five cards of the same suit)
        /// </summary>
        private static bool IsFlush(List<Card> cards, out int[] tieBreakers)
        {
            var firstSuit = cards[0].Suit;
            
            if (cards.All(c => c.Suit == firstSuit))
            {
                // Tie breakers are all five card ranks in descending order
                tieBreakers = cards
                    .Select(c => c.RankValue)
                    .OrderByDescending(r => r)
                    .ToArray();
                
                return true;
            }
            
            tieBreakers = Array.Empty<int>();
            return false;
        }
        
        /// <summary>
        /// Checks if the hand is a straight (five cards in sequence)
        /// </summary>
        private static bool IsStraight(List<Card> cards, out int[] tieBreakers)
        {
            // Get the distinct ranks in ascending order
            var ranks = cards
                .Select(c => c.RankValue)
                .OrderBy(r => r)
                .Distinct()
                .ToList();
            
            // A straight must have exactly 5 distinct ranks
            if (ranks.Count != 5)
            {
                tieBreakers = Array.Empty<int>();
                return false;
            }
            
            // Special case for A-5 straight (where Ace is treated as 1)
            if (ranks.SequenceEqual(new[] { 2, 3, 4, 5, 14 }))
            {
                // In A-5 straight, the highest card is actually 5
                tieBreakers = new[] { 5 };
                return true;
            }
            
            // Check if the ranks form a sequence
            for (int i = 1; i < ranks.Count; i++)
            {
                if (ranks[i] != ranks[i - 1] + 1)
                {
                    tieBreakers = Array.Empty<int>();
                    return false;
                }
            }
            
            // The highest card determines the straight
            tieBreakers = new[] { ranks.Max() };
            return true;
        }
        
        /// <summary>
        /// Checks if the hand is three of a kind (three cards of the same rank)
        /// </summary>
        private static bool IsThreeOfAKind(List<Card> cards, out int[] tieBreakers)
        {
            var rankCounts = GetRankCountMap(cards);
            
            var threeOfAKind = rankCounts.FirstOrDefault(kvp => kvp.Value == 3);
            if (threeOfAKind.Value == 3)
            {
                // Make sure it's not a full house
                if (rankCounts.Any(kvp => kvp.Value == 2))
                {
                    tieBreakers = Array.Empty<int>();
                    return false;
                }
                
                // The first tie breaker is the rank of the three of a kind
                // The next tie breakers are the ranks of the kickers in descending order
                var kickers = rankCounts
                    .Where(kvp => kvp.Value == 1)
                    .Select(kvp => kvp.Key)
                    .OrderByDescending(r => r)
                    .ToList();
                
                tieBreakers = new[] { threeOfAKind.Key, kickers[0], kickers[1] };
                return true;
            }
            
            tieBreakers = Array.Empty<int>();
            return false;
        }
        
        /// <summary>
        /// Checks if the hand is two pair (two cards of one rank, two cards of another rank)
        /// </summary>
        private static bool IsTwoPair(List<Card> cards, out int[] tieBreakers)
        {
            var rankCounts = GetRankCountMap(cards);
            
            var pairs = rankCounts
                .Where(kvp => kvp.Value == 2)
                .Select(kvp => kvp.Key)
                .OrderByDescending(r => r)
                .ToList();
            
            if (pairs.Count == 2)
            {
                // The first two tie breakers are the ranks of the pairs in descending order
                // The third tie breaker is the rank of the kicker
                var kicker = rankCounts.First(kvp => kvp.Value == 1).Key;
                tieBreakers = new[] { pairs[0], pairs[1], kicker };
                return true;
            }
            
            tieBreakers = Array.Empty<int>();
            return false;
        }
        
        /// <summary>
        /// Checks if the hand is one pair (two cards of the same rank)
        /// </summary>
        private static bool IsOnePair(List<Card> cards, out int[] tieBreakers)
        {
            var rankCounts = GetRankCountMap(cards);
            
            var pair = rankCounts.FirstOrDefault(kvp => kvp.Value == 2);
            if (pair.Value == 2)
            {
                // Make sure it's not two pair or better
                if (rankCounts.Count(kvp => kvp.Value == 2) > 1 || rankCounts.Any(kvp => kvp.Value > 2))
                {
                    tieBreakers = Array.Empty<int>();
                    return false;
                }
                
                // The first tie breaker is the rank of the pair
                // The next tie breakers are the ranks of the kickers in descending order
                var kickers = rankCounts
                    .Where(kvp => kvp.Value == 1)
                    .Select(kvp => kvp.Key)
                    .OrderByDescending(r => r)
                    .ToList();
                
                tieBreakers = new[] { pair.Key, kickers[0], kickers[1], kickers[2] };
                return true;
            }
            
            tieBreakers = Array.Empty<int>();
            return false;
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Generates all possible 5-card combinations from a list of cards
        /// </summary>
        private static List<List<Card>> GenerateAllFiveCardCombinations(List<Card> cards)
        {
            var result = new List<List<Card>>();
            
            // If there are exactly 5 cards, there's only one possible combination
            if (cards.Count == 5)
            {
                result.Add(new List<Card>(cards));
                return result;
            }
            
            // If there are more than 5 cards, generate all 5-card combinations
            GenerateCombinations(cards, 0, new List<Card>(), result, 5);
            
            return result;
        }
        
        /// <summary>
        /// Recursive helper method for generating combinations
        /// </summary>
        private static void GenerateCombinations(List<Card> cards, int startIndex, List<Card> currentCombination, 
            List<List<Card>> result, int combinationLength)
        {
            // If we've reached the desired combination length, add it to the result
            if (currentCombination.Count == combinationLength)
            {
                result.Add(new List<Card>(currentCombination));
                return;
            }
            
            // If we've run out of cards to add, return
            if (startIndex >= cards.Count)
            {
                return;
            }
            
            // Try including the current card
            currentCombination.Add(cards[startIndex]);
            GenerateCombinations(cards, startIndex + 1, currentCombination, result, combinationLength);
            
            // Try excluding the current card
            currentCombination.RemoveAt(currentCombination.Count - 1);
            GenerateCombinations(cards, startIndex + 1, currentCombination, result, combinationLength);
        }
        
        /// <summary>
        /// Gets a map of card ranks to their counts in the hand
        /// </summary>
        private static Dictionary<int, int> GetRankCountMap(List<Card> cards)
        {
            var rankCounts = new Dictionary<int, int>();
            
            foreach (var card in cards)
            {
                if (rankCounts.ContainsKey(card.RankValue))
                {
                    rankCounts[card.RankValue]++;
                }
                else
                {
                    rankCounts[card.RankValue] = 1;
                }
            }
            
            return rankCounts;
        }
        
        #endregion
    }
}