using NUnit.Framework;
using PokerGame.Core.Game;
using PokerGame.Core.Models;
using System.Collections.Generic;

namespace PokerGame.Tests.New.Core.Game;

[TestFixture]
public class BettingRoundTests
{
    private BettingRound _bettingRound;
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
        _bettingRound = new BettingRound();
    }
    
    [Test]
    public void StartBettingRound_SetsCorrectInitialValues()
    {
        // Act
        _bettingRound.StartBettingRound(_players, 5, 10);
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_bettingRound.CurrentBet, Is.EqualTo(10));
            Assert.That(_bettingRound.SmallBlind, Is.EqualTo(5));
            Assert.That(_bettingRound.BigBlind, Is.EqualTo(10));
            Assert.That(_bettingRound.ActivePlayers, Is.EqualTo(_players));
            Assert.That(_bettingRound.TotalPot, Is.EqualTo(0));
        });
    }
    
    [Test]
    public void PostBlinds_DeductsCorrectAmountsFromPlayers()
    {
        // Arrange
        _bettingRound.StartBettingRound(_players, 5, 10);
        
        // Act
        _bettingRound.PostBlinds(0, 1);
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_player1.CurrentBet, Is.EqualTo(5)); // Small blind
            Assert.That(_player2.CurrentBet, Is.EqualTo(10)); // Big blind
            Assert.That(_player3.CurrentBet, Is.EqualTo(0)); // No blind
            Assert.That(_player1.ChipCount, Is.EqualTo(995));
            Assert.That(_player2.ChipCount, Is.EqualTo(990));
            Assert.That(_bettingRound.TotalPot, Is.EqualTo(15));
        });
    }
    
    [Test]
    public void HandlePlayerAction_WhenPlayerChecks_SetsHasActedToTrue()
    {
        // Arrange
        _bettingRound.StartBettingRound(_players, 5, 10);
        _bettingRound.PostBlinds(0, 1);
        
        // Set current bet to 0 for this test
        _bettingRound.CurrentBet = 0;
        _player1.CurrentBet = 0;
        _player2.CurrentBet = 0;
        _player3.CurrentBet = 0;
        
        var action = new PlayerAction 
        { 
            PlayerId = "player1", 
            ActionType = PlayerActionType.Check,
            Amount = 0
        };
        
        // Act
        _bettingRound.HandlePlayerAction(action);
        
        // Assert
        Assert.That(_player1.HasActed, Is.True);
        Assert.That(_player1.CurrentBet, Is.EqualTo(0));
        Assert.That(_bettingRound.CurrentBet, Is.EqualTo(0));
    }
    
    [Test]
    public void HandlePlayerAction_WhenPlayerCalls_MatchesCurrentBet()
    {
        // Arrange
        _bettingRound.StartBettingRound(_players, 5, 10);
        _bettingRound.PostBlinds(0, 1);
        
        // Third player calls the big blind
        var action = new PlayerAction 
        { 
            PlayerId = "player3", 
            ActionType = PlayerActionType.Call,
            Amount = 10
        };
        
        // Act
        _bettingRound.HandlePlayerAction(action);
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_player3.HasActed, Is.True);
            Assert.That(_player3.CurrentBet, Is.EqualTo(10));
            Assert.That(_player3.ChipCount, Is.EqualTo(990));
            Assert.That(_bettingRound.TotalPot, Is.EqualTo(25)); // 5 (SB) + 10 (BB) + 10 (call)
            Assert.That(_bettingRound.CurrentBet, Is.EqualTo(10));
        });
    }
    
    [Test]
    public void HandlePlayerAction_WhenPlayerRaises_UpdatesCurrentBet()
    {
        // Arrange
        _bettingRound.StartBettingRound(_players, 5, 10);
        _bettingRound.PostBlinds(0, 1);
        
        // Third player raises
        var action = new PlayerAction 
        { 
            PlayerId = "player3", 
            ActionType = PlayerActionType.Raise,
            Amount = 25 // Raise to 25
        };
        
        // Act
        _bettingRound.HandlePlayerAction(action);
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_player3.HasActed, Is.True);
            Assert.That(_player3.CurrentBet, Is.EqualTo(25));
            Assert.That(_player3.ChipCount, Is.EqualTo(975));
            Assert.That(_bettingRound.TotalPot, Is.EqualTo(40)); // 5 (SB) + 10 (BB) + 25 (raise)
            Assert.That(_bettingRound.CurrentBet, Is.EqualTo(25));
            
            // Other players should have HasActed set to false because of the raise
            Assert.That(_player1.HasActed, Is.False);
            Assert.That(_player2.HasActed, Is.False);
        });
    }
    
    [Test]
    public void HandlePlayerAction_WhenPlayerFolds_MarksPlayerAsFolded()
    {
        // Arrange
        _bettingRound.StartBettingRound(_players, 5, 10);
        _bettingRound.PostBlinds(0, 1);
        
        // Third player folds
        var action = new PlayerAction 
        { 
            PlayerId = "player3", 
            ActionType = PlayerActionType.Fold
        };
        
        // Act
        _bettingRound.HandlePlayerAction(action);
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_player3.HasActed, Is.True);
            Assert.That(_player3.HasFolded, Is.True);
            Assert.That(_bettingRound.TotalPot, Is.EqualTo(15)); // 5 (SB) + 10 (BB)
            Assert.That(_bettingRound.CurrentBet, Is.EqualTo(10));
            
            // Check that player is still in ActivePlayers list but marked as folded
            Assert.That(_bettingRound.ActivePlayers, Contains.Item(_player3));
        });
    }
    
    [Test]
    public void IsBettingRoundComplete_WhenAllPlayersActedAndBetsMatch_ReturnsTrue()
    {
        // Arrange
        _bettingRound.StartBettingRound(_players, 5, 10);
        _bettingRound.PostBlinds(0, 1);
        
        // First player calls
        var action1 = new PlayerAction 
        { 
            PlayerId = "player1", 
            ActionType = PlayerActionType.Call,
            Amount = 10 // Match big blind (adding 5 more to their SB)
        };
        _bettingRound.HandlePlayerAction(action1);
        
        // Third player calls
        var action3 = new PlayerAction 
        { 
            PlayerId = "player3", 
            ActionType = PlayerActionType.Call,
            Amount = 10
        };
        _bettingRound.HandlePlayerAction(action3);
        
        // Big blind checks
        var action2 = new PlayerAction 
        { 
            PlayerId = "player2", 
            ActionType = PlayerActionType.Check
        };
        _bettingRound.HandlePlayerAction(action2);
        
        // Act
        bool isComplete = _bettingRound.IsBettingRoundComplete();
        
        // Assert
        Assert.IsTrue(isComplete);
        Assert.That(_bettingRound.TotalPot, Is.EqualTo(30)); // Everyone put in 10
    }
    
    [Test]
    public void IsBettingRoundComplete_WhenNotAllPlayersActed_ReturnsFalse()
    {
        // Arrange
        _bettingRound.StartBettingRound(_players, 5, 10);
        _bettingRound.PostBlinds(0, 1);
        
        // Only first player calls
        var action1 = new PlayerAction 
        { 
            PlayerId = "player1", 
            ActionType = PlayerActionType.Call,
            Amount = 10 // Match big blind (adding 5 more to their SB)
        };
        _bettingRound.HandlePlayerAction(action1);
        
        // Act
        bool isComplete = _bettingRound.IsBettingRoundComplete();
        
        // Assert
        Assert.IsFalse(isComplete);
    }
    
    [Test]
    public void IsBettingRoundComplete_WhenBetsDoNotMatch_ReturnsFalse()
    {
        // Arrange
        _bettingRound.StartBettingRound(_players, 5, 10);
        _bettingRound.PostBlinds(0, 1);
        
        // Third player raises
        var action3 = new PlayerAction 
        { 
            PlayerId = "player3", 
            ActionType = PlayerActionType.Raise,
            Amount = 20
        };
        _bettingRound.HandlePlayerAction(action3);
        
        // First player calls
        var action1 = new PlayerAction 
        { 
            PlayerId = "player1", 
            ActionType = PlayerActionType.Call,
            Amount = 20 // Match the raise
        };
        _bettingRound.HandlePlayerAction(action1);
        
        // Big blind player has acted but hasn't matched the new bet
        
        // Act
        bool isComplete = _bettingRound.IsBettingRoundComplete();
        
        // Assert
        Assert.IsFalse(isComplete);
    }
    
    [Test]
    public void IsBettingRoundComplete_WhenAllButFoldedPlayersHaveActed_ReturnsTrue()
    {
        // Arrange
        _bettingRound.StartBettingRound(_players, 5, 10);
        _bettingRound.PostBlinds(0, 1);
        
        // Third player folds
        var action3 = new PlayerAction 
        { 
            PlayerId = "player3", 
            ActionType = PlayerActionType.Fold
        };
        _bettingRound.HandlePlayerAction(action3);
        
        // First player calls
        var action1 = new PlayerAction 
        { 
            PlayerId = "player1", 
            ActionType = PlayerActionType.Call,
            Amount = 10 // Match big blind
        };
        _bettingRound.HandlePlayerAction(action1);
        
        // Big blind checks
        var action2 = new PlayerAction 
        { 
            PlayerId = "player2", 
            ActionType = PlayerActionType.Check
        };
        _bettingRound.HandlePlayerAction(action2);
        
        // Act
        bool isComplete = _bettingRound.IsBettingRoundComplete();
        
        // Assert
        Assert.IsTrue(isComplete);
    }
    
    [Test]
    public void EndBettingRound_AddsAllBetsToTotalPot_AndClearsBets()
    {
        // Arrange
        _bettingRound.StartBettingRound(_players, 5, 10);
        _bettingRound.PostBlinds(0, 1);
        
        // First player calls
        var action1 = new PlayerAction 
        { 
            PlayerId = "player1", 
            ActionType = PlayerActionType.Call,
            Amount = 10
        };
        _bettingRound.HandlePlayerAction(action1);
        
        // Third player calls
        var action3 = new PlayerAction 
        { 
            PlayerId = "player3", 
            ActionType = PlayerActionType.Call,
            Amount = 10
        };
        _bettingRound.HandlePlayerAction(action3);
        
        // Act
        int finalPot = _bettingRound.EndBettingRound();
        
        // Assert
        Assert.That(finalPot, Is.EqualTo(30)); // 10 * 3 players
        
        // Check that current bet and player bets are reset
        Assert.That(_bettingRound.CurrentBet, Is.EqualTo(0));
        Assert.That(_player1.CurrentBet, Is.EqualTo(0));
        Assert.That(_player2.CurrentBet, Is.EqualTo(0));
        Assert.That(_player3.CurrentBet, Is.EqualTo(0));
        
        // Check that all players' HasActed flags are reset
        Assert.That(_player1.HasActed, Is.False);
        Assert.That(_player2.HasActed, Is.False);
        Assert.That(_player3.HasActed, Is.False);
    }
}