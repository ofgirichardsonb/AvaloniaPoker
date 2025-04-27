using System;
using PokerGame.Abstractions.Models;
using Xunit;
using FluentAssertions;

namespace PokerGame.Tests.Core.Models
{
    public class CardTests
    {
        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            Suit suit = Suit.Hearts;
            Rank rank = Rank.Ace;
            
            // Act
            var card = new Card(suit, rank);
            
            // Assert
            card.Suit.Should().Be(suit);
            card.Rank.Should().Be(rank);
        }
        
        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var card = new Card(Suit.Spades, Rank.King);
            
            // Act
            string result = card.ToString();
            
            // Assert
            result.Should().Be("K♠");
        }
        
        [Fact]
        public void Equals_WithSameCardValues_ShouldReturnTrue()
        {
            // Arrange
            var card1 = new Card(Suit.Diamonds, Rank.Ten);
            var card2 = new Card(Suit.Diamonds, Rank.Ten);
            
            // Act
            bool areEqual = card1.Equals(card2);
            
            // Assert
            areEqual.Should().BeTrue();
        }
        
        [Fact]
        public void Equals_WithDifferentCardValues_ShouldReturnFalse()
        {
            // Arrange
            var card1 = new Card(Suit.Diamonds, Rank.Ten);
            var card2 = new Card(Suit.Hearts, Rank.Ten);
            
            // Act
            bool areEqual = card1.Equals(card2);
            
            // Assert
            areEqual.Should().BeFalse();
        }
        
        [Fact]
        public void Equals_WithNull_ShouldReturnFalse()
        {
            // Arrange
            var card = new Card(Suit.Clubs, Rank.Two);
            
            // Act
            bool areEqual = card.Equals(null);
            
            // Assert
            areEqual.Should().BeFalse();
        }
        
        [Fact]
        public void GetHashCode_WithSameCardValues_ShouldReturnSameHashCode()
        {
            // Arrange
            var card1 = new Card(Suit.Hearts, Rank.Queen);
            var card2 = new Card(Suit.Hearts, Rank.Queen);
            
            // Act
            int hashCode1 = card1.GetHashCode();
            int hashCode2 = card2.GetHashCode();
            
            // Assert
            hashCode1.Should().Be(hashCode2);
        }
        
        [Fact]
        public void GetHashCode_WithDifferentCardValues_ShouldReturnDifferentHashCodes()
        {
            // Arrange
            var card1 = new Card(Suit.Hearts, Rank.Queen);
            var card2 = new Card(Suit.Spades, Rank.Queen);
            
            // Act
            int hashCode1 = card1.GetHashCode();
            int hashCode2 = card2.GetHashCode();
            
            // Assert
            hashCode1.Should().NotBe(hashCode2);
        }
        
        [Fact]
        public void EqualityOperator_WithSameCardValues_ShouldReturnTrue()
        {
            // Arrange
            var card1 = new Card(Suit.Clubs, Rank.Five);
            var card2 = new Card(Suit.Clubs, Rank.Five);
            
            // Act
            bool areEqual = card1 == card2;
            
            // Assert
            areEqual.Should().BeTrue();
        }
        
        [Fact]
        public void InequalityOperator_WithDifferentCardValues_ShouldReturnTrue()
        {
            // Arrange
            var card1 = new Card(Suit.Clubs, Rank.Five);
            var card2 = new Card(Suit.Diamonds, Rank.Five);
            
            // Act
            bool areNotEqual = card1 != card2;
            
            // Assert
            areNotEqual.Should().BeTrue();
        }
    }
}