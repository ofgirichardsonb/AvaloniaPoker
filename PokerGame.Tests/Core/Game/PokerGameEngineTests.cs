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
                new Player { Id = "player1", Name = "Player 1", ChipCount = _startingChips, HoleCards = new List<CardModel>() },
                new Player { Id = "player2", Name = "Player 2", ChipCount = _startingChips, HoleCards = new List<CardModel>() }
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
        public void Constructor_InitializesPropertiesCorrectly()
        {
            // Arrange
            var gameEngine = new PokerGameEngine();
            
            // Assert
            Assert.That(gameEngine.GameState, Is.EqualTo(GameState.WaitingToStart));
            Assert.That(gameEngine.Players, Is.Not.Null);
            Assert.That(gameEngine.CommunityCards, Is.Not.Null);
            Assert.That(gameEngine.CurrentBettingRound, Is.Null);
        }

        [Test]
        public void AddPlayer_AddsPlayerToPlayersList()
        {
            // Arrange
            var gameEngine = new PokerGameEngine();
            var player = new Player { Id = "test-player", Name = "Test Player", ChipCount = 1000 };
            
            // Act
            gameEngine.AddPlayer(player);
            
            // Assert
            Assert.That(gameEngine.Players, Contains.Item(player));
            Assert.That(gameEngine.Players.Count, Is.EqualTo(1));
        }

        [Test]
        public void RemovePlayer_RemovesPlayerFromPlayersList()
        {
            // Arrange
            var gameEngine = new PokerGameEngine();
            var player = new Player { Id = "test-player", Name = "Test Player", ChipCount = 1000 };
            gameEngine.AddPlayer(player);
            
            // Act
            gameEngine.RemovePlayer(player.Id);
            
            // Assert
            Assert.That(gameEngine.Players, Does.Not.Contain(player));
            Assert.That(gameEngine.Players.Count, Is.EqualTo(0));
        }

        [Test]
        public void StartHand_DealsHoleCardsToPlayers()
        {
            // Arrange
            _gameEngine.GameState = GameState.WaitingToStart;
            
            // Act
            _gameEngine.StartHand();
            
            // Assert
            Assert.That(_gameEngine.GameState, Is.EqualTo(GameState.PreFlop));
            foreach (var player in _gameEngine.Players)
            {
                Assert.That(player.HoleCards.Count, Is.EqualTo(2));
            }
        }

        [Test]
        public void StartHand_AssignsBlindCorrectly()
        {
            // Arrange
            _gameEngine.GameState = GameState.WaitingToStart;
            
            // Act
            _gameEngine.StartHand();
            
            // Assert
            Assert.That(_gameEngine.DealerPosition, Is.EqualTo(0));
            
            // Small blind should be player after dealer (position 1 in a 2-player game)
            var smallBlindPlayer = _gameEngine.Players.ElementAt(1);
            Assert.That(smallBlindPlayer.CurrentBet, Is.EqualTo(_smallBlind));
            Assert.That(smallBlindPlayer.ChipCount, Is.EqualTo(_startingChips - _smallBlind));
            
            // Big blind should be player after small blind (position 0 in a 2-player game with wrap-around)
            var bigBlindPlayer = _gameEngine.Players.ElementAt(0);
            Assert.That(bigBlindPlayer.CurrentBet, Is.EqualTo(_bigBlind));
            Assert.That(bigBlindPlayer.ChipCount, Is.EqualTo(_startingChips - _bigBlind));
        }

        [Test]
        public void DealFlop_DealThreeCardsToTable()
        {
            // Arrange
            _gameEngine.GameState = GameState.PreFlop;
            _gameEngine.CommunityCards.Clear();
            
            // Act
            _gameEngine.DealFlop();
            
            // Assert
            Assert.That(_gameEngine.GameState, Is.EqualTo(GameState.Flop));
            Assert.That(_gameEngine.CommunityCards.Count, Is.EqualTo(3));
        }

        [Test]
        public void DealTurn_DealsFourthCommunityCard()
        {
            // Arrange
            _gameEngine.GameState = GameState.Flop;
            _gameEngine.CommunityCards.Clear();
            _gameEngine.CommunityCards.AddRange(GetMockCommunityCards(3)); // Add 3 cards for flop
            
            // Act
            _gameEngine.DealTurn();
            
            // Assert
            Assert.That(_gameEngine.GameState, Is.EqualTo(GameState.Turn));
            Assert.That(_gameEngine.CommunityCards.Count, Is.EqualTo(4));
        }

        [Test]
        public void DealRiver_DealsFifthCommunityCard()
        {
            // Arrange
            _gameEngine.GameState = GameState.Turn;
            _gameEngine.CommunityCards.Clear();
            _gameEngine.CommunityCards.AddRange(GetMockCommunityCards(4)); // Add 4 cards for turn
            
            // Act
            _gameEngine.DealRiver();
            
            // Assert
            Assert.That(_gameEngine.GameState, Is.EqualTo(GameState.River));
            Assert.That(_gameEngine.CommunityCards.Count, Is.EqualTo(5));
        }

        [Test]
        public void StartBettingRound_CreatesNewBettingRound()
        {
            // Arrange
            _gameEngine.GameState = GameState.PreFlop;
            _gameEngine.CurrentBettingRound = null;
            
            // Act
            _gameEngine.StartBettingRound();
            
            // Assert
            Assert.That(_gameEngine.CurrentBettingRound, Is.Not.Null);
            Assert.That(_gameEngine.CurrentBettingRound.Players.Count, Is.EqualTo(_gameEngine.Players.Count));
            Assert.That(_gameEngine.CurrentBettingRound.CurrentBet, Is.EqualTo(_bigBlind));
        }

        [Test]
        public void GetWinners_ReturnsCorrectWinners()
        {
            // Arrange
            _gameEngine.GameState = GameState.River;
            _players[0].HoleCards = new List<CardModel>
            {
                new CardModel { Rank = "A", Suit = "Spades" },
                new CardModel { Rank = "K", Suit = "Spades" }
            };
            _players[1].HoleCards = new List<CardModel>
            {
                new CardModel { Rank = "2", Suit = "Hearts" },
                new CardModel { Rank = "3", Suit = "Diamonds" }
            };
            _gameEngine.CommunityCards = new List<CardModel>
            {
                new CardModel { Rank = "10", Suit = "Spades" },
                new CardModel { Rank = "J", Suit = "Spades" },
                new CardModel { Rank = "Q", Suit = "Spades" },
                new CardModel { Rank = "9", Suit = "Hearts" },
                new CardModel { Rank = "2", Suit = "Clubs" }
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
            var action = new PlayerAction
            {
                ActionType = PlayerActionType.Call,
                PlayerId = player.Id,
                Amount = _bigBlind // Match the big blind
            };
            
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
            var action = new PlayerAction
            {
                ActionType = PlayerActionType.Raise,
                PlayerId = player.Id,
                Amount = raiseAmount
            };
            
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
            var action = new PlayerAction
            {
                ActionType = PlayerActionType.Fold,
                PlayerId = player.Id
            };
            
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
            var action = new PlayerAction
            {
                ActionType = PlayerActionType.Check,
                PlayerId = player.Id
            };
            
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
            string[] suits = { "Hearts", "Diamonds", "Clubs", "Spades" };
            string[] ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };
            
            foreach (var suit in suits)
            {
                foreach (var rank in ranks)
                {
                    deck.Add(new CardModel { Rank = rank, Suit = suit });
                }
            }
            
            return deck;
        }
        
        private List<CardModel> GetMockCommunityCards(int count)
        {
            var cards = new List<CardModel>();
            var deck = GetMockDeck();
            
            for (int i = 0; i < count; i++)
            {
                cards.Add(deck[i]);
            }
            
            return cards;
        }
    }
}