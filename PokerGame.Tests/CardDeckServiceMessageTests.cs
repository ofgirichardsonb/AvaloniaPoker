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
        private readonly Mock<CardDeckService> _mockService;
        private readonly MethodInfo _handleMessageAsyncMethod;
        
        public CardDeckServiceMessageTests()
        {
            // Create a mock of CardDeckService that calls the real methods
            _mockService = new Mock<CardDeckService>(0, 0) { CallBase = true };
            
            // Get the private HandleMessageAsync method for testing
            _handleMessageAsyncMethod = typeof(CardDeckService).GetMethod(
                "HandleMessageAsync", 
                BindingFlags.NonPublic | BindingFlags.Instance);
        }
        
        [Fact]
        public async Task HandleMessageAsync_DeckCreate_ShouldCreateDeck()
        {
            // Arrange
            var deckId = "test-message-deck-1";
            var payload = new DeckCreatePayload { DeckId = deckId, Shuffle = true };
            var message = Message.Create(MessageType.DeckCreate, payload);
            
            // Mock the BroadcastDeckStatus method to prevent NetMQ calls
            _mockService.Setup(m => m.Broadcast(It.IsAny<Message>()));
            
            // Act
            await (Task)_handleMessageAsyncMethod.Invoke(_mockService.Object, new object[] { message });
            
            // Assert
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(_mockService.Object);
            
            Assert.True(decks.ContainsKey(deckId));
            Assert.Equal(52, decks[deckId].CardsRemaining);
            
            // Verify that BroadcastDeckStatus was called
            _mockService.Verify(m => m.Broadcast(
                It.Is<Message>(msg => msg.Type == MessageType.DeckStatus)), 
                Times.Once);
        }
        
        [Fact]
        public async Task HandleMessageAsync_DeckShuffle_ShouldShuffleDeck()
        {
            // Arrange
            var deckId = "test-message-deck-2";
            
            // First create a deck
            var createPayload = new DeckCreatePayload { DeckId = deckId, Shuffle = false };
            var createMessage = Message.Create(MessageType.DeckCreate, createPayload);
            
            // Mock the BroadcastDeckStatus method to prevent NetMQ calls
            _mockService.Setup(m => m.Broadcast(It.IsAny<Message>()));
            
            await (Task)_handleMessageAsyncMethod.Invoke(_mockService.Object, new object[] { createMessage });
            
            // Then create a shuffle message
            var shufflePayload = new DeckIdPayload { DeckId = deckId };
            var shuffleMessage = Message.Create(MessageType.DeckShuffle, shufflePayload);
            
            // Act
            await (Task)_handleMessageAsyncMethod.Invoke(_mockService.Object, new object[] { shuffleMessage });
            
            // Assert - Since we can't easily verify the shuffle itself, we just verify that the method completed
            // and BroadcastDeckStatus was called
            _mockService.Verify(m => m.Broadcast(
                It.Is<Message>(msg => msg.Type == MessageType.DeckStatus)), 
                Times.Exactly(2)); // Once for create, once for shuffle
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
            
            // Mock broadcast and SendTo to prevent NetMQ calls
            _mockService.Setup(m => m.Broadcast(It.IsAny<Message>()));
            _mockService.Setup(m => m.SendTo(It.IsAny<Message>(), It.IsAny<string>()));
            
            await (Task)_handleMessageAsyncMethod.Invoke(_mockService.Object, new object[] { createMessage });
            
            // Then create a deal message
            var dealPayload = new DeckDealPayload { DeckId = deckId, Count = 5 };
            var dealMessage = Message.Create(MessageType.DeckDeal, dealPayload);
            dealMessage.SenderId = senderId;
            
            // Act
            await (Task)_handleMessageAsyncMethod.Invoke(_mockService.Object, new object[] { dealMessage });
            
            // Assert
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(_mockService.Object);
            
            Assert.Equal(47, decks[deckId].CardsRemaining); // 52 - 5 = 47 cards remaining
            
            // Verify that SendTo was called with a DeckDealResponse message
            _mockService.Verify(m => m.SendTo(
                It.Is<Message>(msg => msg.Type == MessageType.DeckDealResponse),
                It.Is<string>(s => s == senderId)), 
                Times.Once);
            
            // Verify that BroadcastDeckStatus was called
            _mockService.Verify(m => m.Broadcast(
                It.Is<Message>(msg => msg.Type == MessageType.DeckStatus)), 
                Times.Exactly(2)); // Once for create, once for deal
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
            
            // Mock broadcast and SendTo to prevent NetMQ calls
            _mockService.Setup(m => m.Broadcast(It.IsAny<Message>()));
            _mockService.Setup(m => m.SendTo(It.IsAny<Message>(), It.IsAny<string>()));
            
            await (Task)_handleMessageAsyncMethod.Invoke(_mockService.Object, new object[] { createMessage });
            
            // Then create a burn message
            var burnPayload = new DeckBurnPayload { DeckId = deckId, FaceUp = true };
            var burnMessage = Message.Create(MessageType.DeckBurn, burnPayload);
            burnMessage.SenderId = senderId;
            
            // Act
            await (Task)_handleMessageAsyncMethod.Invoke(_mockService.Object, new object[] { burnMessage });
            
            // Assert
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var burnPilesField = typeof(CardDeckService).GetField("_burnPiles", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(_mockService.Object);
            var burnPiles = (Dictionary<string, List<Card>>)burnPilesField.GetValue(_mockService.Object);
            
            Assert.Equal(51, decks[deckId].CardsRemaining); // 52 - 1 = 51 cards remaining
            Assert.Equal(1, burnPiles[deckId].Count); // 1 card in burn pile
            
            // Verify that SendTo was called with a DeckBurnResponse message
            _mockService.Verify(m => m.SendTo(
                It.Is<Message>(msg => msg.Type == MessageType.DeckBurnResponse),
                It.Is<string>(s => s == senderId)), 
                Times.Once);
            
            // Verify that BroadcastDeckStatus was called
            _mockService.Verify(m => m.Broadcast(
                It.Is<Message>(msg => msg.Type == MessageType.DeckStatus)), 
                Times.Exactly(2)); // Once for create, once for burn
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
            
            // Mock broadcast and SendTo to prevent NetMQ calls
            _mockService.Setup(m => m.Broadcast(It.IsAny<Message>()));
            _mockService.Setup(m => m.SendTo(It.IsAny<Message>(), It.IsAny<string>()));
            
            await (Task)_handleMessageAsyncMethod.Invoke(_mockService.Object, new object[] { createMessage });
            await (Task)_handleMessageAsyncMethod.Invoke(_mockService.Object, new object[] { dealMessage });
            
            // Then create a reset message
            var resetPayload = new DeckIdPayload { DeckId = deckId };
            var resetMessage = Message.Create(MessageType.DeckReset, resetPayload);
            
            // Act
            await (Task)_handleMessageAsyncMethod.Invoke(_mockService.Object, new object[] { resetMessage });
            
            // Assert
            var deckIdField = typeof(CardDeckService).GetField("_decks", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var decks = (Dictionary<string, Deck>)deckIdField.GetValue(_mockService.Object);
            
            Assert.Equal(52, decks[deckId].CardsRemaining); // All 52 cards back in the deck
            
            // Verify that BroadcastDeckStatus was called
            _mockService.Verify(m => m.Broadcast(
                It.Is<Message>(msg => msg.Type == MessageType.DeckStatus)), 
                Times.Exactly(3)); // Once for create, once for deal, once for reset
        }
        
        [Fact]
        public async Task HandleMessageAsync_DeckStatus_ShouldBroadcastStatus()
        {
            // Arrange
            var deckId = "test-message-deck-6";
            
            // First create a deck
            var createPayload = new DeckCreatePayload { DeckId = deckId, Shuffle = true };
            var createMessage = Message.Create(MessageType.DeckCreate, createPayload);
            
            // Mock broadcast to prevent NetMQ calls
            _mockService.Setup(m => m.Broadcast(It.IsAny<Message>()));
            
            await (Task)_handleMessageAsyncMethod.Invoke(_mockService.Object, new object[] { createMessage });
            
            // Clear invocations to reset the call count
            _mockService.Invocations.Clear();
            
            // Then create a status message
            var statusPayload = new DeckIdPayload { DeckId = deckId };
            var statusMessage = Message.Create(MessageType.DeckStatus, statusPayload);
            
            // Act
            await (Task)_handleMessageAsyncMethod.Invoke(_mockService.Object, new object[] { statusMessage });
            
            // Assert - Verify that BroadcastDeckStatus was called
            _mockService.Verify(m => m.Broadcast(
                It.Is<Message>(msg => msg.Type == MessageType.DeckStatus)), 
                Times.Once);
        }
        
        [Fact]
        public async Task HandleMessageAsync_InvalidDeckId_ShouldSendErrorResponse()
        {
            // Arrange
            var nonExistentDeckId = "non-existent-deck";
            var senderId = "test-sender";
            
            // Create a message with a non-existent deck ID
            var dealPayload = new DeckDealPayload { DeckId = nonExistentDeckId, Count = 5 };
            var dealMessage = Message.Create(MessageType.DeckDeal, dealPayload);
            dealMessage.SenderId = senderId;
            
            // Mock SendTo to prevent NetMQ calls
            _mockService.Setup(m => m.SendTo(It.IsAny<Message>(), It.IsAny<string>()));
            
            // Act & Assert
            await Assert.ThrowsAsync<TargetInvocationException>(async () => 
                await (Task)_handleMessageAsyncMethod.Invoke(_mockService.Object, new object[] { dealMessage }));
            
            // Verify that SendTo was called with an Error message
            _mockService.Verify(m => m.SendTo(
                It.Is<Message>(msg => msg.Type == MessageType.Error),
                It.Is<string>(s => s == senderId)), 
                Times.Once);
        }
    }
}