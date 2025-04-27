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
            var hand = new Hand(new List<Card>
            {
                new Card(Suit.Hearts, Rank.Ten),
                new Card(Suit.Hearts, Rank.Jack),
                new Card(Suit.Hearts, Rank.Queen),
                new Card(Suit.Hearts, Rank.King),
                new Card(Suit.Hearts, Rank.Ace)
            });
            
            // Act
            var result = HandEvaluator.EvaluateHand(hand);
            
            // Assert
            result.HandRank.Should().Be(HandRank.RoyalFlush);
            result.HighCard.Rank.Should().Be(Rank.Ace);
        }
        
        [Fact]
        public void EvaluateHand_WithStraightFlush_ShouldReturnStraightFlush()
        {
            // Arrange
            var hand = new Hand(new List<Card>
            {
                new Card(Suit.Clubs, Rank.Six),
                new Card(Suit.Clubs, Rank.Seven),
                new Card(Suit.Clubs, Rank.Eight),
                new Card(Suit.Clubs, Rank.Nine),
                new Card(Suit.Clubs, Rank.Ten)
            });
            
            // Act
            var result = HandEvaluator.EvaluateHand(hand);
            
            // Assert
            result.HandRank.Should().Be(HandRank.StraightFlush);
            result.HighCard.Rank.Should().Be(Rank.Ten);
        }
        
        [Fact]
        public void EvaluateHand_WithFourOfAKind_ShouldReturnFourOfAKind()
        {
            // Arrange
            var hand = new Hand(new List<Card>
            {
                new Card(Suit.Hearts, Rank.Eight),
                new Card(Suit.Diamonds, Rank.Eight),
                new Card(Suit.Clubs, Rank.Eight),
                new Card(Suit.Spades, Rank.Eight),
                new Card(Suit.Hearts, Rank.King)
            });
            
            // Act
            var result = HandEvaluator.EvaluateHand(hand);
            
            // Assert
            result.HandRank.Should().Be(HandRank.FourOfAKind);
            result.HighCard.Rank.Should().Be(Rank.Eight);
        }
        
        [Fact]
        public void EvaluateHand_WithFullHouse_ShouldReturnFullHouse()
        {
            // Arrange
            var hand = new Hand(new List<Card>
            {
                new Card(Suit.Hearts, Rank.Nine),
                new Card(Suit.Diamonds, Rank.Nine),
                new Card(Suit.Clubs, Rank.Nine),
                new Card(Suit.Hearts, Rank.Two),
                new Card(Suit.Diamonds, Rank.Two)
            });
            
            // Act
            var result = HandEvaluator.EvaluateHand(hand);
            
            // Assert
            result.HandRank.Should().Be(HandRank.FullHouse);
            result.HighCard.Rank.Should().Be(Rank.Nine);
        }
        
        [Fact]
        public void EvaluateHand_WithFlush_ShouldReturnFlush()
        {
            // Arrange
            var hand = new Hand(new List<Card>
            {
                new Card(Suit.Spades, Rank.Two),
                new Card(Suit.Spades, Rank.Five),
                new Card(Suit.Spades, Rank.Seven),
                new Card(Suit.Spades, Rank.Ten),
                new Card(Suit.Spades, Rank.Queen)
            });
            
            // Act
            var result = HandEvaluator.EvaluateHand(hand);
            
            // Assert
            result.HandRank.Should().Be(HandRank.Flush);
            result.HighCard.Rank.Should().Be(Rank.Queen);
        }
        
        [Fact]
        public void EvaluateHand_WithStraight_ShouldReturnStraight()
        {
            // Arrange
            var hand = new Hand(new List<Card>
            {
                new Card(Suit.Hearts, Rank.Four),
                new Card(Suit.Diamonds, Rank.Five),
                new Card(Suit.Clubs, Rank.Six),
                new Card(Suit.Spades, Rank.Seven),
                new Card(Suit.Hearts, Rank.Eight)
            });
            
            // Act
            var result = HandEvaluator.EvaluateHand(hand);
            
            // Assert
            result.HandRank.Should().Be(HandRank.Straight);
            result.HighCard.Rank.Should().Be(Rank.Eight);
        }
        
        [Fact]
        public void EvaluateHand_WithThreeOfAKind_ShouldReturnThreeOfAKind()
        {
            // Arrange
            var hand = new Hand(new List<Card>
            {
                new Card(Suit.Hearts, Rank.Jack),
                new Card(Suit.Diamonds, Rank.Jack),
                new Card(Suit.Clubs, Rank.Jack),
                new Card(Suit.Spades, Rank.Three),
                new Card(Suit.Hearts, Rank.Nine)
            });
            
            // Act
            var result = HandEvaluator.EvaluateHand(hand);
            
            // Assert
            result.HandRank.Should().Be(HandRank.ThreeOfAKind);
            result.HighCard.Rank.Should().Be(Rank.Jack);
        }
        
        [Fact]
        public void EvaluateHand_WithTwoPair_ShouldReturnTwoPair()
        {
            // Arrange
            var hand = new Hand(new List<Card>
            {
                new Card(Suit.Hearts, Rank.Queen),
                new Card(Suit.Diamonds, Rank.Queen),
                new Card(Suit.Clubs, Rank.Four),
                new Card(Suit.Spades, Rank.Four),
                new Card(Suit.Hearts, Rank.Ace)
            });
            
            // Act
            var result = HandEvaluator.EvaluateHand(hand);
            
            // Assert
            result.HandRank.Should().Be(HandRank.TwoPair);
            result.HighCard.Rank.Should().Be(Rank.Queen);
        }
        
        [Fact]
        public void EvaluateHand_WithOnePair_ShouldReturnOnePair()
        {
            // Arrange
            var hand = new Hand(new List<Card>
            {
                new Card(Suit.Hearts, Rank.Ten),
                new Card(Suit.Diamonds, Rank.Ten),
                new Card(Suit.Clubs, Rank.Five),
                new Card(Suit.Spades, Rank.Seven),
                new Card(Suit.Hearts, Rank.King)
            });
            
            // Act
            var result = HandEvaluator.EvaluateHand(hand);
            
            // Assert
            result.HandRank.Should().Be(HandRank.OnePair);
            result.HighCard.Rank.Should().Be(Rank.Ten);
        }
        
        [Fact]
        public void EvaluateHand_WithHighCard_ShouldReturnHighCard()
        {
            // Arrange
            var hand = new Hand(new List<Card>
            {
                new Card(Suit.Hearts, Rank.Two),
                new Card(Suit.Diamonds, Rank.Five),
                new Card(Suit.Clubs, Rank.Seven),
                new Card(Suit.Spades, Rank.Ten),
                new Card(Suit.Hearts, Rank.Ace)
            });
            
            // Act
            var result = HandEvaluator.EvaluateHand(hand);
            
            // Assert
            result.HandRank.Should().Be(HandRank.HighCard);
            result.HighCard.Rank.Should().Be(Rank.Ace);
        }
        
        [Fact]
        public void EvaluateHand_WithLessThanFiveCards_ShouldThrowException()
        {
            // Arrange
            var hand = new Hand(new List<Card>
            {
                new Card(Suit.Hearts, Rank.Two),
                new Card(Suit.Diamonds, Rank.Five),
                new Card(Suit.Clubs, Rank.Seven),
                new Card(Suit.Spades, Rank.Ten)
            });
            
            // Act & Assert
            Action action = () => HandEvaluator.EvaluateHand(hand);
            action.Should().Throw<ArgumentException>()
                .WithMessage("*five cards*", "Hand evaluation requires exactly five cards");
        }
        
        [Fact]
        public void CompareHands_WithDifferentHandRanks_ShouldReturnHigherRank()
        {
            // Arrange
            var straightFlush = new Hand(new List<Card>
            {
                new Card(Suit.Clubs, Rank.Six),
                new Card(Suit.Clubs, Rank.Seven),
                new Card(Suit.Clubs, Rank.Eight),
                new Card(Suit.Clubs, Rank.Nine),
                new Card(Suit.Clubs, Rank.Ten)
            });
            
            var fourOfAKind = new Hand(new List<Card>
            {
                new Card(Suit.Hearts, Rank.Eight),
                new Card(Suit.Diamonds, Rank.Eight),
                new Card(Suit.Clubs, Rank.Eight),
                new Card(Suit.Spades, Rank.Eight),
                new Card(Suit.Hearts, Rank.King)
            });
            
            // Act
            var straightFlushEval = HandEvaluator.EvaluateHand(straightFlush);
            var fourOfAKindEval = HandEvaluator.EvaluateHand(fourOfAKind);
            int comparison = HandEvaluator.CompareHands(straightFlushEval, fourOfAKindEval);
            
            // Assert
            comparison.Should().BeGreaterThan(0, "Straight flush should rank higher than four of a kind");
        }
        
        [Fact]
        public void CompareHands_WithSameHandRank_ShouldCompareHighCards()
        {
            // Arrange
            var pairOfKings = new Hand(new List<Card>
            {
                new Card(Suit.Hearts, Rank.King),
                new Card(Suit.Diamonds, Rank.King),
                new Card(Suit.Clubs, Rank.Five),
                new Card(Suit.Spades, Rank.Seven),
                new Card(Suit.Hearts, Rank.Ten)
            });
            
            var pairOfQueens = new Hand(new List<Card>
            {
                new Card(Suit.Hearts, Rank.Queen),
                new Card(Suit.Diamonds, Rank.Queen),
                new Card(Suit.Clubs, Rank.Five),
                new Card(Suit.Spades, Rank.Seven),
                new Card(Suit.Hearts, Rank.Ace)
            });
            
            // Act
            var kingsEval = HandEvaluator.EvaluateHand(pairOfKings);
            var queensEval = HandEvaluator.EvaluateHand(pairOfQueens);
            int comparison = HandEvaluator.CompareHands(kingsEval, queensEval);
            
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
            var bestHand = HandEvaluator.FindBestHand(allCards);
            
            // Assert
            bestHand.HandRank.Should().Be(HandRank.RoyalFlush, "Should find royal flush from the seven cards");
            bestHand.Cards.Should().HaveCount(5, "Best hand should contain exactly 5 cards");
            
            // Verify it has the right cards for a royal flush
            bestHand.Cards.Should().Contain(new Card(Suit.Hearts, Rank.Ten));
            bestHand.Cards.Should().Contain(new Card(Suit.Hearts, Rank.Jack));
            bestHand.Cards.Should().Contain(new Card(Suit.Hearts, Rank.Queen));
            bestHand.Cards.Should().Contain(new Card(Suit.Hearts, Rank.King));
            bestHand.Cards.Should().Contain(new Card(Suit.Hearts, Rank.Ace));
        }
    }
}