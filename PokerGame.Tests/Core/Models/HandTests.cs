using System;
using System.Collections.Generic;
using PokerGame.Abstractions.Models;
using Xunit;
using FluentAssertions;

namespace PokerGame.Tests.Core.Models
{
    public class HandTests
    {
        [Fact]
        public void Constructor_WithNoCards_ShouldInitializeEmptyHand()
        {
            // Arrange & Act
            var hand = new Hand();
            
            // Assert
            hand.Cards.Should().NotBeNull();
            hand.Cards.Should().BeEmpty();
        }
        
        [Fact]
        public void Constructor_WithCardCollection_ShouldInitializeWithCards()
        {
            // Arrange
            var cards = new List<Card>
            {
                new Card(Suit.Hearts, Rank.Ace),
                new Card(Suit.Spades, Rank.King)
            };
            
            // Act
            var hand = new Hand(cards);
            
            // Assert
            hand.Cards.Should().HaveCount(2);
            hand.Cards.Should().Contain(cards);
        }
        
        [Fact]
        public void AddCard_ShouldAddCardToHand()
        {
            // Arrange
            var hand = new Hand();
            var card = new Card(Suit.Diamonds, Rank.Queen);
            
            // Act
            hand.AddCard(card);
            
            // Assert
            hand.Cards.Should().HaveCount(1);
            hand.Cards.Should().Contain(card);
        }
        
        [Fact]
        public void AddCards_ShouldAddMultipleCardsToHand()
        {
            // Arrange
            var hand = new Hand();
            var cards = new List<Card>
            {
                new Card(Suit.Clubs, Rank.Ten),
                new Card(Suit.Hearts, Rank.Jack)
            };
            
            // Act
            hand.AddCards(cards);
            
            // Assert
            hand.Cards.Should().HaveCount(2);
            hand.Cards.Should().Contain(cards);
        }
        
        [Fact]
        public void RemoveCard_WithExistingCard_ShouldRemoveCardFromHand()
        {
            // Arrange
            var card = new Card(Suit.Spades, Rank.Ace);
            var hand = new Hand(new List<Card> { card });
            
            // Act
            bool result = hand.RemoveCard(card);
            
            // Assert
            result.Should().BeTrue();
            hand.Cards.Should().BeEmpty();
        }
        
        [Fact]
        public void RemoveCard_WithNonExistingCard_ShouldReturnFalse()
        {
            // Arrange
            var hand = new Hand(new List<Card> { new Card(Suit.Hearts, Rank.Two) });
            var nonExistingCard = new Card(Suit.Diamonds, Rank.Three);
            
            // Act
            bool result = hand.RemoveCard(nonExistingCard);
            
            // Assert
            result.Should().BeFalse();
            hand.Cards.Should().HaveCount(1);
        }
        
        [Fact]
        public void Clear_ShouldRemoveAllCardsFromHand()
        {
            // Arrange
            var hand = new Hand(new List<Card>
            {
                new Card(Suit.Clubs, Rank.Four),
                new Card(Suit.Diamonds, Rank.Five)
            });
            
            // Act
            hand.Clear();
            
            // Assert
            hand.Cards.Should().BeEmpty();
        }
        
        [Fact]
        public void Contains_WithExistingCard_ShouldReturnTrue()
        {
            // Arrange
            var card = new Card(Suit.Hearts, Rank.King);
            var hand = new Hand(new List<Card> { card });
            
            // Act
            bool result = hand.Contains(card);
            
            // Assert
            result.Should().BeTrue();
        }
        
        [Fact]
        public void Contains_WithNonExistingCard_ShouldReturnFalse()
        {
            // Arrange
            var hand = new Hand(new List<Card> { new Card(Suit.Spades, Rank.Queen) });
            var nonExistingCard = new Card(Suit.Hearts, Rank.Queen);
            
            // Act
            bool result = hand.Contains(nonExistingCard);
            
            // Assert
            result.Should().BeFalse();
        }
        
        [Fact]
        public void ToString_ShouldReturnFormattedStringOfCards()
        {
            // Arrange
            var hand = new Hand(new List<Card>
            {
                new Card(Suit.Hearts, Rank.Ace),
                new Card(Suit.Diamonds, Rank.King)
            });
            
            // Act
            string result = hand.ToString();
            
            // Assert
            result.Should().Contain("A♥");
            result.Should().Contain("K♦");
        }
    }
}