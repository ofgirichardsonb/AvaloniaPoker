using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using PokerGame.Core.Game;
using PokerGame.Core.Interfaces;
using PokerGame.Core.Models;
using GameState = PokerGame.Core.Game.GameState;
using ReactiveUI;

namespace PokerGame.Avalonia.ViewModels
{
    /// <summary>
    /// View model for the poker game
    /// </summary>
    public class GameViewModel : ViewModelBase, IPokerGameUI
    {
        private PokerGameEngine _gameEngine;
        private ObservableCollection<PlayerViewModel> _players = new ObservableCollection<PlayerViewModel>();
        private ObservableCollection<CardViewModel> _communityCards = new ObservableCollection<CardViewModel>();
        private int _pot;
        private string _gameStatus = "Welcome to Texas Hold'em Poker!";
        private string _currentBet = "0";
        private PlayerViewModel? _currentPlayer;
        private bool _canCheck;
        private bool _canCall;
        private bool _canRaise;
        private bool _canFold;
        private bool _canStartHand;
        private int _minRaiseAmount = 20;
        private int _raiseAmount = 20;
        private string _logMessages = "";
        
        public GameViewModel()
        {
            // Create commands
            CheckCommand = ReactiveCommand.Create(ExecuteCheck);
            CallCommand = ReactiveCommand.Create(ExecuteCall);
            FoldCommand = ReactiveCommand.Create(ExecuteFold);
            RaiseCommand = ReactiveCommand.Create(ExecuteRaise);
            StartHandCommand = ReactiveCommand.Create(ExecuteStartHand);
            AddPlayerCommand = ReactiveCommand.Create(ExecuteAddPlayer);
            
            // Create game engine
            _gameEngine = new PokerGameEngine(this);
            
            // Initialize with some players for testing
            _gameEngine.StartGame(new[] { "Player 1", "Player 2", "Player 3", "Player 4" });
            
            // Enable start hand button
            _canStartHand = true;
        }
        
        #region Properties
        
        /// <summary>
        /// Gets the collection of players
        /// </summary>
        public ObservableCollection<PlayerViewModel> Players => _players;
        
        /// <summary>
        /// Gets the collection of community cards
        /// </summary>
        public ObservableCollection<CardViewModel> CommunityCards => _communityCards;
        
        /// <summary>
        /// Gets or sets the pot amount
        /// </summary>
        public int Pot
        {
            get => _pot;
            set => SetProperty(ref _pot, value);
        }
        
        /// <summary>
        /// Gets or sets the game status message
        /// </summary>
        public string GameStatus
        {
            get => _gameStatus;
            set => SetProperty(ref _gameStatus, value);
        }
        
        /// <summary>
        /// Gets or sets the current bet amount
        /// </summary>
        public string CurrentBet
        {
            get => _currentBet;
            set => SetProperty(ref _currentBet, value);
        }
        
        /// <summary>
        /// Gets or sets the current player
        /// </summary>
        public PlayerViewModel? CurrentPlayer
        {
            get => _currentPlayer;
            set 
            {
                if (_currentPlayer == value)
                    return;
                
                // Clear any previous player's current status
                if (_currentPlayer != null)
                {
                    _currentPlayer.IsCurrent = false;
                }
                
                // Set the new current player
                _currentPlayer = value;
                
                // Set new player's current status
                if (_currentPlayer != null)
                {
                    _currentPlayer.IsCurrent = true;
                }
                
                // Notify property changed
                RaisePropertyChanged(nameof(CurrentPlayer));
            }
        }
        
        /// <summary>
        /// Gets or sets whether the check action is available
        /// </summary>
        public bool CanCheck
        {
            get => _canCheck;
            set => SetProperty(ref _canCheck, value);
        }
        
        /// <summary>
        /// Gets or sets whether the call action is available
        /// </summary>
        public bool CanCall
        {
            get => _canCall;
            set => SetProperty(ref _canCall, value);
        }
        
        /// <summary>
        /// Gets or sets whether the raise action is available
        /// </summary>
        public bool CanRaise
        {
            get => _canRaise;
            set => SetProperty(ref _canRaise, value);
        }
        
        /// <summary>
        /// Gets or sets whether the fold action is available
        /// </summary>
        public bool CanFold
        {
            get => _canFold;
            set => SetProperty(ref _canFold, value);
        }
        
        /// <summary>
        /// Gets or sets whether the start hand action is available
        /// </summary>
        public bool CanStartHand
        {
            get => _canStartHand;
            set => SetProperty(ref _canStartHand, value);
        }
        
        /// <summary>
        /// Gets or sets the minimum raise amount
        /// </summary>
        public int MinRaiseAmount
        {
            get => _minRaiseAmount;
            set => SetProperty(ref _minRaiseAmount, value);
        }
        
        /// <summary>
        /// Gets or sets the raise amount
        /// </summary>
        public int RaiseAmount
        {
            get => _raiseAmount;
            set => SetProperty(ref _raiseAmount, value);
        }
        
        /// <summary>
        /// Gets or sets the log messages
        /// </summary>
        public string LogMessages
        {
            get => _logMessages;
            set => SetProperty(ref _logMessages, value);
        }
        
        #endregion
        
        #region Commands
        
        /// <summary>
        /// Command to check
        /// </summary>
        public ReactiveCommand<Unit, Unit> CheckCommand { get; }
        
        /// <summary>
        /// Command to call
        /// </summary>
        public ReactiveCommand<Unit, Unit> CallCommand { get; }
        
        /// <summary>
        /// Command to fold
        /// </summary>
        public ReactiveCommand<Unit, Unit> FoldCommand { get; }
        
        /// <summary>
        /// Command to raise
        /// </summary>
        public ReactiveCommand<Unit, Unit> RaiseCommand { get; }
        
        /// <summary>
        /// Command to start a new hand
        /// </summary>
        public ReactiveCommand<Unit, Unit> StartHandCommand { get; }
        
        /// <summary>
        /// Command to add a new player
        /// </summary>
        public ReactiveCommand<Unit, Unit> AddPlayerCommand { get; }
        
        #endregion
        
        #region Command Handlers
        
        /// <summary>
        /// Executes the check command
        /// </summary>
        private void ExecuteCheck()
        {
            _gameEngine.ProcessPlayerAction("check");
        }
        
        /// <summary>
        /// Executes the call command
        /// </summary>
        private void ExecuteCall()
        {
            _gameEngine.ProcessPlayerAction("call");
        }
        
        /// <summary>
        /// Executes the fold command
        /// </summary>
        private void ExecuteFold()
        {
            _gameEngine.ProcessPlayerAction("fold");
        }
        
        /// <summary>
        /// Executes the raise command
        /// </summary>
        private void ExecuteRaise()
        {
            _gameEngine.ProcessPlayerAction("raise", RaiseAmount);
        }
        
        /// <summary>
        /// Executes the start hand command
        /// </summary>
        private void ExecuteStartHand()
        {
            _gameEngine.StartHand();
        }
        
        /// <summary>
        /// Executes the add player command
        /// </summary>
        private void ExecuteAddPlayer()
        {
            // In a real app, you would show a dialog to get the player name
            Random random = new Random();
            int playerNumber = _players.Count + 1;
            string playerName = $"Player {playerNumber}";
            
            // Add the player to our collection
            bool isCurrentUser = (_players.Count == 0); // First player added is the current user
            var player = new Player(Guid.NewGuid().ToString(), playerName, 1000)
            {
                IsCurrentUser = isCurrentUser // Set the IsCurrentUser flag in the model
            };
            var playerViewModel = new PlayerViewModel(player);
            _players.Add(playerViewModel);
            
            // Show a message that the player was added
            string message;
            if (isCurrentUser)
            {
                message = $"Added you to the game as {playerName}.";
                if (_players.Count == 1)
                {
                    message += " Add at least one more player to start the game.";
                }
            }
            else
            {
                message = $"Added {playerName} to the game.";
            }
            ShowMessage(message);
            
            // Only start the game when we have at least 2 players
            if (_players.Count >= 2)
            {
                // Get all player names
                List<string> playerNames = _players.Select(p => p.Name).ToList();
                
                // Start the game with all current players
                _gameEngine.StartGame(playerNames.ToArray());
            }
        }
        
        #endregion
        
        #region IPokerGameUI Implementation
        
        /// <summary>
        /// Shows a message to the user
        /// </summary>
        /// <param name="message">The message to display</param>
        public void ShowMessage(string message)
        {
            LogMessages = message + Environment.NewLine + LogMessages;
        }
        
        /// <summary>
        /// Gets an action from the current player
        /// </summary>
        /// <param name="player">The player to get the action from</param>
        /// <param name="gameEngine">The current game engine instance</param>
        public void GetPlayerAction(Player player, PokerGameEngine gameEngine)
        {
            // Find the corresponding player view model
            PlayerViewModel? playerViewModel = _players.FirstOrDefault(p => p.Name == player.Name);
            
            if (playerViewModel != null)
            {
                CurrentPlayer = playerViewModel;
                
                // Update available actions with detailed logging and enhanced state management
                bool shouldCheck = player.CurrentBet == gameEngine.CurrentBet;
                bool shouldCall = player.CurrentBet < gameEngine.CurrentBet && player.Chips > 0;
                
                // Fix for the issue where Call button is disabled when it should be enabled
                // If current bet is higher than player's bet, they MUST call or fold
                bool mustCallOrFold = gameEngine.CurrentBet > player.CurrentBet && player.Chips > 0;
                
                // Enable raising only if player has enough chips and it's not a forced call situation
                bool shouldRaise = player.Chips > 0;
                
                Console.WriteLine($"★★★★★ UI Action Evaluation for {player.Name} ★★★★★");
                Console.WriteLine($"★★★★★ Player CurrentBet: {player.CurrentBet}, GameEngine CurrentBet: {gameEngine.CurrentBet}, Player Chips: {player.Chips} ★★★★★");
                Console.WriteLine($"★★★★★ Should Check: {shouldCheck} (CurrentBet == GameEngine.CurrentBet: {player.CurrentBet == gameEngine.CurrentBet}) ★★★★★");
                Console.WriteLine($"★★★★★ Should Call: {shouldCall} (CurrentBet < GameEngine.CurrentBet: {player.CurrentBet < gameEngine.CurrentBet} AND Chips > 0: {player.Chips > 0}) ★★★★★");
                Console.WriteLine($"★★★★★ Must Call Or Fold: {mustCallOrFold} ★★★★★");
                Console.WriteLine($"★★★★★ Should Raise: {shouldRaise} (Chips > 0: {player.Chips > 0}) ★★★★★");
                
                // Set the UI button states
                CanCheck = shouldCheck;
                CanCall = shouldCall; // This MUST be true when CurrentBet < GameEngine.CurrentBet
                
                // Force-enable call button if the player must call (can't check)
                if (mustCallOrFold)
                {
                    Console.WriteLine($"★★★★★ FORCE ENABLING CALL BUTTON for {player.Name} - Must call {gameEngine.CurrentBet - player.CurrentBet} or fold ★★★★★");
                    CanCall = true;
                    CanCheck = false; // Can't check when call is required
                }
                
                CanRaise = shouldRaise;
                CanFold = true;
                
                // Update minimum raise amount
                MinRaiseAmount = gameEngine.CurrentBet + 10;
                RaiseAmount = MinRaiseAmount;
                
                // Disable start hand button during gameplay
                CanStartHand = false;
                
                // Update game status based on whether this is the current user or another player
                if (playerViewModel.IsCurrentUser)
                {
                    GameStatus = "Your turn - make your move";
                }
                else
                {
                    GameStatus = $"Waiting for {player.Name} to make a move";
                }
            }
        }
        
        /// <summary>
        /// Updates the UI with the current game state
        /// </summary>
        /// <param name="gameEngine">The current game engine instance</param>
        public void UpdateGameState(PokerGameEngine gameEngine)
        {
            // Update pot and current bet
            Pot = gameEngine.Pot;
            CurrentBet = gameEngine.CurrentBet.ToString();
            
            // Update game status
            GameStatus = $"Game State: {gameEngine.State}";
            
            // Update players
            _players.Clear();
            
            // In a multiplayer game, each player might already have their IsCurrentUser flag set
            // But for backwards compatibility, we'll default the first player as current user if none is set
            bool anyCurrentUser = gameEngine.Players.Any(p => p.IsCurrentUser);
            bool gameOver = gameEngine.State == GameState.HandComplete;
            
            if (!anyCurrentUser && gameEngine.Players.Count > 0)
            {
                // For demonstration, set the first player as current user
                gameEngine.Players[0].IsCurrentUser = true;
            }
            
            foreach (var player in gameEngine.Players)
            {
                // Create player view model - it will use the Player.IsCurrentUser value
                var playerViewModel = new PlayerViewModel(player);
                
                // Update card visibility based on game state
                playerViewModel.UpdateCardVisibility(gameOver);
                
                _players.Add(playerViewModel);
            }
            
            // Update community cards
            _communityCards.Clear();
            foreach (var card in gameEngine.CommunityCards)
            {
                // Community cards are always visible
                _communityCards.Add(new CardViewModel(card, true));
            }
            
            // If the hand is complete, enable the start hand button
            if (gameEngine.State == GameState.HandComplete || gameEngine.State == GameState.WaitingToStart)
            {
                CanStartHand = true;
                CanCheck = false;
                CanCall = false;
                CanRaise = false;
                CanFold = false;
                CurrentPlayer = null;
                
                // If the hand is complete, make all cards visible
                if (gameEngine.State == GameState.HandComplete)
                {
                    foreach (var player in _players)
                    {
                        player.UpdateCardVisibility(true);
                    }
                }
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// View model for a player
    /// </summary>
    public class PlayerViewModel : ViewModelBase
    {
        private readonly Player _player;
        private ObservableCollection<CardViewModel> _holeCards = new ObservableCollection<CardViewModel>();
        private bool _isCurrentUser;
        
        public PlayerViewModel(Player player, bool isCurrentUser = false)
        {
            _player = player;
            
            // Set current user flag, preferring the model's value if it's set
            _isCurrentUser = player.IsCurrentUser || isCurrentUser;
            
            // Create view models for hole cards
            foreach (var card in player.HoleCards)
            {
                _holeCards.Add(new CardViewModel(card, true)); // Cards are visible by default
            }
            
            // If this isn't the current user, hide the cards (show backs) unless the game is over
            UpdateCardVisibility();
        }
        
        /// <summary>
        /// Gets the player's name
        /// </summary>
        public string Name => _player.Name;
        
        /// <summary>
        /// Gets the player's chip count
        /// </summary>
        public int Chips => _player.Chips;
        
        /// <summary>
        /// Gets the player's current bet
        /// </summary>
        public int CurrentBet => _player.CurrentBet;
        
        /// <summary>
        /// Gets whether the player has folded
        /// </summary>
        public bool HasFolded => _player.HasFolded;
        
        /// <summary>
        /// Gets whether the player is all-in
        /// </summary>
        public bool IsAllIn => _player.IsAllIn;
        
        /// <summary>
        /// Gets or sets whether this player is the current user
        /// </summary>
        public bool IsCurrentUser
        {
            get => _isCurrentUser;
            set
            {
                if (SetProperty(ref _isCurrentUser, value))
                {
                    UpdateCardVisibility();
                }
            }
        }
        
        /// <summary>
        /// Gets or sets whether this player is the player whose turn it is
        /// Used for UI highlighting
        /// </summary>
        private bool _isCurrent;
        public bool IsCurrent 
        { 
            get => _isCurrent;
            set => SetProperty(ref _isCurrent, value);
        }
        
        /// <summary>
        /// Gets the player's status text
        /// </summary>
        public string Status
        {
            get
            {
                if (_player.HasFolded)
                    return "Folded";
                if (_player.IsAllIn)
                    return "All-In";
                return IsCurrentUser ? "You" : "Active";
            }
        }
        
        /// <summary>
        /// Gets the player's hole cards
        /// </summary>
        public ObservableCollection<CardViewModel> HoleCards => _holeCards;
        
        /// <summary>
        /// Updates the visibility of the player's hole cards
        /// </summary>
        /// <param name="gameOver">Whether the game is over (showing all cards)</param>
        public void UpdateCardVisibility(bool gameOver = false)
        {
            // Cards are visible if:
            // 1. This is the current user (the player whose cards we're showing belongs to the human player)
            // 2. The game is over (showdown)
            // 3. The player has folded (in which case we can safely show their cards)
            bool showCards = IsCurrentUser || gameOver || HasFolded;
            
            foreach (var card in _holeCards)
            {
                card.IsVisible = showCards;
            }
        }
    }
    
    /// <summary>
    /// View model for a card
    /// </summary>
    public class CardViewModel : ViewModelBase
    {
        private readonly Card _card;
        private bool _isVisible;
        
        public CardViewModel(Card card, bool isVisible = true)
        {
            _card = card;
            _isVisible = isVisible;
        }
        
        /// <summary>
        /// Gets or sets whether the card is visible (face up)
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }
        
        /// <summary>
        /// Gets the card's rank
        /// </summary>
        public string Rank
        {
            get
            {
                switch (_card.Rank)
                {
                    case Core.Models.Rank.Ace:
                        return "A";
                    case Core.Models.Rank.King:
                        return "K";
                    case Core.Models.Rank.Queen:
                        return "Q";
                    case Core.Models.Rank.Jack:
                        return "J";
                    case Core.Models.Rank.Ten:
                        return "10";
                    default:
                        return ((int)_card.Rank).ToString();
                }
            }
        }
        
        /// <summary>
        /// Gets the card's suit
        /// </summary>
        public string Suit
        {
            get
            {
                switch (_card.Suit)
                {
                    case Core.Models.Suit.Hearts:
                        return "♥";
                    case Core.Models.Suit.Diamonds:
                        return "♦";
                    case Core.Models.Suit.Clubs:
                        return "♣";
                    case Core.Models.Suit.Spades:
                        return "♠";
                    default:
                        return "";
                }
            }
        }
        
        /// <summary>
        /// Gets whether the card is a red card
        /// </summary>
        public bool IsRed => _card.Suit == Core.Models.Suit.Hearts || _card.Suit == Core.Models.Suit.Diamonds;
        
        /// <summary>
        /// Gets the display text for the card
        /// </summary>
        public string Display => IsVisible ? $"{Rank}{Suit}" : "🂠";
    }
}
