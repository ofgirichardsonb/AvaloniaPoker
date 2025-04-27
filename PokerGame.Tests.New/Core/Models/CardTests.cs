using System;
using PokerGame.Core.Models;
using Xunit;
using FluentAssertions;

namespace PokerGame.Tests.New.Core.Models
{
    public class CardTests
    {
        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            Rank rank = Rank.Ace;
            Suit suit = Suit.Hearts;
            
            // Act
            var card = new Card(rank, suit);
            
            // Assert
            card.Suit.Should().Be(suit);
            card.Rank.Should().Be(rank);
        }
        
        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var card = new Card(Rank.King, Suit.Spades);
            
            // Act
            string result = card.ToString();
            
            // Assert
            result.Should().Be("K♠");
        }
        
        [Fact]
        public void Equals_WithSameCardValues_ShouldReturnTrue()
        {
            // Arrange
            var card1 = new Card(Rank.Ten, Suit.Diamonds);
            var card2 = new Card(Rank.Ten, Suit.Diamonds);
            
            // Act
            bool areEqual = card1.Equals(card2);
            
            // Assert
            areEqual.Should().BeTrue();
        }
        
        [Fact]
        public void Equals_WithDifferentCardValues_ShouldReturnFalse()
        {
            // Arrange
            var card1 = new Card(Rank.Ten, Suit.Diamonds);
            var card2 = new Card(Rank.Ten, Suit.Hearts);
            
            // Act
            bool areEqual = card1.Equals(card2);
            
            // Assert
            areEqual.Should().BeFalse();
        }
        
        [Fact]
        public void Equals_WithNull_ShouldReturnFalse()
        {
            // Arrange
            var card = new Card(Rank.Two, Suit.Clubs);
            
            // Act
            bool areEqual = card.Equals(null);
            
            // Assert
            areEqual.Should().BeFalse();
        }
        
        [Fact]
        public void GetHashCode_WithSameCardValues_ShouldReturnSameHashCode()
        {
            // Arrange
            var card1 = new Card(Rank.Queen, Suit.Hearts);
            var card2 = new Card(Rank.Queen, Suit.Hearts);
            
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
            var card1 = new Card(Rank.Queen, Suit.Hearts);
            var card2 = new Card(Rank.Queen, Suit.Spades);
            
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
            var card1 = new Card(Rank.Five, Suit.Clubs);
            var card2 = new Card(Rank.Five, Suit.Clubs);
            
            // Act
            bool areEqual = card1 == card2;
            
            // Assert
            areEqual.Should().BeTrue();
        }
        
        [Fact]
        public void InequalityOperator_WithDifferentCardValues_ShouldReturnTrue()
        {
            // Arrange
            var card1 = new Card(Rank.Five, Suit.Clubs);
            var card2 = new Card(Rank.Five, Suit.Diamonds);
            
            // Act
            bool areNotEqual = card1 != card2;
            
            // Assert
            areNotEqual.Should().BeTrue();
        }
    }
}