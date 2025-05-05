using NUnit.Framework;
using Moq;
using PokerGame.Core.Models;
using PokerGame.Core.Game;
using PokerGame.Core.Microservices;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using CardModel = PokerGame.Core.Models.Card;

namespace PokerGame.Tests.Core.Game
{
    [TestFixture]
    public class PokerGameEngineTests
    {
        private PokerGameEngine _engine;
        private Mock<ICardDeckService> _mockCardDeckService;
        private List<Player> _players;
        private readonly int _initialChips = 1000;
        private readonly int _bigBlind = 10;
        private readonly int _smallBlind = 5;
        private readonly int _maxBet = 100;

        [SetUp]
        public void Setup()
        {
            _mockCardDeckService = new Mock<ICardDeckService>();
            _players = new List<Player>
            {
                new Player { Id = "player1", Name = "Player 1", ChipCount = _initialChips, HoleCards = new List<CardModel>() },
                new Player { Id = "player2", Name = "Player 2", ChipCount = _initialChips, HoleCards = new List<CardModel>() },
                new Player { Id = "player3", Name = "Player 3", ChipCount = _initialChips, HoleCards = new List<CardModel>() }
            };

            _engine = new PokerGameEngine(_mockCardDeckService.Object)
            {
                BigBlind = _bigBlind,
                SmallBlind = _smallBlind,
                MaxBet = _maxBet
            };
            
            // Set up the mock card deck service to return specific cards
            SetupMockCardDeckService();
            
            // Add players to the game
            foreach (var player in _players)
            {
                _engine.AddPlayer(player);
            }
        }

        private void SetupMockCardDeckService()
        {
            // Set up mock for DealHoleCards
            _mockCardDeckService.Setup(m => m.DealHoleCards(It.IsAny<Player>()))
                .Callback<Player>(player => 
                {
                    player.HoleCards = new List<CardModel>
                    {
                        new CardModel { Rank = "A", Suit = "Hearts" },
                        new CardModel { Rank = "K", Suit = "Diamonds" }
                    };
                })
                .Returns(Task.CompletedTask);
                
            // Set up mock for dealing community cards
            _mockCardDeckService.Setup(m => m.DealCommunityCards(It.IsAny<int>()))
                .Returns<int>(count => 
                {
                    var cards = new List<CardModel>();
                    if (count == 3) // Flop
                    {
                        cards.Add(new CardModel { Rank = "10", Suit = "Clubs" });
                        cards.Add(new CardModel { Rank = "J", Suit = "Clubs" });
                        cards.Add(new CardModel { Rank = "Q", Suit = "Clubs" });
                    }
                    else if (count == 1 && _engine.CommunityCards.Count == 3) // Turn
                    {
                        cards.Add(new CardModel { Rank = "K", Suit = "Clubs" });
                    }
                    else if (count == 1 && _engine.CommunityCards.Count == 4) // River
                    {
                        cards.Add(new CardModel { Rank = "A", Suit = "Clubs" });
                    }
                    return Task.FromResult(cards);
                });
                
            // Set up mock for shuffling the deck
            _mockCardDeckService.Setup(m => m.ShuffleDeck())
                .Returns(Task.CompletedTask);
        }

        [Test]
        public void StartHand_SetsCorrectGameState()
        {
            // Act
            _engine.StartHand();
            
            // Assert
            Assert.That(_engine.CurrentState, Is.EqualTo(GameState.PreFlop));
            Assert.That(_engine.Pot, Is.EqualTo(_smallBlind + _bigBlind));
            Assert.That(_engine.CurrentBet, Is.EqualTo(_bigBlind));
            
            // Check small blind and big blind players
            var smallBlindPlayer = _players[0]; // Dealer is 0, small blind is 1, big blind is 2, but after StartHand current player is 0
            var bigBlindPlayer = _players[1]; 
            
            Assert.That(smallBlindPlayer.CurrentBet, Is.EqualTo(_smallBlind));
            Assert.That(bigBlindPlayer.CurrentBet, Is.EqualTo(_bigBlind));
            
            // Verify that hole cards were dealt to all players
            _mockCardDeckService.Verify(m => m.DealHoleCards(It.IsAny<Player>()), Times.Exactly(_players.Count));
        }
        
        [Test]
        public void ProcessAction_Call_UpdatesPlayerAndGameState()
        {
            // Arrange
            _engine.StartHand();
            var currentPlayer = _engine.GetCurrentPlayer();
            var initialChipCount = currentPlayer.ChipCount;
            
            // Act
            _engine.ProcessAction(new PlayerAction
            {
                PlayerId = currentPlayer.Id,
                ActionType = PlayerActionType.Call,
                Amount = _engine.CurrentBet
            });
            
            // Assert
            Assert.That(currentPlayer.CurrentBet, Is.EqualTo(_engine.CurrentBet));
            Assert.That(currentPlayer.ChipCount, Is.EqualTo(initialChipCount - _engine.CurrentBet));
            Assert.That(currentPlayer.HasActed, Is.True);
        }
        
        [Test]
        public void ProcessAction_Raise_UpdatesCurrentBet()
        {
            // Arrange
            _engine.StartHand();
            var currentPlayer = _engine.GetCurrentPlayer();
            var initialChipCount = currentPlayer.ChipCount;
            var raiseAmount = _engine.CurrentBet * 2;
            
            // Act
            _engine.ProcessAction(new PlayerAction
            {
                PlayerId = currentPlayer.Id,
                ActionType = PlayerActionType.Raise,
                Amount = raiseAmount
            });
            
            // Assert
            Assert.That(_engine.CurrentBet, Is.EqualTo(raiseAmount));
            Assert.That(currentPlayer.CurrentBet, Is.EqualTo(raiseAmount));
            Assert.That(currentPlayer.ChipCount, Is.EqualTo(initialChipCount - raiseAmount));
            Assert.That(currentPlayer.HasActed, Is.True);
            
            // Other players should not have HasActed = true since they need to respond to the raise
            var otherPlayers = _players.Where(p => p.Id != currentPlayer.Id).ToList();
            Assert.That(otherPlayers.All(p => !p.HasActed), Is.True);
        }
        
        [Test]
        public void ProcessAction_Fold_UpdatesPlayerState()
        {
            // Arrange
            _engine.StartHand();
            var currentPlayer = _engine.GetCurrentPlayer();
            
            // Act
            _engine.ProcessAction(new PlayerAction
            {
                PlayerId = currentPlayer.Id,
                ActionType = PlayerActionType.Fold
            });
            
            // Assert
            Assert.That(currentPlayer.HasFolded, Is.True);
            Assert.That(currentPlayer.HasActed, Is.True);
        }
        
        [Test]
        public void ProcessAction_Check_WhenPossible_UpdatesPlayerState()
        {
            // Arrange - advance to flop where all players can check
            _engine.StartHand();
            
            // Have all players call
            foreach (var player in _players)
            {
                _engine.ProcessAction(new PlayerAction
                {
                    PlayerId = player.Id,
                    ActionType = PlayerActionType.Call,
                    Amount = _engine.CurrentBet
                });
            }
            
            // Should now be in flop state where checking is possible
            Assert.That(_engine.CurrentState, Is.EqualTo(GameState.Flop));
            Assert.That(_engine.CurrentBet, Is.EqualTo(0)); // No bet during flop yet
            
            var currentPlayer = _engine.GetCurrentPlayer();
            
            // Act
            _engine.ProcessAction(new PlayerAction
            {
                PlayerId = currentPlayer.Id,
                ActionType = PlayerActionType.Check
            });
            
            // Assert
            Assert.That(currentPlayer.HasActed, Is.True);
            Assert.That(currentPlayer.CurrentBet, Is.EqualTo(0));
        }
        
        [Test]
        public void AdvanceToNextBettingRound_FlopsThreeCards()
        {
            // Arrange
            _engine.StartHand();
            
            // Have all players call
            foreach (var player in _players)
            {
                _engine.ProcessAction(new PlayerAction
                {
                    PlayerId = player.Id,
                    ActionType = PlayerActionType.Call,
                    Amount = _engine.CurrentBet
                });
            }
            
            // Assert
            Assert.That(_engine.CurrentState, Is.EqualTo(GameState.Flop));
            Assert.That(_engine.CommunityCards.Count, Is.EqualTo(3)); // Three cards for the flop
            
            // Verify the flop was dealt
            _mockCardDeckService.Verify(m => m.DealCommunityCards(3), Times.Once);
        }
        
        [Test]
        public void AdvanceToNextBettingRound_CompleteFlow_EndsWithShowdown()
        {
            // Arrange
            _engine.StartHand();
            
            // Move through all betting rounds
            for (int round = 0; round < 4; round++) // PreFlop, Flop, Turn, River
            {
                // Have all players call or check
                foreach (var player in _players)
                {
                    PlayerActionType action = _engine.CurrentBet > 0 ? PlayerActionType.Call : PlayerActionType.Check;
                    _engine.ProcessAction(new PlayerAction
                    {
                        PlayerId = player.Id,
                        ActionType = action,
                        Amount = _engine.CurrentBet
                    });
                }
            }
            
            // Assert
            Assert.That(_engine.CurrentState, Is.EqualTo(GameState.Showdown));
            Assert.That(_engine.CommunityCards.Count, Is.EqualTo(5)); // All five community cards
            
            // Verify all cards were dealt
            _mockCardDeckService.Verify(m => m.DealCommunityCards(3), Times.Once); // Flop
            _mockCardDeckService.Verify(m => m.DealCommunityCards(1), Times.Exactly(2)); // Turn and River
        }
        
        [Test]
        public void ProcessAction_AllButOneFold_EndsHandWithWinner()
        {
            // Arrange
            _engine.StartHand();
            
            // Have all but the last player fold
            for (int i = 0; i < _players.Count - 1; i++)
            {
                _engine.ProcessAction(new PlayerAction
                {
                    PlayerId = _engine.GetCurrentPlayer().Id,
                    ActionType = PlayerActionType.Fold
                });
            }
            
            // Assert
            Assert.That(_engine.CurrentState, Is.EqualTo(GameState.Showdown));
            
            // The last player should be the winner (hasn't folded)
            var winner = _players.SingleOrDefault(p => !p.HasFolded);
            Assert.That(winner, Is.Not.Null);
            
            // Pot should go to the winner
            var expectedWinnings = _engine.Pot;
            var initialChips = _initialChips;
            
            _engine.DetermineWinners();
            
            Assert.That(winner.ChipCount, Is.GreaterThan(initialChips));
            Assert.That(_engine.Pot, Is.EqualTo(0)); // Pot should be empty after determining winners
        }
        
        [Test]
        public void DetermineWinners_CalculatesCorrectWinnings()
        {
            // Arrange
            _engine.StartHand();
            
            // All players call, advancing to Flop
            foreach (var player in _players)
            {
                _engine.ProcessAction(new PlayerAction
                {
                    PlayerId = player.Id,
                    ActionType = PlayerActionType.Call,
                    Amount = _engine.CurrentBet
                });
            }
            
            // On the flop, everyone checks
            foreach (var player in _players)
            {
                _engine.ProcessAction(new PlayerAction
                {
                    PlayerId = player.Id,
                    ActionType = PlayerActionType.Check
                });
            }
            
            // Turn: everyone checks
            foreach (var player in _players)
            {
                _engine.ProcessAction(new PlayerAction
                {
                    PlayerId = player.Id,
                    ActionType = PlayerActionType.Check
                });
            }
            
            // River: everyone checks, moves to showdown
            foreach (var player in _players)
            {
                _engine.ProcessAction(new PlayerAction
                {
                    PlayerId = player.Id,
                    ActionType = PlayerActionType.Check
                });
            }
            
            Assert.That(_engine.CurrentState, Is.EqualTo(GameState.Showdown));
            
            // Remember initial chip counts and pot total
            var initialChips = _players.ToDictionary(p => p.Id, p => p.ChipCount);
            var totalPot = _engine.Pot;
            
            // Act
            _engine.DetermineWinners();
            
            // Assert
            Assert.That(_engine.Pot, Is.EqualTo(0)); // Pot should be empty
            
            // At least one player should have more chips than they started with
            Assert.That(_players.Any(p => p.ChipCount > initialChips[p.Id]), Is.True);
            
            // Total chips in the system should remain constant
            var totalChipsAfter = _players.Sum(p => p.ChipCount);
            var totalChipsBefore = initialChips.Values.Sum() + totalPot;
            Assert.That(totalChipsAfter, Is.EqualTo(totalChipsBefore));
        }
        
        [Test]
        public void ResetPlayerStateForNewHand_ClearsPlayerState()
        {
            // Arrange
            _engine.StartHand();
            
            // Simulate some actions
            var currentPlayer = _engine.GetCurrentPlayer();
            _engine.ProcessAction(new PlayerAction
            {
                PlayerId = currentPlayer.Id,
                ActionType = PlayerActionType.Raise,
                Amount = _engine.CurrentBet * 2
            });
            
            // Act
            _engine.ResetPlayerStateForNewHand();
            
            // Assert
            foreach (var player in _players)
            {
                Assert.That(player.CurrentBet, Is.EqualTo(0));
                Assert.That(player.HasActed, Is.False);
                Assert.That(player.HasFolded, Is.False);
                Assert.That(player.HoleCards, Is.Empty);
            }
        }
    }
}