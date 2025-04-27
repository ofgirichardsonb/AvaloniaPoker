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
    public class GameEngineServiceTests
    {
        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service");
            
            // Act
            var service = new GameEngineService(executionContext);
            
            // Assert
            service.Should().NotBeNull();
        }
        
        [Fact]
        public void AddPlayer_ShouldAddPlayerToGame()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service");
            var service = new GameEngineService(executionContext);
            string playerId = "player1";
            string playerName = "Test Player";
            
            // Act
            service.AddPlayer(playerId, playerName);
            
            // Assert
            // We need to use reflection to access private fields for testing
            var playersField = typeof(GameEngineService)
                .GetField("_players", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var players = playersField?.GetValue(service) as Dictionary<string, Player>;
            
            players.Should().NotBeNull();
            players.Should().ContainKey(playerId);
            players[playerId].Name.Should().Be(playerName);
        }
        
        [Fact]
        public void RemovePlayer_WithExistingPlayer_ShouldRemovePlayerFromGame()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service");
            var service = new GameEngineService(executionContext);
            string playerId = "player1";
            string playerName = "Test Player";
            
            // Add player first
            service.AddPlayer(playerId, playerName);
            
            // Act
            service.RemovePlayer(playerId);
            
            // Assert - Use reflection to check internal state
            var playersField = typeof(GameEngineService)
                .GetField("_players", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var players = playersField?.GetValue(service) as Dictionary<string, Player>;
            
            players.Should().NotContainKey(playerId);
        }
        
        [Fact]
        public void GetPlayerCount_ShouldReturnCorrectCount()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service");
            var service = new GameEngineService(executionContext);
            
            // Add some players
            service.AddPlayer("player1", "Player One");
            service.AddPlayer("player2", "Player Two");
            service.AddPlayer("player3", "Player Three");
            
            // Act
            int playerCount = service.GetPlayerCount();
            
            // Assert
            playerCount.Should().Be(3);
        }
        
        [Fact]
        public void GetPlayerById_WithExistingPlayer_ShouldReturnPlayer()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service");
            var service = new GameEngineService(executionContext);
            string playerId = "player1";
            string playerName = "Test Player";
            
            // Add player first
            service.AddPlayer(playerId, playerName);
            
            // Act
            var player = service.GetPlayerById(playerId);
            
            // Assert
            player.Should().NotBeNull();
            player.Id.Should().Be(playerId);
            player.Name.Should().Be(playerName);
        }
        
        [Fact]
        public void GetPlayerById_WithNonExistingPlayer_ShouldReturnNull()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service");
            var service = new GameEngineService(executionContext);
            
            // Act
            var player = service.GetPlayerById("non-existing-player");
            
            // Assert
            player.Should().BeNull();
        }
        
        [Fact]
        public void DealHoleCards_ShouldDealTwoCardsToAllPlayers()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service");
            var mockBroker = new Mock<IMessageBroker>();
            var service = new GameEngineService(executionContext, mockBroker.Object);
            
            // Add some players
            service.AddPlayer("player1", "Player One");
            service.AddPlayer("player2", "Player Two");
            
            // Create a deck field through reflection (for testing purposes)
            var deckField = typeof(GameEngineService)
                .GetField("_currentDeck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Initialize a deck with cards
            var deck = new List<Card>();
            for (int i = 0; i < 10; i++) // Add some test cards
            {
                deck.Add(new Card((Suit)(i % 4), (Rank)(i % 13 + 1)));
            }
            deckField?.SetValue(service, deck);
            
            // Act
            service.DealHoleCards();
            
            // Assert - Check that players have cards
            var playersField = typeof(GameEngineService)
                .GetField("_players", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var players = playersField?.GetValue(service) as Dictionary<string, Player>;
            
            foreach (var player in players.Values)
            {
                player.HoleCards.Should().NotBeNull();
                player.HoleCards.Cards.Should().HaveCount(2, "Each player should receive exactly 2 hole cards");
            }
        }
        
        [Fact]
        public void DealCommunityCards_ShouldDealCorrectNumberOfCards()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service");
            var mockBroker = new Mock<IMessageBroker>();
            var service = new GameEngineService(executionContext, mockBroker.Object);
            
            // Create a deck field through reflection (for testing purposes)
            var deckField = typeof(GameEngineService)
                .GetField("_currentDeck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Initialize a deck with cards
            var deck = new List<Card>();
            for (int i = 0; i < 10; i++) // Add some test cards
            {
                deck.Add(new Card((Suit)(i % 4), (Rank)(i % 13 + 1)));
            }
            deckField?.SetValue(service, deck);
            
            // Create a community cards field through reflection
            var communityCardsField = typeof(GameEngineService)
                .GetField("_communityCards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var communityCards = new Hand();
            communityCardsField?.SetValue(service, communityCards);
            
            // Act - Deal the flop (3 cards)
            service.DealCommunityCards(3);
            
            // Assert
            communityCards.Cards.Should().HaveCount(3, "The flop should have 3 community cards");
            
            // Act - Deal the turn (1 more card)
            service.DealCommunityCards(1);
            
            // Assert
            communityCards.Cards.Should().HaveCount(4, "After the turn, there should be 4 community cards");
            
            // Act - Deal the river (1 more card)
            service.DealCommunityCards(1);
            
            // Assert
            communityCards.Cards.Should().HaveCount(5, "After the river, there should be 5 community cards");
        }
    }
}