using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PokerGame.Core.Game;
using PokerGame.Core.Models;
using PokerGame.Core.ServiceManagement;
using PokerGame.Core.Messaging;
using MSA.Foundation.Messaging;

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
        // Test logging for build verification
        static ConsoleUIService() {
            Console.WriteLine("********** CONSOLE UI SERVICE LOADED - NEW VERSION WITH STARTHAND LOGGING **********");
        }
        
        /// <summary>
        /// Handles service registration messages directly from the broker
        /// </summary>
        /// <param name="message">The registration message</param>
        private void HandleServiceRegistration(NetworkMessage message)
        {
            try
            {
                Console.WriteLine($"Received direct service registration from: {message.SenderId}");
                
                if (message.Type == Messaging.MessageType.ServiceRegistration)
                {
                    try
                    {
                        var payload = System.Text.Json.JsonSerializer.Deserialize<ServiceRegistrationPayload>(
                            message.Payload ?? "{}");
                        
                        if (payload != null && payload.ServiceType == ServiceConstants.ServiceTypes.GameEngine 
                            && string.IsNullOrEmpty(_gameEngineServiceId))
                        {
                            _gameEngineServiceId = message.SenderId;
                            Console.WriteLine($"Updated game engine service ID to: {_gameEngineServiceId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deserializing payload: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling service registration: {ex.Message}");
            }
        }
        
        private bool _useEnhancedUI = false;
        private object? _enhancedUiInstance = null;
        private Task? _inputProcessingTask = null;
        private string? _gameEngineServiceId = null;
        private bool _waitingForPlayerAction = false;
        private string? _activePlayerId = null;
        private Dictionary<string, Models.Player> _players = new Dictionary<string, Models.Player>();
        private Models.GameState _currentGameState = Models.GameState.NotStarted;
        private List<Card> _communityCards = new List<Card>();
        private int _pot = 0;
        private int _currentBet = 0;
        private new int _publisherPort;
        private new int _subscriberPort;
        
        /// <summary>
        /// Creates a new instance of the ConsoleUIService
        /// </summary>
        /// <param name="publisherPort">Publisher port</param>
        /// <param name="subscriberPort">Subscriber port</param>
        /// <param name="enhancedUI">Whether to use enhanced UI mode (box drawing, etc)</param>
        public ConsoleUIService(int publisherPort, int subscriberPort, bool enhancedUI = false) 
            : base(ServiceConstants.ServiceTypes.ConsoleUI, "ConsoleUIService", publisherPort, subscriberPort)
        {
            _useEnhancedUI = enhancedUI;
            _publisherPort = publisherPort;
            _subscriberPort = subscriberPort;
            
            // Try to load the CursesUI if enhanced mode is requested
            if (_useEnhancedUI)
            {
                try
                {
                    // Look for the CursesUI type in all loaded assemblies
                    var cursesUIType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .FirstOrDefault(t => t.Name == "CursesUI");
                    
                    if (cursesUIType != null)
                    {
                        _enhancedUiInstance = Activator.CreateInstance(cursesUIType);
                        Console.WriteLine("CursesUI loaded successfully");
                    }
                    else
                    {
                        Console.WriteLine("   ERROR: Curses UI requested but CursesUI class not found");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading CursesUI: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Creates a new instance of the ConsoleUIService with MSA Foundation execution context
        /// </summary>
        /// <param name="executionContext">The microservice execution context</param>
        /// <param name="enhancedUI">Whether to use enhanced UI mode</param>
        public ConsoleUIService(MSA.Foundation.ServiceManagement.ExecutionContext executionContext, bool enhancedUI = false)
            : base(ServiceConstants.ServiceTypes.ConsoleUI, "ConsoleUIService", 
                  new Messaging.ExecutionContext())
        {
            _useEnhancedUI = enhancedUI;
            
            // Try to load the CursesUI if enhanced mode is requested
            if (_useEnhancedUI)
            {
                try
                {
                    // Look for the CursesUI type in all loaded assemblies
                    var cursesUIType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .FirstOrDefault(t => t.Name == "CursesUI");
                    
                    if (cursesUIType != null)
                    {
                        _enhancedUiInstance = Activator.CreateInstance(cursesUIType);
                        Console.WriteLine("CursesUI loaded successfully");
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Curses UI requested but CursesUI class not found");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading CursesUI: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Gets information about whether the enhanced UI (curses) is being used
        /// </summary>
        public bool IsUsingEnhancedUI => _useEnhancedUI && _enhancedUiInstance != null;
        
        /// <summary>
        /// Starts the service
        /// </summary>
        public override void Start()
        {
            base.Start();
            
            Console.WriteLine("ConsoleUIService starting!");
            
            if (_useEnhancedUI)
            {
                Console.WriteLine($"POKER GAME UI INITIALIZATION: Curses UI flag: {_useEnhancedUI}, Curses UI instance exists: {_enhancedUiInstance != null}");
                
                // If we have an enhanced UI instance, try to initialize it
                if (_enhancedUiInstance != null)
                {
                    try
                    {
                        // Get the Initialize method
                        var initMethod = _enhancedUiInstance.GetType().GetMethod("Initialize");
                        if (initMethod != null)
                        {
                            // Call the Initialize method
                            initMethod.Invoke(_enhancedUiInstance, null);
                            Console.WriteLine("CursesUI initialized successfully");
                        }
                        else
                        {
                            Console.WriteLine("ERROR: CursesUI does not have an Initialize method");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error initializing CursesUI: {ex.Message}");
                    }
                }
            }
            
            // Start the background input processing task
            _inputProcessingTask = Task.Run(ProcessUserInputAsync);
            
            // Register our service
            PublishServiceRegistration();
            
            // Start discovery to find the game engine
            _ = Task.Run(DiscoverGameEngineAsync);
        }
        
        /// <summary>
        /// Stops the service
        /// </summary>
        public override void Stop()
        {
            // Cancel any running tasks
            _cancellationTokenSource?.Cancel();
            
            if (_useEnhancedUI && _enhancedUiInstance != null)
            {
                try
                {
                    // Get the Cleanup method
                    var cleanupMethod = _enhancedUiInstance.GetType().GetMethod("Cleanup");
                    if (cleanupMethod != null)
                    {
                        // Call the Cleanup method
                        cleanupMethod.Invoke(_enhancedUiInstance, null);
                        Console.WriteLine("CursesUI cleaned up successfully");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error cleaning up CursesUI: {ex.Message}");
                }
            }
            
            base.Stop();
        }
        
        /// <summary>
        /// Publishes service registration information
        /// </summary>
        private new void PublishServiceRegistration()
        {
            // Socket management is handled by the base class and SocketCommunicationAdapter
            var message = Message.Create(MessageType.ServiceRegistration);
            message.MessageId = Guid.NewGuid().ToString();
            message.SenderId = _serviceId;
            
            // Create ServiceRegistrationPayload
            var payload = new ServiceRegistrationPayload
            {
                ServiceId = _serviceId,
                ServiceName = "ConsoleUI",
                ServiceType = "ConsoleUI"
            };
            
            // Set payload using serialization
            message.SetPayload(payload);
            
            // Broadcast the registration
            Broadcast(message);
            
            Console.WriteLine($"Published service registration: ConsoleUI with ID {_serviceId}");
        }
        
        /// <summary>
        /// Handles an incoming message
        /// </summary>
        /// <param name="message">The message to handle</param>
        public override async Task HandleMessageAsync(Message message)
        {
            if (message == null)
            {
                Console.WriteLine("Warning: Received null message in ConsoleUIService");
                return;
            }
            
            try
            {
                // Check message type
                switch (message.Type)
                {
                    case MessageType.ServiceRegistration:
                        // Get ServiceRegistrationPayload from message
                        var regPayload = message.GetPayload<ServiceRegistrationPayload>();
                        if (regPayload != null && regPayload.ServiceType == "GameEngine")
                        {
                            string serviceId = message.SenderId;
                            if (string.IsNullOrEmpty(_gameEngineServiceId))
                            {
                                _gameEngineServiceId = serviceId;
                                Console.WriteLine($"Connected to game engine service: {_gameEngineServiceId}");
                            }
                        }
                        break;
                        
                    case MessageType.ServiceDiscovery:
                        // Similar to ServiceRegistration
                        var discoveryPayload = message.GetPayload<ServiceRegistrationPayload>();
                        if (discoveryPayload != null && discoveryPayload.ServiceType == "GameEngine")
                        {
                            string serviceId = message.SenderId;
                            if (string.IsNullOrEmpty(_gameEngineServiceId))
                            {
                                _gameEngineServiceId = serviceId;
                                Console.WriteLine($"Connected to game engine service: {_gameEngineServiceId}");
                            }
                        }
                        break;
                        
                    case MessageType.GameState:
                        // Process a game update
                        if (_gameEngineServiceId == message.SenderId)
                        {
                            var gameStatePayload = message.GetPayload<GameStatePayload>();
                            if (gameStatePayload != null)
                            {
                                // Need to convert Game.GameState to Models.GameState
                                _currentGameState = (Models.GameState)(int)gameStatePayload.CurrentState;
                                _communityCards = gameStatePayload.CommunityCards;
                                _pot = gameStatePayload.Pot;
                                _currentBet = gameStatePayload.CurrentBet;
                                
                                // Convert PlayerInfo to Player
                                _players.Clear();
                                foreach (var playerInfo in gameStatePayload.Players)
                                {
                                    var player = new Player
                                    {
                                        Id = playerInfo.PlayerId,
                                        Name = playerInfo.Name,
                                        Chips = playerInfo.Chips,
                                        CurrentBet = playerInfo.CurrentBet,
                                        HasFolded = playerInfo.HasFolded,
                                        IsAllIn = playerInfo.IsAllIn,
                                        HoleCards = playerInfo.HoleCards
                                    };
                                    _players[player.Id] = player;
                                }
                                
                                await DisplayGameStateAsync();
                            }
                        }
                        break;
                        
                    case MessageType.PlayerAction:
                        // Handle a request for player action
                        if (_gameEngineServiceId == message.SenderId)
                        {
                            var actionPayload = message.GetPayload<PlayerActionPayload>();
                            if (actionPayload != null)
                            {
                                _activePlayerId = actionPayload.PlayerId;
                                _waitingForPlayerAction = true;
                                
                                Console.WriteLine($"Your turn! Player: {_activePlayerId}");
                                Console.WriteLine("Enter F (fold), C (check/call), or R (raise):");
                            }
                        }
                        break;
                        
                    case MessageType.HandComplete:
                        // Handle game results
                        if (_gameEngineServiceId == message.SenderId)
                        {
                            var resultPayload = message.GetPayload<GameStatePayload>();
                            if (resultPayload != null && resultPayload.WinnerIds.Count > 0)
                            {
                                Console.WriteLine("==== GAME RESULTS ====");
                                foreach (var winnerId in resultPayload.WinnerIds)
                                {
                                    // Find the player in the updated player list
                                    var winnerInfo = resultPayload.Players.FirstOrDefault(p => p.PlayerId == winnerId);
                                    if (winnerInfo != null)
                                    {
                                        Console.WriteLine($"Winner: {winnerInfo.Name} with {winnerInfo.Chips} chips");
                                    }
                                    else if (_players.ContainsKey(winnerId))
                                    {
                                        var winner = _players[winnerId];
                                        Console.WriteLine($"Winner: {winner.Name} with {winner.Chips} chips");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Winner ID: {winnerId} (Player info not available)");
                                    }
                                }
                                Console.WriteLine("=====================");
                            }
                        }
                        break;
                        
                    case MessageType.Debug:
                        // Log debug messages
                        Console.WriteLine($"Debug message from {message.SenderId}: {message.Payload}");
                        break;
                        
                    case MessageType.StartHand:
                        Console.WriteLine("RECEIVED STARTHAND MESSAGE RESPONSE");
                        break;
                        
                    default:
                        Console.WriteLine($"Unhandled message type: {message.Type} from {message.SenderId}");
                        break;
                }
                
                // Custom acknowledgment handling
                try
                {
                    // Check if we need to send an acknowledgment back
                    if (!string.IsNullOrEmpty(message.MessageId))
                    {
                        // Create a Core message acknowledgment
                        var ackMessage = Message.Create(MessageType.Acknowledgment);
                        ackMessage.SenderId = _serviceId;
                        ackMessage.ReceiverId = message.SenderId;
                        ackMessage.InResponseTo = message.MessageId;
                        
                        // Send the acknowledgment through central broker instead of direct broadcast
                        Console.WriteLine($"Sending acknowledgment for message {message.MessageId} through central broker");
                        BrokerManager.Instance.CentralBroker?.Publish(ackMessage.ToNetworkMessage());
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling acknowledgment: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling message: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Displays the current game state
        /// </summary>
        private async Task DisplayGameStateAsync()
        {
            if (_useEnhancedUI && _enhancedUiInstance != null)
            {
                try
                {
                    // Use reflection to call the enhanced UI methods for display
                    var updateMethod = _enhancedUiInstance.GetType().GetMethod("UpdateDisplay");
                    if (updateMethod != null)
                    {
                        // Get the parameters for the update method
                        var parameters = updateMethod.GetParameters();
                        if (parameters.Length == 3)
                        {
                            // Parameters for the method are: List<Card>, Dictionary<string, Player>, int
                            updateMethod.Invoke(_enhancedUiInstance, new object[] { _communityCards, _players, _pot });
                        }
                        else
                        {
                            Console.WriteLine("ERROR: UpdateDisplay method has incorrect parameter count");
                        }
                    }
                    else
                    {
                        Console.WriteLine("ERROR: UpdateDisplay method not found on CursesUI");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating CursesUI display: {ex.Message}");
                }
            }
            else
            {
                // Basic console display
                Console.Clear();
                Console.WriteLine("═══════════════════════════════════════════════");
                Console.WriteLine("               POKER GAME STATE                ");
                Console.WriteLine("═══════════════════════════════════════════════");
                Console.WriteLine($"Game State: {_currentGameState}");
                Console.WriteLine($"Current Pot: {_pot} chips");
                Console.WriteLine($"Current Bet: {_currentBet} chips");
                
                // Display community cards
                Console.WriteLine("\nCommunity Cards:");
                if (_communityCards.Count == 0)
                {
                    Console.WriteLine("  (No community cards yet)");
                }
                else
                {
                    foreach (var card in _communityCards)
                    {
                        Console.Write($" {FormatCard(card)}");
                    }
                    Console.WriteLine();
                }
                
                // Display players
                Console.WriteLine("\nPlayers:");
                foreach (var player in _players.Values)
                {
                    string status = "";
                    if (player.HasFolded)
                        status = "[FOLDED]";
                    else if (player.IsAllIn)
                        status = "[ALL IN]";
                    
                    Console.WriteLine($"  {player.Name}: {player.Chips} chips, Bet: {player.CurrentBet} {status}");
                    
                    // Show this player's hole cards
                    if (player.Id == _activePlayerId && !player.HasFolded)
                    {
                        Console.Write("    Cards: ");
                        foreach (var card in player.HoleCards)
                        {
                            Console.Write($" {FormatCard(card)}");
                        }
                        Console.WriteLine();
                    }
                }
                
                Console.WriteLine("═══════════════════════════════════════════════");
            }
            
            await Task.CompletedTask; // for async signature
        }
        
        /// <summary>
        /// Formats a card for display
        /// </summary>
        /// <param name="card">The card to format</param>
        /// <returns>A string representation of the card</returns>
        private string FormatCard(Card card)
        {
            string symbol = "";
            switch (card.Suit)
            {
                case Suit.Hearts:
                    symbol = "♥";
                    break;
                case Suit.Diamonds:
                    symbol = "♦";
                    break;
                case Suit.Clubs:
                    symbol = "♣";
                    break;
                case Suit.Spades:
                    symbol = "♠";
                    break;
            }
            
            string value = "";
            switch (card.Rank)
            {
                case Rank.Two:
                    value = "2";
                    break;
                case Rank.Three:
                    value = "3";
                    break;
                case Rank.Four:
                    value = "4";
                    break;
                case Rank.Five:
                    value = "5";
                    break;
                case Rank.Six:
                    value = "6";
                    break;
                case Rank.Seven:
                    value = "7";
                    break;
                case Rank.Eight:
                    value = "8";
                    break;
                case Rank.Nine:
                    value = "9";
                    break;
                case Rank.Ten:
                    value = "10";
                    break;
                case Rank.Jack:
                    value = "J";
                    break;
                case Rank.Queen:
                    value = "Q";
                    break;
                case Rank.King:
                    value = "K";
                    break;
                case Rank.Ace:
                    value = "A";
                    break;
            }
            
            return $"{value}{symbol}";
        }

        /// <summary>
        /// Discovers the game engine service
        /// </summary>
        private async Task DiscoverGameEngineAsync()
        {
            int maxAttempts = 10;
            int delayMs = 500;
            
            Console.WriteLine($"Starting game engine discovery (max attempts: {maxAttempts}, delay: {delayMs}ms)");
            
            // Explicitly subscribe to ServiceRegistration messages
            string subscriptionId = BrokerManager.Instance.CentralBroker?.Subscribe(
                Messaging.MessageType.ServiceRegistration, 
                HandleServiceRegistration) ?? "";
                
            Console.WriteLine($"Subscribed to ServiceRegistration messages with ID: {subscriptionId}");
            
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!string.IsNullOrEmpty(_gameEngineServiceId))
                {
                    Console.WriteLine($"Game engine already discovered: {_gameEngineServiceId}");
                    break;
                }
                
                Console.WriteLine($"Discovery attempt {attempt + 1}/{maxAttempts}...");
                
                // Send a service discovery message through central broker using direct NetworkMessage creation
                var discoveryNetworkMsg = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = Messaging.MessageType.ServiceDiscovery,  // Use Messaging.MessageType directly
                    SenderId = _serviceId,
                    Timestamp = DateTime.UtcNow
                };
                
                Console.WriteLine($"====> [PlayerUI {_serviceId}] Broadcasting message {discoveryNetworkMsg.Type} (ID: {discoveryNetworkMsg.MessageId}) through central broker");
                BrokerManager.Instance.CentralBroker?.Publish(discoveryNetworkMsg);
                
                // Wait a bit
                await Task.Delay(delayMs);
            }
            
            // Final check
            if (string.IsNullOrEmpty(_gameEngineServiceId))
            {
                Console.WriteLine("WARNING: Could not discover game engine service after multiple attempts");
                
                // Fall back to static ID for testing 
                // This needs to match the static ID set in GameEngineService
                _gameEngineServiceId = "static_game_engine_service";
                
                Console.WriteLine($"Using fallback static game engine ID: {_gameEngineServiceId}");
                
                // Send a debug message through the broker using direct NetworkMessage creation
                var debugNetworkMsg = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = Messaging.MessageType.Debug,  // Use Messaging.MessageType directly
                    SenderId = _serviceId,
                    ReceiverId = _gameEngineServiceId,
                    Timestamp = DateTime.UtcNow,
                    Payload = $"Connection attempt from ConsoleUI ({_serviceId}) to GameEngine via central broker"
                };
                
                // Important: Use CentralBroker to publish the message
                Console.WriteLine($"DIRECT: Sending message type {debugNetworkMsg.Type} to {_gameEngineServiceId} through central broker");
                BrokerManager.Instance.CentralBroker?.Publish(debugNetworkMsg);
                
                // For logging
                Console.WriteLine($"Starting game with engine: {_gameEngineServiceId}");
            }
            
            // Start the game automatically for testing
            await StartGameAsync();
        }
        
        /// <summary>
        /// Starts a new game
        /// </summary>
        private async Task StartGameAsync()
        {
            if (string.IsNullOrEmpty(_gameEngineServiceId))
            {
                Console.WriteLine("ERROR: Cannot start game - game engine service not discovered");
                return;
            }
            
            Console.WriteLine($"Starting game with engine: {_gameEngineServiceId}");
            
            try
            {
                // Create a few players for testing
                var players = new List<PlayerInfo>
                {
                    new PlayerInfo { PlayerId = "Player1", Name = "Alice", Chips = 1000 },
                    new PlayerInfo { PlayerId = "Player2", Name = "Bob", Chips = 1000 },
                    new PlayerInfo { PlayerId = "Player3", Name = "Charlie", Chips = 1000 },
                    new PlayerInfo { PlayerId = "Player4", Name = "Dave", Chips = 1000 }
                };
                
                // First, send a service discovery message through central broker using direct NetworkMessage creation
                var discoveryNetworkMsg = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = Messaging.MessageType.ServiceDiscovery,  // Use Messaging.MessageType directly
                    SenderId = _serviceId,
                    Timestamp = DateTime.UtcNow
                };
                
                Console.WriteLine($"====> [PlayerUI {_serviceId}] Broadcasting message {discoveryNetworkMsg.Type} (ID: {discoveryNetworkMsg.MessageId}) through central broker");
                BrokerManager.Instance.CentralBroker?.Publish(discoveryNetworkMsg);
                
                await Task.Delay(500);
                
                // Send a setup message to configure the game using direct NetworkMessage creation
                // Create game config payload
                var gameConfig = new
                {
                    AnteAmount = 10,
                    StartingChips = 1000,
                    SmallBlind = 5,
                    BigBlind = 10,
                    Players = players
                };
                
                // Create the setup message directly as a NetworkMessage
                var setupNetworkMsg = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = Messaging.MessageType.StartGame,  // Use Messaging.MessageType directly
                    SenderId = _serviceId,
                    ReceiverId = _gameEngineServiceId,
                    Timestamp = DateTime.UtcNow,
                    Payload = System.Text.Json.JsonSerializer.Serialize(gameConfig)
                };
                
                Console.WriteLine("Sending game setup message...");
                Console.WriteLine($"DIRECT: Sending message type {setupNetworkMsg.Type} to {_gameEngineServiceId} through central broker");
                BrokerManager.Instance.CentralBroker?.Publish(setupNetworkMsg);
                
                await Task.Delay(500);
                
                // Send a message to start the game using direct NetworkMessage creation
                var startNetworkMsg = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = Messaging.MessageType.StartGame,  // Use Messaging.MessageType directly
                    SenderId = _serviceId,
                    ReceiverId = _gameEngineServiceId,
                    Timestamp = DateTime.UtcNow
                };
                
                Console.WriteLine("Sending start game message...");
                Console.WriteLine($"DIRECT: Sending message type {startNetworkMsg.Type} to {_gameEngineServiceId} through central broker");
                BrokerManager.Instance.CentralBroker?.Publish(startNetworkMsg);
                
                await Task.Delay(500);
                
                // Send a message to start the first hand
                // Create a NetworkMessage directly for StartHand to avoid conversion issues 
                var networkMessage = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = PokerGame.Core.Messaging.MessageType.StartHand,  // Use fully qualified name
                    SenderId = _serviceId,
                    ReceiverId = _gameEngineServiceId,
                    Timestamp = DateTime.UtcNow,
                    Headers = new Dictionary<string, string>
                    {
                        { "MessageSubType", "StartHand" }  // Add explicit subtype for better routing
                    }
                };
                
                Console.WriteLine("Sending start hand message...");
                Console.WriteLine($"DIRECT: Sending message type {networkMessage.Type} to {_gameEngineServiceId} through central broker");
                BrokerManager.Instance.CentralBroker?.Publish(networkMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting game: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sends a player action to the game engine
        /// </summary>
        /// <param name="actionType">The type of action (fold, check, call, raise)</param>
        /// <param name="betAmount">The bet amount for raise actions</param>
        private void SendPlayerAction(string actionType, int betAmount = 0)
        {
            if (string.IsNullOrEmpty(_gameEngineServiceId) || string.IsNullOrEmpty(_activePlayerId))
            {
                Console.WriteLine("Cannot send player action - game engine or active player not set");
                return;
            }
            
            try
            {
                // Create player action payload
                var actionPayload = new PlayerActionPayload
                {
                    PlayerId = _activePlayerId,
                    ActionType = actionType,
                    BetAmount = betAmount
                };
                
                // Create the player action message directly as a NetworkMessage
                var actionNetworkMsg = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = PokerGame.Core.Messaging.MessageType.PlayerAction,  // Use fully qualified name
                    SenderId = _serviceId,
                    ReceiverId = _gameEngineServiceId,
                    Timestamp = DateTime.UtcNow,
                    Payload = System.Text.Json.JsonSerializer.Serialize(actionPayload),
                    Headers = new Dictionary<string, string>
                    {
                        { "MessageSubType", "PlayerAction" }  // Add explicit subtype for better routing
                    }
                };
                
                Console.WriteLine($"Sending player action: {actionType} {(betAmount > 0 ? $"with bet {betAmount}" : "")}");
                Console.WriteLine($"DIRECT: Sending message type {actionNetworkMsg.Type} to {_gameEngineServiceId} through central broker");
                BrokerManager.Instance.CentralBroker?.Publish(actionNetworkMsg);
                
                _waitingForPlayerAction = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending player action: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Processes user input for player actions
        /// </summary>
        private async Task ProcessUserInputAsync()
        {
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (_waitingForPlayerAction)
                    {
                        // Check if console input is available
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            
                            switch (char.ToUpper(key.KeyChar))
                            {
                                case 'F':
                                    // Fold
                                    SendPlayerAction("fold");
                                    break;
                                    
                                case 'C':
                                    // Check/Call
                                    SendPlayerAction("call");
                                    break;
                                    
                                case 'R':
                                    // Raise
                                    Console.WriteLine("Enter raise amount:");
                                    string input = Console.ReadLine();
                                    if (int.TryParse(input, out int raiseAmount) && raiseAmount > 0)
                                    {
                                        SendPlayerAction("raise", raiseAmount);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Invalid raise amount, please try again");
                                    }
                                    break;
                                    
                                default:
                                    Console.WriteLine("Invalid action, please enter F (fold), C (check/call), or R (raise)");
                                    break;
                            }
                        }
                    }
                    
                    // Check for command input even when not waiting for player action
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        
                        // Global commands
                        switch (char.ToUpper(key.KeyChar))
                        {
                            case 'Q':
                                // Quit
                                Console.WriteLine("Quitting...");
                                _cancellationTokenSource.Cancel();
                                break;
                                
                            case 'S':
                                // Start game
                                await StartGameAsync();
                                break;
                                
                            case 'N':
                                // New hand
                                if (!string.IsNullOrEmpty(_gameEngineServiceId))
                                {
                                    // Create a NetworkMessage directly to avoid message type conversion issues
                                    var networkMessage = new NetworkMessage
                                    {
                                        MessageId = Guid.NewGuid().ToString(),
                                        Type = PokerGame.Core.Messaging.MessageType.StartHand,  // Use fully qualified name
                                        SenderId = _serviceId,
                                        ReceiverId = _gameEngineServiceId,
                                        Timestamp = DateTime.UtcNow,
                                        Headers = new Dictionary<string, string>
                                        {
                                            { "MessageSubType", "StartHand" }  // Add explicit subtype for better routing
                                        }
                                    };
                                    
                                    Console.WriteLine("Sending start hand message...");
                                    Console.WriteLine($"DIRECT: Sending message type {networkMessage.Type} to {_gameEngineServiceId} through central broker");
                                    BrokerManager.Instance.CentralBroker?.Publish(networkMessage);
                                }
                                break;
                                
                            case 'D':
                                // Refresh display
                                await DisplayGameStateAsync();
                                break;
                        }
                    }
                    
                    // Sleep to avoid excessive CPU usage
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing user input: {ex.Message}");
            }
        }
    }
}