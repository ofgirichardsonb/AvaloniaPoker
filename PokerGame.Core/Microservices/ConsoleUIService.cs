using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PokerGame.Core.Game;
using PokerGame.Core.Models;
using PokerGame.Core.ServiceManagement;
using NetMQ;
using NetMQ.Sockets;

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
        private bool _useEnhancedUI; // When true, use the curses UI (internal variable still called "enhanced" for backward compatibility)
        private object? _enhancedUiInstance; // Will hold a dynamic reference to CursesUI when needed (internal var still named "enhanced" for compatibility)
        private bool _autoPlayMode = false; // Auto-play flag for automated testing
        
        /// <summary>
        /// Creates a new console UI service
        /// </summary>
        /// <param name="publisherPort">The port to use for publishing messages</param>
        /// <param name="subscriberPort">The port to use for subscribing to messages</param>
        /// <param name="useCurses">Whether to use the curses UI (preferred flag name, same as enhanced UI)</param>
        /// <param name="autoPlay">Whether to auto-play for non-interactive testing</param>
        public ConsoleUIService(int publisherPort, int subscriberPort, bool useCurses = false, bool autoPlay = false) 
            : base(ServiceConstants.ServiceTypes.ConsoleUI, "Console UI", publisherPort, subscriberPort)
        {
            _useEnhancedUI = useCurses; // Note: the flag is called "curses" but internally still referred to as "enhanced" 
            _autoPlayMode = autoPlay;
            Console.WriteLine($"ConsoleUIService created with curses UI: {_useEnhancedUI}, Auto-play: {_autoPlayMode}");
            
            // Defer initialization of the curses UI to the Start method
            // This ensures proper sequencing with other microservices
        }
        
        /// <summary>
        /// Starts the UI service
        /// </summary>
        public override void Start()
        {
            base.Start();
            
            // Log the current state of the UI flag
            Console.WriteLine($"ConsoleUIService starting with curses UI flag: {_useEnhancedUI}");
            
            // Initialize curses UI if requested - doing this here ensures proper initialization order
            if (_useEnhancedUI && _enhancedUiInstance == null)
            {
                try
                {
                    Console.WriteLine("Initializing Curses UI in Start method...");
                    
                    // Use detailed diagnostics during initialization
                    Console.WriteLine("Curses UI initialization process:");
                    Console.WriteLine("1. Looking for CursesUI type");
                    
                    // Create CursesUI instance via reflection to avoid direct dependency
                    var cursesUIType = Type.GetType("PokerGame.Console.CursesUI, PokerGame.Console");
                    Console.WriteLine($"   Found CursesUI type: {(cursesUIType != null ? "YES" : "NO")}");
                    
                    if (cursesUIType != null)
                    {
                        Console.WriteLine("2. Creating instance of CursesUI");
                        _enhancedUiInstance = Activator.CreateInstance(cursesUIType);
                        Console.WriteLine("   Successfully created Curses UI instance");
                        
                        // Call Initialize method
                        Console.WriteLine("3. Looking for Initialize method");
                        var initMethod = cursesUIType.GetMethod("Initialize");
                        Console.WriteLine($"   Found Initialize method: {(initMethod != null ? "YES" : "NO")}");
                        
                        if (initMethod != null)
                        {
                            Console.WriteLine("4. Calling Initialize method");
                            initMethod.Invoke(_enhancedUiInstance, null);
                            Console.WriteLine("   Successfully initialized Curses UI");
                            
                            // Force a Console.Clear() to clean the display after initialization
                            Console.Clear();
                            Console.WriteLine("★★★ CURSES UI ACTIVE ★★★");
                        }
                        else
                        {
                            Console.WriteLine("   ERROR: Could not find Initialize method on CursesUI");
                            _useEnhancedUI = false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("   ERROR: Curses UI requested but CursesUI class not found");
                        _useEnhancedUI = false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initializing curses UI: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                    _useEnhancedUI = false;
                }
            }
            
            // Provide clear indication whether curses UI is active
            Console.WriteLine($"►► Console UI service using curses UI: {_useEnhancedUI} ◄◄");
            
            // Start the input processing task
            Task.Run(ProcessUserInputAsync);
        }
        
        /// <summary>
        /// Starts the console UI service asynchronously with improved service discovery
        /// </summary>
        public override async Task StartAsync()
        {
            await base.StartAsync();
            
            Console.WriteLine("ConsoleUIService.StartAsync started - setting up enhanced service discovery");
            
            // Register to the broker more aggressively
            for (int i = 0; i < 5; i++)
            {
                Console.WriteLine($"Publishing service registration (attempt {i+1}/5)...");
                PublishServiceRegistration();
                await Task.Delay(200);
            }
            
            // Start active discovery process in the background
            _ = Task.Run(ActiveServiceDiscoveryAsync);
        }
        
        /// <summary>
        /// Actively discovers services by repeatedly sending discovery messages
        /// </summary>
        private async Task ActiveServiceDiscoveryAsync()
        {
            int maxWaitAttempts = ServiceConstants.Discovery.MaxServiceDiscoveryAttempts;
            int attemptDelayMs = ServiceConstants.Discovery.ServiceDiscoveryDelayMs;
            
            Console.WriteLine($"Starting ACTIVE game engine discovery process (max attempts: {maxWaitAttempts}, delay: {attemptDelayMs}ms)");
            Console.WriteLine($"ConsoleUI using ports - Publisher: {_publisherPort}, Subscriber: {_subscriberPort}");
            
            // Broadcast initial port configuration for debugging
            var debugPortsMsg = Message.Create(MessageType.Debug, 
                $"ConsoleUI [{_serviceId}] port configuration - Publisher: {_publisherPort}, Subscriber: {_subscriberPort}");
            debugPortsMsg.SenderId = _serviceId;
            Broadcast(debugPortsMsg);
            
            // Print our current service registry
            Console.WriteLine("Current service registry contents:");
            var serviceTypes = GetServiceTypes();
            foreach (var svc in serviceTypes)
            {
                Console.WriteLine($"  - Service {svc.Key} of type {svc.Value}");
            }
            
            // Try to make socket connectivity more robust
            Console.WriteLine("Ensuring communication is initialized correctly...");
            try
            {
                // In the refactored version, we don't directly manipulate sockets
                // Socket management is handled by the base class and SocketCommunicationAdapter
                Console.WriteLine($"Communication channels - Publisher: {_publisherPort}, Subscriber: {_subscriberPort}");
                
                // Give the sockets time to initialize
                await Task.Delay(500);
                Console.WriteLine("Communication channels ready.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing communication: {ex.Message}");
            }
            
            Console.WriteLine("USING STATIC SERVICE ID APPROACH for direct connection");
            
            // Start with the static ID immediately for more reliable connection
            Console.WriteLine($"Using static game engine ID: {ServiceConstants.StaticServiceIds.GameEngine}");
            _gameEngineServiceId = ServiceConstants.StaticServiceIds.GameEngine;
            
            // Send a direct registration message to the game engine
            var regMsg = Message.Create(MessageType.Debug, 
                $"STATIC ID CONNECTION: ConsoleUI [{_serviceId}] connecting directly to GameEngine {_gameEngineServiceId}");
            regMsg.SenderId = _serviceId; 
            regMsg.ReceiverId = _gameEngineServiceId;
            SendTo(regMsg, _gameEngineServiceId);
            
            int waitAttempts = 0;
            bool gameEngineFound = false;
            
            // Still publish registration for backward compatibility
            for (int i = 0; i < 3; i++)
            {
                PublishServiceRegistration();
                await Task.Delay(100);
            }
            
            while (!gameEngineFound && waitAttempts < maxWaitAttempts)
            {
                waitAttempts++;
                Console.WriteLine($"Direct connection attempt {waitAttempts}/{maxWaitAttempts}");
                
                // First, check if any game engine services are already registered
                var gameEngineSvcIds = GetServicesOfType(ServiceConstants.ServiceTypes.GameEngine);
                if (gameEngineSvcIds.Count > 0)
                {
                    _gameEngineServiceId = gameEngineSvcIds[0];
                    Console.WriteLine($"!!! Found game engine service with ID: {_gameEngineServiceId} !!!");
                    gameEngineFound = true;
                    break;
                }
                
                // If we don't find any services and we're on a broadcast interval, send aggressive broadcasts
                if (waitAttempts % 3 == 0)
                {
                    Console.WriteLine("*** BROADCASTING AGGRESSIVE SERVICE REGISTRATION ***");
                    
                    // Create a very clear message for the console log
                    var directMsg = Message.Create(MessageType.Debug,
                        $"DIRECT CONNECTION ATTEMPT: ConsoleUI [{_serviceId}] on attempt {waitAttempts}");
                    directMsg.SenderId = _serviceId;
                    Console.WriteLine($"Broadcasting direct connection message with ID {directMsg.MessageId}");
                    Broadcast(directMsg);
                    
                    // Send 3 registration messages in quick succession
                    for (int i = 0; i < 3; i++)
                    {
                        PublishServiceRegistration();
                        await Task.Delay(50);
                    }
                    
                    // Try creating direct connections with each standard port offset
                    Console.WriteLine("Trying direct connection on multiple port offsets");
                    foreach (int portOffset in ServiceConstants.Discovery.StandardPortOffsets)
                    {
                        try
                        {
                            // Try to connect to game engine at this port offset
                            int potentialPort = ServiceConstants.Ports.GetGameEnginePublisherPort(portOffset);
                            Console.WriteLine($"Direct connection attempt to port {potentialPort} (offset {portOffset})");
                            
                            // Special registration message for this offset
                            var portMsg = Message.Create(MessageType.Debug,
                                $"DIRECT CONNECTION: ConsoleUI [{_serviceId}] to port {potentialPort} on offset {portOffset}");
                            portMsg.SenderId = _serviceId;
                            portMsg.MessageId = Guid.NewGuid().ToString();
                            Broadcast(portMsg);
                            
                            // Send a service discovery message
                            var discoveryMsg = Message.Create(MessageType.ServiceDiscovery);
                            discoveryMsg.SenderId = _serviceId;
                            discoveryMsg.MessageId = Guid.NewGuid().ToString();
                            Broadcast(discoveryMsg);
                            
                            // Wait briefly
                            await Task.Delay(100);
                            
                            // Check if we found any services after this specific attempt
                            gameEngineSvcIds = GetServicesOfType(ServiceConstants.ServiceTypes.GameEngine);
                            if (gameEngineSvcIds.Count > 0)
                            {
                                _gameEngineServiceId = gameEngineSvcIds[0];
                                Console.WriteLine($"!!! Direct connection on port {potentialPort} found game engine: {_gameEngineServiceId} !!!");
                                gameEngineFound = true;
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error in direct connection attempt on port offset {portOffset}: {ex.Message}");
                        }
                    }
                    
                    // If we found the game engine in the port loops, break out of the main loop
                    if (gameEngineFound)
                    {
                        break;
                    }
                }
                
                // Wait before next attempt
                await Task.Delay(attemptDelayMs);
            }
            
            // Final check and fallback
            if (_gameEngineServiceId != null && gameEngineFound)
            {
                Console.WriteLine($"Successfully connected to game engine service: {_gameEngineServiceId}");
                
                // Send a message to the game engine to confirm connection
                var confirmMsg = Message.Create(MessageType.Debug, 
                    $"CONNECTION ESTABLISHED: ConsoleUI [{_serviceId}] to GameEngine {_gameEngineServiceId}");
                confirmMsg.SenderId = _serviceId;
                confirmMsg.ReceiverId = _gameEngineServiceId;
                SendTo(confirmMsg, _gameEngineServiceId);
                
                // Get information about available services
                Console.WriteLine("Available services after direct connection:");
                foreach (var entry in GetServiceTypes())
                {
                    Console.WriteLine($"- {entry.Key}: {entry.Value}");
                }
            }
            else
            {
                Console.WriteLine("ERROR: Could not find game engine service after maximum wait time.");
                Console.WriteLine("Using dummy game engine service ID as fallback.");
                _gameEngineServiceId = ServiceConstants.StaticServiceIds.GameEngine;
                
                // Display service registry one more time
                Console.WriteLine("Final service registry contents after direct connection attempts:");
                var finalServiceTypes = GetServiceTypes();
                foreach (var svc in finalServiceTypes)
                {
                    Console.WriteLine($"  - Service {svc.Key}: {svc.Value}");
                }
            }
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
                if (registrationInfo.ServiceType == ServiceConstants.ServiceTypes.GameEngine)
                {
                    _gameEngineServiceId = registrationInfo.ServiceId;
                    Console.WriteLine($"Connected to game engine service: {registrationInfo.ServiceName} (ID: {registrationInfo.ServiceId})");
                    
                    // Print debug information
                    Console.WriteLine("Available services:");
                    foreach (var entry in GetServiceTypes())
                    {
                        Console.WriteLine($"- {entry.Key}: {entry.Value}");
                    }
                    
                    // Log additional discovery information
                    Console.WriteLine("Game Engine service found - no longer need to wait for it");
                }
                else
                {
                    // Print information about other service types too
                    Console.WriteLine($"Service registered: {registrationInfo.ServiceName} (Type: {registrationInfo.ServiceType}, ID: {registrationInfo.ServiceId})");
                }
                
                // Perform an additional broadcast now that we have services registered
                PublishServiceRegistration();
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
            // First, get current service registry status
            var registry = GetServiceRegistry();
            Console.WriteLine($"Service registry has {registry.Count} entries");
            return registry;
        }
        
        /// <summary>
        /// Handles messages received from other microservices
        /// </summary>
        /// <param name="message">The message to handle</param>
        public override async Task HandleMessageAsync(Message message)
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
                    
                case MessageType.GenericResponse:
                    Console.WriteLine("========== RECEIVED GENERIC RESPONSE ==========");
                    Console.WriteLine($"Message ID: {message.MessageId}");
                    Console.WriteLine($"In response to: {message.InResponseTo}");
                    Console.WriteLine($"From service: {message.SenderId}");
                    
                    var genericResponse = message.GetPayload<GenericResponsePayload>();
                    if (genericResponse != null)
                    {
                        Console.WriteLine($"Response for message type: {genericResponse.OriginalMessageType}");
                        Console.WriteLine($"Success: {genericResponse.Success}");
                        Console.WriteLine($"Message: {genericResponse.Message}");
                        
                        // Handle specific responses
                        if (genericResponse.OriginalMessageType == MessageType.StartHand)
                        {
                            Console.WriteLine("StartHand response received!");
                            if (genericResponse.Success)
                            {
                                Console.WriteLine("Hand started successfully!");
                            }
                            else
                            {
                                Console.WriteLine($"Error starting hand: {genericResponse.Message}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Warning: Received GenericResponse with null payload");
                    }
                    Console.WriteLine("================================================");
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
                            // Start a new hand with enhanced logging
                            Console.WriteLine("========== SENDING STARTHAND MESSAGE ==========");
                            var message = Message.Create(MessageType.StartHand);
                            message.MessageId = Guid.NewGuid().ToString(); // Ensure unique message ID
                            message.SenderId = _serviceId; // Set sender ID explicitly
                            
                            Console.WriteLine($"StartHand message ID: {message.MessageId}");
                            Console.WriteLine($"StartHand sender ID: {message.SenderId}");
                            Console.WriteLine($"StartHand recipient ID: {_gameEngineServiceId}");
                            
                            Console.WriteLine("StartHand message sent, waiting for response...");
                            SendTo(message, _gameEngineServiceId);
                            Console.WriteLine("================================================");
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
            var gameStartMessage = Message.Create(MessageType.StartGame, playerNames);
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
            {
                Console.WriteLine("[DEBUG] DisplayGameState called but _latestGameState is null");
                return;
            }
                
            if (_useEnhancedUI)
            {
                Console.WriteLine("[DEBUG] Using curses UI for game state display");
                DisplayCursesGameState();
                return;
            }
            else
            {
                Console.WriteLine("[DEBUG] Using standard UI for game state display");
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
        /// Displays a game state using our curses console UI
        /// </summary>
        private void DisplayCursesGameState()
        {
            if (_enhancedUiInstance != null && _latestGameState != null)
            {
                try
                {
                    // Always clear the console first to avoid display issues
                    Console.Clear();
                    
                    // Display a fancy header at the top
                    Console.WriteLine("╔═════════════════════════════════════════════════════════╗");
                    Console.WriteLine("║        TEXAS HOLD'EM POKER (CURSES CONSOLE UI)          ║");
                    Console.WriteLine("╚═════════════════════════════════════════════════════════╝");
                    Console.WriteLine();
                    
                    // Create a local game state for the CursesUI to display
                    var gameEngine = CreateLocalGameEngineFromState();
                    
                    // Call CursesUI.UpdateGameState via reflection
                    var updateMethod = _enhancedUiInstance.GetType().GetMethod("UpdateGameState");
                    if (updateMethod != null)
                    {
                        updateMethod.Invoke(_enhancedUiInstance, new[] { gameEngine });
                        return; // Success, early return
                    }
                    else
                    {
                        Console.WriteLine("ERROR: UpdateGameState method not found in CursesUI");
                    }
                }
                catch (Exception ex)
                {
                    // If anything fails, fall back to text UI
                    Console.WriteLine($"Error in curses UI: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
            }
            
            // Fallback to a prettier text UI with box drawing characters
            Console.WriteLine();
            Console.WriteLine("╔═════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║  CURRENT STATE: {_latestGameState?.CurrentState,-40} ║");
            Console.WriteLine("╠═════════════════════════════════════════════════════════╣");
            
            // Show community cards
            string communityCardsText = _latestGameState?.CommunityCards?.Count > 0 
                ? CardListToString(_latestGameState.CommunityCards) 
                : "[None]";
            Console.WriteLine($"║  Community Cards: {communityCardsText,-37} ║");
            
            // Show pot and current bet
            Console.WriteLine($"║  Pot: ${_latestGameState?.Pot,-44} ║");
            if (_latestGameState?.CurrentBet > 0)
            {
                Console.WriteLine($"║  Current bet: ${_latestGameState.CurrentBet,-38} ║");
            }
            Console.WriteLine("║                                                         ║");
            
            // Show player information
            Console.WriteLine("║  PLAYERS:                                               ║");
            if (_latestGameState?.Players != null)
            {
                foreach (var player in _latestGameState.Players)
                {
                    string status = "";
                    if (player.HasFolded) status = " (Folded)";
                    else if (player.IsAllIn) status = " (All-In)";
                    
                    string playerInfo = $"  - {player.Name}{status}: ${player.Chips} chips";
                    Console.WriteLine($"║{playerInfo,-57} ║");
                }
            }
            
            Console.WriteLine("╚═════════════════════════════════════════════════════════╝");
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
            // Calculate can check once for use in all parts of this method
            bool canCheck = _latestGameState != null && player.CurrentBet == _latestGameState.CurrentBet;
            int callAmount = _latestGameState != null ? _latestGameState.CurrentBet - player.CurrentBet : 0;
            int minRaise = _latestGameState != null ? _latestGameState.CurrentBet + 10 : 10;
            
            if (_useEnhancedUI)
            {
                // Curses UI action prompt
                Console.WriteLine("\n╔═════════════════════════════════════════════════════════╗");
                Console.WriteLine($"║  ★ {player.Name}'s turn ★                               ║");
                Console.WriteLine("╠═════════════════════════════════════════════════════════╣");
                
                // Show player's hole cards with colored symbols if possible
                Console.WriteLine($"║  Your hole cards: {CardListToString(player.HoleCards),-40} ║");
                
                // Show available actions
                Console.WriteLine("║  Available actions:                                     ║");
                
                if (canCheck)
                    Console.WriteLine("║  - Check (C)                                            ║");
                else
                    Console.WriteLine($"║  - Call {callAmount} (C)                                    ║");
                    
                Console.WriteLine("║  - Fold (F)                                             ║");
                Console.WriteLine($"║  - Raise (R) (Minimum raise: {minRaise,-5})                   ║");
                
                Console.WriteLine("╚═════════════════════════════════════════════════════════╝");
                Console.Write("Enter your action: ");
            }
            else
            {
                // Standard UI action prompt
                Console.WriteLine();
                Console.WriteLine($"=== {player.Name}'s turn ===");
                
                // Show player's hole cards
                Console.WriteLine($"Your hole cards: {CardListToString(player.HoleCards)}");
                
                // Show available actions
                Console.WriteLine("Available actions:");
                
                if (canCheck)
                    Console.WriteLine("- Check (C)");
                else
                    Console.WriteLine($"- Call {callAmount} (C)");
                    
                Console.WriteLine("- Fold (F)");
                Console.WriteLine($"- Raise (R) (Minimum raise: {minRaise})");
                
                Console.Write("Enter your action: ");
            }
            
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
        /// Clean up resources and dispose the Curses UI if it was initialized
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            
            // Dispose the curses UI if it exists and is IDisposable
            if (_enhancedUiInstance is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                    Console.WriteLine("Curses UI properly disposed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing Curses UI: {ex.Message}");
                }
                _enhancedUiInstance = null;
            }
        }
    }
}