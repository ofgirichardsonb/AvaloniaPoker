using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;
using MSA.Foundation.ServiceManagement;
using PokerGame.Abstractions.Models;
using PokerGame.Core.Microservices;
using Xunit;
using FluentAssertions;
using Moq;

namespace PokerGame.Tests.Core.Microservices
{
    public class CardDeckServiceTests
    {
        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service");
            var mockBroker = new Mock<IMessageBroker>();
            
            // Act
            var service = new CardDeckService(executionContext, mockBroker.Object);
            
            // Assert
            service.Should().NotBeNull();
        }
        
        [Fact]
        public void CreateDeck_ShouldReturnDeckWith52Cards()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service");
            var mockBroker = new Mock<IMessageBroker>();
            var service = new CardDeckService(executionContext, mockBroker.Object);
            
            // Act
            var deck = service.CreateDeck();
            
            // Assert
            deck.Should().NotBeNull();
            deck.Should().HaveCount(52, "A standard deck should have 52 cards");
            
            // Verify all suits and ranks are present
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                {
                    deck.Should().Contain(card => card.Suit == suit && card.Rank == rank,
                        $"Deck should contain {rank} of {suit}");
                }
            }
        }
        
        [Fact]
        public void Shuffle_ShouldReorderDeck()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service");
            var mockBroker = new Mock<IMessageBroker>();
            var service = new CardDeckService(executionContext, mockBroker.Object);
            var deck = service.CreateDeck();
            
            // Copy original deck for comparison
            var originalOrder = new List<Card>(deck);
            
            // Act
            var shuffledDeck = service.Shuffle(deck);
            
            // Assert
            shuffledDeck.Should().NotBeNull();
            shuffledDeck.Should().HaveCount(52, "Shuffled deck should still have 52 cards");
            
            // The shuffled deck should contain the same cards but in a different order
            shuffledDeck.Should().ContainInAnyOrder(originalOrder);
            
            // It's statistically almost impossible that a shuffled deck would be in the exact same order
            // However, to avoid potential test flakiness, we'll check if at least one card has changed position
            bool atLeastOneCardChangedPosition = false;
            for (int i = 0; i < deck.Count; i++)
            {
                if (!deck[i].Equals(originalOrder[i]))
                {
                    atLeastOneCardChangedPosition = true;
                    break;
                }
            }
            
            atLeastOneCardChangedPosition.Should().BeTrue("At least one card should change position after shuffling");
        }
        
        [Fact]
        public void DealCard_ShouldRemoveAndReturnTopCard()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service");
            var mockBroker = new Mock<IMessageBroker>();
            var service = new CardDeckService(executionContext, mockBroker.Object);
            var deck = service.CreateDeck();
            var expectedCard = deck[0]; // Top card
            var initialCount = deck.Count;
            
            // Act
            var dealtCard = service.DealCard(deck);
            
            // Assert
            dealtCard.Should().NotBeNull();
            dealtCard.Should().Be(expectedCard, "Dealt card should be the top card from the deck");
            deck.Should().HaveCount(initialCount - 1, "Deck should have one less card after dealing");
            deck.Should().NotContain(dealtCard, "Dealt card should no longer be in the deck");
        }
        
        [Fact]
        public void DealCard_WithEmptyDeck_ShouldReturnNull()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service");
            var mockBroker = new Mock<IMessageBroker>();
            var service = new CardDeckService(executionContext, mockBroker.Object);
            var emptyDeck = new List<Card>();
            
            // Act
            var dealtCard = service.DealCard(emptyDeck);
            
            // Assert
            dealtCard.Should().BeNull("Dealing from an empty deck should return null");
        }
        
        [Fact]
        public void DealCards_ShouldRemoveAndReturnSpecifiedNumberOfCards()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service");
            var mockBroker = new Mock<IMessageBroker>();
            var service = new CardDeckService(executionContext, mockBroker.Object);
            var deck = service.CreateDeck();
            int numberOfCards = 5;
            var expectedCards = new List<Card>();
            
            // Get the top 5 cards that we expect to be dealt
            for (int i = 0; i < numberOfCards; i++)
            {
                expectedCards.Add(deck[i]);
            }
            
            var initialCount = deck.Count;
            
            // Act
            var dealtCards = service.DealCards(deck, numberOfCards);
            
            // Assert
            dealtCards.Should().NotBeNull();
            dealtCards.Should().HaveCount(numberOfCards, $"Should deal exactly {numberOfCards} cards");
            dealtCards.Should().Equal(expectedCards, "Dealt cards should be the top cards from the deck in order");
            deck.Should().HaveCount(initialCount - numberOfCards, $"Deck should have {numberOfCards} fewer cards after dealing");
            
            foreach (var card in dealtCards)
            {
                deck.Should().NotContain(card, "Dealt cards should no longer be in the deck");
            }
        }
        
        [Fact]
        public void DealCards_RequestingMoreCardsThanAvailable_ShouldReturnAllRemainingCards()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service");
            var mockBroker = new Mock<IMessageBroker>();
            var service = new CardDeckService(executionContext, mockBroker.Object);
            var smallDeck = new List<Card>
            {
                new Card(Suit.Hearts, Rank.Ace),
                new Card(Suit.Spades, Rank.King),
                new Card(Suit.Diamonds, Rank.Queen)
            };
            int numberOfCards = 5; // More than available
            
            // Act
            var dealtCards = service.DealCards(smallDeck, numberOfCards);
            
            // Assert
            dealtCards.Should().NotBeNull();
            dealtCards.Should().HaveCount(3, "Should deal all remaining cards in deck");
            smallDeck.Should().BeEmpty("Deck should be empty after dealing all cards");
        }
    }
}