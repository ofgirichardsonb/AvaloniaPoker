using System;
using System.Collections.Generic;
using System.Linq;
using PokerGame.Core.Game;
using PokerGame.Core.Models;

namespace QuickTestApp
{
    public class HandEvaluatorQuickTest
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Testing the HandEvaluator with our fixed tests...");
            
            // Test 1: Royal Flush
            var royalFlushCards = new List<Card>
            {
                new Card(Suit.Hearts, Rank.Ten),
                new Card(Suit.Hearts, Rank.Jack),
                new Card(Suit.Hearts, Rank.Queen),
                new Card(Suit.Hearts, Rank.King),
                new Card(Suit.Hearts, Rank.Ace)
            };
            
            var royalFlushResult = HandEvaluator.EvaluateHand(royalFlushCards);
            Console.WriteLine($"Royal Flush Test - Expected: {HandRank.RoyalFlush}, Actual: {royalFlushResult.Rank}");
            Console.WriteLine($"High Card Value - Expected: 14 (Ace), Actual: {royalFlushResult.TieBreakers[0]}");
            
            // Test 2: Straight Flush
            var straightFlushCards = new List<Card>
            {
                new Card(Suit.Clubs, Rank.Six),
                new Card(Suit.Clubs, Rank.Seven),
                new Card(Suit.Clubs, Rank.Eight),
                new Card(Suit.Clubs, Rank.Nine),
                new Card(Suit.Clubs, Rank.Ten)
            };
            
            var straightFlushResult = HandEvaluator.EvaluateHand(straightFlushCards);
            Console.WriteLine($"Straight Flush Test - Expected: {HandRank.StraightFlush}, Actual: {straightFlushResult.Rank}");
            Console.WriteLine($"High Card Value - Expected: 10, Actual: {straightFlushResult.TieBreakers[0]}");
            
            // Test 3: Four of a Kind
            var fourOfAKindCards = new List<Card>
            {
                new Card(Suit.Hearts, Rank.Eight),
                new Card(Suit.Diamonds, Rank.Eight),
                new Card(Suit.Clubs, Rank.Eight),
                new Card(Suit.Spades, Rank.Eight),
                new Card(Suit.Hearts, Rank.King)
            };
            
            var fourOfAKindResult = HandEvaluator.EvaluateHand(fourOfAKindCards);
            Console.WriteLine($"Four of a Kind Test - Expected: {HandRank.FourOfAKind}, Actual: {fourOfAKindResult.Rank}");
            Console.WriteLine($"Four of a Kind Value - Expected: 8, Actual: {fourOfAKindResult.TieBreakers[0]}");
            
            // Test 4: Full House
            var fullHouseCards = new List<Card>
            {
                new Card(Suit.Hearts, Rank.Nine),
                new Card(Suit.Diamonds, Rank.Nine),
                new Card(Suit.Clubs, Rank.Nine),
                new Card(Suit.Hearts, Rank.Two),
                new Card(Suit.Diamonds, Rank.Two)
            };
            
            var fullHouseResult = HandEvaluator.EvaluateHand(fullHouseCards);
            Console.WriteLine($"Full House Test - Expected: {HandRank.FullHouse}, Actual: {fullHouseResult.Rank}");
            Console.WriteLine($"Three of a Kind Value - Expected: 9, Actual: {fullHouseResult.TieBreakers[0]}");
            Console.WriteLine($"Pair Value - Expected: 2, Actual: {fullHouseResult.TieBreakers[1]}");
            
            // Test 5: Compare Hands
            int comparison = straightFlushResult.CompareTo(fourOfAKindResult);
            Console.WriteLine($"Hand Comparison - Expected: Positive (StraightFlush > FourOfAKind), Actual: {comparison}");
            
            // Test 6: Best Hand Evaluation
            var holeCards = new List<Card>
            {
                new Card(Suit.Hearts, Rank.Ace),
                new Card(Suit.Hearts, Rank.King)
            };
            
            var communityCards = new List<Card>
            {
                new Card(Suit.Hearts, Rank.Queen),
                new Card(Suit.Hearts, Rank.Jack),
                new Card(Suit.Hearts, Rank.Ten),
                new Card(Suit.Diamonds, Rank.Nine),
                new Card(Suit.Clubs, Rank.Eight)
            };
            
            var bestHand = HandEvaluator.EvaluateBestHand(holeCards, communityCards);
            Console.WriteLine($"Best Hand Test - Expected: {HandRank.RoyalFlush}, Actual: {bestHand.Rank}");
            Console.WriteLine($"Best Hand Cards Count - Expected: 5, Actual: {bestHand.Cards.Count}");
            
            // Print the cards in the best hand
            Console.WriteLine("Best Hand Cards:");
            foreach (var card in bestHand.Cards)
            {
                Console.WriteLine($"  {card.Rank} of {card.Suit}");
            }
            
            Console.WriteLine("All tests completed.");
        }
    }
}