using System;
using System.Collections.Generic;
using PokerGame.Abstractions.Models;
using PokerGame.Core.Game;
using Xunit;
using FluentAssertions;

namespace PokerGame.Tests.Core.Game
{
    public class HandEvaluatorTests
    {
        [Fact]
        public void EvaluateHand_WithRoyalFlush_ShouldReturnRoyalFlush()
        {
            // Arrange
            var cards = new List<Card>
            {
                new Card(Suit.Hearts, Rank.Ten),
                new Card(Suit.Hearts, Rank.Jack),
                new Card(Suit.Hearts, Rank.Queen),
                new Card(Suit.Hearts, Rank.King),
                new Card(Suit.Hearts, Rank.Ace)
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
                new Card(Suit.Clubs, Rank.Six),
                new Card(Suit.Clubs, Rank.Seven),
                new Card(Suit.Clubs, Rank.Eight),
                new Card(Suit.Clubs, Rank.Nine),
                new Card(Suit.Clubs, Rank.Ten)
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
                new Card(Suit.Hearts, Rank.Eight),
                new Card(Suit.Diamonds, Rank.Eight),
                new Card(Suit.Clubs, Rank.Eight),
                new Card(Suit.Spades, Rank.Eight),
                new Card(Suit.Hearts, Rank.King)
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
                new Card(Suit.Hearts, Rank.Nine),
                new Card(Suit.Diamonds, Rank.Nine),
                new Card(Suit.Clubs, Rank.Nine),
                new Card(Suit.Hearts, Rank.Two),
                new Card(Suit.Diamonds, Rank.Two)
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
                new Card(Suit.Spades, Rank.Two),
                new Card(Suit.Spades, Rank.Five),
                new Card(Suit.Spades, Rank.Seven),
                new Card(Suit.Spades, Rank.Ten),
                new Card(Suit.Spades, Rank.Queen)
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
                new Card(Suit.Hearts, Rank.Four),
                new Card(Suit.Diamonds, Rank.Five),
                new Card(Suit.Clubs, Rank.Six),
                new Card(Suit.Spades, Rank.Seven),
                new Card(Suit.Hearts, Rank.Eight)
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
                new Card(Suit.Hearts, Rank.Jack),
                new Card(Suit.Diamonds, Rank.Jack),
                new Card(Suit.Clubs, Rank.Jack),
                new Card(Suit.Spades, Rank.Three),
                new Card(Suit.Hearts, Rank.Nine)
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
                new Card(Suit.Hearts, Rank.Queen),
                new Card(Suit.Diamonds, Rank.Queen),
                new Card(Suit.Clubs, Rank.Four),
                new Card(Suit.Spades, Rank.Four),
                new Card(Suit.Hearts, Rank.Ace)
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
                new Card(Suit.Hearts, Rank.Ten),
                new Card(Suit.Diamonds, Rank.Ten),
                new Card(Suit.Clubs, Rank.Five),
                new Card(Suit.Spades, Rank.Seven),
                new Card(Suit.Hearts, Rank.King)
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
                new Card(Suit.Hearts, Rank.Two),
                new Card(Suit.Diamonds, Rank.Five),
                new Card(Suit.Clubs, Rank.Seven),
                new Card(Suit.Spades, Rank.Ten),
                new Card(Suit.Hearts, Rank.Ace)
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
                new Card(Suit.Hearts, Rank.Two),
                new Card(Suit.Diamonds, Rank.Five),
                new Card(Suit.Clubs, Rank.Seven),
                new Card(Suit.Spades, Rank.Ten)
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
                new Card(Suit.Clubs, Rank.Six),
                new Card(Suit.Clubs, Rank.Seven),
                new Card(Suit.Clubs, Rank.Eight),
                new Card(Suit.Clubs, Rank.Nine),
                new Card(Suit.Clubs, Rank.Ten)
            };
            
            var fourOfAKindCards = new List<Card>
            {
                new Card(Suit.Hearts, Rank.Eight),
                new Card(Suit.Diamonds, Rank.Eight),
                new Card(Suit.Clubs, Rank.Eight),
                new Card(Suit.Spades, Rank.Eight),
                new Card(Suit.Hearts, Rank.King)
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
                new Card(Suit.Hearts, Rank.King),
                new Card(Suit.Diamonds, Rank.King),
                new Card(Suit.Clubs, Rank.Five),
                new Card(Suit.Spades, Rank.Seven),
                new Card(Suit.Hearts, Rank.Ten)
            };
            
            var pairOfQueensCards = new List<Card>
            {
                new Card(Suit.Hearts, Rank.Queen),
                new Card(Suit.Diamonds, Rank.Queen),
                new Card(Suit.Clubs, Rank.Five),
                new Card(Suit.Spades, Rank.Seven),
                new Card(Suit.Hearts, Rank.Ace)
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
                new Card(Suit.Hearts, Rank.Ace),
                new Card(Suit.Hearts, Rank.King),
                
                // Community cards
                new Card(Suit.Hearts, Rank.Queen),
                new Card(Suit.Hearts, Rank.Jack),
                new Card(Suit.Hearts, Rank.Ten),
                new Card(Suit.Diamonds, Rank.Nine),
                new Card(Suit.Clubs, Rank.Eight)
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