using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using PokerGame.Core.AI;
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
        
        // AI Player stuff
        private AIPokerPlayer _aiPlayer = new AIPokerPlayer();
        private Dictionary<string, bool> _aiPlayers = new Dictionary<string, bool>();
        
        public GameViewModel()
        {
            // Create commands
            CheckCommand = ReactiveCommand.Create(ExecuteCheck);
            CallCommand = ReactiveCommand.Create(ExecuteCall);
            FoldCommand = ReactiveCommand.Create(ExecuteFold);
            RaiseCommand = ReactiveCommand.Create(ExecuteRaise);
            StartHandCommand = ReactiveCommand.Create(ExecuteStartHand);
            AddPlayerCommand = ReactiveCommand.Create(ExecuteAddPlayer);
            AddAIPlayerCommand = ReactiveCommand.Create(ExecuteAddAIPlayer);
            
            // Create game engine
            _gameEngine = new PokerGameEngine(this);
            
            // Initialize with some players for testing - now just the human player
            //_gameEngine.StartGame(new[] { "Player 1", "Player 2", "Player 3", "Player 4" });
            
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
        
        /// <summary>
        /// Command to add a new AI player
        /// </summary>
        public ReactiveCommand<Unit, Unit> AddAIPlayerCommand { get; }
        
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
            // If the player is trying to "raise" to the exact amount they need to call,
            // then they're actually just calling, not raising
            if (CurrentPlayer != null && RaiseAmount <= int.Parse(CurrentBet))
            {
                // Just call instead
                Console.WriteLine($"★★★★★ [UI] User clicked 'Raise' with {RaiseAmount} which is not a valid raise, treating as Call ★★★★★");
                ExecuteCall();
                return;
            }
            
            // Get how much additional the player needs to call
            int callAmount = 0;
            if (CurrentPlayer != null)
            {
                callAmount = int.Parse(CurrentBet) - CurrentPlayer.CurrentBet;
            }
            
            // If the raise amount is less than current bet + minimum raise (usually big blind), it's invalid
            if (RaiseAmount < int.Parse(CurrentBet) + _gameEngine.BigBlind)
            {
                Console.WriteLine($"★★★★★ [UI] Invalid raise amount: {RaiseAmount}, minimum is {int.Parse(CurrentBet) + _gameEngine.BigBlind} ★★★★★");
                // Show an error message
                ShowMessage($"Invalid raise amount. Minimum raise is {int.Parse(CurrentBet) + _gameEngine.BigBlind}.");
                return;
            }
            
            Console.WriteLine($"★★★★★ [UI] User raised to {RaiseAmount} (current bet was {CurrentBet}) ★★★★★");
            _gameEngine.ProcessPlayerAction("raise", RaiseAmount);
        }
        
        /// <summary>
        /// Executes the start hand command
        /// </summary>
        private void ExecuteStartHand()
        {
            if (_players.Count < 2)
            {
                ShowMessage("You need at least 2 players to start a hand. Please add another player.");
                return;
            }
            
            // Extra validation to ensure all players have proper state before starting a new hand
            Console.WriteLine("★★★★★ VALIDATING PLAYER STATES BEFORE STARTING NEW HAND ★★★★★");
            foreach (var player in _gameEngine.Players)
            {
                // Check if this is an AI player
                bool isAIPlayer = _aiPlayers.ContainsKey(player.Name) && _aiPlayers[player.Name];
                
                // Log player state for debugging
                Console.WriteLine($"★★★★★ Player {player.Name} (AI: {isAIPlayer}) - HasActed: {player.HasActed}, IsActive: {player.IsActive}, HasFolded: {player.HasFolded} ★★★★★");
                
                // Ensure player states are correctly reset
                if (!player.IsActive)
                {
                    Console.WriteLine($"★★★★★ FIXING INACTIVE PLAYER: {player.Name} ★★★★★");
                    player.IsActive = true;
                }
                
                if (player.HasActed)
                {
                    Console.WriteLine($"★★★★★ FIXING HASACTED FLAG FOR: {player.Name} ★★★★★");
                    player.HasActed = false;
                }
            }
            
            // Start the hand in the game engine
            _gameEngine.StartHand();
            
            // Log AI player tracking after hand start
            Console.WriteLine("★★★★★ AI PLAYER TRACKING AFTER HAND START ★★★★★");
            foreach (var kvp in _aiPlayers)
            {
                Console.WriteLine($"★★★★★ AI PLAYER: {kvp.Key}, Tracked: {kvp.Value} ★★★★★");
            }
        }
        
        /// <summary>
        /// Executes the add player command
        /// </summary>
        private void ExecuteAddPlayer()
        {
            // Check if we've reached the maximum number of players
            if (_players.Count >= _gameEngine.MaxPlayers)
            {
                ShowMessage($"Cannot add more players. Maximum of {_gameEngine.MaxPlayers} players reached.");
                return;
            }
            
            // In a real app, you would show a dialog to get the player name
            Random random = new Random();
            int playerNumber = _players.Count + 1;
            string playerName = $"Player {playerNumber}";
            
            // Add the player to our collection
            bool isCurrentUser = (_players.Count == 0); // First player added is the current user
            var player = new Player(Guid.NewGuid().ToString(), playerName, _gameEngine.MaxTableLimit)
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
        
        /// <summary>
        /// Executes the add AI player command
        /// </summary>
        private void ExecuteAddAIPlayer()
        {
            // Check if we've reached the maximum number of players
            if (_players.Count >= _gameEngine.MaxPlayers)
            {
                ShowMessage($"Cannot add more players. Maximum of {_gameEngine.MaxPlayers} players reached.");
                return;
            }
            
            // In a real app, you would show a dialog to get the player name
            Random random = new Random();
            int playerNumber = _players.Count + 1;
            string playerName = $"AI Player {playerNumber}";
            
            // Add the player to our collection with table limit-enforced chips
            var player = new Player(Guid.NewGuid().ToString(), playerName, _gameEngine.MaxTableLimit)
            {
                IsCurrentUser = false // AI players are never the current user
            };
            var playerViewModel = new PlayerViewModel(player);
            _players.Add(playerViewModel);
            
            // Track this player as an AI player
            _aiPlayers[playerName] = true;
            
            // Show a message that the AI player was added
            string message = $"Added AI opponent {playerName} to the game.";
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
            
            if (playerViewModel == null)
            {
                Console.WriteLine($"★★★★★ [CRITICAL ERROR] Player {player.Name} not found in UI view models! ★★★★★");
                
                // Debug player collection to diagnose issues
                Console.WriteLine("Available players in UI:");
                foreach (var p in _players)
                {
                    Console.WriteLine($"  - {p.Name} (CurrentUser: {p.IsCurrentUser})");
                }
                
                // Update UI with player list from game engine to recover
                UpdateGameState(gameEngine);
                
                // Try again with updated player list
                playerViewModel = _players.FirstOrDefault(p => p.Name == player.Name);
                
                if (playerViewModel == null)
                {
                    Console.WriteLine($"★★★★★ [UNRECOVERABLE ERROR] Still can't find player {player.Name} after UI update! ★★★★★");
                    return;
                }
                
                Console.WriteLine($"★★★★★ [RECOVERED] Found player {player.Name} after UI update ★★★★★");
            }
            
            // Check if this is an AI player (with debugging)
            bool isAIPlayer = _aiPlayers.ContainsKey(player.Name) && _aiPlayers[player.Name];
            Console.WriteLine($"★★★★★ GetPlayerAction for {player.Name} - IsAIPlayer: {isAIPlayer}, State: {gameEngine.State}, HasActed: {player.HasActed} ★★★★★");
            
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
            Console.WriteLine($"★★★★★ Is AI Player: {isAIPlayer}, Player.HasActed: {player.HasActed}, Player.IsActive: {player.IsActive} ★★★★★");
            
            // If this is an AI player, make the decision and execute it
            if (isAIPlayer)
            {
                Console.WriteLine($"[UI] Processing AI action for {player.Name} (Game State: {gameEngine.State})");
                
                // First, check if the AI player has folded
                if (player.HasFolded)
                {
                    Console.WriteLine($"[UI] AI player {player.Name} has already folded - skipping turn");
                    _gameEngine.ProcessPlayerAction("fold", 0);
                    return;
                }
                
                // Important: Check if we're in a valid game state for AI actions
                if (gameEngine.State == GameState.HandComplete || gameEngine.State == GameState.WaitingToStart)
                {
                    Console.WriteLine($"[UI] Cannot get AI action in state {gameEngine.State} - waiting for hand to start");
                    return;
                }
                
                // Reset player state to ensure the AI can act
                if (!player.IsActive)
                {
                    Console.WriteLine($"[UI] Reactivating AI player {player.Name} before decision");
                    player.IsActive = true;
                }
                
                player.HasActed = false;
                
                // Make AI decision with improved error handling
                string action;
                int betAmount;
                
                try
                {
                    (action, betAmount) = _aiPlayer.DetermineAction(player, gameEngine);
                    Console.WriteLine($"[UI] AI player {player.Name} decided to {action} with amount {betAmount}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UI] ERROR getting AI decision: {ex.Message}");
                    // Default to safe action on error
                    action = player.CurrentBet == gameEngine.CurrentBet ? "check" : "fold";
                    betAmount = 0;
                }
                
                // Apply table limits to bet amounts
                if (action == "raise" && betAmount > 100)
                {
                    Console.WriteLine($"[UI] Capping AI bet from {betAmount} to maximum 100");
                    betAmount = 100; // Maximum bet size
                }
                
                // Add a small delay to make it look like the AI is thinking
                Task.Delay(1000).ContinueWith(_ =>
                {
                    // Log the AI action
                    ShowMessage($"AI {player.Name} decides to {action}" + 
                                (action == "raise" ? $" with {betAmount}" : ""));
                    
                    // Execute the action
                    _gameEngine.ProcessPlayerAction(action, betAmount);
                });
                
                // Update game status
                GameStatus = $"{player.Name} is thinking...";
                return; // Don't update UI controls for AI player
            }
            
            // Only for human players - Set the UI button states based on game rules:
            
            // Only enable check if player's bet equals the current bet
            CanCheck = shouldCheck;
            
            // Enable call if player needs to match a higher bet
            CanCall = shouldCall; 
            
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
        
        /// <summary>
        /// Updates the UI with the current game state using a simplified approach
        /// </summary>
        /// <param name="gameEngine">The current game engine instance</param>
        public void UpdateGameState(PokerGameEngine gameEngine)
        {
            Console.WriteLine($"[UI] Updating game state: Game={gameEngine.State}, Players={gameEngine.Players.Count}");
            
            // STEP 1: Update simple properties first
            Pot = gameEngine.Pot;
            CurrentBet = gameEngine.CurrentBet.ToString();
            bool gameOver = gameEngine.State == GameState.HandComplete;
            
            // STEP 2: COMPLETE RESET - Clear all collections
            _players.Clear();
            _communityCards.Clear();
            _aiPlayers.Clear();
            
            // STEP 3: Set current user if none is set
            bool anyCurrentUser = gameEngine.Players.Any(p => p.IsCurrentUser);
            if (!anyCurrentUser && gameEngine.Players.Count > 0)
            {
                // Find first non-AI player if available
                var humanPlayer = gameEngine.Players.FirstOrDefault(p => !p.Name.Contains("AI"));
                if (humanPlayer != null)
                {
                    humanPlayer.IsCurrentUser = true;
                }
                else if (gameEngine.Players.Count > 0)
                {
                    // If all players are AI, set the first one as current user
                    gameEngine.Players[0].IsCurrentUser = true;
                }
            }
            
            // STEP 4: Process players with deduplication
            PlayerViewModel? currentPlayerViewModel = null;
            var deduplicatedPlayers = DeduplicatePlayers(gameEngine.Players);
            
            // Create view models for unique players
            foreach (var player in deduplicatedPlayers)
            {
                // Mark AI players
                if (player.Name.Contains("AI"))
                {
                    _aiPlayers[player.Name] = true;
                }
                
                // Deduplicate cards for this player
                var deduplicatedCards = DeduplicateCards(player.HoleCards);
                
                // Create view model with pre-filtered cards
                var playerViewModel = new PlayerViewModel(player, player.IsCurrentUser, deduplicatedCards);
                
                // Set if this is the current player
                if (gameEngine.CurrentPlayer != null && player.Name == gameEngine.CurrentPlayer.Name)
                {
                    playerViewModel.IsCurrent = true;
                    currentPlayerViewModel = playerViewModel;
                }
                
                // Update card visibility based on game state
                playerViewModel.UpdateCardVisibility(gameOver);
                
                // Add to collection
                _players.Add(playerViewModel);
            }
            
            // STEP 5: Add deduplicated community cards
            var deduplicatedCommunityCards = DeduplicateCards(gameEngine.CommunityCards);
            foreach (var card in deduplicatedCommunityCards)
            {
                _communityCards.Add(new CardViewModel(card, true));
            }
            
            // STEP 6: Update game state status and controls
            UpdateGameStatus(gameEngine, currentPlayerViewModel, gameOver);
        }
        
        /// <summary>
        /// Updates game status and UI button states based on game state
        /// </summary>
        private void UpdateGameStatus(PokerGameEngine gameEngine, PlayerViewModel? currentPlayerViewModel, bool gameOver)
        {
            // Handle end of hand or waiting to start
            if (gameEngine.State == GameState.HandComplete || gameEngine.State == GameState.WaitingToStart)
            {
                // Update UI state
                GameStatus = gameEngine.State == GameState.HandComplete 
                    ? "Hand complete. Click 'Start Hand' to play again."
                    : "Ready to start. Click 'Start Hand' to begin.";
                
                // Set button states
                CanStartHand = true;
                CanCheck = false;
                CanCall = false;
                CanRaise = false;
                CanFold = false;
                CurrentPlayer = null;
                
                // Make all cards visible at end of hand
                if (gameOver)
                {
                    foreach (var player in _players)
                    {
                        player.UpdateCardVisibility(true);
                    }
                }
                
                return;
            }
            
            // Handle active gameplay
            if (currentPlayerViewModel != null)
            {
                // Set current player
                CurrentPlayer = currentPlayerViewModel;
                
                // Get player model from engine
                var playerModel = gameEngine.Players.FirstOrDefault(p => p.Name == currentPlayerViewModel.Name);
                if (playerModel == null) return;
                
                // Determine available actions
                bool shouldCheck = playerModel.CurrentBet == gameEngine.CurrentBet;
                bool shouldCall = playerModel.CurrentBet < gameEngine.CurrentBet && playerModel.Chips > 0;
                bool mustCallOrFold = gameEngine.CurrentBet > playerModel.CurrentBet && playerModel.Chips > 0;
                bool shouldRaise = playerModel.Chips > 0;
                
                // Set button states
                CanCheck = shouldCheck;
                CanCall = shouldCall;
                CanRaise = shouldRaise;
                CanFold = true;
                
                // Force-enable call button if player must call (can't check)
                if (mustCallOrFold)
                {
                    CanCall = true;
                    CanCheck = false;
                }
                
                // Set minimum raise amount
                MinRaiseAmount = gameEngine.CurrentBet + 10;
                RaiseAmount = MinRaiseAmount;
                
                // Update game status message
                GameStatus = currentPlayerViewModel.IsCurrentUser 
                    ? "Your turn - make your move" 
                    : $"Waiting for {playerModel.Name} to make a move";
            }
            else if (gameEngine.CurrentPlayer != null)
            {
                // Log a warning if current player isn't in UI model
                Console.WriteLine($"[UI] WARNING: Current player in engine ({gameEngine.CurrentPlayer.Name}) not found in UI model!");
                GameStatus = "Waiting for player action...";
            }
        }
        
        /// <summary>
        /// Removes duplicate players with the same name, keeping the first instance
        /// </summary>
        private List<Player> DeduplicatePlayers(IEnumerable<Player> players)
        {
            var uniquePlayers = new Dictionary<string, Player>();
            
            foreach (var player in players)
            {
                if (!uniquePlayers.ContainsKey(player.Name))
                {
                    uniquePlayers[player.Name] = player;
                }
                else
                {
                    Console.WriteLine($"[UI] Duplicate player removed: {player.Name}");
                }
            }
            
            return uniquePlayers.Values.ToList();
        }
        
        /// <summary>
        /// Removes duplicate cards with the same rank and suit
        /// </summary>
        private List<Card> DeduplicateCards(IEnumerable<Card> cards)
        {
            var uniqueCards = new HashSet<string>();
            var result = new List<Card>();
            
            foreach (var card in cards)
            {
                string cardKey = $"{card.Rank}-{card.Suit}";
                if (!uniqueCards.Contains(cardKey))
                {
                    uniqueCards.Add(cardKey);
                    result.Add(card);
                }
                else
                {
                    Console.WriteLine($"[UI] Duplicate card removed: {card.Rank} of {card.Suit}");
                }
            }
            
            return result;
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
        
        /// <summary>
        /// Creates a new PlayerViewModel with automatic card deduplication
        /// </summary>
        public PlayerViewModel(Player player, bool isCurrentUser = false)
        {
            _player = player;
            
            // Set current user flag, preferring the model's value if it's set
            _isCurrentUser = player.IsCurrentUser || isCurrentUser;
            
            // Create view models for hole cards with deduplication
            var uniqueCards = new HashSet<string>();
            var deduplicatedCards = new List<Card>();
            
            foreach (var card in player.HoleCards)
            {
                string cardKey = $"{card.Rank}-{card.Suit}";
                if (!uniqueCards.Contains(cardKey))
                {
                    uniqueCards.Add(cardKey);
                    deduplicatedCards.Add(card);
                }
                else
                {
                    Console.WriteLine($"★★★★★ [UI] Prevented duplicate card for {player.Name} in constructor: {card.Rank} of {card.Suit} ★★★★★");
                }
            }
            
            // Initialize with the deduplicated cards
            InitializeWithCards(deduplicatedCards);
        }
        
        /// <summary>
        /// Creates a new PlayerViewModel with pre-filtered cards for efficiency
        /// </summary>
        public PlayerViewModel(Player player, bool isCurrentUser, IEnumerable<Card> filteredCards)
        {
            _player = player;
            
            // Set current user flag, preferring the model's value if it's set
            _isCurrentUser = player.IsCurrentUser || isCurrentUser;
            
            // Initialize with the pre-filtered cards (already deduplicated)
            InitializeWithCards(filteredCards);
        }
        
        /// <summary>
        /// Common initialization method for both constructors
        /// </summary>
        private void InitializeWithCards(IEnumerable<Card> cards)
        {
            // Clear any existing cards
            _holeCards.Clear();
            
            // Determine initial visibility based on player status
            bool showCards = _isCurrentUser || _player.HasFolded;
            
            // Add each card with the determined visibility
            foreach (var card in cards)
            {
                _holeCards.Add(new CardViewModel(card, showCards));
            }
            
            // Log what we did
            Console.WriteLine($"Created PlayerViewModel for {_player.Name} with {_holeCards.Count} cards, visibility={showCards}");
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
            
            // Debug initialization
            Console.WriteLine($"Created card {card.Rank} of {card.Suit} with visibility: {isVisible}");
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
        public string Display 
        { 
            get 
            {
                Console.WriteLine($"Card display requested: {_card.Rank} of {_card.Suit}, IsVisible={IsVisible}");
                return IsVisible ? $"{Rank}{Suit}" : "🂠"; 
            }
        }
    }
}
