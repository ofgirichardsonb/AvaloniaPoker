using System;
using PokerGame.Core.Models;
using Xunit;
using FluentAssertions;
using System.Collections.Generic;

namespace PokerGame.Tests.New.Core.Models
{
    public class PlayerTests
    {
        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            string id = "player123";
            string name = "Test Player";
            
            // Act
            var player = new Player(id, name);
            
            // Assert
            player.Id.Should().Be(id);
            player.Name.Should().Be(name);
            player.HoleCards.Should().NotBeNull();
            player.HoleCards.Cards.Should().BeEmpty();
            player.ChipCount.Should().Be(1000); // Default chip count
            player.HasFolded.Should().BeFalse();
            player.IsAllIn.Should().BeFalse();
            player.CurrentBet.Should().Be(0);
        }
        
        [Fact]
        public void Constructor_WithChipCount_ShouldSetSpecifiedChips()
        {
            // Arrange
            string id = "player123";
            string name = "Test Player";
            int chipCount = 2000;
            
            // Act
            var player = new Player(id, name, chipCount);
            
            // Assert
            player.ChipCount.Should().Be(chipCount);
        }
        
        [Fact]
        public void PlaceBet_WithValidAmount_ShouldDeductChipsAndUpdateCurrentBet()
        {
            // Arrange
            var player = new Player("player123", "Test Player", 1000);
            int betAmount = 200;
            int initialChips = player.ChipCount;
            
            // Act
            bool result = player.PlaceBet(betAmount);
            
            // Assert
            result.Should().BeTrue();
            player.ChipCount.Should().Be(initialChips - betAmount);
            player.CurrentBet.Should().Be(betAmount);
        }
        
        [Fact]
        public void PlaceBet_WithInsufficientChips_ShouldReturnFalse()
        {
            // Arrange
            var player = new Player("player123", "Test Player", 100);
            int betAmount = 200; // More than available chips
            int initialChips = player.ChipCount;
            
            // Act
            bool result = player.PlaceBet(betAmount);
            
            // Assert
            result.Should().BeFalse();
            player.ChipCount.Should().Be(initialChips); // Chips should not change
            player.CurrentBet.Should().Be(0); // Current bet should not change
        }
        
        [Fact]
        public void PlaceBet_WithAllChips_ShouldSetIsAllInTrue()
        {
            // Arrange
            var player = new Player("player123", "Test Player", 500);
            int betAmount = 500; // All available chips
            
            // Act
            bool result = player.PlaceBet(betAmount);
            
            // Assert
            result.Should().BeTrue();
            player.ChipCount.Should().Be(0);
            player.CurrentBet.Should().Be(betAmount);
            player.IsAllIn.Should().BeTrue();
        }
        
        [Fact]
        public void Fold_ShouldSetHasFoldedTrue()
        {
            // Arrange
            var player = new Player("player123", "Test Player");
            
            // Act
            player.Fold();
            
            // Assert
            player.HasFolded.Should().BeTrue();
        }
        
        [Fact]
        public void WinPot_ShouldAddChipsToPlayerTotal()
        {
            // Arrange
            var player = new Player("player123", "Test Player", 500);
            int potAmount = 300;
            int initialChips = player.ChipCount;
            
            // Act
            player.WinPot(potAmount);
            
            // Assert
            player.ChipCount.Should().Be(initialChips + potAmount);
        }
        
        [Fact]
        public void ResetForNewHand_ShouldResetPlayerStateForNewHand()
        {
            // Arrange
            var player = new Player("player123", "Test Player");
            
            // Set up player state from previous hand
            player.PlaceBet(100);
            player.Fold();
            player.HoleCards.AddCard(new Card(Rank.Ace, Suit.Hearts));
            player.HoleCards.AddCard(new Card(Rank.King, Suit.Spades));
            
            // Act
            player.ResetForNewHand();
            
            // Assert
            player.HasFolded.Should().BeFalse();
            player.IsAllIn.Should().BeFalse();
            player.CurrentBet.Should().Be(0);
            player.HoleCards.Cards.Should().BeEmpty();
        }
        
        [Fact]
        public void CheckCall_WithEnoughChips_ShouldMatchTargetBet()
        {
            // Arrange
            var player = new Player("player123", "Test Player", 1000);
            int currentPlayerBet = 50;
            player.PlaceBet(currentPlayerBet);
            int targetBet = 150; // Current player bet + 100 more
            int expectedBetAmount = targetBet - currentPlayerBet; // 100
            int initialChips = player.ChipCount;
            
            // Act
            bool result = player.CheckCall(targetBet);
            
            // Assert
            result.Should().BeTrue();
            player.CurrentBet.Should().Be(targetBet);
            player.ChipCount.Should().Be(initialChips - expectedBetAmount);
        }
        
        [Fact]
        public void CheckCall_WithInsufficientChips_ShouldGoAllIn()
        {
            // Arrange
            var player = new Player("player123", "Test Player", 100);
            int currentPlayerBet = 50;
            player.PlaceBet(currentPlayerBet);
            int targetBet = 200; // More than player can afford
            
            // Act
            bool result = player.CheckCall(targetBet);
            
            // Assert
            result.Should().BeTrue();
            player.CurrentBet.Should().Be(currentPlayerBet + 100); // Bet everything remaining
            player.ChipCount.Should().Be(0);
            player.IsAllIn.Should().BeTrue();
        }
        
        [Fact]
        public void Raise_WithEnoughChips_ShouldIncreaseTargetBetByRaiseAmount()
        {
            // Arrange
            var player = new Player("player123", "Test Player", 1000);
            int currentPlayerBet = 50;
            player.PlaceBet(currentPlayerBet);
            int currentTableBet = 100;
            int raiseAmount = 100;
            int expectedTotalBet = currentTableBet + raiseAmount; // 200
            int expectedAdditionalBet = expectedTotalBet - currentPlayerBet; // 150
            int initialChips = player.ChipCount;
            
            // Act
            bool result = player.Raise(currentTableBet, raiseAmount);
            
            // Assert
            result.Should().BeTrue();
            player.CurrentBet.Should().Be(expectedTotalBet);
            player.ChipCount.Should().Be(initialChips - expectedAdditionalBet);
        }
        
        [Fact]
        public void Raise_WithInsufficientChips_ShouldReturnFalse()
        {
            // Arrange
            var player = new Player("player123", "Test Player", 120);
            int currentPlayerBet = 50;
            player.PlaceBet(currentPlayerBet);
            int currentTableBet = 100;
            int raiseAmount = 100; // This would require 150 more chips, but player only has 70 left
            int initialChips = player.ChipCount;
            int initialBet = player.CurrentBet;
            
            // Act
            bool result = player.Raise(currentTableBet, raiseAmount);
            
            // Assert
            result.Should().BeFalse();
            player.CurrentBet.Should().Be(initialBet); // Bet should not change
            player.ChipCount.Should().Be(initialChips); // Chips should not change
        }
        
        [Fact]
        public void DealHoleCard_ShouldAddCardToPlayerHoleCards()
        {
            // Arrange
            var player = new Player("player123", "Test Player");
            var card = new Card(Rank.Queen, Suit.Diamonds);
            
            // Act
            player.DealHoleCard(card);
            
            // Assert
            player.HoleCards.Cards.Should().HaveCount(1);
            player.HoleCards.Cards.Should().Contain(card);
        }
        
        [Fact]
        public void ToString_ShouldReturnFormattedPlayerInformation()
        {
            // Arrange
            var player = new Player("player123", "Test Player", 500);
            
            // Act
            string result = player.ToString();
            
            // Assert
            result.Should().Contain(player.Name);
            result.Should().Contain(player.ChipCount.ToString());
        }
    }
}