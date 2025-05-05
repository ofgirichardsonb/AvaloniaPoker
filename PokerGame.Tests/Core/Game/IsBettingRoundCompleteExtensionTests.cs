using NUnit.Framework;
using PokerGame.Core.Game;
using PokerGame.Core.Models;
using System.Collections.Generic;

namespace PokerGame.Tests.Core.Game;

[TestFixture]
public class IsBettingRoundCompleteExtensionTests
{
    private List<Player> _players;
    private Player _player1;
    private Player _player2;
    private Player _player3;
    
    [SetUp]
    public void Setup()
    {
        _player1 = new Player 
        { 
            Id = "player1", 
            Name = "Player 1", 
            ChipCount = 1000,
            CurrentBet = 0,
            HasActed = false,
            HasFolded = false
        };
        
        _player2 = new Player 
        { 
            Id = "player2", 
            Name = "Player 2", 
            ChipCount = 1000,
            CurrentBet = 0,
            HasActed = false,
            HasFolded = false
        };
        
        _player3 = new Player 
        { 
            Id = "player3", 
            Name = "Player 3", 
            ChipCount = 1000,
            CurrentBet = 0,
            HasActed = false,
            HasFolded = false
        };
        
        _players = new List<Player> { _player1, _player2, _player3 };
    }
    
    [Test]
    public void IsBettingRoundComplete_WhenAllPlayersActedAndBetsMatch_ReturnsTrue()
    {
        // Arrange
        _player1.HasActed = true;
        _player2.HasActed = true;
        _player3.HasActed = true;
        
        _player1.CurrentBet = 10;
        _player2.CurrentBet = 10;
        _player3.CurrentBet = 10;
        
        // Act
        bool result = _players.IsBettingRoundComplete();
        
        // Assert
        Assert.IsTrue(result);
    }
    
    [Test]
    public void IsBettingRoundComplete_WhenNotAllPlayersHaveActed_ReturnsFalse()
    {
        // Arrange
        _player1.HasActed = true;
        _player2.HasActed = false; // One player hasn't acted
        _player3.HasActed = true;
        
        _player1.CurrentBet = 10;
        _player2.CurrentBet = 0; // Bet doesn't match
        _player3.CurrentBet = 10;
        
        // Act
        bool result = _players.IsBettingRoundComplete();
        
        // Assert
        Assert.IsFalse(result);
    }
    
    [Test]
    public void IsBettingRoundComplete_WhenAllActivePlayersActedButBetsDontMatch_ReturnsFalse()
    {
        // Arrange
        _player1.HasActed = true;
        _player2.HasActed = true;
        _player3.HasActed = true;
        
        _player1.CurrentBet = 10;
        _player2.CurrentBet = 20; // Bet doesn't match
        _player3.CurrentBet = 10;
        
        // Act
        bool result = _players.IsBettingRoundComplete();
        
        // Assert
        Assert.IsFalse(result);
    }
    
    [Test]
    public void IsBettingRoundComplete_WithFoldedPlayers_OnlyChecksActivePlayersBets()
    {
        // Arrange
        _player1.HasActed = true;
        _player2.HasActed = true;
        _player3.HasActed = true;
        _player3.HasFolded = true; // This player has folded
        
        _player1.CurrentBet = 10;
        _player2.CurrentBet = 10;
        _player3.CurrentBet = 0; // Folded player's bet doesn't need to match
        
        // Act
        bool result = _players.IsBettingRoundComplete();
        
        // Assert
        Assert.IsTrue(result);
    }
    
    [Test]
    public void IsBettingRoundComplete_WithAllButOneFolded_ReturnsTrue()
    {
        // Arrange
        _player1.HasActed = true;
        _player2.HasActed = true;
        _player2.HasFolded = true;
        _player3.HasActed = true;
        _player3.HasFolded = true;
        
        _player1.CurrentBet = 10;
        _player2.CurrentBet = 5;
        _player3.CurrentBet = 5;
        
        // Act
        bool result = _players.IsBettingRoundComplete();
        
        // Assert
        Assert.IsTrue(result);
    }
    
    [Test]
    public void IsBettingRoundComplete_WithAllFolded_ReturnsTrue()
    {
        // This is an edge case that shouldn't happen in an actual game
        // Arrange
        _player1.HasActed = true;
        _player1.HasFolded = true;
        _player2.HasActed = true;
        _player2.HasFolded = true;
        _player3.HasActed = true;
        _player3.HasFolded = true;
        
        _player1.CurrentBet = 10;
        _player2.CurrentBet = 5;
        _player3.CurrentBet = 0;
        
        // Act
        bool result = _players.IsBettingRoundComplete();
        
        // Assert
        Assert.IsTrue(result);
    }
    
    [Test]
    public void IsBettingRoundComplete_WithEmptyPlayerList_ReturnsTrue()
    {
        // Arrange
        var emptyList = new List<Player>();
        
        // Act
        bool result = emptyList.IsBettingRoundComplete();
        
        // Assert
        Assert.IsTrue(result);
    }
    
    [Test]
    public void IsBettingRoundComplete_WithOnePlayer_ReturnsTrue()
    {
        // Arrange
        var singlePlayerList = new List<Player> { _player1 };
        _player1.HasActed = true;
        
        // Act
        bool result = singlePlayerList.IsBettingRoundComplete();
        
        // Assert
        Assert.IsTrue(result);
    }
    
    [Test]
    public void IsBettingRoundComplete_WithAllInPlayers_OnlyChecksNonAllInPlayers()
    {
        // Arrange
        _player1.HasActed = true;
        _player2.HasActed = true;
        _player2.IsAllIn = true; // This player is all-in
        _player3.HasActed = true;
        
        _player1.CurrentBet = 50;
        _player2.CurrentBet = 30; // All-in player's bet doesn't need to match
        _player3.CurrentBet = 50;
        
        // Act
        bool result = _players.IsBettingRoundComplete();
        
        // Assert
        Assert.IsTrue(result);
    }
}