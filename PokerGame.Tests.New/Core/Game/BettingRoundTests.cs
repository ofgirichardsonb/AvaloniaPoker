using NUnit.Framework;
using PokerGame.Core.Models;
using PokerGame.Core.Game;
using System.Collections.Generic;
using System.Linq;

namespace PokerGame.Tests.New.Core.Game
{
    [TestFixture]
    public class BettingRoundTests
    {
        private BettingRound _bettingRound;
        private List<Player> _players;

        [SetUp]
        public void Setup()
        {
            _bettingRound = new BettingRound();
            _players = new List<Player>
            {
                new Player("player1", "Player 1", 1000),
                new Player("player2", "Player 2", 1000),
                new Player("player3", "Player 3", 1000)
            };
        }

        [Test]
        public void StartBettingRound_InitializesCorrectly()
        {
            // Act
            _bettingRound.StartBettingRound(_players, 5, 10);
            
            // Assert
            Assert.That(_bettingRound.SmallBlind, Is.EqualTo(5));
            Assert.That(_bettingRound.BigBlind, Is.EqualTo(10));
            Assert.That(_bettingRound.CurrentBet, Is.EqualTo(0));
            Assert.That(_bettingRound.TotalPot, Is.EqualTo(0));
            Assert.That(_bettingRound.ActivePlayers, Is.EqualTo(_players));
        }
        
        [Test]
        public void PostBlinds_UpdatesPotAndPlayerChips()
        {
            // Arrange
            _bettingRound.StartBettingRound(_players, 5, 10);
            
            // Act
            _bettingRound.PostBlinds(0, 1);
            
            // Assert
            Assert.That(_bettingRound.CurrentBet, Is.EqualTo(10));
            Assert.That(_bettingRound.TotalPot, Is.EqualTo(15));
            
            Assert.That(_players[0].ChipCount, Is.EqualTo(995)); // Small blind
            Assert.That(_players[0].CurrentBet, Is.EqualTo(5));
            
            Assert.That(_players[1].ChipCount, Is.EqualTo(990)); // Big blind
            Assert.That(_players[1].CurrentBet, Is.EqualTo(10));
        }
        
        [Test]
        public void HandlePlayerAction_Call_UpdatesPlayerAndPot()
        {
            // Arrange
            _bettingRound.StartBettingRound(_players, 5, 10);
            _bettingRound.PostBlinds(0, 1);
            
            // Call the big blind
            var action = new PlayerAction(ActionType.Call, 10, "player3");
            
            // Act
            _bettingRound.HandlePlayerAction(action);
            
            // Assert
            Assert.That(_bettingRound.TotalPot, Is.EqualTo(25)); // 5 + 10 + 10
            Assert.That(_players[2].ChipCount, Is.EqualTo(990));
            Assert.That(_players[2].CurrentBet, Is.EqualTo(10));
            Assert.That(_players[2].HasActed, Is.True);
        }
        
        [Test]
        public void HandlePlayerAction_Raise_UpdatesCurrentBetAndResetsFlagsExceptRaiser()
        {
            // Arrange
            _bettingRound.StartBettingRound(_players, 5, 10);
            _bettingRound.PostBlinds(0, 1);
            
            // Set all players as acted
            foreach (var player in _players)
            {
                player.HasActed = true;
            }
            
            // Player 3 raises to 20
            var raiseAction = new PlayerAction(ActionType.Raise, 20, "player3");
            
            // Act
            _bettingRound.HandlePlayerAction(raiseAction);
            
            // Assert
            Assert.That(_bettingRound.CurrentBet, Is.EqualTo(20));
            Assert.That(_bettingRound.TotalPot, Is.EqualTo(35)); // 5 + 10 + 20
            
            Assert.That(_players[0].HasActed, Is.False); // Reset
            Assert.That(_players[1].HasActed, Is.False); // Reset
            Assert.That(_players[2].HasActed, Is.True);  // Raiser still marked as acted
        }
        
        [Test]
        public void HandlePlayerAction_Check_MarksPlayerAsActed()
        {
            // Arrange
            _bettingRound.StartBettingRound(_players, 5, 10);
            _bettingRound.PostBlinds(0, 1);
            
            // Set current bet to 0 to allow checking
            _bettingRound.CurrentBet = 0;
            
            // Player 3 checks
            var checkAction = new PlayerAction(ActionType.Check, 0, "player3");
            
            // Act
            _bettingRound.HandlePlayerAction(checkAction);
            
            // Assert
            Assert.That(_players[2].HasActed, Is.True);
            Assert.That(_bettingRound.TotalPot, Is.EqualTo(15)); // Unchanged
        }
        
        [Test]
        public void HandlePlayerAction_Fold_MarksPlayerAsFolded()
        {
            // Arrange
            _bettingRound.StartBettingRound(_players, 5, 10);
            _bettingRound.PostBlinds(0, 1);
            
            // Player 3 folds
            var foldAction = new PlayerAction(ActionType.Fold, 0, "player3");
            
            // Act
            _bettingRound.HandlePlayerAction(foldAction);
            
            // Assert
            Assert.That(_players[2].HasFolded, Is.True);
            Assert.That(_players[2].HasActed, Is.True);
            Assert.That(_bettingRound.TotalPot, Is.EqualTo(15)); // Unchanged
        }
        
        [Test]
        public void IsBettingRoundComplete_WhenAllPlayersActedWithSameBet_ReturnsTrue()
        {
            // Arrange
            _bettingRound.StartBettingRound(_players, 5, 10);
            _bettingRound.PostBlinds(0, 1);
        
            // First player calls
            var action1 = new PlayerAction(ActionType.Call, 10, "player1"); // Match big blind (adding 5 more to their SB)
            _bettingRound.HandlePlayerAction(action1);
        
            // Third player calls
            var action3 = new PlayerAction(ActionType.Call, 10, "player3");
            _bettingRound.HandlePlayerAction(action3);
        
            // Big blind checks
            var action2 = new PlayerAction(ActionType.Check, 0, "player2");
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
            var action1 = new PlayerAction(ActionType.Call, 10, "player1"); // Match big blind (adding 5 more to their SB)
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
            var action3 = new PlayerAction(ActionType.Raise, 20, "player3");
            _bettingRound.HandlePlayerAction(action3);
        
            // First player calls
            var action1 = new PlayerAction(ActionType.Call, 20, "player1");
            _bettingRound.HandlePlayerAction(action1);
        
            // Second player hasn't acted on the raise yet
        
            // Act
            bool isComplete = _bettingRound.IsBettingRoundComplete();
        
            // Assert
            Assert.IsFalse(isComplete);
        }
        
        [Test]
        public void IsBettingRoundComplete_WhenAllButOneFold_ReturnsTrue()
        {
            // Arrange
            _bettingRound.StartBettingRound(_players, 5, 10);
            _bettingRound.PostBlinds(0, 1);
        
            // Second player folds
            var action2 = new PlayerAction(ActionType.Fold, 0, "player2");
            _bettingRound.HandlePlayerAction(action2);
        
            // Third player folds
            var action3 = new PlayerAction(ActionType.Fold, 0, "player3");
            _bettingRound.HandlePlayerAction(action3);
        
            // Act
            bool isComplete = _bettingRound.IsBettingRoundComplete();
        
            // Assert
            Assert.IsTrue(isComplete);
        }
        
        [Test]
        public void ResetPlayerBets_ClearsAllPlayerBets()
        {
            // Arrange
            _bettingRound.StartBettingRound(_players, 5, 10);
            _bettingRound.PostBlinds(0, 1);
        
            // Act
            _bettingRound.ResetPlayerBets();
        
            // Assert
            foreach (var player in _players)
            {
                Assert.That(player.CurrentBet, Is.EqualTo(0));
            }
            Assert.That(_bettingRound.CurrentBet, Is.EqualTo(0));
        }
        
        [Test]
        public void GetWinner_WhenOnlyOnePlayerNotFolded_ReturnsThatPlayer()
        {
            // Arrange
            _bettingRound.StartBettingRound(_players, 5, 10);
            _bettingRound.PostBlinds(0, 1);
        
            // Two players fold
            _players[0].HasFolded = true;
            _players[2].HasFolded = true;
        
            // Act
            var winner = _bettingRound.GetWinnerByFold();
        
            // Assert
            Assert.That(winner, Is.EqualTo(_players[1]));
        }
        
        [Test]
        public void GetWinner_WhenMultiplePlayersNotFolded_ReturnsNull()
        {
            // Arrange
            _bettingRound.StartBettingRound(_players, 5, 10);
            _bettingRound.PostBlinds(0, 1);
        
            // One player folds
            _players[0].HasFolded = true;
        
            // Act
            var winner = _bettingRound.GetWinnerByFold();
        
            // Assert
            Assert.IsNull(winner);
        }
    }
}