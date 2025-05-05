using System;
using System.Collections.Generic;
using System.Linq;
using PokerGame.Core.Models;

namespace PokerGame.Core.Game
{
    /// <summary>
    /// Evaluates poker hands and determines their ranking
    /// </summary>
    public static class HandEvaluator
    {
        /// <summary>
        /// Evaluates a hand of cards and determines its ranking
        /// </summary>
        /// <param name="cards">The cards to evaluate</param>
        /// <returns>The ranking of the hand</returns>
        public static HandRanking EvaluateHand(List<Card> cards)
        {
            if (cards == null || cards.Count < 5)
                throw new ArgumentException("A poker hand must consist of at least 5 cards", nameof(cards));
                
            // Take the best 5-card hand if more than 5 cards are provided
            var bestHand = FindBestFiveCardHand(cards);
            
            // Check for royal flush
            if (IsRoyalFlush(bestHand))
                return new HandRanking(HandRank.RoyalFlush, new List<int> { 14 }); // Ace high
                
            // Check for straight flush
            if (IsStraightFlush(bestHand, out var straightFlushHighCard))
                return new HandRanking(HandRank.StraightFlush, new List<int> { straightFlushHighCard });
                
            // Check for four of a kind
            if (IsFourOfAKind(bestHand, out var fourOfAKindRank, out var fourOfAKindKicker))
                return new HandRanking(HandRank.FourOfAKind, new List<int> { fourOfAKindRank, fourOfAKindKicker });
                
            // Check for full house
            if (IsFullHouse(bestHand, out var fullHouseThreeRank, out var fullHouseTwoRank))
                return new HandRanking(HandRank.FullHouse, new List<int> { fullHouseThreeRank, fullHouseTwoRank });
                
            // Check for flush
            if (IsFlush(bestHand, out var flushHighCards))
                return new HandRanking(HandRank.Flush, flushHighCards);
                
            // Check for straight
            if (IsStraight(bestHand, out var straightHighCard))
                return new HandRanking(HandRank.Straight, new List<int> { straightHighCard });
                
            // Check for three of a kind
            if (IsThreeOfAKind(bestHand, out var threeOfAKindRank, out var threeOfAKindKickers))
                return new HandRanking(HandRank.ThreeOfAKind, new List<int> { threeOfAKindRank }.Concat(threeOfAKindKickers).ToList());
                
            // Check for two pair
            if (IsTwoPair(bestHand, out var twoPairHighRank, out var twoPairLowRank, out var twoPairKicker))
                return new HandRanking(HandRank.TwoPair, new List<int> { twoPairHighRank, twoPairLowRank, twoPairKicker });
                
            // Check for one pair
            if (IsOnePair(bestHand, out var onePairRank, out var onePairKickers))
                return new HandRanking(HandRank.OnePair, new List<int> { onePairRank }.Concat(onePairKickers).ToList());
                
            // High card
            var highCardValues = bestHand.Select(GetCardValue).OrderByDescending(v => v).Take(5).ToList();
            return new HandRanking(HandRank.HighCard, highCardValues);
        }
        
        /// <summary>
        /// Finds the best 5-card hand from a set of cards
        /// </summary>
        /// <param name="cards">The cards to evaluate</param>
        /// <returns>The best 5-card hand</returns>
        private static List<Card> FindBestFiveCardHand(List<Card> cards)
        {
            if (cards.Count <= 5)
                return cards.ToList();
                
            // Generate all possible 5-card combinations
            var allPossibleHands = new List<List<Card>>();
            GenerateCombinations(cards, 5, 0, new Card[5], 0, allPossibleHands);
            
            // Evaluate each hand and return the best one
            List<Card> bestHand = null;
            HandRank bestRank = HandRank.HighCard;
            List<int> bestHighCards = null;
            
            foreach (var hand in allPossibleHands)
            {
                var ranking = EvaluateHand(hand);
                if (bestHand == null || ranking.Rank > bestRank || 
                   (ranking.Rank == bestRank && CompareHighCards(ranking.HighCardValues, bestHighCards) > 0))
                {
                    bestHand = hand;
                    bestRank = ranking.Rank;
                    bestHighCards = ranking.HighCardValues;
                }
            }
            
            return bestHand;
        }
        
        /// <summary>
        /// Generates all possible combinations of k cards from n cards
        /// </summary>
        private static void GenerateCombinations(List<Card> cards, int k, int start, Card[] combination, int index, List<List<Card>> result)
        {
            if (index == k)
            {
                result.Add(combination.ToList());
                return;
            }
            
            for (int i = start; i <= cards.Count - k + index; i++)
            {
                combination[index] = cards[i];
                GenerateCombinations(cards, k, i + 1, combination, index + 1, result);
            }
        }
        
        /// <summary>
        /// Compares two lists of high card values
        /// </summary>
        private static int CompareHighCards(List<int> a, List<int> b)
        {
            if (a == null)
                return b == null ? 0 : -1;
                
            if (b == null)
                return 1;
                
            for (int i = 0; i < Math.Min(a.Count, b.Count); i++)
            {
                int comparison = a[i].CompareTo(b[i]);
                if (comparison != 0)
                    return comparison;
            }
            
            return a.Count.CompareTo(b.Count);
        }
        
        /// <summary>
        /// Checks if a hand is a royal flush
        /// </summary>
        private static bool IsRoyalFlush(List<Card> cards)
        {
            return IsStraightFlush(cards, out var highCard) && highCard == 14; // Ace high
        }
        
        /// <summary>
        /// Checks if a hand is a straight flush
        /// </summary>
        private static bool IsStraightFlush(List<Card> cards, out int highCard)
        {
            highCard = 0;
            
            if (!IsFlush(cards, out _))
                return false;
                
            return IsStraight(cards, out highCard);
        }
        
        /// <summary>
        /// Checks if a hand is four of a kind
        /// </summary>
        private static bool IsFourOfAKind(List<Card> cards, out int fourRank, out int kicker)
        {
            fourRank = 0;
            kicker = 0;
            
            var groups = cards.GroupBy(c => GetCardValue(c)).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
            
            if (groups.Count >= 2 && groups[0].Count() == 4)
            {
                fourRank = groups[0].Key;
                kicker = groups[1].Key;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Checks if a hand is a full house
        /// </summary>
        private static bool IsFullHouse(List<Card> cards, out int threeRank, out int twoRank)
        {
            threeRank = 0;
            twoRank = 0;
            
            var groups = cards.GroupBy(c => GetCardValue(c)).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
            
            if (groups.Count >= 2 && groups[0].Count() >= 3 && groups[1].Count() >= 2)
            {
                threeRank = groups[0].Key;
                twoRank = groups[1].Key;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Checks if a hand is a flush
        /// </summary>
        private static bool IsFlush(List<Card> cards, out List<int> highCards)
        {
            highCards = cards.Select(c => GetCardValue(c)).OrderByDescending(v => v).Take(5).ToList();
            
            var grouped = cards.GroupBy(c => c.Suit);
            return grouped.Any(g => g.Count() >= 5);
        }
        
        /// <summary>
        /// Checks if a hand is a straight
        /// </summary>
        private static bool IsStraight(List<Card> cards, out int highCard)
        {
            highCard = 0;
            
            var distinctValues = cards.Select(c => GetCardValue(c)).Distinct().OrderBy(v => v).ToList();
            
            // Handle Ace as both high and low
            if (distinctValues.Contains(14)) // Ace
                distinctValues.Insert(0, 1); // Add Ace as 1 as well
                
            for (int i = 0; i <= distinctValues.Count - 5; i++)
            {
                if (distinctValues[i + 4] - distinctValues[i] == 4)
                {
                    highCard = distinctValues[i + 4];
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Checks if a hand is three of a kind
        /// </summary>
        private static bool IsThreeOfAKind(List<Card> cards, out int threeRank, out List<int> kickers)
        {
            threeRank = 0;
            kickers = new List<int>();
            
            var groups = cards.GroupBy(c => GetCardValue(c)).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
            
            if (groups.Count >= 3 && groups[0].Count() == 3 && groups[1].Count() != 2)
            {
                threeRank = groups[0].Key;
                kickers = groups.Skip(1).Take(2).Select(g => g.Key).ToList();
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Checks if a hand is two pair
        /// </summary>
        private static bool IsTwoPair(List<Card> cards, out int highPairRank, out int lowPairRank, out int kicker)
        {
            highPairRank = 0;
            lowPairRank = 0;
            kicker = 0;
            
            var groups = cards.GroupBy(c => GetCardValue(c)).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
            
            if (groups.Count >= 3 && groups[0].Count() == 2 && groups[1].Count() == 2)
            {
                highPairRank = groups[0].Key;
                lowPairRank = groups[1].Key;
                kicker = groups[2].Key;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Checks if a hand is one pair
        /// </summary>
        private static bool IsOnePair(List<Card> cards, out int pairRank, out List<int> kickers)
        {
            pairRank = 0;
            kickers = new List<int>();
            
            var groups = cards.GroupBy(c => GetCardValue(c)).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
            
            if (groups.Count >= 4 && groups[0].Count() == 2)
            {
                pairRank = groups[0].Key;
                kickers = groups.Skip(1).Take(3).Select(g => g.Key).ToList();
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets the numerical value of a card (2-14, with Ace as 14)
        /// </summary>
        private static int GetCardValue(Card card)
        {
            return (int)card.Rank;
        }
        
        /// <summary>
        /// Evaluates the best possible hand from the given hole cards and community cards
        /// </summary>
        /// <param name="holeCards">The player's hole cards</param>
        /// <param name="communityCards">The community cards</param>
        /// <param name="playerId">The player ID for reference</param>
        /// <returns>The best hand for the player</returns>
        public static Hand EvaluateBestHand(List<Card> holeCards, List<Card> communityCards, string playerId)
        {
            // Combine hole cards and community cards
            var allCards = new List<Card>();
            allCards.AddRange(holeCards);
            allCards.AddRange(communityCards);
            
            // Find the best 5-card hand
            var bestCards = FindBestFiveCardHand(allCards);
            
            // Evaluate the hand ranking
            var handRanking = EvaluateHand(bestCards);
            
            // Convert to an array of tie breakers
            int[] tieBreakers = handRanking.HighCardValues.ToArray();
            
            // Create the hand object with the ranking and description
            var hand = new Hand(bestCards, handRanking.Rank, tieBreakers, playerId);
            
            return hand;
        }
        
        /// <summary>
        /// Gets a human-readable description of the hand
        /// </summary>
        private static string GetHandDescription(HandRank rank, List<int> tieBreakers)
        {
            switch (rank)
            {
                case HandRank.RoyalFlush:
                    return "Royal Flush";
                    
                case HandRank.StraightFlush:
                    return $"Straight Flush, {GetCardName(tieBreakers[0])} high";
                    
                case HandRank.FourOfAKind:
                    return $"Four of a Kind, {GetCardName(tieBreakers[0])}s";
                    
                case HandRank.FullHouse:
                    return $"Full House, {GetCardName(tieBreakers[0])}s full of {GetCardName(tieBreakers[1])}s";
                    
                case HandRank.Flush:
                    return $"Flush, {GetCardName(tieBreakers[0])} high";
                    
                case HandRank.Straight:
                    return $"Straight, {GetCardName(tieBreakers[0])} high";
                    
                case HandRank.ThreeOfAKind:
                    return $"Three of a Kind, {GetCardName(tieBreakers[0])}s";
                    
                case HandRank.TwoPair:
                    return $"Two Pair, {GetCardName(tieBreakers[0])}s and {GetCardName(tieBreakers[1])}s";
                    
                case HandRank.OnePair:
                    return $"Pair of {GetCardName(tieBreakers[0])}s";
                    
                case HandRank.HighCard:
                    return $"High Card {GetCardName(tieBreakers[0])}";
                    
                default:
                    return "Unknown Hand";
            }
        }
        
        /// <summary>
        /// Gets the name of a card by its numerical value
        /// </summary>
        private static string GetCardName(int value)
        {
            switch (value)
            {
                case 14: return "Ace";
                case 13: return "King";
                case 12: return "Queen";
                case 11: return "Jack";
                case 10: return "10";
                default: return value.ToString();
            }
        }
        
        /// <summary>
        /// Determines the winners among the players
        /// </summary>
        /// <param name="playerHands">Dictionary mapping players to their best hands</param>
        /// <returns>List of winning players</returns>
        public static List<Player> DetermineWinners(Dictionary<Player, Hand> playerHands)
        {
            List<Player> winners = new List<Player>();
            Player? bestPlayer = null;
            
            foreach (var kvp in playerHands)
            {
                var player = kvp.Key;
                var hand = kvp.Value;
                
                if (bestPlayer == null || 
                    CompareHands(hand, playerHands[bestPlayer]) > 0)
                {
                    bestPlayer = player;
                    winners.Clear();
                    winners.Add(player);
                }
                else if (bestPlayer != null && 
                        CompareHands(hand, playerHands[bestPlayer]) == 0)
                {
                    winners.Add(player);
                }
            }
            
            return winners;
        }
        
        /// <summary>
        /// Compares two poker hands
        /// </summary>
        /// <param name="hand1">The first hand</param>
        /// <param name="hand2">The second hand</param>
        /// <returns>A positive value if hand1 is better, 0 if they're equal, a negative value if hand2 is better</returns>
        private static int CompareHands(Hand hand1, Hand hand2)
        {
            // First compare by hand rank
            int rankComparison = hand1.Rank.CompareTo(hand2.Rank);
            if (rankComparison != 0)
                return rankComparison;
                
            // If ranks are equal, compare by tie breakers
            // This is handled by the Hand.CompareTo method
            return hand1.CompareTo(hand2);
        }
    }
}