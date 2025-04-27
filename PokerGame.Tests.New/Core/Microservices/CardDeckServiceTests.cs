using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;
using MSAEC = MSA.Foundation.ServiceManagement.ExecutionContext;
using MSA.Foundation.ServiceManagement;
using PokerGame.Core.Models;
using PokerGame.Core.Microservices;
using Xunit;
using FluentAssertions;
using Moq;

namespace PokerGame.Tests.New.Core.Microservices
{
    public class CardDeckServiceTests
    {
        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var executionContext = new MSAEC("test-service");
            
            // Act
            var service = new CardDeckService(executionContext);
            
            // Assert
            service.Should().NotBeNull();
        }
        
        [Fact]
        public void Deck_InitializeAndShuffle_ShouldCreateDeckWith52Cards()
        {
            // Arrange
            var deck = new Deck();
            
            // Act
            deck.Initialize();
            deck.Shuffle();
            var cards = deck.GetAllCards();
            
            // Assert
            cards.Should().NotBeNull();
            cards.Should().HaveCount(52, "A standard deck should have 52 cards");
            
            // Verify all suits and ranks are present
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                {
                    cards.Should().Contain(card => card.Suit == suit && card.Rank == rank,
                        $"Deck should contain {rank} of {suit}");
                }
            }
        }
        
        [Fact]
        public void Deck_Shuffle_ShouldReorderDeck()
        {
            // Arrange
            var deck = new Deck();
            deck.Initialize();
            
            // Copy original deck for comparison
            var originalOrder = deck.GetAllCards();
            
            // Act
            deck.Shuffle();
            var shuffledCards = deck.GetAllCards();
            
            // Assert
            shuffledCards.Should().NotBeNull();
            shuffledCards.Should().HaveCount(52, "Shuffled deck should still have 52 cards");
            
            // The shuffled deck should contain the same cards but in a different order
            shuffledCards.Should().ContainInAnyOrder(originalOrder);
            
            // It's statistically almost impossible that a shuffled deck would be in the exact same order
            // However, to avoid potential test flakiness, we'll check if at least one card has changed position
            bool atLeastOneCardChangedPosition = false;
            for (int i = 0; i < shuffledCards.Count; i++)
            {
                if (!shuffledCards[i].Equals(originalOrder[i]))
                {
                    atLeastOneCardChangedPosition = true;
                    break;
                }
            }
            
            atLeastOneCardChangedPosition.Should().BeTrue("At least one card should change position after shuffling");
        }
        
        [Fact]
        public void Deck_DealCard_ShouldRemoveAndReturnTopCard()
        {
            // Arrange
            var deck = new Deck();
            deck.Initialize();
            var cards = deck.GetAllCards();
            var expectedCard = cards[0]; // Top card
            var initialCount = cards.Count;
            
            // Act
            var dealtCard = deck.DealCard();
            
            // Assert
            dealtCard.Should().NotBeNull();
            dealtCard.Should().Be(expectedCard, "Dealt card should be the top card from the deck");
            deck.RemainingCards.Should().Be(initialCount - 1, "Deck should have one less card after dealing");
            deck.GetAllCards().Should().NotContain(dealtCard, "Dealt card should no longer be in the deck");
        }
        
        [Fact]
        public void Deck_DealCards_ShouldRemoveAndReturnSpecifiedNumberOfCards()
        {
            // Arrange
            var deck = new Deck();
            deck.Initialize();
            int numberOfCards = 5;
            var cards = deck.GetAllCards();
            var expectedCards = new List<Card>();
            
            // Get the top 5 cards that we expect to be dealt
            for (int i = 0; i < numberOfCards; i++)
            {
                expectedCards.Add(cards[i]);
            }
            
            var initialCount = cards.Count;
            
            // Act
            var dealtCards = deck.DealCards(numberOfCards);
            
            // Assert
            dealtCards.Should().NotBeNull();
            dealtCards.Should().HaveCount(numberOfCards, $"Should deal exactly {numberOfCards} cards");
            dealtCards.Should().Equal(expectedCards, "Dealt cards should be the top cards from the deck in order");
            deck.RemainingCards.Should().Be(initialCount - numberOfCards, $"Deck should have {numberOfCards} fewer cards after dealing");
            
            foreach (var card in dealtCards)
            {
                deck.GetAllCards().Should().NotContain(card, "Dealt cards should no longer be in the deck");
            }
        }
        
        [Fact]
        public void Deck_Reset_ShouldReinitializeAndShuffleDeck()
        {
            // Arrange
            var deck = new Deck();
            deck.Initialize();
            deck.DealCards(10); // Remove some cards
            
            // Act
            deck.Reset();
            
            // Assert
            deck.RemainingCards.Should().Be(52, "Reset deck should have 52 cards");
            
            // Verify all suits and ranks are present
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                {
                    deck.GetAllCards().Should().Contain(card => card.Suit == suit && card.Rank == rank,
                        $"Reset deck should contain {rank} of {suit}");
                }
            }
        }
    }
}