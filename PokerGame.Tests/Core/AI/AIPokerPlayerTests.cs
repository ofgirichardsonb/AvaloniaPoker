using NUnit.Framework;
using Moq;
using PokerGame.Core.AI;
using PokerGame.Core.Models;
using System.Collections.Generic;
using PokerGame.Core.Game;
using CardModel = PokerGame.Core.Models.Card;

namespace PokerGame.Tests.Core.AI;

[TestFixture]
public class AIPokerPlayerTests
{
    private AIPokerPlayer _aiPlayer;
    private Player _playerModel;
    private List<CardModel> _communityCards;
    private int _currentBet;
    private int _bigBlind;
    private int _maxBet;
    
    [SetUp]
    public void Setup()
    {
        _playerModel = new Player
        {
            Id = "ai-player-1",
            Name = "AI Player 1",
            ChipCount = 1000,
            HoleCards = new List<CardModel>()
        };
        
        _communityCards = new List<CardModel>();
        _currentBet = 0;
        _bigBlind = 10;
        _maxBet = 100;
        
        _aiPlayer = new AIPokerPlayer(_playerModel)
        {
            BigBlind = _bigBlind,
            MaxBet = _maxBet
        };
    }
    
    [Test]
    public void Constructor_SetsPlayerModelCorrectly()
    {
        // Assert
        Assert.That(_aiPlayer.Player, Is.EqualTo(_playerModel));
        Assert.That(_aiPlayer.BigBlind, Is.EqualTo(_bigBlind));
        Assert.That(_aiPlayer.MaxBet, Is.EqualTo(_maxBet));
    }
    
    [Test]
    public void MakeDecision_WithPairInHand_ShouldCall()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel { Rank = "7", Suit = "Hearts" });
        _playerModel.HoleCards.Add(new CardModel { Rank = "7", Suit = "Diamonds" });
        _currentBet = 20;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(PlayerActionType.Call));
        Assert.That(decision.Amount, Is.EqualTo(_currentBet));
    }
    
    [Test]
    public void MakeDecision_WithHighCards_AndLowBet_ShouldCall()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel { Rank = "A", Suit = "Hearts" });
        _playerModel.HoleCards.Add(new CardModel { Rank = "K", Suit = "Diamonds" });
        _currentBet = 5; // Low bet
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(PlayerActionType.Call));
        Assert.That(decision.Amount, Is.EqualTo(_currentBet));
    }
    
    [Test]
    public void MakeDecision_WithLowCards_AndHighBet_ShouldFold()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel { Rank = "2", Suit = "Hearts" });
        _playerModel.HoleCards.Add(new CardModel { Rank = "4", Suit = "Diamonds" });
        _currentBet = 50; // High bet
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(PlayerActionType.Fold));
    }
    
    [Test]
    public void MakeDecision_WithGoodHand_ShouldRaise()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel { Rank = "A", Suit = "Hearts" });
        _playerModel.HoleCards.Add(new CardModel { Rank = "A", Suit = "Diamonds" });
        _currentBet = 20;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(PlayerActionType.Raise));
        Assert.That(decision.Amount, Is.GreaterThan(_currentBet));
        Assert.That(decision.Amount, Is.LessThanOrEqualTo(_maxBet));
    }
    
    [Test]
    public void MakeDecision_WithNoCurrentBet_ShouldCheckOrBet()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel { Rank = "10", Suit = "Hearts" });
        _playerModel.HoleCards.Add(new CardModel { Rank = "J", Suit = "Diamonds" });
        _currentBet = 0;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.AnyOf(PlayerActionType.Check, PlayerActionType.Bet));
        if (decision.ActionType == PlayerActionType.Bet)
        {
            Assert.That(decision.Amount, Is.GreaterThan(0));
            Assert.That(decision.Amount, Is.LessThanOrEqualTo(_maxBet));
        }
    }
    
    [Test]
    public void MakeDecision_WithPossibleStraightDraw_ShouldCall()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel { Rank = "10", Suit = "Hearts" });
        _playerModel.HoleCards.Add(new CardModel { Rank = "J", Suit = "Diamonds" });
        
        _communityCards.Add(new CardModel { Rank = "9", Suit = "Clubs" });
        _communityCards.Add(new CardModel { Rank = "8", Suit = "Spades" });
        _communityCards.Add(new CardModel { Rank = "2", Suit = "Hearts" });
        
        _currentBet = 15;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(PlayerActionType.Call));
        Assert.That(decision.Amount, Is.EqualTo(_currentBet));
    }
    
    [Test]
    public void MakeDecision_WithPossibleFlushDraw_ShouldCall()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel { Rank = "2", Suit = "Hearts" });
        _playerModel.HoleCards.Add(new CardModel { Rank = "7", Suit = "Hearts" });
        
        _communityCards.Add(new CardModel { Rank = "A", Suit = "Hearts" });
        _communityCards.Add(new CardModel { Rank = "10", Suit = "Hearts" });
        _communityCards.Add(new CardModel { Rank = "6", Suit = "Diamonds" });
        
        _currentBet = 25;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(PlayerActionType.Call));
        Assert.That(decision.Amount, Is.EqualTo(_currentBet));
    }
    
    [Test]
    public void MakeDecision_WithCurrentBetHigherThanChips_ShouldFold()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel { Rank = "10", Suit = "Hearts" });
        _playerModel.HoleCards.Add(new CardModel { Rank = "J", Suit = "Diamonds" });
        _playerModel.ChipCount = 10;
        _currentBet = 100;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(PlayerActionType.Fold));
    }
    
    [Test]
    public void MakeDecision_WhenCanCheckIsFalse_AndCurrentBetZero_ShouldNotCheck()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel { Rank = "10", Suit = "Hearts" });
        _playerModel.HoleCards.Add(new CardModel { Rank = "J", Suit = "Diamonds" });
        _currentBet = 0;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.Not.EqualTo(PlayerActionType.Check));
        Assert.That(decision.ActionType, Is.EqualTo(PlayerActionType.Bet));
        Assert.That(decision.Amount, Is.GreaterThan(0));
    }
    
    [Test]
    public void MakeDecision_WhenCanCheckIsTrue_AndCurrentBetZero_AllowsCheck()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel { Rank = "2", Suit = "Hearts" });
        _playerModel.HoleCards.Add(new CardModel { Rank = "5", Suit = "Diamonds" }); // Lower value hand
        _currentBet = 0;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, true);
        
        // Assert
        Assert.That(decision.ActionType, Is.AnyOf(PlayerActionType.Check, PlayerActionType.Bet));
    }
    
    [Test]
    public void MakeDecision_WithVeryStrongHand_ShouldRaiseHighAmount()
    {
        // Arrange - Pocket Aces
        _playerModel.HoleCards.Add(new CardModel { Rank = "A", Suit = "Hearts" });
        _playerModel.HoleCards.Add(new CardModel { Rank = "A", Suit = "Spades" });
        _currentBet = 10;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(PlayerActionType.Raise));
        Assert.That(decision.Amount, Is.GreaterThan(_currentBet * 2)); // Should raise significantly
    }
}