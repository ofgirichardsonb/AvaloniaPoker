using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PokerGame.Core.Game;
using PokerGame.Core.Models;
using PokerGame.Core.ServiceManagement;

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
        
        private readonly bool _useEnhancedUI = false;
        private object _enhancedUiInstance = null;
        private Task _inputProcessingTask = null;
        private string _gameEngineServiceId = null;
        private bool _waitingForPlayerAction = false;
        private string _activePlayerId = null;
        private Dictionary<string, Models.Player> _players = new Dictionary<string, Models.Player>();
        private GameState _currentGameState = GameState.NotStarted;
        private List<Card> _communityCards = new List<Card>();
        private int _pot = 0;
        private int _currentBet = 0;
        private int _publisherPort;
        private int _subscriberPort;
        
        /// <summary>
        /// Creates a new instance of the ConsoleUIService
        /// </summary>
        /// <param name="publisherPort">Publisher port</param>
        /// <param name="subscriberPort">Subscriber port</param>
        /// <param name="enhancedUI">Whether to use enhanced UI mode (box drawing, etc)</param>
        public ConsoleUIService(int publisherPort, int subscriberPort, bool enhancedUI = false) 
            : base(publisherPort, subscriberPort)
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
            : base(executionContext)
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
        /// Starts the console UI service
        /// </summary>
        public override async Task StartAsync()
        {
            // Ensure the service ID is set
            _serviceId = _serviceId ?? Guid.NewGuid().ToString();
            
            Console.WriteLine($"ConsoleUIService starting with ID: {_serviceId}");
            
            // Use the base class cancellation token source that is already set up
            _cancellationTokenSource = _cancellationTokenSource ?? new CancellationTokenSource();
            
            // Start discovery in the background
            _ = Task.Run(ActiveServiceDiscoveryAsync);
            
            // Start processing user input in a separate task
            _inputProcessingTask = Task.Run(ProcessUserInputAsync);
            
            PublishServiceRegistration();
            
            Console.WriteLine("ConsoleUIService started successfully");
        }
        
        /// <summary>
        /// Actively discovers services by periodically sending discovery messages
        /// </summary>
        private async Task ActiveServiceDiscoveryAsync()
        {
            // Use the base class cancellation token
            var token = _cancellationTokenSource?.Token ?? CancellationToken.None;
            
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // Send a service discovery message
                        var message = Message.Create(MessageType.ServiceDiscovery);
                        message.SenderId = _serviceId;
                        Broadcast(message);
                        
                        // Wait before sending again
                        await Task.Delay(5000, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in service discovery: {ex.Message}");
                        await Task.Delay(1000, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Active service discovery exited with error: {ex.Message}");
            }
            
            Console.WriteLine("Service discovery task exited");
        }
        
        /// <summary>
        /// Publishes the service registration to the network
        /// </summary>
        protected void PublishServiceRegistration()
        {
            // Socket management is handled by the base class and SocketCommunicationAdapter
            var message = Message.Create(MessageType.ServiceRegistration);
            message.MessageId = Guid.NewGuid().ToString();
            message.SenderId = _serviceId;
            message.Data = new Dictionary<string, object>
            {
                { "ServiceId", _serviceId },
                { "ServiceType", "ConsoleUI" }
            };
            
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
                switch (message.MessageType)
                {
                    case MessageType.ServiceRegistration:
                        if (message.Data != null && 
                            message.Data.ContainsKey("ServiceType") && 
                            message.Data["ServiceType"].ToString() == "GameEngine")
                        {
                            string serviceId = message.SenderId;
                            if (string.IsNullOrEmpty(_gameEngineServiceId))
                            {
                                _gameEngineServiceId = serviceId;
                                Console.WriteLine($"Connected to game engine service: {_gameEngineServiceId}");
                            }
                        }
                        break;
                        
                    case MessageType.ServiceDiscoveryResponse:
                        if (message.Data != null && 
                            message.Data.ContainsKey("ServiceType") && 
                            message.Data["ServiceType"].ToString() == "GameEngine")
                        {
                            string serviceId = message.SenderId;
                            if (string.IsNullOrEmpty(_gameEngineServiceId))
                            {
                                _gameEngineServiceId = serviceId;
                                Console.WriteLine($"Connected to game engine service: {_gameEngineServiceId}");
                            }
                        }
                        break;
                        
                    case MessageType.GameUpdate:
                        // Process a game update
                        if (message.Data != null && _gameEngineServiceId == message.SenderId)
                        {
                            if (message.Data.ContainsKey("GameState") && message.Data["GameState"] is GameState gameState)
                            {
                                _currentGameState = gameState;
                            }
                            
                            if (message.Data.ContainsKey("CommunityCards") && message.Data["CommunityCards"] is List<Card> communityCards)
                            {
                                _communityCards = communityCards;
                            }
                            
                            if (message.Data.ContainsKey("Pot") && message.Data["Pot"] is int pot)
                            {
                                _pot = pot;
                            }
                            
                            if (message.Data.ContainsKey("CurrentBet") && message.Data["CurrentBet"] is int currentBet)
                            {
                                _currentBet = currentBet;
                            }
                            
                            if (message.Data.ContainsKey("Players") && message.Data["Players"] is Dictionary<string, Models.Player> players)
                            {
                                _players = players;
                            }
                            
                            await DisplayGameStateAsync();
                        }
                        break;
                        
                    case MessageType.PlayerActionRequest:
                        // Handle a request for player action
                        if (message.Data != null && _gameEngineServiceId == message.SenderId)
                        {
                            if (message.Data.ContainsKey("PlayerId"))
                            {
                                _activePlayerId = message.Data["PlayerId"].ToString();
                                _waitingForPlayerAction = true;
                                
                                Console.WriteLine($"Your turn! Player: {_activePlayerId}");
                                Console.WriteLine("Enter F (fold), C (check/call), or R (raise):");
                            }
                        }
                        break;
                        
                    case MessageType.GameResult:
                        // Handle game results
                        if (message.Data != null && _gameEngineServiceId == message.SenderId)
                        {
                            if (message.Data.ContainsKey("Winners") && message.Data["Winners"] is List<string> winnerIds)
                            {
                                Console.WriteLine("==== GAME RESULTS ====");
                                foreach (var winnerId in winnerIds)
                                {
                                    if (_players.ContainsKey(winnerId))
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
                        Console.WriteLine($"Debug message from {message.SenderId}: {message.Data}");
                        break;
                        
                    case MessageType.StartHand:
                        Console.WriteLine("RECEIVED STARTHAND MESSAGE RESPONSE");
                        break;
                        
                    default:
                        Console.WriteLine($"Unhandled message type: {message.MessageType} from {message.SenderId}");
                        break;
                }
                
                // Acknowledge the message if requested
                if (message.Data != null && 
                    message.Data.ContainsKey("RequireAck") && 
                    (bool)message.Data["RequireAck"])
                {
                    var ackMessage = Message.Create(MessageType.Acknowledgement);
                    ackMessage.MessageId = Guid.NewGuid().ToString();
                    ackMessage.SenderId = _serviceId;
                    ackMessage.ReceiverId = message.SenderId;
                    ackMessage.Data = new Dictionary<string, object>
                    {
                        { "AcknowledgedMessageId", message.MessageId }
                    };
                    
                    SendTo(ackMessage, message.SenderId);
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
            try
            {
                // Clear the console for a fresh display
                Console.Clear();
                
                if (_useEnhancedUI && _enhancedUiInstance != null)
                {
                    // Enhanced UI with box drawing characters
                    await DisplayGameStateEnhancedAsync();
                }
                else
                {
                    // Simple text-based UI
                    await DisplayGameStateSimpleAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error displaying game state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Continuously processes user input
        /// </summary>
        private async Task ProcessUserInputAsync()
        {
            try
            {
                // Setup initial game
                await SetupGameAsync();
                
                // Use the cancellation token from the base class
                var token = _cancellationTokenSource?.Token ?? CancellationToken.None;
                
                while (!token.IsCancellationRequested)
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
                                if (_currentGameState != GameState.NotStarted)
                                {
                                    // Simple auto-play logic: 
                                    // - Always call if bet is small (<= 10% of chips)
                                    // - Always check if possible
                                    // - Otherwise fold
                                    var playerInfo = _players[_activePlayerId];
                                    bool canCheck = _currentBet == playerInfo.CurrentBet;
                                    int betToCall = _currentBet - playerInfo.CurrentBet;
                                    
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
                                        bool canCheck = _currentBet == playerInfo.CurrentBet;
                                        
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
                        else if (_currentGameState == GameState.HandComplete ||
                                _currentGameState == GameState.WaitingToStart)
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
                                // Start a new hand with enhanced logging
                                Console.WriteLine("\n\n");
                                Console.WriteLine("**********************************************************");
                                Console.WriteLine("*                                                        *");
                                Console.WriteLine("*            CONSOLE UI SENDING STARTHAND                *");
                                Console.WriteLine("*                                                        *");
                                Console.WriteLine("**********************************************************");
                                Console.WriteLine("\n\n");
                                
                                var message = Message.Create(MessageType.StartHand);
                                message.MessageId = Guid.NewGuid().ToString(); // Ensure unique message ID
                                message.SenderId = _serviceId; // Set sender ID explicitly
                                message.ReceiverId = _gameEngineServiceId; // Explicitly target the game engine
                                
                                Console.WriteLine($"StartHand message ID: {message.MessageId}");
                                Console.WriteLine($"StartHand sender ID: {message.SenderId}");
                                Console.WriteLine($"StartHand recipient ID: {_gameEngineServiceId}");
                                
                                // Log to file - using improved FileLogger that finds writable location
                                PokerGame.Core.Logging.FileLogger.MessageTrace("ConsoleUI", 
                                    $"SENDING STARTHAND MESSAGE - ID: {message.MessageId}, Recipient: {_gameEngineServiceId}");
                                    
                                // Also echo to console with more visibility
                                Console.WriteLine($">>>>>> MESSAGE TRACE: [ConsoleUI] SENDING STARTHAND MESSAGE - ID: {message.MessageId}, Recipient: {_gameEngineServiceId} <<<<<<");
                                
                                // Try multiple delivery methods to ensure reliability
                                Console.WriteLine("1. Broadcasting StartHand message to all services");
                                Broadcast(message);
                                
                                Console.WriteLine("2. Direct sending StartHand to " + _gameEngineServiceId);
                                SendTo(message, _gameEngineServiceId);
                                
                                // Also try the most reliable method with acknowledgment
                                try 
                                {
                                    Console.WriteLine("3. Attempting send with acknowledgment");
                                    bool sent = await PokerGame.Core.Messaging.MessageBrokerExtensions.SendWithAcknowledgmentAsync(
                                        this, 
                                        message, 
                                        _gameEngineServiceId, 
                                        timeoutMs: 5000, 
                                        maxRetries: 3,
                                        useExponentialBackoff: true);
                                        
                                    if (sent)
                                    {
                                        Console.WriteLine("StartHand sent with acknowledgment!");
                                    }
                                    else
                                    {
                                        Console.WriteLine("Failed to send StartHand with acknowledgment after retries");
                                        Console.WriteLine("But don't worry, we already tried broadcast and direct send methods");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error sending with acknowledgment: {ex.Message}");
                                    // Stack trace might be too verbose, but helpful for debugging
                                    Console.WriteLine(ex.StackTrace);
                                }
                                
                                Console.WriteLine("StartHand message sent using multiple delivery methods, waiting for response...");
                                
                                PokerGame.Core.Logging.FileLogger.MessageTrace("ConsoleUI", 
                                    "StartHand message sent using multiple delivery methods, waiting for response...");
                                    
                                Console.WriteLine("================================================");
                            }
                        }
                        
                        await Task.Delay(100, token); // Small pause to prevent CPU overuse
                    }
                    catch (OperationCanceledException)
                    {
                        // Handle normal cancellation
                        Console.WriteLine("User input processing cancelled.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing input: {ex.Message}");
                    }
                }
                
                Console.WriteLine("Thanks for playing!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ProcessUserInputAsync exited with error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sets up the initial game with player information
        /// </summary>
        private async Task SetupGameAsync()
        {
            Console.Clear();
            
            // Create a more visible debug message about the UI mode
            Console.WriteLine("==== POKER GAME UI INITIALIZATION ====");
            Console.WriteLine($"Curses UI flag: {_useEnhancedUI}");
            Console.WriteLine($"Curses UI instance exists: {_enhancedUiInstance != null}");
            Console.WriteLine("====================================");
            
            if (_useEnhancedUI)
            {
                try 
                {
                    // Try to set console colors for better visuals
                    Console.BackgroundColor = ConsoleColor.Black;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error setting console colors: {ex.Message}");
                }
                
                // Draw a fancy header with box-drawing characters
                Console.WriteLine("╔═════════════════════════════════════════════════════════╗");
                Console.WriteLine("║       TEXAS HOLD'EM POKER - ENHANCED CONSOLE UI         ║");
                Console.WriteLine("╚═════════════════════════════════════════════════════════╝");
                
                try 
                {
                    // Reset colors
                    Console.ResetColor();
                }
                catch { }
                
                Console.WriteLine("Curses UI in microservices mode is ready!");
            }
            else
            {
                Console.WriteLine("===============================================");
                Console.WriteLine("           TEXAS HOLD'EM POKER GAME           ");
                Console.WriteLine("===============================================");
                Console.WriteLine();
            }
            
            // Force the curses UI to true to ensure box-drawing characters are used
            // This is a fallback in case the flag isn't properly passed from command line
            _useEnhancedUI = true;
            
            // Using static service ID approach for reliable direct connection
            Console.WriteLine("=== STATIC SERVICE ID CONNECTION APPROACH ===");
            Console.WriteLine($"Using static game engine ID: {ServiceConstants.StaticServiceIds.GameEngine}");
            
            // Set the game engine service ID directly using the static ID
            _gameEngineServiceId = ServiceConstants.StaticServiceIds.GameEngine;
            
            // Send a direct connection message to the game engine
            var connectMsg = Message.Create(MessageType.Debug, 
                $"DIRECT STATIC CONNECTION: ConsoleUI [{_serviceId}] connecting to GameEngine {_gameEngineServiceId}");
            connectMsg.SenderId = _serviceId;
            connectMsg.ReceiverId = _gameEngineServiceId;
            SendTo(connectMsg, _gameEngineServiceId);
            
            // Still broadcast our presence several times for backward compatibility
            Console.WriteLine("Broadcasting our service registration for backward compatibility...");
            for (int i = 0; i < 3; i++)
            {
                PublishServiceRegistration();
                await Task.Delay(100);
            }
            
            // Wait a moment for connections to establish
            await Task.Delay(500);
            
            // Log connection information
            Console.WriteLine($"Directly connected to game engine using static ID: {_gameEngineServiceId}");
            Console.WriteLine("Service registry contents:");
            var registryTypes = GetServiceTypes();
            foreach (var svc in registryTypes)
            {
                Console.WriteLine($"  - Service {svc.Key}: {svc.Value}");
            }
            
            // Also broadcast using the legacy method for backward compatibility
            var discoveryMsg = Message.Create(MessageType.ServiceDiscovery);
            discoveryMsg.SenderId = _serviceId;
            Broadcast(discoveryMsg);
            
            // Confirm the static ID is being used
            Console.WriteLine("========= STATIC ID CONNECTION APPROACH =========");
            Console.WriteLine($"Using static ID approach with game engine ID: {_gameEngineServiceId}");
            
            // Always ensure we have the game engine ID set
            if (_gameEngineServiceId == null)
            {
                Console.WriteLine("WARNING: Game engine ID was null, using static ID as fallback");
                _gameEngineServiceId = ServiceConstants.StaticServiceIds.GameEngine;
            }
            
            Console.WriteLine($"Connection established with game engine service: {_gameEngineServiceId}");
            Console.WriteLine("=================================================");
            
            // Get player information using the Curses UI if available, otherwise fallback to console
            string[] playerNames;
            int numPlayers = 3; // Default
            
            if (_useEnhancedUI && _enhancedUiInstance != null)
            {
                try
                {
                    Console.WriteLine("Using Curses UI for player setup...");
                    PokerGame.Core.Logging.FileLogger.Info("ConsoleUI", "Using Curses UI for player setup");
                    
                    // Get the CursesUI type via reflection
                    var cursesUIType = _enhancedUiInstance.GetType();
                    
                    // First, make sure we're in a clean state
                    var clearMethod = cursesUIType.GetMethod("ClearScreen");
                    if (clearMethod != null)
                    {
                        clearMethod.Invoke(_enhancedUiInstance, null);
                    }
                    
                    // Try to get the GetPlayerSetup method
                    var setupMethod = cursesUIType.GetMethod("GetPlayerSetup");
                    if (setupMethod != null)
                    {
                        PokerGame.Core.Logging.FileLogger.Info("ConsoleUI", "Calling GetPlayerSetup method on Curses UI");
                        var result = setupMethod.Invoke(_enhancedUiInstance, null);
                        
                        if (result is Tuple<int, string[]> setupResult)
                        {
                            numPlayers = setupResult.Item1;
                            playerNames = setupResult.Item2;
                            PokerGame.Core.Logging.FileLogger.Info("ConsoleUI", $"Got {numPlayers} players from Curses UI");
                        }
                        else
                        {
                            // Fallback in case of unexpected return type
                            PokerGame.Core.Logging.FileLogger.Info("ConsoleUI", "Unexpected return type from GetPlayerSetup, using defaults");
                            numPlayers = 3;
                            playerNames = new string[numPlayers];
                            for (int i = 0; i < numPlayers; i++)
                            {
                                playerNames[i] = $"Player {i+1}";
                            }
                        }
                    }
                    else
                    {
                        // Fallback using default players
                        numPlayers = 3;
                        playerNames = new string[numPlayers];
                        for (int i = 0; i < numPlayers; i++)
                        {
                            playerNames[i] = $"Player {i+1}";
                        }
                        PokerGame.Core.Logging.FileLogger.Info("ConsoleUI", "GetPlayerSetup method not found, using defaults");
                    }
                }
                catch (Exception ex)
                {
                    // Fallback if there's any error with the Curses UI
                    numPlayers = 3;
                    playerNames = new string[numPlayers];
                    for (int i = 0; i < numPlayers; i++)
                    {
                        playerNames[i] = $"Player {i+1}";
                    }
                    PokerGame.Core.Logging.FileLogger.Error("ConsoleUI", $"Error in Curses UI player setup: {ex.Message}");
                    Console.WriteLine($"Error in Curses UI: {ex.Message}");
                }
            }
            else
            {
                // Simple console-based setup
                Console.WriteLine("How many players? (2-8): ");
                string input = Console.ReadLine() ?? "";
                
                if (int.TryParse(input, out int parsedNumPlayers) && parsedNumPlayers >= 2 && parsedNumPlayers <= 8)
                {
                    numPlayers = parsedNumPlayers;
                }
                else
                {
                    Console.WriteLine("Invalid input. Using default: 3 players");
                    numPlayers = 3;
                }
                
                playerNames = new string[numPlayers];
                
                for (int i = 0; i < numPlayers; i++)
                {
                    Console.WriteLine($"Enter name for Player {i+1}: ");
                    string name = Console.ReadLine() ?? "";
                    
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        playerNames[i] = $"Player {i+1}";
                    }
                    else
                    {
                        playerNames[i] = name;
                    }
                }
            }
            
            // Create the game setup message
            var setupMessage = Message.Create(MessageType.GameSetup);
            setupMessage.SenderId = _serviceId;
            setupMessage.ReceiverId = _gameEngineServiceId;
            
            var playerInfos = new Dictionary<string, object>();
            for (int i = 0; i < numPlayers; i++)
            {
                string playerId = $"Player_{i+1}";
                playerInfos[playerId] = new Dictionary<string, object>
                {
                    { "Name", playerNames[i] },
                    { "Chips", 1000 }, // Starting chips
                    { "IsHuman", i == 0 } // Only the first player is human
                };
            }
            
            setupMessage.Data = new Dictionary<string, object>
            {
                { "PlayerInfos", playerInfos },
                { "AnteAmount", 10 },
                { "BlindAmount", 20 }
            };
            
            // Send the setup message
            Console.WriteLine("Sending game setup message...");
            SendTo(setupMessage, _gameEngineServiceId);
            
            // Wait a short time for the game engine to process
            await Task.Delay(500);
            
            // Now send a start hand message to begin the game
            var startHandMessage = Message.Create(MessageType.StartHand);
            startHandMessage.SenderId = _serviceId;
            startHandMessage.ReceiverId = _gameEngineServiceId;
            
            Console.WriteLine("Sending initial start hand message...");
            SendTo(startHandMessage, _gameEngineServiceId);
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
                
                var message = Message.Create(MessageType.PlayerAction);
                message.SenderId = _serviceId;
                message.ReceiverId = _gameEngineServiceId;
                message.Data = new Dictionary<string, object>
                {
                    { "PlayerId", _activePlayerId },
                    { "ActionType", actionType },
                    { "BetAmount", betAmount }
                };
                
                SendTo(message, _gameEngineServiceId);
                
                // Reset waiting flag
                _waitingForPlayerAction = false;
            }
        }
        
        /// <summary>
        /// Displays the game state using a simple text-based UI
        /// </summary>
        private async Task DisplayGameStateSimpleAsync()
        {
            Console.WriteLine("=== POKER GAME ===");
            Console.WriteLine($"Pot: {_pot}  Current Bet: {_currentBet}");
            Console.WriteLine($"Game State: {_currentGameState}");
            
            // Display community cards
            Console.Write("Community Cards: ");
            if (_communityCards != null && _communityCards.Count > 0)
            {
                foreach (var card in _communityCards)
                {
                    Console.Write(FormatCard(card) + " ");
                }
            }
            else
            {
                Console.Write("None");
            }
            Console.WriteLine();
            
            // Display player information
            Console.WriteLine("\nPlayers:");
            foreach (var player in _players.Values)
            {
                string status = !player.IsActive ? "OUT" : player.HasFolded ? "FOLDED" : "IN";
                string holeCards = "";
                
                if (player.HoleCards != null && player.HoleCards.Count > 0)
                {
                    foreach (var card in player.HoleCards)
                    {
                        holeCards += FormatCard(card) + " ";
                    }
                }
                else
                {
                    holeCards = "Hidden";
                }
                
                Console.WriteLine($"{player.Name} - Chips: {player.Chips} - Bet: {player.CurrentBet} - Status: {status}");
                Console.WriteLine($"  Cards: {holeCards}");
            }
            
            Console.WriteLine("\nActions: F (Fold), C (Check/Call), R (Raise)");
            
            await Task.CompletedTask; // This method doesn't need async, but keeping it async for interface consistency
        }
        
        /// <summary>
        /// Displays the game state using an enhanced UI with box drawing characters
        /// </summary>
        private async Task DisplayGameStateEnhancedAsync()
        {
            // Table border
            Console.WriteLine("╔═════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                     POKER GAME                          ║");
            Console.WriteLine("╠═════════════════════════════════════════════════════════╣");
            
            // Game state information
            Console.WriteLine($"║  Pot: {_pot,-6}  Current Bet: {_currentBet,-6}  State: {_currentGameState,-12} ║");
            
            // Community cards
            Console.Write("║  Community Cards: ");
            if (_communityCards != null && _communityCards.Count > 0)
            {
                foreach (var card in _communityCards)
                {
                    Console.Write(FormatCard(card) + " ");
                }
                // Padding to align the box
                int padding = 48 - (_communityCards.Count * 5);
                Console.Write(new string(' ', padding > 0 ? padding : 1));
            }
            else
            {
                Console.Write("None" + new string(' ', 43));
            }
            Console.WriteLine("║");
            
            Console.WriteLine("╠═════════════════════════════════════════════════════════╣");
            
            // Player information
            Console.WriteLine("║                       PLAYERS                           ║");
            Console.WriteLine("╠═════════════════════════════════════════════════════════╣");
            
            foreach (var player in _players.Values)
            {
                string status = !player.IsActive ? "OUT" : player.HasFolded ? "FOLDED" : "IN";
                
                Console.WriteLine($"║  {player.Name,-15} Chips: {player.Chips,-6} Bet: {player.CurrentBet,-6} Status: {status,-8} ║");
                
                Console.Write("║    Cards: ");
                if (player.HoleCards != null && player.HoleCards.Count > 0)
                {
                    foreach (var card in player.HoleCards)
                    {
                        Console.Write(FormatCard(card) + " ");
                    }
                    // Padding
                    int padding = 41 - (player.HoleCards.Count * 5);
                    Console.Write(new string(' ', padding > 0 ? padding : 1));
                }
                else
                {
                    Console.Write("Hidden" + new string(' ', 36));
                }
                Console.WriteLine("║");
                
                // Add a separator between players
                Console.WriteLine("╟─────────────────────────────────────────────────────────╢");
            }
            
            // Actions
            Console.WriteLine("║  Actions: F (Fold), C (Check/Call), R (Raise)             ║");
            Console.WriteLine("╚═════════════════════════════════════════════════════════╝");
            
            await Task.CompletedTask; // This method doesn't need async, but keeping it async for interface consistency
        }
        
        /// <summary>
        /// Formats a card for display
        /// </summary>
        /// <param name="card">The card to format</param>
        /// <returns>A formatted string representation of the card</returns>
        private string FormatCard(Card card)
        {
            string rank;
            switch (card.Rank)
            {
                case Rank.Ace:
                    rank = "A";
                    break;
                case Rank.King:
                    rank = "K";
                    break;
                case Rank.Queen:
                    rank = "Q";
                    break;
                case Rank.Jack:
                    rank = "J";
                    break;
                case Rank.Ten:
                    rank = "T";
                    break;
                default:
                    rank = ((int)card.Rank).ToString();
                    break;
            }
            
            string suit;
            switch (card.Suit)
            {
                case Suit.Spades:
                    suit = "♠";
                    break;
                case Suit.Hearts:
                    suit = "♥";
                    break;
                case Suit.Diamonds:
                    suit = "♦";
                    break;
                case Suit.Clubs:
                    suit = "♣";
                    break;
                default:
                    suit = "?";
                    break;
            }
            
            return $"[{rank}{suit}]";
        }
        
        /// <summary>
        /// Clean up resources and dispose the Curses UI if it was initialized
        /// </summary>
        public override void Dispose()
        {
            Console.WriteLine("ConsoleUIService.Dispose() called - beginning clean shutdown");
            
            try
            {
                // Wait for the input processing task to complete
                if (_inputProcessingTask != null && !_inputProcessingTask.IsCompleted)
                {
                    Console.WriteLine("Waiting for input processing task to complete");
                    // Don't wait indefinitely - set a timeout
                    bool completed = _inputProcessingTask.Wait(TimeSpan.FromSeconds(3));
                    if (!completed)
                    {
                        Console.WriteLine("Input processing task did not complete in time");
                    }
                }
                
                // Clean up the Curses UI if it was initialized
                if (_enhancedUiInstance != null)
                {
                    Console.WriteLine("Disposing Curses UI");
                    try
                    {
                        // Use reflection to check if the instance implements IDisposable
                        var disposable = _enhancedUiInstance as IDisposable;
                        if (disposable != null)
                        {
                            disposable.Dispose();
                            Console.WriteLine("Curses UI properly disposed");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error disposing Curses UI: {ex.Message}");
                    }
                    _enhancedUiInstance = null;
                }
                
                // Base class handles socket cleanup
                base.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ConsoleUIService.Dispose(): {ex.Message}");
            }
            
            Console.WriteLine("ConsoleUIService shutdown complete");
        }
    }
}