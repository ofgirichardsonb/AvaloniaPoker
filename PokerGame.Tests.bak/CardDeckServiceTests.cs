using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using PokerGame.Core.Microservices;
using PokerGame.Core.Models;

namespace PokerGame.Tests
{
    public class CardDeckServiceTests
    {
        // Helper method to create a testable CardDeckService (bypassing the NetMQ ports)
        private CardDeckService CreateCardDeckServiceForTesting()
        {
            // We don't actually need working ports for direct tests
            // NetMQ will be mocked/bypassed
            return new CardDeckService(0, 0);
        }
        
        [Fact]
        public void CreateDeck_ShouldCreateNewDeck()
        {
            // Arrange
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var burnPilesField = typeof(CardDeckService).GetField("_burnPiles", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var service = CreateCardDeckServiceForTesting();
            var deckId = "test-deck-1";
            
            var createDeckMethod = typeof(CardDeckService).GetMethod("CreateDeck", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Act
            createDeckMethod.Invoke(service, new object[] { deckId, true });
            
            // Assert
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(service);
            var burnPiles = (Dictionary<string, List<Card>>)burnPilesField.GetValue(service);
            
            Assert.NotNull(decks);
            Assert.NotNull(burnPiles);
            Assert.True(decks.ContainsKey(deckId));
            Assert.True(burnPiles.ContainsKey(deckId));
            Assert.Equal(52, decks[deckId].CardsRemaining);
        }
        
        [Fact]
        public void ShuffleDeck_ShouldShuffleDeck()
        {
            // Arrange
            var service = CreateCardDeckServiceForTesting();
            var deckId = "test-deck-2";
            
            var createDeckMethod = typeof(CardDeckService).GetMethod("CreateDeck", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var shuffleDeckMethod = typeof(CardDeckService).GetMethod("ShuffleDeck", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Create an ordered deck (no shuffle)
            createDeckMethod.Invoke(service, new object[] { deckId, false });
            
            // Get original order
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(service);
            var originalOrder = new List<Card>(decks[deckId].DealCards(52));
            
            // Reset deck after checking original order
            createDeckMethod.Invoke(service, new object[] { deckId, false });
            
            // Act
            shuffleDeckMethod.Invoke(service, new object[] { deckId });
            
            // Assert
            decks = (Dictionary<string, Deck>)deckIdField.GetValue(service);
            var shuffledOrder = decks[deckId].DealCards(52);
            
            Assert.Equal(52, originalOrder.Count);
            Assert.Equal(52, shuffledOrder.Count);
            
            // Check if the order has changed (not a perfect test, but good enough)
            int samePositionCount = 0;
            for (int i = 0; i < 52; i++)
            {
                if (originalOrder[i].Equals(shuffledOrder[i]))
                {
                    samePositionCount++;
                }
            }
            
            // In a properly shuffled deck, some cards should be in different positions
            // It's statistically possible (but extremely unlikely) for a shuffle to result
            // in the exact same order, so we check if most cards moved
            Assert.True(samePositionCount < 52 * 0.5); // Less than 50% in the same position
        }
        
        [Fact]
        public void DealCards_ShouldReturnCorrectNumberOfCards()
        {
            // Arrange
            var service = CreateCardDeckServiceForTesting();
            var deckId = "test-deck-3";
            
            var createDeckMethod = typeof(CardDeckService).GetMethod("CreateDeck", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var dealCardsMethod = typeof(CardDeckService).GetMethod("DealCards", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Create a new deck
            createDeckMethod.Invoke(service, new object[] { deckId, true });
            
            // Act
            var cards = (List<Card>)dealCardsMethod.Invoke(service, new object[] { deckId, 5 });
            
            // Assert
            Assert.NotNull(cards);
            Assert.Equal(5, cards.Count);
            
            // Check remaining cards
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(service);
            Assert.Equal(47, decks[deckId].CardsRemaining);
        }
        
        [Fact]
        public void DealCards_ShouldReturnAllRemainingCardsWhenRequestTooMany()
        {
            // Arrange
            var service = CreateCardDeckServiceForTesting();
            var deckId = "test-deck-4";
            
            var createDeckMethod = typeof(CardDeckService).GetMethod("CreateDeck", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var dealCardsMethod = typeof(CardDeckService).GetMethod("DealCards", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Create a new deck and deal 50 cards
            createDeckMethod.Invoke(service, new object[] { deckId, true });
            dealCardsMethod.Invoke(service, new object[] { deckId, 50 });
            
            // Act - attempt to deal 10 more cards when only 2 remain
            var cards = (List<Card>)dealCardsMethod.Invoke(service, new object[] { deckId, 10 });
            
            // Assert
            Assert.NotNull(cards);
            Assert.Equal(2, cards.Count); // Should only get the 2 remaining cards
            
            // Check remaining cards
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(service);
            Assert.Equal(0, decks[deckId].CardsRemaining);
        }
        
        [Fact]
        public void BurnCard_ShouldMoveCardToBurnPile()
        {
            // Arrange
            var service = CreateCardDeckServiceForTesting();
            var deckId = "test-deck-5";
            
            var createDeckMethod = typeof(CardDeckService).GetMethod("CreateDeck", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var burnCardMethod = typeof(CardDeckService).GetMethod("BurnCard", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Create a new deck
            createDeckMethod.Invoke(service, new object[] { deckId, true });
            
            // Act
            var burnedCard = burnCardMethod.Invoke(service, new object[] { deckId, false });
            
            // Assert
            Assert.NotNull(burnedCard);
            
            // Check burn pile
            var burnPilesField = typeof(CardDeckService).GetField("_burnPiles", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var burnPiles = (Dictionary<string, List<Card>>)burnPilesField.GetValue(service);
            
            Assert.Equal(1, burnPiles[deckId].Count);
            
            // Check remaining cards
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(service);
            Assert.Equal(51, decks[deckId].CardsRemaining);
        }
        
        [Fact]
        public void ResetDeck_ShouldRestoreAllCardsAndClearBurnPile()
        {
            // Arrange
            var service = CreateCardDeckServiceForTesting();
            var deckId = "test-deck-6";
            
            var createDeckMethod = typeof(CardDeckService).GetMethod("CreateDeck", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var dealCardsMethod = typeof(CardDeckService).GetMethod("DealCards", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var burnCardMethod = typeof(CardDeckService).GetMethod("BurnCard", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var resetDeckMethod = typeof(CardDeckService).GetMethod("ResetDeck", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Create a new deck, deal some cards and burn some cards
            createDeckMethod.Invoke(service, new object[] { deckId, true });
            dealCardsMethod.Invoke(service, new object[] { deckId, 5 });
            burnCardMethod.Invoke(service, new object[] { deckId, false });
            burnCardMethod.Invoke(service, new object[] { deckId, false });
            
            // Act
            resetDeckMethod.Invoke(service, new object[] { deckId });
            
            // Assert
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var burnPilesField = typeof(CardDeckService).GetField("_burnPiles", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(service);
            var burnPiles = (Dictionary<string, List<Card>>)burnPilesField.GetValue(service);
            
            Assert.Equal(52, decks[deckId].CardsRemaining); // All cards are back
            Assert.Empty(burnPiles[deckId]); // Burn pile is empty
        }
    }
}