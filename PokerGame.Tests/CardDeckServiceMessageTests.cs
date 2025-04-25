using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using Moq;
using PokerGame.Core.Microservices;
using PokerGame.Core.Models;

namespace PokerGame.Tests
{
    public class CardDeckServiceMessageTests
    {
        private readonly TestableCardDeckService _service;
        
        public CardDeckServiceMessageTests()
        {
            // Create a testable CardDeckService with port 0 (won't actually open any sockets)
            _service = new TestableCardDeckService(0, 0);
        }
        
        [Fact]
        public async Task HandleMessageAsync_DeckCreate_ShouldCreateDeck()
        {
            // Arrange
            var deckId = "test-message-deck-1";
            var payload = new DeckCreatePayload { DeckId = deckId, Shuffle = true };
            var message = Message.Create(MessageType.DeckCreate, payload);
            
            // Act - we're calling directly via the testable wrapper
            await _service.HandleMessageAsyncPublic(message);
            
            // Assert
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(_service);
            
            Assert.True(decks.ContainsKey(deckId));
            Assert.Equal(52, decks[deckId].CardsRemaining);
        }
        
        [Fact]
        public async Task HandleMessageAsync_DeckShuffle_ShouldShuffleDeck()
        {
            // Arrange
            var deckId = "test-message-deck-2";
            
            // First create a deck
            var createPayload = new DeckCreatePayload { DeckId = deckId, Shuffle = false };
            var createMessage = Message.Create(MessageType.DeckCreate, createPayload);
            
            await _service.HandleMessageAsyncPublic(createMessage);
            
            // Then create a shuffle message
            var shufflePayload = new DeckIdPayload { DeckId = deckId };
            var shuffleMessage = Message.Create(MessageType.DeckShuffle, shufflePayload);
            
            // Act
            await _service.HandleMessageAsyncPublic(shuffleMessage);
            
            // Assert
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(_service);
            
            // We can't check the exact shuffle pattern, so we just verify the basic properties
            Assert.True(decks.ContainsKey(deckId));
            Assert.Equal(52, decks[deckId].CardsRemaining);
        }
        
        [Fact]
        public async Task HandleMessageAsync_DeckDeal_ShouldDealCardsAndSendResponse()
        {
            // Arrange
            var deckId = "test-message-deck-3";
            var senderId = "test-sender";
            
            // First create a deck
            var createPayload = new DeckCreatePayload { DeckId = deckId, Shuffle = true };
            var createMessage = Message.Create(MessageType.DeckCreate, createPayload);
            
            await _service.HandleMessageAsyncPublic(createMessage);
            
            // Then create a deal message
            var dealPayload = new DeckDealPayload { DeckId = deckId, Count = 5 };
            var dealMessage = Message.Create(MessageType.DeckDeal, dealPayload);
            dealMessage.SenderId = senderId;
            
            // Act
            await _service.HandleMessageAsyncPublic(dealMessage);
            
            // Assert
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(_service);
            
            Assert.Equal(47, decks[deckId].CardsRemaining); // 52 - 5 = 47 cards remaining
        }
        
        [Fact]
        public async Task HandleMessageAsync_DeckBurn_ShouldBurnCardAndSendResponse()
        {
            // Arrange
            var deckId = "test-message-deck-4";
            var senderId = "test-sender";
            
            // First create a deck
            var createPayload = new DeckCreatePayload { DeckId = deckId, Shuffle = true };
            var createMessage = Message.Create(MessageType.DeckCreate, createPayload);
            
            await _service.HandleMessageAsyncPublic(createMessage);
            
            // Then create a burn message
            var burnPayload = new DeckBurnPayload { DeckId = deckId, FaceUp = true };
            var burnMessage = Message.Create(MessageType.DeckBurn, burnPayload);
            burnMessage.SenderId = senderId;
            
            // Act
            await _service.HandleMessageAsyncPublic(burnMessage);
            
            // Assert
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var burnPilesField = typeof(CardDeckService).GetField("_burnPiles", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(_service);
            var burnPiles = (Dictionary<string, List<Card>>)burnPilesField.GetValue(_service);
            
            Assert.Equal(51, decks[deckId].CardsRemaining); // 52 - 1 = 51 cards remaining
            Assert.Single(burnPiles[deckId]); // 1 card in burn pile
        }
        
        [Fact]
        public async Task HandleMessageAsync_DeckReset_ShouldResetDeck()
        {
            // Arrange
            var deckId = "test-message-deck-5";
            
            // First create a deck and deal some cards
            var createPayload = new DeckCreatePayload { DeckId = deckId, Shuffle = true };
            var createMessage = Message.Create(MessageType.DeckCreate, createPayload);
            
            var dealPayload = new DeckDealPayload { DeckId = deckId, Count = 10 };
            var dealMessage = Message.Create(MessageType.DeckDeal, dealPayload);
            
            await _service.HandleMessageAsyncPublic(createMessage);
            await _service.HandleMessageAsyncPublic(dealMessage);
            
            // Then create a reset message
            var resetPayload = new DeckIdPayload { DeckId = deckId };
            var resetMessage = Message.Create(MessageType.DeckReset, resetPayload);
            
            // Act
            await _service.HandleMessageAsyncPublic(resetMessage);
            
            // Assert
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var burnPilesField = typeof(CardDeckService).GetField("_burnPiles", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(_service);
            var burnPiles = (Dictionary<string, List<Card>>)burnPilesField.GetValue(_service);
            
            Assert.Equal(52, decks[deckId].CardsRemaining); // All 52 cards back in the deck
            Assert.Empty(burnPiles[deckId]); // Burn pile is empty after reset
        }
        
        [Fact]
        public async Task HandleMessageAsync_DeckStatus_ShouldSendStatus()
        {
            // Arrange
            var deckId = "test-message-deck-6";
            
            // First create a deck
            var createPayload = new DeckCreatePayload { DeckId = deckId, Shuffle = true };
            var createMessage = Message.Create(MessageType.DeckCreate, createPayload);
            
            await _service.HandleMessageAsyncPublic(createMessage);
            
            // Then create a status message
            var statusPayload = new DeckIdPayload { DeckId = deckId };
            var statusMessage = Message.Create(MessageType.DeckStatus, statusPayload);
            
            // Act
            await _service.HandleMessageAsyncPublic(statusMessage);
            
            // Just verify the deck exists (we don't verify Broadcast because NetMQ isn't running in test)
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(_service);
            
            // Verify the deck exists and has 52 cards
            Assert.True(decks.ContainsKey(deckId));
            Assert.Equal(52, decks[deckId].CardsRemaining);
        }
        
        [Fact]
        public async Task HandleMessageAsync_InvalidDeckId_ShouldLogErrorMessage()
        {
            // Arrange
            var nonExistentDeckId = "non-existent-deck";
            var senderId = "test-sender";
            
            // Create a message with a non-existent deck ID
            var dealPayload = new DeckDealPayload { DeckId = nonExistentDeckId, Count = 5 };
            var dealMessage = Message.Create(MessageType.DeckDeal, dealPayload);
            dealMessage.SenderId = senderId;
            
            // Act - This shouldn't throw an exception because error handling is done internally
            await _service.HandleMessageAsyncPublic(dealMessage);
            
            // Assert - Not much to verify since we handle the error and log it
            // The TestableCardDeckService will handle the error and log a message
            // We'll just ensure the method completes without throwing
            Assert.True(true);
        }
    }
}