using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;
using MSAEC = MSA.Foundation.ServiceManagement.ExecutionContext;
using PokerGame.Core.Messaging;
using PokerGame.Core.Models;
using PokerGame.Core.Game;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// A service that provides a console-based UI for the poker game
    /// </summary>
    public class ConsoleUIService : MicroserviceBase
    {
        private bool _isEnhancedUI;
        private string _gameEngineServiceId = "";
        private string _cardDeckServiceId = "";
        private readonly Dictionary<string, Player> _players = new();
        private readonly List<Card> _communityCards = new();
        private string _currentGameState = "Waiting";
        private int _currentBet = 0;
        private int _pot = 0;
        private bool _gameInProgress = false;
        private bool _serviceDiscoveryCompleted = false;
        private bool _cursesUI = false;
        private bool _uiRunning = false;
        private readonly ManualResetEventSlim _shutdownEvent = new(false);
        private bool _isShuttingDown = false;

        /// <summary>
        /// Gets a value indicating whether this services is using enhanced UI mode
        /// </summary>
        public bool IsEnhancedUI => _isEnhancedUI;

        /// <summary>
        /// Gets a value indicating whether the service is in curses UI mode
        /// </summary>
        public bool IsCursesUI => _cursesUI;

        /// <summary>
        /// Gets the player currently associated with this UI service
        /// </summary>
        public Player? CurrentPlayer { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleUIService"/> class
        /// </summary>
        /// <param name="context">The execution context</param>
        /// <param name="enhancedUI">Whether to use enhanced UI mode</param>
        /// <param name="cursesUI">Whether to use curses UI mode</param>
        public ConsoleUIService(MSAEC context, bool enhancedUI = false, bool cursesUI = false) 
            : base(context)
        {
            _isEnhancedUI = enhancedUI;
            _cursesUI = cursesUI;
            
            // Display mode info for debugging
            Console.WriteLine($"POKER GAME UI INITIALIZATION: Curses UI flag = {_cursesUI}, Curses UI instance exists = {_cursesUI}");
            
            // Register message handlers
            RegisterMessageHandlers();
        }

        /// <summary>
        /// Registers handlers for different message types
        /// </summary>
        private void RegisterMessageHandlers()
        {
            // Core message types
            RegisterHandler(MSA.Foundation.Messaging.MessageType.ServiceRegistration, HandleServiceRegistration);
            RegisterHandler(MSA.Foundation.Messaging.MessageType.ServiceDiscoveryRequest, HandleServiceDiscoveryRequest);
            RegisterHandler(MSA.Foundation.Messaging.MessageType.ServiceDiscoveryResponse, HandleServiceDiscoveryResponse);
            
            // Game-specific message types
            RegisterHandler(MSA.Foundation.Messaging.MessageType.GameAction, HandleGameAction);
            RegisterHandler(MSA.Foundation.Messaging.MessageType.GameState, HandleGameState);
            RegisterHandler(MSA.Foundation.Messaging.MessageType.DeckShuffled, HandleDeckShuffled);
            RegisterHandler(MSA.Foundation.Messaging.MessageType.HoleCards, HandleHoleCards);
            RegisterHandler(MSA.Foundation.Messaging.MessageType.CommunityCards, HandleCommunityCards);
            RegisterHandler(MSA.Foundation.Messaging.MessageType.PlayerAction, HandlePlayerAction);
            RegisterHandler(MSA.Foundation.Messaging.MessageType.PlayerJoined, HandlePlayerJoined);
            RegisterHandler(MSA.Foundation.Messaging.MessageType.PlayerLeft, HandlePlayerLeft);
            RegisterHandler(MSA.Foundation.Messaging.MessageType.HandResult, HandleHandResult);
        }

        /// <summary>
        /// Sets up the console UI
        /// </summary>
        private void SetupConsoleUI()
        {
            Console.WriteLine("Setting up console UI...");
            Console.WriteLine($"Enhanced UI: {_isEnhancedUI}");
            Console.WriteLine($"Curses UI: {_cursesUI}");
            
            // If using enhanced UI mode, set up the console for it
            if (_isEnhancedUI || _cursesUI)
            {
                Console.WriteLine("Setting up enhanced console UI...");
                Console.Clear();
                Console.CursorVisible = false;
            }
            
            _uiRunning = true;
        }

        /// <summary>
        /// Handles shutdown of the UI
        /// </summary>
        private void ShutdownUI()
        {
            // Only perform shutdown if we're actually running
            if (!_uiRunning) return;
            
            Console.WriteLine("Shutting down console UI...");
            
            // If using enhanced UI mode, restore the console
            if (_isEnhancedUI || _cursesUI)
            {
                Console.WriteLine("Restoring console from enhanced UI mode...");
                Console.CursorVisible = true;
            }
            
            _uiRunning = false;
        }

        /// <summary>
        /// Handles a player joining the game
        /// </summary>
        /// <param name="message">The player joined message</param>
        private void HandlePlayerJoined(Message message)
        {
            try
            {
                var player = message.GetPayload<Player>();
                Console.WriteLine($"Player joined: {player.Name}");
                
                // Add player to our local collection
                _players[player.Id] = player;
                
                // Update the UI
                DisplayGameStateAsync().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling player joined message: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles a player leaving the game
        /// </summary>
        /// <param name="message">The player left message</param>
        private void HandlePlayerLeft(Message message)
        {
            try
            {
                var playerId = message.GetPayload<string>();
                
                // Remove the player from our local collection
                if (_players.ContainsKey(playerId))
                {
                    string playerName = _players[playerId].Name;
                    _players.Remove(playerId);
                    Console.WriteLine($"Player left: {playerName}");
                }
                
                // Update the UI
                DisplayGameStateAsync().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling player left message: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles the result of a poker hand
        /// </summary>
        /// <param name="message">The hand result message</param>
        private void HandleHandResult(Message message)
        {
            try
            {
                var handResult = message.GetPayload<HandResult>();
                Console.WriteLine($"Hand result received: {handResult.Winner.Name} wins {handResult.Pot} chips with {handResult.WinningHand}");
                
                // Update our local state
                _currentGameState = "Hand Complete";
                _pot = 0;
                _currentBet = 0;
                
                // Clear the community cards
                _communityCards.Clear();
                
                // Update players' chip counts
                foreach (var player in handResult.Players)
                {
                    if (_players.ContainsKey(player.Id))
                    {
                        _players[player.Id].Chips = player.Chips;
                    }
                }
                
                // Update the UI
                DisplayGameStateAsync().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling hand result message: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles player action messages from the game engine
        /// </summary>
        /// <param name="message">The player action message</param>
        private void HandlePlayerAction(Message message)
        {
            try
            {
                var playerAction = message.GetPayload<PlayerAction>();
                Console.WriteLine($"Player action from engine: Player {playerAction.PlayerId} action {playerAction.Action} amount {playerAction.Amount}");
                
                // Update the game state based on player action
                if (_players.ContainsKey(playerAction.PlayerId))
                {
                    switch (playerAction.Action)
                    {
                        case "bet":
                        case "raise":
                            _pot += playerAction.Amount;
                            _currentBet = playerAction.Amount;
                            _players[playerAction.PlayerId].Chips -= playerAction.Amount;
                            _players[playerAction.PlayerId].CurrentBet = playerAction.Amount;
                            break;
                        case "call":
                            int callAmount = _currentBet - _players[playerAction.PlayerId].CurrentBet;
                            _pot += callAmount;
                            _players[playerAction.PlayerId].Chips -= callAmount;
                            _players[playerAction.PlayerId].CurrentBet = _currentBet;
                            break;
                        case "fold":
                            _players[playerAction.PlayerId].HasFolded = true;
                            break;
                        case "all-in":
                            int allInAmount = _players[playerAction.PlayerId].Chips;
                            _pot += allInAmount;
                            _players[playerAction.PlayerId].Chips = 0;
                            _players[playerAction.PlayerId].CurrentBet += allInAmount;
                            _players[playerAction.PlayerId].IsAllIn = true;
                            break;
                    }
                }
                
                // Update the UI
                DisplayGameStateAsync().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling player action message: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles hole cards messages
        /// </summary>
        /// <param name="message">The hole cards message</param>
        private void HandleHoleCards(Message message)
        {
            try 
            {
                var holeCardsMessage = message.GetPayload<PlayerCards>();
                Console.WriteLine($"Received hole cards for player {holeCardsMessage.PlayerId}");
                
                // Update the player's hole cards
                if (_players.ContainsKey(holeCardsMessage.PlayerId))
                {
                    _players[holeCardsMessage.PlayerId].HoleCards.Clear();
                    _players[holeCardsMessage.PlayerId].HoleCards.AddRange(holeCardsMessage.Cards);
                    
                    Console.WriteLine($"Player {holeCardsMessage.PlayerId} cards: {string.Join(", ", holeCardsMessage.Cards.Select(c => $"{c.Rank}{c.Suit}"))}");
                }
                
                // Update the UI
                DisplayGameStateAsync().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling hole cards message: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles community cards messages
        /// </summary>
        /// <param name="message">The community cards message</param>
        private void HandleCommunityCards(Message message)
        {
            try
            {
                var cards = message.GetPayload<List<Card>>();
                Console.WriteLine($"Received community cards: {string.Join(", ", cards.Select(c => $"{c.Rank}{c.Suit}"))}");
                
                // Update the community cards
                _communityCards.Clear();
                _communityCards.AddRange(cards);
                
                // Update the UI
                DisplayGameStateAsync().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling community cards message: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles a deck shuffled message
        /// </summary>
        /// <param name="message">The deck shuffled message</param>
        private void HandleDeckShuffled(Message message)
        {
            try
            {
                Console.WriteLine("Deck shuffled");
                
                // Process this with a separate method to avoid blocking the message handler
                ProcessDeckShuffledMessageAsync(message).ContinueWith(task => 
                {
                    if (task.Exception != null)
                    {
                        Console.WriteLine($"Error in processing deck shuffled: {task.Exception.InnerException?.Message ?? task.Exception.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling deck shuffled message: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles game state messages
        /// </summary>
        /// <param name="message">The game state message</param>
        private void HandleGameState(Message message)
        {
            try
            {
                var gameState = message.GetPayload<GameState>();
                Console.WriteLine($"Game state received: {gameState.CurrentState}");
                
                // Update our local state with the new game state
                _currentGameState = gameState.CurrentState;
                _pot = gameState.Pot;
                _currentBet = gameState.CurrentBet;
                
                // Update the community cards if present
                if (gameState.CommunityCards != null && gameState.CommunityCards.Count > 0)
                {
                    _communityCards.Clear();
                    _communityCards.AddRange(gameState.CommunityCards);
                }
                
                // Update the players if present
                if (gameState.Players != null)
                {
                    foreach (var player in gameState.Players)
                    {
                        if (_players.ContainsKey(player.Id))
                        {
                            _players[player.Id] = player;
                        }
                        else
                        {
                            _players.Add(player.Id, player);
                        }
                    }
                }
                
                // Update the UI
                DisplayGameStateAsync().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling game state message: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles game action messages
        /// </summary>
        /// <param name="message">The game action message</param>
        private void HandleGameAction(Message message)
        {
            try
            {
                var gameAction = message.GetPayload<GameAction>();
                Console.WriteLine($"Game action received: {gameAction.Action}");
                
                // Process the game action
                switch (gameAction.Action)
                {
                    case "start":
                        _gameInProgress = true;
                        _currentGameState = "Starting";
                        break;
                    case "stop":
                        _gameInProgress = false;
                        _currentGameState = "Game Over";
                        break;
                    case "deal":
                        _currentGameState = "Dealing Cards";
                        break;
                    case "betting_round":
                        _currentGameState = $"Betting Round {gameAction.Data}";
                        break;
                    case "showdown":
                        _currentGameState = "Showdown";
                        break;
                }
                
                // Update the UI
                DisplayGameStateAsync().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling game action message: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles a service discovery response message
        /// </summary>
        /// <param name="message">The service discovery response message</param>
        private void HandleServiceDiscoveryResponse(Message message)
        {
            try
            {
                var serviceInfo = message.GetPayload<ServiceInfo>();
                Console.WriteLine($"Service discovery response received for {serviceInfo.ServiceId} of type {serviceInfo.ServiceType}");
                
                // Store the service ID based on service type
                if (serviceInfo.ServiceType == "CardDeckService")
                {
                    _cardDeckServiceId = serviceInfo.ServiceId;
                    Console.WriteLine($"CardDeckService found: {_cardDeckServiceId}");
                }
                else if (serviceInfo.ServiceType == "GameEngineService")
                {
                    _gameEngineServiceId = serviceInfo.ServiceId;
                    Console.WriteLine($"GameEngineService found: {_gameEngineServiceId}");
                }
                
                // If we have both services, mark discovery as completed
                if (!string.IsNullOrEmpty(_cardDeckServiceId) && !string.IsNullOrEmpty(_gameEngineServiceId))
                {
                    _serviceDiscoveryCompleted = true;
                    Console.WriteLine("Service discovery completed successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling service discovery response: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles a service discovery request message
        /// </summary>
        /// <param name="message">The service discovery request message</param>
        private void HandleServiceDiscoveryRequest(Message message)
        {
            try
            {
                Console.WriteLine("Service discovery request received, sending response");
                
                // Create and send service registration for this UI service
                var responseMessage = Message.CreateResponse(message, MSA.Foundation.Messaging.MessageType.ServiceRegistration, 
                    new ServiceInfo
                    {
                        ServiceId = _serviceId,
                        ServiceType = "ConsoleUIService",
                        Version = "1.0",
                        Status = "Running",
                        Capabilities = new List<string> { "UI", "PlayerInput" }
                    });
                
                Console.WriteLine($"Sending service registration as response to discovery request");
                
                MessageBroker?.Publish(responseMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling service discovery request: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles a service registration message
        /// </summary>
        /// <param name="message">The service registration message</param>
        private void HandleServiceRegistration(Message message)
        {
            try
            {
                var serviceInfo = message.GetPayload<ServiceInfo>();
                Console.WriteLine($"Service registration received for {serviceInfo.ServiceId} of type {serviceInfo.ServiceType}");
                
                // Store the service ID based on service type
                if (serviceInfo.ServiceType == "CardDeckService")
                {
                    _cardDeckServiceId = serviceInfo.ServiceId;
                    Console.WriteLine($"CardDeckService registered: {_cardDeckServiceId}");
                }
                else if (serviceInfo.ServiceType == "GameEngineService")
                {
                    _gameEngineServiceId = serviceInfo.ServiceId;
                    Console.WriteLine($"GameEngineService registered: {_gameEngineServiceId}");
                }
                
                // If we have both services, mark discovery as completed
                if (!string.IsNullOrEmpty(_cardDeckServiceId) && !string.IsNullOrEmpty(_gameEngineServiceId))
                {
                    _serviceDiscoveryCompleted = true;
                    Console.WriteLine("Service discovery completed through registration messages");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling service registration: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a DeckShuffled message and advances the game
        /// </summary>
        /// <param name="message">The DeckShuffled message received</param>
        private async Task ProcessDeckShuffledMessageAsync(Message message)
        {
            try
            {
                Console.WriteLine("Processing DeckShuffled message and advancing game...");
                
                // In response to a DeckShuffled message, we should now deal cards to players
                // and prepare for the first round of betting
                
                // First, make sure we have players in the game
                if (_players.Count == 0)
                {
                    Console.WriteLine("No players in the game yet, adding default players");
                    
                    // Add some default players
                    var player1 = new Player { Id = "player1", Name = "Player 1", Chips = 1000 };
                    var player2 = new Player { Id = "player2", Name = "Player 2", Chips = 1000 };
                    
                    _players.Add(player1.Id, player1);
                    _players.Add(player2.Id, player2);
                }
                
                // Set up the game state
                _currentGameState = "Betting Round 1";
                _currentBet = 0;
                _pot = 0;
                
                // Request hole cards for each player
                Console.WriteLine("Requesting hole cards for each player...");
                
                foreach (var player in _players.Values)
                {
                    // Reset player state for new hand
                    player.HoleCards.Clear();
                    player.HasFolded = false;
                    player.IsAllIn = false;
                    player.CurrentBet = 0;
                    
                    // Request hole cards from game engine
                    await RequestHoleCardsAsync(player.Id);
                }
                
                // Update the UI
                await DisplayGameStateAsync();
                
                // Send a GameState request to get current game state from engine
                await RequestGameStateAsync();
                
                Console.WriteLine("DeckShuffled message processed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing DeckShuffled message: {ex.Message}");
            }
        }

        /// <summary>
        /// Requests hole cards for a player
        /// </summary>
        private async Task RequestHoleCardsAsync(string playerId)
        {
            try
            {
                // Create a request for hole cards
                var holeCardsRequestMsg = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = Messaging.MessageType.RequestHoleCards,
                    SenderId = _serviceId,
                    ReceiverId = _gameEngineServiceId,
                    Timestamp = DateTime.UtcNow,
                    Payload = System.Text.Json.JsonSerializer.Serialize(playerId),
                    Headers = new Dictionary<string, string>
                    {
                        { "MessageSubType", "RequestHoleCards" },
                        { "PlayerId", playerId }
                    }
                };
                
                Console.WriteLine($"Requesting hole cards for player {playerId}");
                BrokerManager.Instance.CentralBroker?.Publish(holeCardsRequestMsg);
                
                // In a real implementation, we would wait for a response
                // For now, just add a delay for demonstration
                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error requesting hole cards: {ex.Message}");
            }
        }

        /// <summary>
        /// Requests the current game state from the game engine
        /// </summary>
        private async Task RequestGameStateAsync()
        {
            try
            {
                // Create a request for game state
                var gameStateRequestMsg = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = Messaging.MessageType.GameStateRequest,
                    SenderId = _serviceId,
                    ReceiverId = _gameEngineServiceId,
                    Timestamp = DateTime.UtcNow,
                    Headers = new Dictionary<string, string>
                    {
                        { "MessageSubType", "GameStateRequest" }
                    }
                };
                
                Console.WriteLine("Requesting current game state");
                BrokerManager.Instance.CentralBroker?.Publish(gameStateRequestMsg);
                
                // In a real implementation, we would wait for a response
                // For now, just add a delay for demonstration
                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error requesting game state: {ex.Message}");
            }
        }

        /// <summary>
        /// Formats a card for display
        /// </summary>
        /// <param name="card">The card to format</param>
        /// <returns>A string representation of the card</returns>
        private string FormatCard(Card card)
        {
            string rank = card.Rank switch
            {
                Rank.Ace => "A",
                Rank.King => "K",
                Rank.Queen => "Q",
                Rank.Jack => "J",
                Rank.Ten => "10",
                _ => ((int)card.Rank).ToString()
            };
            
            string suit = card.Suit switch
            {
                Suit.Hearts => "♥",
                Suit.Diamonds => "♦",
                Suit.Clubs => "♣",
                Suit.Spades => "♠",
                _ => "?"
            };
            
            return $"{rank}{suit}";
        }

        /// <summary>
        /// Displays the current game state in the console
        /// </summary>
        private async Task DisplayGameStateAsync()
        {
            if (!_uiRunning) return;
            
            try
            {
                if (_isEnhancedUI || _cursesUI)
                {
                    // Clear screen for enhanced UI mode
                    Console.Clear();
                }
                
                Console.WriteLine("\n");
                Console.WriteLine("==============================================");
                Console.WriteLine("           TEXAS HOLD'EM POKER GAME          ");
                Console.WriteLine("==============================================");
                Console.WriteLine($"Game State: {_currentGameState}");
                Console.WriteLine($"Pot: {_pot}");
                Console.WriteLine($"Current Bet: {_currentBet}");
                Console.WriteLine("----------------------------------------------");
                
                if (_communityCards.Count > 0)
                {
                    Console.WriteLine("Community Cards:");
                    Console.WriteLine(string.Join(" ", _communityCards.Select(FormatCard)));
                    Console.WriteLine("----------------------------------------------");
                }
                
                Console.WriteLine("Players:");
                foreach (var player in _players.Values)
                {
                    Console.Write($"{player.Name} - Chips: {player.Chips}");
                    
                    if (player.IsAllIn)
                    {
                        Console.Write(" (ALL IN)");
                    }
                    else if (player.HasFolded)
                    {
                        Console.Write(" (FOLDED)");
                    }
                    
                    if (player.CurrentBet > 0)
                    {
                        Console.Write($" - Bet: {player.CurrentBet}");
                    }
                    
                    Console.WriteLine();
                    
                    if (player.HoleCards.Count > 0)
                    {
                        Console.WriteLine($"  Cards: {string.Join(" ", player.HoleCards.Select(FormatCard))}");
                    }
                }
                
                Console.WriteLine("==============================================");
                Console.WriteLine("Commands:");
                Console.WriteLine("start - Start a new game");
                Console.WriteLine("fold - Fold your hand");
                Console.WriteLine("check - Check (if no bets)");
                Console.WriteLine("call - Call the current bet");
                Console.WriteLine("bet [amount] - Place a bet");
                Console.WriteLine("raise [amount] - Raise the current bet");
                Console.WriteLine("quit - Exit the game");
                Console.WriteLine("==============================================");
                
                // This is where we'd render a more sophisticated UI if using enhanced mode
                
                await Task.CompletedTask; // Just to make this method async consistent
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error displaying game state: {ex.Message}");
            }
        }

        /// <summary>
        /// Discovers required services
        /// </summary>
        private async Task DiscoverServicesAsync()
        {
            try
            {
                Console.WriteLine("Starting service discovery...");
                
                // Create a service discovery request
                var discoveryRequest = Message.Create(MSA.Foundation.Messaging.MessageType.ServiceDiscoveryRequest,
                    new MSA.Foundation.Messaging.ServiceDiscoveryRequest
                    {
                        RequestingServiceId = ServiceId,
                        RequestingServiceName = ServiceName,
                        RequestingServiceType = ServiceType,
                        ServiceTypesRequested = new List<string> { "CardDeckService", "GameEngineService" }
                    });
                
                // Send the discovery request
                Console.WriteLine("Sending service discovery request");
                MSA.Foundation.Messaging.MessageBroker.Instance?.PublishMessage(discoveryRequest);
                
                // Wait for the discovery response with a timeout
                int retries = 0;
                while (!_serviceDiscoveryCompleted && retries < 10)
                {
                    Console.WriteLine($"Discovery attempt {retries + 1}/10: PlayerUI ({ServiceId}) console ui service. Broadcasting message ServiceDiscoveryRequest.");
                    
                    await Task.Delay(1000);
                    retries++;
                    
                    if (!_serviceDiscoveryCompleted)
                    {
                        Console.WriteLine("No discovery response received yet, retrying...");
                        
                        // Retry sending the discovery request
                        MSA.Foundation.Messaging.MessageBroker.Instance?.PublishMessage(discoveryRequest);
                    }
                }
                
                if (!_serviceDiscoveryCompleted)
                {
                    Console.WriteLine("Service discovery failed after max retries, using fallback service IDs");
                    
                    // Use fallback service IDs
                    if (string.IsNullOrEmpty(_cardDeckServiceId))
                    {
                        _cardDeckServiceId = "CardDeckService-1";
                        Console.WriteLine($"Using fallback CardDeckService ID: {_cardDeckServiceId}");
                    }
                    
                    if (string.IsNullOrEmpty(_gameEngineServiceId))
                    {
                        _gameEngineServiceId = "GameEngineService-1";
                        Console.WriteLine($"Using fallback GameEngineService ID: {_gameEngineServiceId}");
                    }
                    
                    // For logging
                    Console.WriteLine($"Starting game with engine: {_gameEngineServiceId}");
                }
                
                // Start the game automatically for testing
                await StartGameAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during service discovery: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts a new game
        /// </summary>
        private async Task StartGameAsync()
        {
            if (string.IsNullOrEmpty(_gameEngineServiceId))
            {
                Console.WriteLine("Cannot start game - no game engine service discovered yet");
                return;
            }
            
            try
            {
                Console.WriteLine($"Starting new game with engine {_gameEngineServiceId}");
                
                // Create a start game message
                var startGameMessage = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = Messaging.MessageType.StartGame,
                    SenderId = _serviceId,
                    ReceiverId = _gameEngineServiceId,
                    Timestamp = DateTime.UtcNow,
                    Headers = new Dictionary<string, string>
                    {
                        { "MessageSubType", "StartGame" }
                    }
                };
                
                Console.WriteLine("Publishing StartGame message:");
                BrokerManager.Instance.CentralBroker?.Publish(startGameMessage);
                
                _gameInProgress = true;
                
                // For now, just add default players if we don't have any
                if (_players.Count == 0)
                {
                    // Add some default players
                    var player1 = new Player { Id = "player1", Name = "Player 1", Chips = 1000 };
                    var player2 = new Player { Id = "player2", Name = "Player 2", Chips = 1000 };
                    
                    _players.Add(player1.Id, player1);
                    _players.Add(player2.Id, player2);
                    
                    Console.WriteLine("Added default players");
                }
                
                // Update the UI
                await DisplayGameStateAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting game: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a player action command from the console
        /// </summary>
        /// <param name="input">The input string from the console</param>
        private async Task ProcessPlayerActionAsync(string input)
        {
            if (!_gameInProgress)
            {
                Console.WriteLine("No game in progress - use 'start' to begin a game");
                return;
            }
            
            try
            {
                // Parse the command
                string[] parts = input.Trim().ToLower().Split(' ');
                string command = parts[0];
                
                // Create a player action message based on the command
                var playerAction = new PlayerAction
                {
                    PlayerId = "player1", // Assume we're always player 1 for now
                    Action = command
                };
                
                // Add amount for bet or raise
                if ((command == "bet" || command == "raise") && parts.Length > 1)
                {
                    if (int.TryParse(parts[1], out int amount))
                    {
                        playerAction.Amount = amount;
                    }
                    else
                    {
                        Console.WriteLine($"Invalid amount for {command}");
                        return;
                    }
                }
                
                // Send the player action message
                Console.WriteLine($"Sending player action: {playerAction.Action} {playerAction.Amount}");
                
                var playerActionMessage = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = Messaging.MessageType.PlayerAction,
                    SenderId = _serviceId,
                    ReceiverId = _gameEngineServiceId,
                    Timestamp = DateTime.UtcNow,
                    Payload = System.Text.Json.JsonSerializer.Serialize(playerAction),
                    Headers = new Dictionary<string, string>
                    {
                        { "MessageSubType", "PlayerAction" },
                        { "PlayerId", playerAction.PlayerId }
                    }
                };
                
                BrokerManager.Instance.CentralBroker?.Publish(playerActionMessage);
                
                // Update the UI (in a real implementation we'd wait for a response)
                await Task.Delay(200);
                await DisplayGameStateAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing player action: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public override async Task StartAsync()
        {
            try
            {
                // Setup the UI
                SetupConsoleUI();
                
                // Console UI is already registered through the base class initialization
                // No separate registration needed
                
                // All required message subscriptions are set up in RegisterMessageHandlers
                
                Console.WriteLine("ConsoleUIService started");
                
                // Discover services
                await DiscoverServicesAsync();
                
                // Start the input processing loop
                await ProcessUserInputAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting ConsoleUIService: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes user input from the console
        /// </summary>
        private async Task ProcessUserInputAsync()
        {
            try
            {
                while (!_isShuttingDown)
                {
                    if (Console.KeyAvailable)
                    {
                        string? input = Console.ReadLine();
                        
                        if (!string.IsNullOrEmpty(input))
                        {
                            input = input.Trim().ToLower();
                            
                            if (input == "quit" || input == "exit")
                            {
                                Console.WriteLine("Exiting poker game...");
                                _isShuttingDown = true;
                                _shutdownEvent.Set();
                                break;
                            }
                            else if (input == "start")
                            {
                                await StartGameAsync();
                            }
                            else
                            {
                                await ProcessPlayerActionAsync(input);
                            }
                        }
                    }
                    
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
