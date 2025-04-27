using System;
using System.Collections.Generic;
using PokerGame.Core.Models;
using Xunit;
using FluentAssertions;

namespace PokerGame.Tests.New.Core.Models
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
                new Card(Rank.Ace, Suit.Hearts),
                new Card(Rank.King, Suit.Spades)
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
            var card = new Card(Rank.Queen, Suit.Diamonds);
            
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
                new Card(Rank.Ten, Suit.Clubs),
                new Card(Rank.Jack, Suit.Hearts)
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
            var card = new Card(Rank.Ace, Suit.Spades);
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
            var hand = new Hand(new List<Card> { new Card(Rank.Two, Suit.Hearts) });
            var nonExistingCard = new Card(Rank.Three, Suit.Diamonds);
            
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
                new Card(Rank.Four, Suit.Clubs),
                new Card(Rank.Five, Suit.Diamonds)
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
            var card = new Card(Rank.King, Suit.Hearts);
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
            var hand = new Hand(new List<Card> { new Card(Rank.Queen, Suit.Spades) });
            var nonExistingCard = new Card(Rank.Queen, Suit.Hearts);
            
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
                new Card(Rank.Ace, Suit.Hearts),
                new Card(Rank.King, Suit.Diamonds)
            });
            
            // Act
            string result = hand.ToString();
            
            // Assert
            result.Should().Contain("A♥");
            result.Should().Contain("K♦");
        }
    }
}