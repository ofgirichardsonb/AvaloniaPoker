using NUnit.Framework;
using Moq;
using PokerGame.Core.Models;
using PokerGame.Core.Game;
using PokerGame.Core.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using CardModel = PokerGame.Core.Models.Card;

namespace PokerGame.Tests.Core.Game
{
    [TestFixture]
    public class PokerGameEngineTests
    {
        private PokerGameEngine _gameEngine;
        private Mock<ICardDeckService> _mockCardDeckService;
        private List<Player> _players;
        private readonly int _startingChips = 1000;
        private readonly int _bigBlind = 10;
        private readonly int _smallBlind = 5;
        private readonly int _maxBet = 100;

        [SetUp]
        public void Setup()
        {
            // Setup players
            _players = new List<Player>
            {
                new Player("player1", "Player 1", _startingChips),
                new Player("player2", "Player 2", _startingChips)
            };

            // Setup mock card deck service
            _mockCardDeckService = new Mock<ICardDeckService>();
            _mockCardDeckService.Setup(s => s.GetShuffledDeck()).Returns(GetMockDeck());
            _mockCardDeckService.Setup(s => s.DrawCard()).Returns(() => GetMockDeck().First());

            // Setup game engine
            _gameEngine = new PokerGameEngine
            {
                CardDeckService = _mockCardDeckService.Object,
                BigBlindAmount = _bigBlind,
                SmallBlindAmount = _smallBlind,
                MaxBet = _maxBet
            };
            
            // Add players to the game
            foreach (var player in _players)
            {
                _gameEngine.AddPlayer(player);
            }
        }
        
        [Test]
        public void StartHand_InitializesGameState()
        {
            // Act
            _gameEngine.StartHand();
            
            // Assert
            Assert.That(_gameEngine.GameState, Is.EqualTo(GameState.PreFlop));
            Assert.That(_gameEngine.CurrentBettingRound, Is.Not.Null);
            
            // Each player should have 2 hole cards
            foreach (var player in _gameEngine.Players)
            {
                Assert.That(player.HoleCards.Count, Is.EqualTo(2));
            }
            
            // Blinds should be posted
            Assert.That(_players[0].CurrentBet, Is.EqualTo(_smallBlind)); // Small blind
            Assert.That(_players[1].CurrentBet, Is.EqualTo(_bigBlind)); // Big blind
            
            // Chip counts should be reduced by blind amounts
            Assert.That(_players[0].ChipCount, Is.EqualTo(_startingChips - _smallBlind));
            Assert.That(_players[1].ChipCount, Is.EqualTo(_startingChips - _bigBlind));
        }
        
        [Test]
        public void StartFlop_AddsThreeCardsToCommunitCards()
        {
            // Arrange
            _gameEngine.StartHand();
            _gameEngine.GameState = GameState.PreFlop; // Ensure correct state
            
            // Act
            _gameEngine.StartFlop();
            
            // Assert
            Assert.That(_gameEngine.CommunityCards.Count, Is.EqualTo(3));
            Assert.That(_gameEngine.GameState, Is.EqualTo(GameState.Flop));
        }
        
        [Test]
        public void StartTurn_AddsFourthCardToCommunitCards()
        {
            // Arrange
            _gameEngine.StartHand();
            _gameEngine.GameState = GameState.Flop;
            _gameEngine.CommunityCards = new List<CardModel>
            {
                new CardModel(Rank.Two, Suit.Hearts),
                new CardModel(Rank.Three, Suit.Diamonds),
                new CardModel(Rank.Four, Suit.Clubs)
            };
            
            // Act
            _gameEngine.StartTurn();
            
            // Assert
            Assert.That(_gameEngine.CommunityCards.Count, Is.EqualTo(4));
            Assert.That(_gameEngine.GameState, Is.EqualTo(GameState.Turn));
        }
        
        [Test]
        public void StartRiver_AddsFifthCardToCommunitCards()
        {
            // Arrange
            _gameEngine.StartHand();
            _gameEngine.GameState = GameState.Turn;
            _gameEngine.CommunityCards = new List<CardModel>
            {
                new CardModel(Rank.Two, Suit.Hearts),
                new CardModel(Rank.Three, Suit.Diamonds),
                new CardModel(Rank.Four, Suit.Clubs),
                new CardModel(Rank.Five, Suit.Spades)
            };
            
            // Act
            _gameEngine.StartRiver();
            
            // Assert
            Assert.That(_gameEngine.CommunityCards.Count, Is.EqualTo(5));
            Assert.That(_gameEngine.GameState, Is.EqualTo(GameState.River));
        }
        
        [Test]
        public void StartShowdown_MovesToShowdownState()
        {
            // Arrange
            _gameEngine.StartHand();
            _gameEngine.GameState = GameState.River;
            
            // Act
            _gameEngine.StartShowdown();
            
            // Assert
            Assert.That(_gameEngine.GameState, Is.EqualTo(GameState.Showdown));
        }
        
        [Test]
        public void GetWinners_ReturnsCorrectWinners()
        {
            // Arrange
            _gameEngine.GameState = GameState.River;
            _players[0].HoleCards = new List<CardModel>
            {
                new CardModel(Rank.Ace, Suit.Spades),
                new CardModel(Rank.King, Suit.Spades)
            };
            _players[1].HoleCards = new List<CardModel>
            {
                new CardModel(Rank.Two, Suit.Hearts),
                new CardModel(Rank.Three, Suit.Diamonds)
            };
            _gameEngine.CommunityCards = new List<CardModel>
            {
                new CardModel(Rank.Ten, Suit.Spades),
                new CardModel(Rank.Jack, Suit.Spades),
                new CardModel(Rank.Queen, Suit.Spades),
                new CardModel(Rank.Nine, Suit.Hearts),
                new CardModel(Rank.Two, Suit.Clubs)
            };
            
            // With these cards, player 1 has a royal flush, player 2 has a pair of 2s
            
            // Act
            var winners = _gameEngine.GetWinners();
            
            // Assert
            Assert.That(winners.Count, Is.EqualTo(1));
            Assert.That(winners[0].Player.Id, Is.EqualTo(_players[0].Id));
        }

        [Test]
        public void ProcessPlayerAction_CallAction_UpdatesPlayerStateCorrectly()
        {
            // Arrange
            _gameEngine.StartHand(); // Setup game state
            var player = _gameEngine.Players.ElementAt(0);
            var action = new PlayerAction(ActionType.Call, _bigBlind, player.Id); // Match the big blind
            
            // Capture player's chip count before action
            var initialChipCount = player.ChipCount;
            
            // Act
            _gameEngine.ProcessPlayerAction(action);
            
            // Assert
            Assert.That(player.HasActed, Is.True);
            Assert.That(player.CurrentBet, Is.EqualTo(_bigBlind));
            Assert.That(player.ChipCount, Is.EqualTo(initialChipCount - _bigBlind + player.CurrentBet));
        }

        [Test]
        public void ProcessPlayerAction_RaiseAction_UpdatesPlayerStateAndCurrentBetCorrectly()
        {
            // Arrange
            _gameEngine.StartHand(); // Setup game state
            var player = _gameEngine.Players.ElementAt(0);
            var raiseAmount = _bigBlind * 2;
            var action = new PlayerAction(ActionType.Raise, raiseAmount, player.Id);
            
            // Capture player's chip count before action
            var initialChipCount = player.ChipCount;
            
            // Act
            _gameEngine.ProcessPlayerAction(action);
            
            // Assert
            Assert.That(player.HasActed, Is.True);
            Assert.That(player.CurrentBet, Is.EqualTo(raiseAmount));
            Assert.That(_gameEngine.CurrentBettingRound.CurrentBet, Is.EqualTo(raiseAmount));
            Assert.That(player.ChipCount, Is.EqualTo(initialChipCount - raiseAmount + player.CurrentBet));
        }

        [Test]
        public void ProcessPlayerAction_FoldAction_MarksPlayerAsFolded()
        {
            // Arrange
            _gameEngine.StartHand(); // Setup game state
            var player = _gameEngine.Players.ElementAt(0);
            var action = new PlayerAction(ActionType.Fold, 0, player.Id);
            
            // Act
            _gameEngine.ProcessPlayerAction(action);
            
            // Assert
            Assert.That(player.HasFolded, Is.True);
            Assert.That(player.HasActed, Is.True);
        }

        [Test]
        public void ProcessPlayerAction_CheckAction_MarksPlayerAsActed()
        {
            // Arrange
            _gameEngine.GameState = GameState.Flop;
            _gameEngine.StartBettingRound();
            var player = _gameEngine.Players.ElementAt(0);
            player.HasActed = false;
            var action = new PlayerAction(ActionType.Check, 0, player.Id);
            
            // Act
            _gameEngine.ProcessPlayerAction(action);
            
            // Assert
            Assert.That(player.HasActed, Is.True);
            Assert.That(player.CurrentBet, Is.EqualTo(0));
        }

        [Test]
        public void EndHand_ResetsBetsAndMovesDealer()
        {
            // Arrange
            _gameEngine.StartHand(); // Setup game state
            
            // Act
            _gameEngine.EndHandForTesting();
            
            // Assert
            Assert.That(_gameEngine.GameState, Is.EqualTo(GameState.WaitingToStart));
            Assert.That(_gameEngine.DealerPosition, Is.EqualTo(1)); // Dealer should have moved
            Assert.That(_gameEngine.CommunityCards.Count, Is.EqualTo(0));
            
            foreach (var player in _gameEngine.Players)
            {
                Assert.That(player.CurrentBet, Is.EqualTo(0));
                Assert.That(player.HasActed, Is.False);
                Assert.That(player.HasFolded, Is.False);
                Assert.That(player.HoleCards.Count, Is.EqualTo(0));
            }
        }

        // Helper methods
        private List<CardModel> GetMockDeck()
        {
            var deck = new List<CardModel>();
            Suit[] suits = { Suit.Hearts, Suit.Diamonds, Suit.Clubs, Suit.Spades };
            Rank[] ranks = { 
                Rank.Two, Rank.Three, Rank.Four, Rank.Five, Rank.Six, Rank.Seven, 
                Rank.Eight, Rank.Nine, Rank.Ten, Rank.Jack, Rank.Queen, Rank.King, Rank.Ace 
            };
            
            foreach (var suit in suits)
            {
                foreach (var rank in ranks)
                {
                    deck.Add(new CardModel(rank, suit));
                }
            }
            
            return deck;
        }
    }
}