using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PokerGame.Core.Game;
using PokerGame.Core.Models;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// A mock implementation of IPokerGameUI that does nothing - used for state display only
    /// </summary>
    public class MockPokerGameUI : Interfaces.IPokerGameUI
    {
        public void SetGameEngine(Game.PokerGameEngine gameEngine) { }
        public void StartGame() { }
        public void ShowMessage(string message) { }
        public int GetAnteAmount() => 10;
        public void GetPlayerAction(Models.Player player, Game.PokerGameEngine gameEngine) { }
        public void UpdateGameState(Game.PokerGameEngine gameEngine) { }
        public void ShowWinner(List<Models.Player> winners, Game.PokerGameEngine gameEngine) { }
    }

    /// <summary>
    /// Microservice that handles the console user interface
    /// </summary>
    public class ConsoleUIService : MicroserviceBase
    {
        private GameStatePayload? _latestGameState;
        private readonly Dictionary<string, PlayerInfo> _players = new Dictionary<string, PlayerInfo>();
        private string? _gameEngineServiceId;
        private bool _waitingForPlayerAction = false;
        private string _activePlayerId = string.Empty;
        private bool _useEnhancedUI;
        private object? _enhancedUiInstance; // Will hold a dynamic reference to CursesUI when needed
        
        /// <summary>
        /// Creates a new console UI service
        /// </summary>
        /// <param name="publisherPort">The port to use for publishing messages</param>
        /// <param name="subscriberPort">The port to use for subscribing to messages</param>
        /// <param name="useEnhancedUI">Whether to use the enhanced curses UI</param>
        public ConsoleUIService(int publisherPort, int subscriberPort, bool useEnhancedUI = false) 
            : base("PlayerUI", "Console UI", publisherPort, subscriberPort)
        {
            _useEnhancedUI = useEnhancedUI;
            Console.WriteLine($"ConsoleUIService created with enhanced UI: {_useEnhancedUI}");
            
            // Defer initialization of the enhanced UI to the Start method
            // This ensures proper sequencing with other microservices
        }
        
        /// <summary>
        /// Starts the UI service
        /// </summary>
        public override void Start()
        {
            base.Start();
            
            // Initialize enhanced UI if requested - doing this here ensures proper initialization order
            if (_useEnhancedUI && _enhancedUiInstance == null)
            {
                try
                {
                    Console.WriteLine("Initializing Enhanced UI in Start method...");
                    // Create CursesUI instance via reflection to avoid direct dependency
                    var cursesUIType = Type.GetType("PokerGame.Console.CursesUI, PokerGame.Console");
                    if (cursesUIType != null)
                    {
                        _enhancedUiInstance = Activator.CreateInstance(cursesUIType);
                        Console.WriteLine("Successfully created Enhanced Console UI instance");
                        
                        // Call Initialize method
                        var initMethod = cursesUIType.GetMethod("Initialize");
                        if (initMethod != null)
                        {
                            initMethod.Invoke(_enhancedUiInstance, null);
                            Console.WriteLine("Successfully initialized Enhanced Console UI");
                        }
                        else
                        {
                            Console.WriteLine("Could not find Initialize method on CursesUI");
                            _useEnhancedUI = false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Enhanced UI requested but CursesUI class not found");
                        _useEnhancedUI = false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initializing enhanced UI: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                    _useEnhancedUI = false;
                }
            }
            
            // Start the input processing task
            Task.Run(ProcessUserInputAsync);
        }
        
        /// <summary>
        /// Called when a new service is registered
        /// </summary>
        /// <param name="registrationInfo">The service registration information</param>
        protected override void OnServiceRegistered(ServiceRegistrationPayload registrationInfo)
        {
            try
            {
                // Keep track of the game engine service
                if (registrationInfo.ServiceType == "GameEngine")
                {
                    _gameEngineServiceId = registrationInfo.ServiceId;
                    Console.WriteLine($"Connected to game engine service: {registrationInfo.ServiceName} (ID: {registrationInfo.ServiceId})");
                    
                    // Print debug information
                    Console.WriteLine("Available services:");
                    foreach (var entry in GetServiceTypes())
                    {
                        Console.WriteLine($"- {entry.Key}: {entry.Value}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in service registration: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets a mapping of service IDs to service types
        /// </summary>
        /// <returns>Dictionary of service IDs to types</returns>
        private Dictionary<string, string> GetServiceTypes()
        {
            // This is a debug helper method to see what services are available
            Dictionary<string, string> result = new Dictionary<string, string>();
            
            foreach (var serviceType in new[] { "GameEngine", "PlayerUI" })
            {
                var serviceIds = GetServicesOfType(serviceType);
                foreach (var id in serviceIds)
                {
                    result[id] = serviceType;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Handles messages received from other microservices
        /// </summary>
        /// <param name="message">The message to handle</param>
        protected internal override async Task HandleMessageAsync(Message message)
        {
            switch (message.Type)
            {
                case MessageType.GameState:
                    var gameState = message.GetPayload<GameStatePayload>();
                    if (gameState != null)
                    {
                        _latestGameState = gameState;
                        DisplayGameState();
                    }
                    break;
                    
                case MessageType.PlayerUpdate:
                    var playerInfo = message.GetPayload<PlayerInfo>();
                    if (playerInfo != null)
                    {
                        _players[playerInfo.PlayerId] = playerInfo;
                    }
                    break;
                    
                case MessageType.DisplayUpdate:
                    var displayText = message.GetPayload<string>();
                    if (!string.IsNullOrEmpty(displayText))
                    {
                        Console.WriteLine(displayText);
                    }
                    break;
                
                case MessageType.ActionResponse:
                    var response = message.GetPayload<ActionResponsePayload>();
                    if (response != null)
                    {
                        Console.WriteLine($"Action response received: {response.Message}");
                        if (!response.Success)
                        {
                            Console.WriteLine($"Error processing action: {response.Message}");
                            // If the action failed, we might want to re-prompt the player
                            _waitingForPlayerAction = true;
                        }
                    }
                    break;
                    
                case MessageType.PlayerAction:
                    var actionPlayer = message.GetPayload<PlayerInfo>();
                    if (actionPlayer != null)
                    {
                        _waitingForPlayerAction = true;
                        _activePlayerId = actionPlayer.PlayerId;
                        
                        // Make sure we have this player in our players dictionary
                        if (!_players.ContainsKey(_activePlayerId))
                        {
                            _players[_activePlayerId] = actionPlayer;
                        }
                        
                        DisplayActionPrompt(actionPlayer);
                    }
                    break;
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// Continuously processes user input
        /// </summary>
        private async Task ProcessUserInputAsync()
        {
            // Setup initial game
            await SetupGameAsync();
            
            while (true)
            {
                try
                {
                    if (_waitingForPlayerAction)
                    {
                        // Process player action
                        string input = Console.ReadLine()?.ToUpper() ?? "";
                        
                        // Check for auto-play mode
                        if (input.Contains("[AUTO]") || input.Trim() == "")
                        {
                            Console.WriteLine("Auto-play mode detected. Making automatic decision...");
                            
                            // Logic for auto decision - typically call/check or fold with bad hands
                            if (_latestGameState != null)
                            {
                                // Simple auto-play logic: 
                                // - Always call if bet is small (<= 10% of chips)
                                // - Always check if possible
                                // - Otherwise fold
                                var playerInfo = _players[_activePlayerId];
                                bool canCheck = _latestGameState.CurrentBet == playerInfo.CurrentBet;
                                int betToCall = _latestGameState.CurrentBet - playerInfo.CurrentBet;
                                
                                // Auto-decision making
                                if (canCheck)
                                {
                                    Console.WriteLine("[AUTO] Checking");
                                    SendPlayerAction("check");
                                }
                                else if (betToCall <= playerInfo.Chips * 0.1) // Call if bet is <= 10% of chips
                                {
                                    Console.WriteLine($"[AUTO] Calling {betToCall}");
                                    SendPlayerAction("call");
                                }
                                else
                                {
                                    Console.WriteLine("[AUTO] Folding");
                                    SendPlayerAction("fold");
                                }
                            }
                            else
                            {
                                // Default to call if no game state available
                                Console.WriteLine("[AUTO] Calling (default)");
                                SendPlayerAction("call");
                            }
                        }
                        else
                        {
                            // Manual play mode
                            switch (input)
                            {
                                case "F":
                                    SendPlayerAction("fold");
                                    break;
                                    
                                case "C":
                                    // This will be check or call depending on the current game state
                                    var playerInfo = _players[_activePlayerId];
                                    bool canCheck = _latestGameState != null &&
                                                  playerInfo.CurrentBet == _latestGameState.CurrentBet;
                                    
                                    SendPlayerAction(canCheck ? "check" : "call");
                                    break;
                                    
                                case "R":
                                    Console.Write("Enter raise amount: ");
                                    if (int.TryParse(Console.ReadLine(), out int raiseAmount))
                                    {
                                        SendPlayerAction("raise", raiseAmount);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Invalid amount. Try again.");
                                    }
                                    break;
                                    
                                default:
                                    Console.WriteLine("Invalid action. Use F (fold), C (check/call), or R (raise).");
                                    break;
                            }
                        }
                    }
                    else if (_latestGameState?.CurrentState == GameState.HandComplete ||
                            _latestGameState?.CurrentState == GameState.WaitingToStart)
                    {
                        // Ask to start a new hand
                        Console.WriteLine("Press Enter to start a new hand or 'Q' to quit.");
                        string input = Console.ReadLine()?.ToUpper() ?? "";
                        
                        if (input == "Q")
                        {
                            break;
                        }
                        else if (string.IsNullOrWhiteSpace(input) && _gameEngineServiceId != null)
                        {
                            // Start a new hand
                            var message = Message.Create(MessageType.StartHand);
                            SendTo(message, _gameEngineServiceId);
                        }
                    }
                    
                    await Task.Delay(100); // Small pause to prevent CPU overuse
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing input: {ex.Message}");
                }
            }
            
            Console.WriteLine("Thanks for playing!");
        }
        
        /// <summary>
        /// Sets up the initial game with player information
        /// </summary>
        private async Task SetupGameAsync()
        {
            Console.Clear();
            
            if (_useEnhancedUI)
            {
                Console.WriteLine("Starting enhanced Console UI for poker game...");
                Console.WriteLine("Enhanced UI in microservices mode is ready!");
            }
            else
            {
                Console.WriteLine("===============================================");
                Console.WriteLine("           TEXAS HOLD'EM POKER GAME           ");
                Console.WriteLine("===============================================");
                Console.WriteLine();
            }
            
            // Wait for the game engine to be available
            while (_gameEngineServiceId == null)
            {
                Console.WriteLine("Waiting for game engine to start...");
                await Task.Delay(1000);
            }
            
            // Get number of players
            int numPlayers = 0;
            bool validInput = false;
            
            // Try up to 3 times to get valid input, then default to 3 players
            int maxAttempts = 3;
            int attempts = 0;
            
            while (!validInput && attempts < maxAttempts)
            {
                Console.Write("Enter number of players (2-8): ");
                string input = Console.ReadLine() ?? "";
                
                if (int.TryParse(input, out numPlayers) && numPlayers >= 2 && numPlayers <= 8)
                {
                    validInput = true;
                }
                else
                {
                    attempts++;
                    Console.WriteLine("Please enter a number between 2 and 8.");
                    
                    // If this is the last attempt, set a default
                    if (attempts >= maxAttempts)
                    {
                        numPlayers = 3; // Default to 3 players
                        Console.WriteLine("Using default: 3 players");
                        validInput = true;
                    }
                }
            }
            
            // Get player names
            string[] playerNames = new string[numPlayers];
            for (int i = 0; i < numPlayers; i++)
            {
                string defaultName = $"Player {i+1}";
                Console.Write($"Enter name for player {i+1} (or press Enter for '{defaultName}'): ");
                string name = Console.ReadLine() ?? "";
                playerNames[i] = string.IsNullOrWhiteSpace(name) ? defaultName : name;
            }
            
            // Start the game
            Console.WriteLine($"Sending GameStart message to {_gameEngineServiceId} with {playerNames.Length} players");
            var gameStartMessage = Message.Create(MessageType.GameStart, playerNames);
            SendTo(gameStartMessage, _gameEngineServiceId);
            
            // Small delay to let the game engine process the start message
            await Task.Delay(1000);
            
            // Now send a message to start the hand
            Console.WriteLine($"Sending StartHand message to {_gameEngineServiceId}");
            var startHandMessage = Message.Create(MessageType.StartHand);
            SendTo(startHandMessage, _gameEngineServiceId);
            
            Console.WriteLine("StartHand message sent, waiting for response");
            // Give some time for processing
            await Task.Delay(1000);
        }
        
        /// <summary>
        /// Sends a player action to the game engine
        /// </summary>
        /// <param name="actionType">The type of action (fold, check, call, raise)</param>
        /// <param name="betAmount">The bet amount (for raise actions)</param>
        private void SendPlayerAction(string actionType, int betAmount = 0)
        {
            if (_gameEngineServiceId != null && _waitingForPlayerAction)
            {
                Console.WriteLine($"Sending player action: {actionType} with bet amount: {betAmount}");
                
                var payload = new PlayerActionPayload
                {
                    PlayerId = _activePlayerId,
                    ActionType = actionType,
                    BetAmount = betAmount
                };
                
                var message = Message.Create(MessageType.PlayerAction, payload);
                
                // Set the sender ID explicitly
                message.SenderId = ServiceId;
                
                Console.WriteLine($"Sending action to game engine {_gameEngineServiceId}");
                SendTo(message, _gameEngineServiceId);
                
                // Give some time for processing before next action
                Thread.Sleep(100);
                
                _waitingForPlayerAction = false;
                Console.WriteLine("Player action sent, waiting for response");
                
                // Request a game state update after sending the action
                // This ensures we see the latest state even if the response handling has issues
                RequestGameStateUpdate();
            }
            else
            {
                Console.WriteLine($"Cannot send player action: gameEngineServiceId={_gameEngineServiceId}, waitingForPlayerAction={_waitingForPlayerAction}");
            }
        }
        
        /// <summary>
        /// Requests a game state update from the game engine
        /// </summary>
        private void RequestGameStateUpdate()
        {
            if (_gameEngineServiceId != null)
            {
                // Create a simple message to request game state
                var message = Message.Create(MessageType.GameState);
                SendTo(message, _gameEngineServiceId);
            }
        }
        
        /// <summary>
        /// Displays the current game state
        /// </summary>
        private void DisplayGameState()
        {
            if (_latestGameState == null)
                return;
                
            if (_useEnhancedUI)
            {
                DisplayEnhancedGameState();
                return;
            }
            
            Console.WriteLine();
            Console.WriteLine("=============================================");
            Console.WriteLine($"CURRENT STATE: {_latestGameState.CurrentState}");
            
            // Show community cards
            string communityCardsText = _latestGameState.CommunityCards.Count > 0 
                ? CardListToString(_latestGameState.CommunityCards) 
                : "[None]";
            Console.WriteLine($"Community Cards: {communityCardsText}");
            
            // Show pot and current bet
            Console.WriteLine($"Pot: ${_latestGameState.Pot}");
            if (_latestGameState.CurrentBet > 0)
            {
                Console.WriteLine($"Current bet: ${_latestGameState.CurrentBet}");
            }
            Console.WriteLine();
            
            // Show player information
            Console.WriteLine("PLAYERS:");
            foreach (var player in _latestGameState.Players)
            {
                string status = "";
                if (player.HasFolded) status = " (Folded)";
                else if (player.IsAllIn) status = " (All-In)";
                
                Console.WriteLine($"- {player.Name}{status}: ${player.Chips} chips");
            }
            
            Console.WriteLine("=============================================");
        }
        
        /// <summary>
        /// Displays an enhanced game state using our enhanced console UI
        /// </summary>
        private void DisplayEnhancedGameState()
        {
            if (_enhancedUiInstance != null && _latestGameState != null)
            {
                try
                {
                    // Create a local game state for the CursesUI to display
                    var gameEngine = CreateLocalGameEngineFromState();
                    
                    // Call CursesUI.UpdateGameState via reflection
                    var updateMethod = _enhancedUiInstance.GetType().GetMethod("UpdateGameState");
                    if (updateMethod != null)
                    {
                        updateMethod.Invoke(_enhancedUiInstance, new[] { gameEngine });
                        return; // Success, early return
                    }
                }
                catch (Exception ex)
                {
                    // If anything fails, fall back to text UI
                    Console.WriteLine($"Error in enhanced UI: {ex.Message}");
                }
            }
            
            // Fallback to match CursesUI format for consistency
            Console.WriteLine();
            Console.WriteLine("=============================================");
            Console.WriteLine($"CURRENT STATE: {_latestGameState?.CurrentState}");
            
            // Show community cards
            string communityCardsText = _latestGameState?.CommunityCards?.Count > 0 
                ? CardListToString(_latestGameState.CommunityCards) 
                : "[None]";
            Console.WriteLine($"Community Cards: {communityCardsText}");
            
            // Show pot and current bet
            Console.WriteLine($"Pot: ${_latestGameState?.Pot}");
            if (_latestGameState?.CurrentBet > 0)
            {
                Console.WriteLine($"Current bet: ${_latestGameState.CurrentBet}");
            }
            Console.WriteLine();
            
            // Show player information
            Console.WriteLine("PLAYERS:");
            if (_latestGameState?.Players != null)
            {
                foreach (var player in _latestGameState.Players)
                {
                    string status = "";
                    if (player.HasFolded) status = " (Folded)";
                    else if (player.IsAllIn) status = " (All-In)";
                    
                    Console.WriteLine($"- {player.Name}{status}: ${player.Chips} chips");
                }
            }
            
            Console.WriteLine("=============================================");
        }
        
        /// <summary>
        /// Creates a local game engine object from the current state for use with the CursesUI
        /// </summary>
        private object CreateLocalGameEngineFromState()
        {
            // We need to create a dynamic PokerGameEngine instance that the CursesUI can use
            // This acts as an adapter between the microservice state and the local UI
            
            if (_latestGameState == null)
                throw new InvalidOperationException("No game state available");
            
            // Create a Mock UI for the engine
            Type? uiInterfaceType = Type.GetType("PokerGame.Core.Interfaces.IPokerGameUI, PokerGame.Core");
            if (uiInterfaceType == null)
                throw new InvalidOperationException("Could not find IPokerGameUI interface");
            
            // Create a proxy UI that does nothing
            var proxyUI = new MockPokerGameUI();
            
            // Create the engine with the proxy UI
            Type? engineType = Type.GetType("PokerGame.Core.Game.PokerGameEngine, PokerGame.Core");
            if (engineType == null)
                throw new InvalidOperationException("Could not find PokerGameEngine type");
                
            // Create the engine with our mock UI
            object? gameEngine = Activator.CreateInstance(engineType, new[] { proxyUI });
            if (gameEngine == null)
                throw new InvalidOperationException("Failed to create game engine instance");
            
            // We need to set the private fields directly via reflection since the properties are read-only
            SetField(gameEngine, "_pot", _latestGameState.Pot);
            SetField(gameEngine, "_currentBet", _latestGameState.CurrentBet);
            SetField(gameEngine, "_gameState", _latestGameState.CurrentState);
            
            // Set community cards - need to access the private field
            var communityCardsField = engineType.GetField("_communityCards", 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.NonPublic);
                
            if (communityCardsField != null)
            {
                // Get the existing list from the field
                var communityCardsList = communityCardsField.GetValue(gameEngine) as List<Card>;
                if (communityCardsList != null)
                {
                    // Clear it and add the new cards
                    communityCardsList.Clear();
                    foreach (var card in _latestGameState.CommunityCards)
                    {
                        communityCardsList.Add(card);
                    }
                }
            }
            
            // Set players - need to access the private field
            var playersField = engineType.GetField("_players", 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.NonPublic);
                
            if (playersField != null)
            {
                // Convert PlayerInfo to Player
                var players = new List<Player>();
                
                foreach (var p in _latestGameState.Players)
                {
                    var player = new Player(p.Name, p.Chips);
                    
                    // Add hole cards - work around read-only collection
                    if (p.HoleCards != null && p.HoleCards.Count > 0)
                    {
                        foreach (var card in p.HoleCards)
                        {
                            player.HoleCards.Add(card);
                        }
                    }
                    
                    // Handle other properties with private setters
                    // We need to use reflection to set these properly
                    if (p.HasFolded)
                    {
                        var foldMethod = typeof(Player).GetMethod("Fold");
                        if (foldMethod != null)
                        {
                            foldMethod.Invoke(player, null);
                        }
                        else
                        {
                            Console.WriteLine("Warning: Could not find Fold method on Player");
                        }
                    }
                    
                    if (p.CurrentBet > 0)
                    {
                        var placeBetMethod = typeof(Player).GetMethod("PlaceBet");
                        if (placeBetMethod != null)
                        {
                            placeBetMethod.Invoke(player, new object[] { p.CurrentBet });
                        }
                        else
                        {
                            Console.WriteLine("Warning: Could not find PlaceBet method on Player");
                        }
                    }
                    
                    // IsAllIn is set automatically by PlaceBet if chips are 0
                    
                    players.Add(player);
                }
                
                // Set the players list to the field
                var playersList = playersField.GetValue(gameEngine) as List<Player>;
                if (playersList != null)
                {
                    playersList.Clear();
                    foreach (var p in players)
                    {
                        playersList.Add(p);
                    }
                }
            }
            
            return gameEngine;
        }
        
        /// <summary>
        /// Helper to set a property via reflection
        /// </summary>
        private void SetProperty(object obj, string propertyName, object value)
        {
            var prop = obj.GetType().GetProperty(propertyName);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(obj, value);
            }
        }
        
        /// <summary>
        /// Helper to set a private field via reflection
        /// </summary>
        private void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.NonPublic);
                
            if (field != null)
            {
                field.SetValue(obj, value);
            }
            else
            {
                Console.WriteLine($"Warning: Could not find field '{fieldName}' on {obj.GetType().Name}");
            }
        }
        
        /// <summary>
        /// Displays the action prompt for a player
        /// </summary>
        /// <param name="player">The player taking action</param>
        private void DisplayActionPrompt(PlayerInfo player)
        {
            Console.WriteLine();
            Console.WriteLine($"=== {player.Name}'s turn ===");
            
            // Show player's hole cards
            Console.WriteLine($"Your hole cards: {CardListToString(player.HoleCards)}");
            
            // Show available actions
            Console.WriteLine("Available actions:");
            
            bool canCheck = _latestGameState != null && player.CurrentBet == _latestGameState.CurrentBet;
            int callAmount = _latestGameState != null ? _latestGameState.CurrentBet - player.CurrentBet : 0;
            
            if (canCheck)
                Console.WriteLine("- Check (C)");
            else
                Console.WriteLine($"- Call {callAmount} (C)");
                
            Console.WriteLine("- Fold (F)");
            
            int minRaise = _latestGameState != null ? _latestGameState.CurrentBet + 10 : 10;
            Console.WriteLine($"- Raise (R) (Minimum raise: {minRaise})");
            
            Console.Write("Enter your action: ");
            
            // For automated testing in microservice mode, automatically use a reasonable action
            // This will prevent the "Invalid action" messages from continuously being displayed
            if (Environment.GetEnvironmentVariable("AUTOMATED_TEST") == "1" || 
                Environment.CommandLine.Contains("--microservices"))
            {
                string autoAction = canCheck ? "C" : "C"; // Defaults to check/call as safest option
                Console.WriteLine($"[AUTO] {autoAction}");
                
                // Small delay to simulate thinking
                Task.Delay(500).Wait();
                
                // Process this action
                if (canCheck)
                    SendPlayerAction("check");
                else
                    SendPlayerAction("call");
                
                _waitingForPlayerAction = false;
            }
        }
        
        /// <summary>
        /// Converts a list of cards to a readable string
        /// </summary>
        private string CardListToString(List<Card> cards)
        {
            return string.Join(" ", cards.Select(c => GetCardDisplay(c)));
        }
        
        /// <summary>
        /// Gets a display representation for a card
        /// </summary>
        private string GetCardDisplay(Card card)
        {
            string rank;
            switch (card.Rank)
            {
                case Rank.Jack:
                    rank = "J";
                    break;
                case Rank.Queen:
                    rank = "Q";
                    break;
                case Rank.King:
                    rank = "K";
                    break;
                case Rank.Ace:
                    rank = "A";
                    break;
                default:
                    rank = ((int)card.Rank).ToString();
                    break;
            }
            
            string suit;
            switch (card.Suit)
            {
                case Suit.Clubs:
                    suit = "♣";
                    break;
                case Suit.Diamonds:
                    suit = "♦";
                    break;
                case Suit.Hearts:
                    suit = "♥";
                    break;
                case Suit.Spades:
                    suit = "♠";
                    break;
                default:
                    suit = "?";
                    break;
            }
            
            return $"[{rank}{suit}]";
        }
        
        /// <summary>
        /// Clean up resources and dispose the Enhanced UI if it was initialized
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            
            // Dispose the enhanced UI if it exists and is IDisposable
            if (_enhancedUiInstance is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                    Console.WriteLine("Enhanced Console UI properly disposed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing Enhanced Console UI: {ex.Message}");
                }
                _enhancedUiInstance = null;
            }
        }
    }
}