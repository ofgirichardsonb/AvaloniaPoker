using NUnit.Framework;
using Moq;
using PokerGame.Core.Models;
using PokerGame.Core.Game;
using System.Collections.Generic;
using System.Linq;

namespace PokerGame.Tests.Core.Game
{
    [TestFixture]
    public class BettingRoundTests
    {
        private BettingRound _bettingRound;
        private List<Player> _players;
        private readonly int _startingChips = 1000;
        private readonly int _smallBlind = 5;
        private readonly int _bigBlind = 10;
        
        [SetUp]
        public void Setup()
        {
            // Create test players
            _players = new List<Player>
            {
                new Player { Id = "player1", Name = "Player 1", ChipCount = _startingChips },
                new Player { Id = "player2", Name = "Player 2", ChipCount = _startingChips },
                new Player { Id = "player3", Name = "Player 3", ChipCount = _startingChips }
            };
            
            // Create betting round
            _bettingRound = new BettingRound();
        }
        
        [Test]
        public void InitializeRound_SetsCorrectInitialState()
        {
            // Arrange
            GameState state = GameState.PreFlop;
            int dealerPosition = 0;
            
            // Act
            _bettingRound.InitializeRound(_players, state, dealerPosition, _smallBlind, _bigBlind);
            
            // Assert
            Assert.That(_bettingRound.CurrentBet, Is.EqualTo(_bigBlind));
            Assert.That(_bettingRound.Pot, Is.EqualTo(_smallBlind + _bigBlind));
            
            // Small blind should be posted by player after dealer
            Assert.That(_players[1].CurrentBet, Is.EqualTo(_smallBlind));
            
            // Big blind should be posted by player after small blind
            Assert.That(_players[2].CurrentBet, Is.EqualTo(_bigBlind));
            
            // Check initial chip counts
            Assert.That(_players[1].ChipCount, Is.EqualTo(_startingChips - _smallBlind));
            Assert.That(_players[2].ChipCount, Is.EqualTo(_startingChips - _bigBlind));
        }
        
        [Test]
        public void GetNextPlayerIndex_ReturnsCorrectNextPlayer()
        {
            // Arrange
            GameState state = GameState.PreFlop;
            int dealerPosition = 0;
            _bettingRound.InitializeRound(_players, state, dealerPosition, _smallBlind, _bigBlind);
            
            // Act - First call should return index 0 (first player after big blind in pre-flop)
            int nextPlayerIndex = _bettingRound.GetNextPlayerIndex();
            
            // Assert
            Assert.That(nextPlayerIndex, Is.EqualTo(0));
            
            // After setting the current player to have acted, next should be 1
            _players[0].HasActed = true;
            nextPlayerIndex = _bettingRound.GetNextPlayerIndex();
            Assert.That(nextPlayerIndex, Is.EqualTo(1));
        }
        
        [Test]
        public void ProcessAction_Call_UpdatesPotAndPlayerState()
        {
            // Arrange
            GameState state = GameState.PreFlop;
            int dealerPosition = 0;
            _bettingRound.InitializeRound(_players, state, dealerPosition, _smallBlind, _bigBlind);
            
            // Player 0 calls the big blind
            int initialPot = _bettingRound.Pot;
            int callAmount = _bettingRound.CurrentBet;
            int playerChipsBeforeCall = _players[0].ChipCount;
            
            // Act
            _bettingRound.ProcessAction(_players[0], new PlayerAction 
            {
                PlayerId = _players[0].Id,
                ActionType = PlayerActionType.Call,
                Amount = callAmount
            });
            
            // Assert
            Assert.That(_players[0].CurrentBet, Is.EqualTo(callAmount));
            Assert.That(_players[0].ChipCount, Is.EqualTo(playerChipsBeforeCall - callAmount));
            Assert.That(_players[0].HasActed, Is.True);
            Assert.That(_bettingRound.Pot, Is.EqualTo(initialPot + callAmount));
        }
        
        [Test]
        public void ProcessAction_Raise_UpdatesCurrentBet()
        {
            // Arrange
            GameState state = GameState.PreFlop;
            int dealerPosition = 0;
            _bettingRound.InitializeRound(_players, state, dealerPosition, _smallBlind, _bigBlind);
            
            // Player 0 raises
            int initialPot = _bettingRound.Pot;
            int raiseAmount = _bettingRound.CurrentBet * 2;
            
            // Act
            _bettingRound.ProcessAction(_players[0], new PlayerAction 
            {
                PlayerId = _players[0].Id,
                ActionType = PlayerActionType.Raise,
                Amount = raiseAmount
            });
            
            // Assert
            Assert.That(_bettingRound.CurrentBet, Is.EqualTo(raiseAmount));
            Assert.That(_players[0].CurrentBet, Is.EqualTo(raiseAmount));
            Assert.That(_players[0].HasActed, Is.True);
            Assert.That(_bettingRound.Pot, Is.EqualTo(initialPot + raiseAmount));
            
            // After a raise, all other players' HasActed flags should be reset
            Assert.That(_players[1].HasActed, Is.False);
            Assert.That(_players[2].HasActed, Is.False);
        }
        
        [Test]
        public void ProcessAction_Fold_UpdatesPlayerState()
        {
            // Arrange
            GameState state = GameState.PreFlop;
            int dealerPosition = 0;
            _bettingRound.InitializeRound(_players, state, dealerPosition, _smallBlind, _bigBlind);
            
            // Act
            _bettingRound.ProcessAction(_players[0], new PlayerAction 
            {
                PlayerId = _players[0].Id,
                ActionType = PlayerActionType.Fold
            });
            
            // Assert
            Assert.That(_players[0].HasFolded, Is.True);
            Assert.That(_players[0].HasActed, Is.True);
            
            // Pot should remain unchanged when folding
            Assert.That(_bettingRound.Pot, Is.EqualTo(_smallBlind + _bigBlind));
        }
        
        [Test]
        public void ProcessAction_Check_WhenNoCurrentBet_UpdatesPlayerState()
        {
            // Arrange - setup a post-flop scenario where checking is allowed
            GameState state = GameState.Flop;
            int dealerPosition = 0;
            
            // Clear any previous bets
            foreach (var player in _players)
            {
                player.CurrentBet = 0;
            }
            
            // Initialize with new state and 0 blinds since we're simulating Flop
            _bettingRound.InitializeRound(_players, state, dealerPosition, 0, 0);
            
            // Act
            _bettingRound.ProcessAction(_players[0], new PlayerAction 
            {
                PlayerId = _players[0].Id,
                ActionType = PlayerActionType.Check
            });
            
            // Assert
            Assert.That(_players[0].HasActed, Is.True);
            Assert.That(_players[0].CurrentBet, Is.EqualTo(0));
        }
        
        [Test]
        public void IsBettingRoundComplete_WhenAllPlayersActed_ReturnsTrue()
        {
            // Arrange
            GameState state = GameState.PreFlop;
            int dealerPosition = 0;
            _bettingRound.InitializeRound(_players, state, dealerPosition, _smallBlind, _bigBlind);
            
            // All players call
            for (int i = 0; i < _players.Count; i++)
            {
                var playerIndex = _bettingRound.GetNextPlayerIndex();
                _bettingRound.ProcessAction(_players[playerIndex], new PlayerAction 
                {
                    PlayerId = _players[playerIndex].Id,
                    ActionType = PlayerActionType.Call,
                    Amount = _bettingRound.CurrentBet
                });
            }
            
            // Act
            bool isComplete = _bettingRound.IsBettingRoundComplete();
            
            // Assert
            Assert.That(isComplete, Is.True);
        }
        
        [Test]
        public void IsBettingRoundComplete_AfterRaise_ReturnsFalse()
        {
            // Arrange
            GameState state = GameState.PreFlop;
            int dealerPosition = 0;
            _bettingRound.InitializeRound(_players, state, dealerPosition, _smallBlind, _bigBlind);
            
            // First player raises
            _bettingRound.ProcessAction(_players[0], new PlayerAction 
            {
                PlayerId = _players[0].Id,
                ActionType = PlayerActionType.Raise,
                Amount = _bettingRound.CurrentBet * 2
            });
            
            // Second player calls
            _bettingRound.ProcessAction(_players[1], new PlayerAction 
            {
                PlayerId = _players[1].Id,
                ActionType = PlayerActionType.Call,
                Amount = _bettingRound.CurrentBet
            });
            
            // Third player hasn't acted yet
            
            // Act
            bool isComplete = _bettingRound.IsBettingRoundComplete();
            
            // Assert
            Assert.That(isComplete, Is.False);
        }
        
        [Test]
        public void IsBettingRoundComplete_AfterRaise_AllCallOrFold_ReturnsTrue()
        {
            // Arrange
            GameState state = GameState.PreFlop;
            int dealerPosition = 0;
            _bettingRound.InitializeRound(_players, state, dealerPosition, _smallBlind, _bigBlind);
            
            // First player raises
            _bettingRound.ProcessAction(_players[0], new PlayerAction 
            {
                PlayerId = _players[0].Id,
                ActionType = PlayerActionType.Raise,
                Amount = _bettingRound.CurrentBet * 2
            });
            
            // Second player calls the raise
            _bettingRound.ProcessAction(_players[1], new PlayerAction 
            {
                PlayerId = _players[1].Id,
                ActionType = PlayerActionType.Call,
                Amount = _bettingRound.CurrentBet
            });
            
            // Third player folds
            _bettingRound.ProcessAction(_players[2], new PlayerAction 
            {
                PlayerId = _players[2].Id,
                ActionType = PlayerActionType.Fold
            });
            
            // Act
            bool isComplete = _bettingRound.IsBettingRoundComplete();
            
            // Assert
            Assert.That(isComplete, Is.True);
        }
        
        [Test]
        public void IsBettingRoundComplete_WhenAllButOneFolded_ReturnsTrue()
        {
            // Arrange
            GameState state = GameState.PreFlop;
            int dealerPosition = 0;
            _bettingRound.InitializeRound(_players, state, dealerPosition, _smallBlind, _bigBlind);
            
            // First two players fold
            _bettingRound.ProcessAction(_players[0], new PlayerAction 
            {
                PlayerId = _players[0].Id,
                ActionType = PlayerActionType.Fold
            });
            
            _bettingRound.ProcessAction(_players[1], new PlayerAction 
            {
                PlayerId = _players[1].Id,
                ActionType = PlayerActionType.Fold
            });
            
            // Act
            bool isComplete = _bettingRound.IsBettingRoundComplete();
            
            // Assert
            Assert.That(isComplete, Is.True);
        }
        
        [Test]
        public void ResetHasActedFlags_ResetsAllPlayerFlags()
        {
            // Arrange
            GameState state = GameState.PreFlop;
            int dealerPosition = 0;
            _bettingRound.InitializeRound(_players, state, dealerPosition, _smallBlind, _bigBlind);
            
            // Set all players as having acted
            foreach (var player in _players)
            {
                player.HasActed = true;
            }
            
            // Act
            _bettingRound.ResetHasActedFlags();
            
            // Assert
            foreach (var player in _players)
            {
                Assert.That(player.HasActed, Is.False);
            }
        }
        
        [Test]
        public void ResetCurrentBets_ClearsAllBets()
        {
            // Arrange
            GameState state = GameState.PreFlop;
            int dealerPosition = 0;
            _bettingRound.InitializeRound(_players, state, dealerPosition, _smallBlind, _bigBlind);
            
            // Act
            _bettingRound.ResetCurrentBets();
            
            // Assert
            Assert.That(_bettingRound.CurrentBet, Is.EqualTo(0));
            foreach (var player in _players)
            {
                Assert.That(player.CurrentBet, Is.EqualTo(0));
            }
        }
        
        [Test]
        public void GetNextBettingPositionIndex_PostFlop_StartsWithPlayerAfterDealer()
        {
            // Arrange
            GameState state = GameState.Flop;
            int dealerPosition = 0;
            
            // Reset current bets
            foreach (var player in _players)
            {
                player.CurrentBet = 0;
            }
            
            _bettingRound.InitializeRound(_players, state, dealerPosition, 0, 0);
            
            // Act
            int nextIndex = _bettingRound.GetNextPlayerIndex();
            
            // Assert - first player after dealer (index 1)
            Assert.That(nextIndex, Is.EqualTo(1));
        }
        
        [Test]
        public void GetNextBettingPositionIndex_SkipsFoldedPlayers()
        {
            // Arrange
            GameState state = GameState.PreFlop;
            int dealerPosition = 0;
            _bettingRound.InitializeRound(_players, state, dealerPosition, _smallBlind, _bigBlind);
            
            // First player folds
            _players[0].HasFolded = true;
            
            // Act
            int nextIndex = _bettingRound.GetNextPlayerIndex();
            
            // Assert - should skip folded player and go to player 1
            Assert.That(nextIndex, Is.EqualTo(1));
        }
    }
}