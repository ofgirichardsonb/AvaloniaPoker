using System;
using System.Collections.Generic;
using System.Linq;
using PokerGame.Core.Game;
using PokerGame.Core.Models;
using Xunit;
using FluentAssertions;

namespace PokerGame.Tests.New.Core.Game
{
    public class HandEvaluatorTests
    {
        [Fact]
        public void EvaluateHand_WithRoyalFlush_ShouldReturnRoyalFlush()
        {
            // Arrange
            var cards = new List<Card>
            {
                new Card(Rank.Ten, Suit.Hearts),
                new Card(Rank.Jack, Suit.Hearts),
                new Card(Rank.Queen, Suit.Hearts),
                new Card(Rank.King, Suit.Hearts),
                new Card(Rank.Ace, Suit.Hearts)
            };
            
            // Act
            var result = HandEvaluator.EvaluateHand(cards);
            
            // Assert
            result.Rank.Should().Be(HandRank.RoyalFlush);
            // In a royal flush, the first tie breaker value should be 14 (Ace)
            result.TieBreakers[0].Should().Be(14);
        }
        
        [Fact]
        public void EvaluateHand_WithStraightFlush_ShouldReturnStraightFlush()
        {
            // Arrange
            var cards = new List<Card>
            {
                new Card(Rank.Six, Suit.Clubs),
                new Card(Rank.Seven, Suit.Clubs),
                new Card(Rank.Eight, Suit.Clubs),
                new Card(Rank.Nine, Suit.Clubs),
                new Card(Rank.Ten, Suit.Clubs)
            };
            
            // Act
            var result = HandEvaluator.EvaluateHand(cards);
            
            // Assert
            result.Rank.Should().Be(HandRank.StraightFlush);
            // In a straight flush, the first tie breaker value should be the highest card (10)
            result.TieBreakers[0].Should().Be(10);
        }
        
        [Fact]
        public void EvaluateHand_WithFourOfAKind_ShouldReturnFourOfAKind()
        {
            // Arrange
            var cards = new List<Card>
            {
                new Card(Rank.Eight, Suit.Hearts),
                new Card(Rank.Eight, Suit.Diamonds),
                new Card(Rank.Eight, Suit.Clubs),
                new Card(Rank.Eight, Suit.Spades),
                new Card(Rank.King, Suit.Hearts)
            };
            
            // Act
            var result = HandEvaluator.EvaluateHand(cards);
            
            // Assert
            result.Rank.Should().Be(HandRank.FourOfAKind);
            // In four of a kind, the first tie breaker value should be the rank of the four cards (8)
            result.TieBreakers[0].Should().Be(8);
        }
        
        [Fact]
        public void EvaluateHand_WithFullHouse_ShouldReturnFullHouse()
        {
            // Arrange
            var cards = new List<Card>
            {
                new Card(Rank.Nine, Suit.Hearts),
                new Card(Rank.Nine, Suit.Diamonds),
                new Card(Rank.Nine, Suit.Clubs),
                new Card(Rank.Two, Suit.Hearts),
                new Card(Rank.Two, Suit.Diamonds)
            };
            
            // Act
            var result = HandEvaluator.EvaluateHand(cards);
            
            // Assert
            result.Rank.Should().Be(HandRank.FullHouse);
            // In a full house, the first tie breaker is the three of a kind value (9)
            result.TieBreakers[0].Should().Be(9);
            // The second tie breaker is the pair value (2)
            result.TieBreakers[1].Should().Be(2);
        }
        
        [Fact]
        public void EvaluateHand_WithFlush_ShouldReturnFlush()
        {
            // Arrange
            var cards = new List<Card>
            {
                new Card(Rank.Two, Suit.Spades),
                new Card(Rank.Five, Suit.Spades),
                new Card(Rank.Seven, Suit.Spades),
                new Card(Rank.Ten, Suit.Spades),
                new Card(Rank.Queen, Suit.Spades)
            };
            
            // Act
            var result = HandEvaluator.EvaluateHand(cards);
            
            // Assert
            result.Rank.Should().Be(HandRank.Flush);
            // In a flush, the first tie breaker value should be the highest card (Queen = 12)
            result.TieBreakers[0].Should().Be(12);
        }
        
        [Fact]
        public void EvaluateHand_WithStraight_ShouldReturnStraight()
        {
            // Arrange
            var cards = new List<Card>
            {
                new Card(Rank.Four, Suit.Hearts),
                new Card(Rank.Five, Suit.Diamonds),
                new Card(Rank.Six, Suit.Clubs),
                new Card(Rank.Seven, Suit.Spades),
                new Card(Rank.Eight, Suit.Hearts)
            };
            
            // Act
            var result = HandEvaluator.EvaluateHand(cards);
            
            // Assert
            result.Rank.Should().Be(HandRank.Straight);
            // In a straight, the first tie breaker value should be the highest card (8)
            result.TieBreakers[0].Should().Be(8);
        }
        
        [Fact]
        public void EvaluateHand_WithThreeOfAKind_ShouldReturnThreeOfAKind()
        {
            // Arrange
            var cards = new List<Card>
            {
                new Card(Rank.Jack, Suit.Hearts),
                new Card(Rank.Jack, Suit.Diamonds),
                new Card(Rank.Jack, Suit.Clubs),
                new Card(Rank.Three, Suit.Spades),
                new Card(Rank.Nine, Suit.Hearts)
            };
            
            // Act
            var result = HandEvaluator.EvaluateHand(cards);
            
            // Assert
            result.Rank.Should().Be(HandRank.ThreeOfAKind);
            // In three of a kind, the first tie breaker is the rank of the three cards (Jack = 11)
            result.TieBreakers[0].Should().Be(11);
        }
        
        [Fact]
        public void EvaluateHand_WithTwoPair_ShouldReturnTwoPair()
        {
            // Arrange
            var cards = new List<Card>
            {
                new Card(Rank.Queen, Suit.Hearts),
                new Card(Rank.Queen, Suit.Diamonds),
                new Card(Rank.Four, Suit.Clubs),
                new Card(Rank.Four, Suit.Spades),
                new Card(Rank.Ace, Suit.Hearts)
            };
            
            // Act
            var result = HandEvaluator.EvaluateHand(cards);
            
            // Assert
            result.Rank.Should().Be(HandRank.TwoPair);
            // In two pair, the first tie breaker is the higher pair rank (Queen = 12)
            result.TieBreakers[0].Should().Be(12);
            // Second tie breaker is the lower pair rank (Four = 4)
            result.TieBreakers[1].Should().Be(4);
        }
        
        [Fact]
        public void EvaluateHand_WithOnePair_ShouldReturnOnePair()
        {
            // Arrange
            var cards = new List<Card>
            {
                new Card(Rank.Ten, Suit.Hearts),
                new Card(Rank.Ten, Suit.Diamonds),
                new Card(Rank.Five, Suit.Clubs),
                new Card(Rank.Seven, Suit.Spades),
                new Card(Rank.King, Suit.Hearts)
            };
            
            // Act
            var result = HandEvaluator.EvaluateHand(cards);
            
            // Assert
            result.Rank.Should().Be(HandRank.OnePair);
            // In one pair, the first tie breaker is the pair rank (Ten = 10)
            result.TieBreakers[0].Should().Be(10);
        }
        
        [Fact]
        public void EvaluateHand_WithHighCard_ShouldReturnHighCard()
        {
            // Arrange
            var cards = new List<Card>
            {
                new Card(Rank.Two, Suit.Hearts),
                new Card(Rank.Five, Suit.Diamonds),
                new Card(Rank.Seven, Suit.Clubs),
                new Card(Rank.Ten, Suit.Spades),
                new Card(Rank.Ace, Suit.Hearts)
            };
            
            // Act
            var result = HandEvaluator.EvaluateHand(cards);
            
            // Assert
            result.Rank.Should().Be(HandRank.HighCard);
            // In high card, the first tie breaker is the highest card value (Ace = 14)
            result.TieBreakers[0].Should().Be(14);
        }
        
        [Fact]
        public void EvaluateHand_WithLessThanFiveCards_ShouldThrowException()
        {
            // Arrange
            var cards = new List<Card>
            {
                new Card(Rank.Two, Suit.Hearts),
                new Card(Rank.Five, Suit.Diamonds),
                new Card(Rank.Seven, Suit.Clubs),
                new Card(Rank.Ten, Suit.Spades)
            };
            
            // Act & Assert
            Action action = () => HandEvaluator.EvaluateHand(cards);
            action.Should().Throw<ArgumentException>()
                .WithMessage("*five cards*", "Hand evaluation requires exactly five cards");
        }
        
        [Fact]
        public void CompareHands_WithDifferentHandRanks_ShouldReturnHigherRank()
        {
            // Arrange
            var straightFlushCards = new List<Card>
            {
                new Card(Rank.Six, Suit.Clubs),
                new Card(Rank.Seven, Suit.Clubs),
                new Card(Rank.Eight, Suit.Clubs),
                new Card(Rank.Nine, Suit.Clubs),
                new Card(Rank.Ten, Suit.Clubs)
            };
            
            var fourOfAKindCards = new List<Card>
            {
                new Card(Rank.Eight, Suit.Hearts),
                new Card(Rank.Eight, Suit.Diamonds),
                new Card(Rank.Eight, Suit.Clubs),
                new Card(Rank.Eight, Suit.Spades),
                new Card(Rank.King, Suit.Hearts)
            };
            
            // Act
            var straightFlushEval = HandEvaluator.EvaluateHand(straightFlushCards);
            var fourOfAKindEval = HandEvaluator.EvaluateHand(fourOfAKindCards);
            
            // Using CompareTo method directly from Hand class
            int comparison = straightFlushEval.CompareTo(fourOfAKindEval);
            
            // Assert
            comparison.Should().BeGreaterThan(0, "Straight flush should rank higher than four of a kind");
        }
        
        [Fact]
        public void CompareHands_WithSameHandRank_ShouldCompareHighCards()
        {
            // Arrange
            var pairOfKingsCards = new List<Card>
            {
                new Card(Rank.King, Suit.Hearts),
                new Card(Rank.King, Suit.Diamonds),
                new Card(Rank.Five, Suit.Clubs),
                new Card(Rank.Seven, Suit.Spades),
                new Card(Rank.Ten, Suit.Hearts)
            };
            
            var pairOfQueensCards = new List<Card>
            {
                new Card(Rank.Queen, Suit.Hearts),
                new Card(Rank.Queen, Suit.Diamonds),
                new Card(Rank.Five, Suit.Clubs),
                new Card(Rank.Seven, Suit.Spades),
                new Card(Rank.Ace, Suit.Hearts)
            };
            
            // Act
            var kingsEval = HandEvaluator.EvaluateHand(pairOfKingsCards);
            var queensEval = HandEvaluator.EvaluateHand(pairOfQueensCards);
            
            // Using CompareTo method directly from Hand class
            int comparison = kingsEval.CompareTo(queensEval);
            
            // Assert
            comparison.Should().BeGreaterThan(0, "Pair of kings should rank higher than pair of queens");
        }
        
        [Fact]
        public void FindBestHand_WithSevenCards_ShouldFindBestFiveCardHand()
        {
            // Arrange - Create a seven card collection (2 hole cards + 5 community cards)
            var allCards = new List<Card>
            {
                // Hole cards
                new Card(Rank.Ace, Suit.Hearts),
                new Card(Rank.King, Suit.Hearts),
                
                // Community cards
                new Card(Rank.Queen, Suit.Hearts),
                new Card(Rank.Jack, Suit.Hearts),
                new Card(Rank.Ten, Suit.Hearts),
                new Card(Rank.Nine, Suit.Diamonds),
                new Card(Rank.Eight, Suit.Clubs)
            };
            
            // Act
            var bestHand = HandEvaluator.EvaluateBestHand(
                allCards.Take(2).ToList(),  // First 2 cards are hole cards
                allCards.Skip(2).ToList()    // Remaining 5 cards are community cards
            );
            
            // Assert
            bestHand.Rank.Should().Be(HandRank.RoyalFlush, "Should find royal flush from the seven cards");
            bestHand.Cards.Should().HaveCount(5, "Best hand should contain exactly 5 cards");
            
            // Verify it contains cards from a royal flush
            bestHand.Cards.Should().Contain(c => c.Suit == Suit.Hearts && c.Rank == Rank.Ten);
            bestHand.Cards.Should().Contain(c => c.Suit == Suit.Hearts && c.Rank == Rank.Jack);
            bestHand.Cards.Should().Contain(c => c.Suit == Suit.Hearts && c.Rank == Rank.Queen);
            bestHand.Cards.Should().Contain(c => c.Suit == Suit.Hearts && c.Rank == Rank.King);
            bestHand.Cards.Should().Contain(c => c.Suit == Suit.Hearts && c.Rank == Rank.Ace);
        }
    }
}