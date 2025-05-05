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
        _playerModel = new Player("ai-player-1", "AI Player 1", 1000);
        
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
        _playerModel.HoleCards.Add(new CardModel(Rank.Seven, Suit.Hearts));
        _playerModel.HoleCards.Add(new CardModel(Rank.Seven, Suit.Diamonds));
        _currentBet = 20;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(ActionType.Call));
        Assert.That(decision.Amount, Is.EqualTo(_currentBet));
    }
    
    [Test]
    public void MakeDecision_WithHighCards_AndLowBet_ShouldCall()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel(Rank.Ace, Suit.Hearts));
        _playerModel.HoleCards.Add(new CardModel(Rank.King, Suit.Diamonds));
        _currentBet = 5; // Low bet
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(ActionType.Call));
        Assert.That(decision.Amount, Is.EqualTo(_currentBet));
    }
    
    [Test]
    public void MakeDecision_WithLowCards_AndHighBet_ShouldFold()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel(Rank.Two, Suit.Hearts));
        _playerModel.HoleCards.Add(new CardModel(Rank.Four, Suit.Diamonds));
        _currentBet = 50; // High bet
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(ActionType.Fold));
    }
    
    [Test]
    public void MakeDecision_WithGoodHand_ShouldRaise()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel(Rank.Ace, Suit.Hearts));
        _playerModel.HoleCards.Add(new CardModel(Rank.Ace, Suit.Diamonds));
        _currentBet = 20;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(ActionType.Raise));
        Assert.That(decision.Amount, Is.GreaterThan(_currentBet));
        Assert.That(decision.Amount, Is.LessThanOrEqualTo(_maxBet));
    }
    
    [Test]
    public void MakeDecision_WithNoCurrentBet_ShouldCheckOrBet()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel(Rank.Ten, Suit.Hearts));
        _playerModel.HoleCards.Add(new CardModel(Rank.Jack, Suit.Diamonds));
        _currentBet = 0;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.AnyOf(ActionType.Check, ActionType.Bet));
        if (decision.ActionType == ActionType.Bet)
        {
            Assert.That(decision.Amount, Is.GreaterThan(0));
            Assert.That(decision.Amount, Is.LessThanOrEqualTo(_maxBet));
        }
    }
    
    [Test]
    public void MakeDecision_WithPossibleStraightDraw_ShouldCall()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel(Rank.Ten, Suit.Hearts));
        _playerModel.HoleCards.Add(new CardModel(Rank.Jack, Suit.Diamonds));
        
        _communityCards.Add(new CardModel(Rank.Nine, Suit.Clubs));
        _communityCards.Add(new CardModel(Rank.Eight, Suit.Spades));
        _communityCards.Add(new CardModel(Rank.Two, Suit.Hearts));
        
        _currentBet = 15;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(ActionType.Call));
        Assert.That(decision.Amount, Is.EqualTo(_currentBet));
    }
    
    [Test]
    public void MakeDecision_WithPossibleFlushDraw_ShouldCall()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel(Rank.Two, Suit.Hearts));
        _playerModel.HoleCards.Add(new CardModel(Rank.Seven, Suit.Hearts));
        
        _communityCards.Add(new CardModel(Rank.Ace, Suit.Hearts));
        _communityCards.Add(new CardModel(Rank.Ten, Suit.Hearts));
        _communityCards.Add(new CardModel(Rank.Six, Suit.Diamonds));
        
        _currentBet = 25;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(ActionType.Call));
        Assert.That(decision.Amount, Is.EqualTo(_currentBet));
    }
    
    [Test]
    public void MakeDecision_WithCurrentBetHigherThanChips_ShouldFold()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel(Rank.Ten, Suit.Hearts));
        _playerModel.HoleCards.Add(new CardModel(Rank.Jack, Suit.Diamonds));
        _playerModel.Chips = 10;
        _currentBet = 100;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(ActionType.Fold));
    }
    
    [Test]
    public void MakeDecision_WhenCanCheckIsFalse_AndCurrentBetZero_ShouldNotCheck()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel(Rank.Ten, Suit.Hearts));
        _playerModel.HoleCards.Add(new CardModel(Rank.Jack, Suit.Diamonds));
        _currentBet = 0;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.Not.EqualTo(ActionType.Check));
        Assert.That(decision.ActionType, Is.EqualTo(ActionType.Bet));
        Assert.That(decision.Amount, Is.GreaterThan(0));
    }
    
    [Test]
    public void MakeDecision_WhenCanCheckIsTrue_AndCurrentBetZero_AllowsCheck()
    {
        // Arrange
        _playerModel.HoleCards.Add(new CardModel(Rank.Two, Suit.Hearts));
        _playerModel.HoleCards.Add(new CardModel(Rank.Five, Suit.Diamonds)); // Lower value hand
        _currentBet = 0;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, true);
        
        // Assert
        Assert.That(decision.ActionType, Is.AnyOf(ActionType.Check, ActionType.Bet));
    }
    
    [Test]
    public void MakeDecision_WithVeryStrongHand_ShouldRaiseHighAmount()
    {
        // Arrange - Pocket Aces
        _playerModel.HoleCards.Add(new CardModel(Rank.Ace, Suit.Hearts));
        _playerModel.HoleCards.Add(new CardModel(Rank.Ace, Suit.Spades));
        _currentBet = 10;
        
        // Act
        var decision = _aiPlayer.MakeDecision(_communityCards, _currentBet, false);
        
        // Assert
        Assert.That(decision.ActionType, Is.EqualTo(ActionType.Raise));
        Assert.That(decision.Amount, Is.GreaterThan(_currentBet * 2)); // Should raise significantly
    }
}