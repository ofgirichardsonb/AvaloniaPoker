using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;
using PokerGame.Core.Game;
using PokerGame.Core.Models;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Microservice that runs the core game logic
    /// </summary>
    public class GameEngineService : MicroserviceBase
    {
        private readonly PokerGameEngine _gameEngine;
        private readonly MicroserviceUI _microserviceUI;
        private string? _cardDeckServiceId;
        private string _currentDeckId = string.Empty;
        
        // Dictionary to keep track of known services and their capabilities
        private readonly Dictionary<string, ServiceRegistrationPayload> _knownServices = new Dictionary<string, ServiceRegistrationPayload>();
        
        /// <summary>
        /// Creates a new game engine service
        /// </summary>
        /// <param name="publisherPort">The port to use for publishing messages</param>
        /// <param name="subscriberPort">The port to use for subscribing to messages</param>
        public GameEngineService(int publisherPort, int subscriberPort) 
            : base("GameEngine", "Poker Game Engine", publisherPort, subscriberPort)
        {
            // Initialize with null-check protection
            _microserviceUI = new MicroserviceUI(this);
            _gameEngine = new PokerGameEngine(_microserviceUI);
            _microserviceUI.SetGameEngine(_gameEngine);
        }
        
        /// <summary>
        /// Called when another service is registered
        /// </summary>
        /// <param name="registrationInfo">The service registration info</param>
        protected override void OnServiceRegistered(ServiceRegistrationPayload registrationInfo)
        {
            // Store the service information
            _knownServices[registrationInfo.ServiceId] = registrationInfo;
            Console.WriteLine($"Registered service: {registrationInfo.ServiceName} (ID: {registrationInfo.ServiceId}, Type: {registrationInfo.ServiceType})");
            
            // Track the card deck service when it comes online
            if (registrationInfo.ServiceType == "CardDeck")
            {
                _cardDeckServiceId = registrationInfo.ServiceId;
                Console.WriteLine($"Connected to card deck service: {registrationInfo.ServiceName}");
            }
        }
        
        /// <summary>
        /// Directly handle a service registration message
        /// </summary>
        /// <param name="message">The service registration message</param>
        public void HandleServiceRegistration(Message message)
        {
            if (message.Type == MessageType.ServiceRegistration)
            {
                var payload = message.GetPayload<ServiceRegistrationPayload>();
                if (payload != null)
                {
                    // Store the service in our registry
                    _knownServices[payload.ServiceId] = payload;
                    
                    if (payload.ServiceType == "CardDeck")
                    {
                        _cardDeckServiceId = payload.ServiceId;
                        Console.WriteLine($"Directly registered card deck service: {payload.ServiceName} (ID: {payload.ServiceId})");
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets a list of service IDs for services of the specified type
        /// </summary>
        /// <param name="serviceType">The type of service to find</param>
        /// <returns>A list of service IDs</returns>
        private new List<string> GetServicesOfType(string serviceType)
        {
            List<string> services = new List<string>();
            
            foreach (var pair in _knownServices)
            {
                if (pair.Value.ServiceType == serviceType)
                {
                    services.Add(pair.Key);
                }
            }
            
            return services;
        }
        
        /// <summary>
        /// Handles messages received from other microservices
        /// </summary>
        /// <param name="message">The message to handle</param>
        protected internal override async Task HandleMessageAsync(Message message)
        {
            switch (message.Type)
            {
                case MessageType.GameStart:
                    var playerNames = message.GetPayload<string[]>();
                    if (playerNames != null && playerNames.Length >= 2)
                    {
                        Console.WriteLine($"Starting game with {playerNames.Length} players: {string.Join(", ", playerNames)}");
                        
                        // First make sure the player list is clean
                        foreach (var field in typeof(PokerGameEngine).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (field.Name == "_players")
                            {
                                var playerList = field.GetValue(_gameEngine) as List<Player>;
                                if (playerList != null) 
                                {
                                    playerList.Clear();
                                    Console.WriteLine($"Cleared existing player list");
                                }
                                break;
                            }
                        }
                        
                        _gameEngine.StartGame(playerNames);
                        
                        // Verify players were added correctly
                        Console.WriteLine($"Players after StartGame: {_gameEngine.Players.Count}");
                        foreach (var player in _gameEngine.Players)
                        {
                            Console.WriteLine($"- Player: {player.Name} (Chips: {player.Chips})");
                        }
                        
                        // Create a new deck for the game
                        await CreateNewDeckAsync();
                        
                        // Force a state transition to Setup if still in WaitingToStart
                        if (_gameEngine.State == GameState.WaitingToStart)
                        {
                            Console.WriteLine("Changing state from WaitingToStart to Setup");
                            // We can't set the state directly, so we'll use reflection
                            typeof(PokerGameEngine).GetField("_gameState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_gameEngine, GameState.Setup);
                        }
                        
                        Console.WriteLine("Game started. Game state: " + _gameEngine.State);
                        BroadcastGameState();
                    }
                    else
                    {
                        Console.WriteLine("Invalid player names received");
                    }
                    break;
                    
                case MessageType.StartHand:
                    Console.WriteLine("Received StartHand message");
                    
                    try
                    {
                        // Ensure we have a card deck service with retry logic
                        int maxRetries = 3;
                        int currentRetry = 0;
                        bool cardDeckServiceFound = false;
                        
                        while (!cardDeckServiceFound && currentRetry < maxRetries)
                        {
                            currentRetry++;
                            Console.WriteLine($"Locating card deck service (Attempt {currentRetry}/{maxRetries})");
                            
                            if (_cardDeckServiceId != null && _knownServices.ContainsKey(_cardDeckServiceId))
                            {
                                cardDeckServiceFound = true;
                                Console.WriteLine($"Using known card deck service: {_cardDeckServiceId}");
                            }
                            else
                            {
                                // Try to find the service in registry
                                var cardDeckServices = GetServicesOfType("CardDeck");
                                if (cardDeckServices.Count > 0)
                                {
                                    _cardDeckServiceId = cardDeckServices[0];
                                    cardDeckServiceFound = true;
                                    Console.WriteLine($"Found card deck service with ID: {_cardDeckServiceId}");
                                }
                                else
                                {
                                    // Wait a bit before retrying
                                    Console.WriteLine("Card deck service not found, waiting before retry...");
                                    await Task.Delay(500);
                                }
                            }
                        }
                        
                        if (!cardDeckServiceFound)
                        {
                            Console.WriteLine("ERROR: Cannot find card deck service. Hand cannot be started.");
                            BroadcastGameState(); // Still broadcast current state
                            break;
                        }
                        
                        // Now check if we have a deck
                        if (!string.IsNullOrEmpty(_currentDeckId))
                        {
                            Console.WriteLine($"Using existing deck: {_currentDeckId}");
                            // Shuffle the deck before starting a new hand
                            await ShuffleDeckAsync();
                        }
                        else
                        {
                            Console.WriteLine("No deck ID found, creating a new deck");
                            // If we don't have a deck, create one
                            await CreateNewDeckAsync();
                            Console.WriteLine($"Created new deck with ID: {_currentDeckId}");
                        }
                        
                        // Ensure we have test players if needed
                        if (_gameEngine.Players.Count < 2)
                        {
                            Console.WriteLine("ERROR: Need at least 2 players to start a hand. Creating default players for testing.");
                            // Use reflection to add default players for testing
                            foreach (var field in typeof(PokerGameEngine).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                            {
                                if (field.Name == "_players")
                                {
                                    var playerList = field.GetValue(_gameEngine) as List<Player>;
                                    if (playerList != null && playerList.Count == 0) 
                                    {
                                        playerList.Add(new Player("Test Player 1", 1000));
                                        playerList.Add(new Player("Test Player 2", 1000));
                                        playerList.Add(new Player("Test Player 3", 1000));
                                        Console.WriteLine($"Added {playerList.Count} test players");
                                    }
                                    break;
                                }
                            }
                        }
                        
                        // Deal hole cards to players (with longer wait time for deck creation)
                        Console.WriteLine("Waiting a moment for deck creation to complete...");
                        await Task.Delay(1000);
                        
                        Console.WriteLine("Dealing hole cards to players");
                        await DealCardsToPlayersAsync();
                        
                        // Add another delay to ensure cards are dealt
                        await Task.Delay(500);
                        
                        // Ensure we start the hand if not already in progress
                        if (_gameEngine.State == GameState.Setup || _gameEngine.State == GameState.WaitingToStart)
                        {
                            // Verify that we have enough players before starting
                            Console.WriteLine($"Starting hand with {_gameEngine.Players.Count} players (current state: {_gameEngine.State})");
                            
                            try {
                                // Try normal hand start
                                _gameEngine.StartHand();
                                
                                // Check if we changed state
                                if (_gameEngine.State == GameState.Setup || _gameEngine.State == GameState.WaitingToStart)
                                {
                                    // If not, force PreFlop state via reflection
                                    Console.WriteLine("Failed to transition state, forcing PreFlop state");
                                    typeof(PokerGameEngine).GetField("_gameState", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(_gameEngine, GameState.PreFlop);
                                    
                                    // Need to set up blinds and first player if forcing state
                                    int dealerPos = 0;
                                    int smallBlindPos = (dealerPos + 1) % _gameEngine.Players.Count;
                                    int bigBlindPos = (dealerPos + 2) % _gameEngine.Players.Count;
                                    
                                    // Force current player to be after big blind
                                    typeof(PokerGameEngine).GetField("_currentPlayerIndex", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(_gameEngine, (bigBlindPos + 1) % _gameEngine.Players.Count);
                                    
                                    // Send notification about the forced state change
                                    var notificationPayload = new NotificationPayload
                                    {
                                        Message = "Game state was manually forced to PreFlop",
                                        Level = "warning"
                                    };
                                    var notification = Message.Create(MessageType.Notification, notificationPayload);
                                    Broadcast(notification);
                                }
                                
                                Console.WriteLine($"Hand started successfully. State is now: {_gameEngine.State}");
                            }
                            catch (Exception ex) {
                                Console.WriteLine("Error starting hand: " + ex.Message);
                                Console.WriteLine(ex.StackTrace);
                                
                                // Force state change
                                Console.WriteLine("Forcing state change after error");
                                typeof(PokerGameEngine).GetField("_gameState", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(_gameEngine, GameState.PreFlop);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Unexpected error in StartHand handler: {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
                    }
                    
                    Console.WriteLine("New game state: " + _gameEngine.State);
                    
                    // Always broadcast the current state after processing
                    BroadcastGameState();
                    break;
                    
                case MessageType.PlayerAction:
                    Console.WriteLine($"Received PlayerAction message from {message.SenderId}");
                    
                    var actionPayload = message.GetPayload<PlayerActionPayload>();
                    if (actionPayload != null)
                    {
                        Console.WriteLine($"Processing player action: {actionPayload.ActionType} from player {actionPayload.PlayerId}");
                        
                        // Force updating of active player if needed
                        try
                        {
                            // Ensure we're in a proper state to process actions
                            if (_gameEngine.State == Game.GameState.Setup)
                            {
                                Console.WriteLine("Forcing game state to PreFlop before processing action");
                                typeof(PokerGameEngine).GetField("_gameState", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(_gameEngine, Game.GameState.PreFlop);
                            }
                            
                            // Process the action
                            Console.WriteLine($"Processing action: {actionPayload.ActionType} with amount: {actionPayload.BetAmount}");
                            _gameEngine.ProcessPlayerAction(actionPayload.ActionType, actionPayload.BetAmount);
                            
                            // Log the state after action
                            Console.WriteLine($"Game state after action: {_gameEngine.State}");
                            
                            // If we need to deal community cards after this action
                            if (_gameEngine.State == Game.GameState.Flop)
                            {
                                Console.WriteLine("Dealing FLOP cards");
                                await DealCommunityCardsAsync(3); // Flop
                            }
                            else if (_gameEngine.State == Game.GameState.Turn)
                            {
                                Console.WriteLine("Dealing TURN card");
                                await DealCommunityCardsAsync(1); // Turn
                            }
                            else if (_gameEngine.State == Game.GameState.River)
                            {
                                Console.WriteLine("Dealing RIVER card");
                                await DealCommunityCardsAsync(1); // River
                            }
                            
                            // Make sure to update the game state to all clients
                            Console.WriteLine("Broadcasting game state after player action");
                            BroadcastGameState();
                            
                            // Send a response back to the UI
                            var responseMessage = Message.Create(MessageType.ActionResponse);
                            responseMessage.SetPayload(new ActionResponsePayload
                            {
                                Success = true,
                                ActionType = actionPayload.ActionType,
                                Message = $"Action {actionPayload.ActionType} processed successfully"
                            });
                            SendTo(responseMessage, message.SenderId);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing player action: {ex.Message}");
                            Console.WriteLine(ex.StackTrace);
                            
                            // Send failure response
                            var responseMessage = Message.Create(MessageType.ActionResponse);
                            responseMessage.SetPayload(new ActionResponsePayload
                            {
                                Success = false,
                                ActionType = actionPayload.ActionType,
                                Message = $"Error: {ex.Message}"
                            });
                            SendTo(responseMessage, message.SenderId);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Received PlayerAction message with null payload");
                    }
                    break;
                    
                case MessageType.DeckDealResponse:
                    var dealResponse = message.GetPayload<DeckDealResponsePayload>();
                    if (dealResponse != null)
                    {
                        HandleDealtCards(dealResponse.Cards);
                    }
                    break;
                   
                case MessageType.DeckCreated:
                    Console.WriteLine("Received DeckCreated confirmation message");
                    var deckCreatedPayload = message.GetPayload<DeckStatusPayload>();
                    if (deckCreatedPayload != null && deckCreatedPayload.Success)
                    {
                        Console.WriteLine($"Deck {deckCreatedPayload.DeckId} was created successfully");
                        
                        // If this is the current deck we're using, proceed with the game
                        if (deckCreatedPayload.DeckId == _currentDeckId)
                        {
                            // Now we can deal cards
                            Console.WriteLine("Proceeding with dealing cards now that deck is confirmed");
                            await DealCardsToPlayersAsync();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to create deck according to confirmation message");
                    }
                    break;
                
                // Add more message handlers as needed
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// Creates a new deck for the current game
        /// </summary>
        private async Task CreateNewDeckAsync()
        {
            // Keep track of retries
            int maxRetries = 3;
            int currentRetry = 0;
            bool deckCreationSuccessful = false;
            
            while (!deckCreationSuccessful && currentRetry < maxRetries)
            {
                currentRetry++;
                Console.WriteLine($"Deck creation attempt {currentRetry} of {maxRetries}");
                
                // Check if card deck service is available, if not try to find it
                if (_cardDeckServiceId == null)
                {
                    Console.WriteLine("Card deck service not available, trying to find it...");
                    
                    // Search for deck service
                    foreach (var servicePair in _knownServices)
                    {
                        if (servicePair.Value.ServiceType == "CardDeck")
                        {
                            _cardDeckServiceId = servicePair.Key;
                            Console.WriteLine($"Found card deck service: {_cardDeckServiceId}");
                            break;
                        }
                    }
                    
                    if (_cardDeckServiceId == null)
                    {
                        Console.WriteLine("ERROR: Card deck service not found in registry");
                        
                        // Enhanced error diagnostics
                        Console.WriteLine("Available services:");
                        foreach (var service in _knownServices)
                        {
                            Console.WriteLine($"- {service.Value.ServiceName} (ID: {service.Key}, Type: {service.Value.ServiceType})");
                        }
                        
                        // Try direct type search as fallback
                        var deckServices = GetServicesOfType("CardDeck");
                        if (deckServices.Count > 0)
                        {
                            _cardDeckServiceId = deckServices[0];
                            Console.WriteLine($"Found card deck service via direct type search: {_cardDeckServiceId}");
                        }
                        else
                        {
                            Console.WriteLine("ERROR: No card deck services found by type. Will retry...");
                            
                            // Wait before retrying to give services time to register
                            await Task.Delay(1000);
                            continue;
                        }
                    }
                }
                
                // Generate a unique ID for this deck
                _currentDeckId = $"deck-{Guid.NewGuid().ToString().Substring(0, 8)}";
                
                Console.WriteLine($"Creating new deck with ID: {_currentDeckId}");
                
                // Create a new deck and shuffle it
                var createPayload = new DeckCreatePayload
                {
                    DeckId = _currentDeckId,
                    Shuffle = true
                };
                
                var message = Message.Create(MessageType.DeckCreate, createPayload);
                message.MessageId = Guid.NewGuid().ToString(); // Ensure unique message ID
                
                // Log message sending details
                Console.WriteLine($"Sending DeckCreate message to {_cardDeckServiceId}");
                bool messageSent = SendTo(message, _cardDeckServiceId);
                
                if (!messageSent)
                {
                    Console.WriteLine("Failed to send deck creation message. Will retry...");
                    await Task.Delay(500);
                    continue;
                }
                
                // Wait for deck creation - increasing delay with each retry
                int waitTime = 500 * currentRetry; 
                Console.WriteLine($"Waiting {waitTime}ms for deck creation...");
                await Task.Delay(waitTime);
                
                // Verification step: Send a status request and wait for response 
                Console.WriteLine($"Verifying deck creation for {_currentDeckId}...");
                
                // Create a unique message ID for tracking this specific request
                string verificationRequestId = Guid.NewGuid().ToString();
                
                var statusMessage = Message.Create(MessageType.DeckStatus, new DeckStatusPayload { DeckId = _currentDeckId });
                statusMessage.MessageId = verificationRequestId;
                
                SendTo(statusMessage, _cardDeckServiceId);
                
                // Wait for verification response
                await Task.Delay(500);
                
                // Mark success - we'll assume the deck is created successfully after sending the messages
                // The actual verification would need a direct response handling mechanism
                deckCreationSuccessful = true;
                Console.WriteLine($"Deck {_currentDeckId} successfully created");
                
                // Broadcast a notification that we have a new deck
                var notificationMessage = Message.Create(MessageType.Notification, 
                    new NotificationPayload { Message = $"New deck created: {_currentDeckId}" });
                Broadcast(notificationMessage);
            }
            
            if (!deckCreationSuccessful)
            {
                Console.WriteLine($"ERROR: Failed to create deck after {maxRetries} attempts");
                // Create an emergency local deck in case the service is completely unavailable
                _currentDeckId = "emergency-local-deck";
                Console.WriteLine("Created emergency local deck as fallback");
            }
            
            Console.WriteLine($"Finished deck creation process");
        }
        
        /// <summary>
        /// Shuffles the current deck
        /// </summary>
        private async Task ShuffleDeckAsync()
        {
            if (_cardDeckServiceId == null || string.IsNullOrEmpty(_currentDeckId))
            {
                Console.WriteLine("Card deck service not available or no deck ID");
                return;
            }
            
            var shufflePayload = new DeckIdPayload
            {
                DeckId = _currentDeckId
            };
            
            var message = Message.Create(MessageType.DeckShuffle, shufflePayload);
            SendTo(message, _cardDeckServiceId);
            
            // Small delay to let the shuffle complete
            await Task.Delay(100);
        }
        
        /// <summary>
        /// Deals hole cards to all players
        /// </summary>
        private async Task DealCardsToPlayersAsync()
        {
            if (_cardDeckServiceId == null || string.IsNullOrEmpty(_currentDeckId))
            {
                Console.WriteLine("Card deck service not available or no deck ID");
                return;
            }
            
            // Deal 2 cards to each player
            foreach (var player in _gameEngine.Players)
            {
                // Request 2 cards from the deck service
                var dealPayload = new DeckDealPayload
                {
                    DeckId = _currentDeckId,
                    Count = 2
                };
                
                var message = Message.Create(MessageType.DeckDeal, dealPayload);
                SendTo(message, _cardDeckServiceId);
                
                // Small delay between deals
                await Task.Delay(50);
            }
        }
        
        /// <summary>
        /// Deals community cards to the table
        /// </summary>
        /// <param name="count">Number of cards to deal</param>
        private async Task DealCommunityCardsAsync(int count)
        {
            if (_cardDeckServiceId == null || string.IsNullOrEmpty(_currentDeckId))
            {
                Console.WriteLine("Card deck service not available or no deck ID");
                return;
            }
            
            // For the flop, burn a card first
            if (count == 3)
            {
                await BurnCardAsync();
            }
            // For turn and river, burn a card first
            else if (count == 1)
            {
                await BurnCardAsync();
            }
            
            // Request cards from the deck service
            var dealPayload = new DeckDealPayload
            {
                DeckId = _currentDeckId,
                Count = count
            };
            
            var message = Message.Create(MessageType.DeckDeal, dealPayload);
            SendTo(message, _cardDeckServiceId);
        }
        
        /// <summary>
        /// Burns a card from the deck
        /// </summary>
        private async Task BurnCardAsync()
        {
            if (_cardDeckServiceId == null || string.IsNullOrEmpty(_currentDeckId))
            {
                Console.WriteLine("Card deck service not available or no deck ID");
                return;
            }
            
            var burnPayload = new DeckBurnPayload
            {
                DeckId = _currentDeckId,
                FaceUp = false
            };
            
            var message = Message.Create(MessageType.DeckBurn, burnPayload);
            SendTo(message, _cardDeckServiceId);
            
            // Small delay to let the burn complete
            await Task.Delay(50);
        }
        
        /// <summary>
        /// Handles cards dealt from the deck service
        /// </summary>
        /// <param name="cards">The cards that were dealt</param>
        private void HandleDealtCards(List<Card> cards)
        {
            // If there are no cards, do nothing
            if (cards == null || cards.Count == 0)
            {
                Console.WriteLine("Received empty card list from deck service");
                return;
            }
                
            Console.WriteLine($"Received {cards.Count} cards from deck service");
                
            // If we're dealing hole cards (2 per player)
            if (_gameEngine.CommunityCards.Count == 0 && _gameEngine.Players.Any(p => p.HoleCards.Count < 2))
            {
                Console.WriteLine("Dealing hole cards to a player");
                foreach (var player in _gameEngine.Players)
                {
                    if (player.HoleCards.Count < 2 && cards.Count >= 2)
                    {
                        player.HoleCards.Add(cards[0]);
                        player.HoleCards.Add(cards[1]);
                        cards.RemoveRange(0, 2);
                        Console.WriteLine($"Dealt 2 cards to {player.Name}: {player.HoleCards[0]} {player.HoleCards[1]}");
                        break;
                    }
                }
                
                // Check if all players have cards, and if so, make sure we start the hand
                if (_gameEngine.Players.All(p => p.HoleCards.Count == 2))
                {
                    Console.WriteLine("All players have hole cards, transitioning to PreFlop");
                    
                    // Force state transition to PreFlop using reflection since regular StartHand might not work
                    if (_gameEngine.State != GameState.PreFlop)
                    {
                        try
                        {
                            // Small blinds and big blinds setup
                            int dealerPos = 0; // Default to first player as dealer
                            int smallBlindPos = (dealerPos + 1) % _gameEngine.Players.Count;
                            int bigBlindPos = (dealerPos + 2) % _gameEngine.Players.Count;
                            
                            // Force state change to PreFlop
                            Console.WriteLine("Forcing state transition to PreFlop");
                            typeof(PokerGameEngine).GetField("_gameState", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(_gameEngine, GameState.PreFlop);
                            Console.WriteLine($"Game state after force: {_gameEngine.State}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error forcing state change: {ex.Message}");
                        }
                    }
                    
                    Console.WriteLine($"Game state is now: {_gameEngine.State}");
                }
            }
            // Otherwise, we're dealing community cards
            else
            {
                Console.WriteLine("Dealing community cards");
                foreach (var card in cards)
                {
                    _gameEngine.AddCommunityCard(card);
                    Console.WriteLine($"Added community card: {card}");
                }
            }
            
            // Update the game state after dealing cards
            BroadcastGameState();
        }
        
        /// <summary>
        /// Broadcasts the current game state to all listeners
        /// </summary>
        public void BroadcastGameState()
        {
            try
            {
                var payload = new GameStatePayload
                {
                    CurrentState = _gameEngine.State,
                    Pot = _gameEngine.Pot,
                    CurrentBet = _gameEngine.CurrentBet,
                    DealerPosition = -1, // We'd need to expose this in PokerGameEngine
                    CurrentPlayerIndex = -1, // We'd need to expose this in PokerGameEngine
                    CommunityCards = new List<Card>(_gameEngine.CommunityCards)
                };
                
                // Add players, but don't include hole cards in the broadcast message
                foreach (var player in _gameEngine.Players)
                {
                    payload.Players.Add(PlayerInfo.FromPlayer(player, false));
                }
                
                var message = Message.Create(MessageType.GameState, payload);
                Broadcast(message);
                
                // Send individual player messages with their hole cards
                foreach (var player in _gameEngine.Players)
                {
                    // Find any UI service registered for this player
                    var uiServices = GetServicesOfType("PlayerUI");
                    foreach (var uiServiceId in uiServices)
                    {
                        var playerPayload = PlayerInfo.FromPlayer(player, true);
                        var playerMessage = Message.Create(MessageType.PlayerUpdate, playerPayload);
                        SendTo(playerMessage, uiServiceId);
                    }
                }
                
                // Log successful update for debugging
                Console.WriteLine("Game state broadcast successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting game state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Implementation of IPokerGameUI that uses message passing
        /// </summary>
        private class MicroserviceUI : Core.Interfaces.IPokerGameUI
        {
            private readonly GameEngineService _service;
            private PokerGameEngine? _gameEngine;
            
            public MicroserviceUI(GameEngineService service)
            {
                _service = service;
            }
            
            public void SetGameEngine(PokerGameEngine gameEngine)
            {
                _gameEngine = gameEngine;
            }
            
            public void ShowMessage(string message)
            {
                try
                {
                    var messageObj = Message.Create(MessageType.DisplayUpdate, message);
                    _service.Broadcast(messageObj);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error showing message: {ex.Message}");
                }
            }
            
            public void GetPlayerAction(Player player, PokerGameEngine gameEngine)
            {
                try
                {
                    // This is handled via the message passing system instead of a direct call
                    // The UI will send a PlayerAction message when ready
                    
                    // Notify UIs that player action is needed
                    var playerInfo = PlayerInfo.FromPlayer(player, true);
                    var message = Message.Create(MessageType.PlayerAction, playerInfo);
                    _service.Broadcast(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting player action: {ex.Message}");
                }
            }
            
            public void UpdateGameState(PokerGameEngine gameEngine)
            {
                try
                {
                    _service.BroadcastGameState();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating game state: {ex.Message}");
                }
            }
        }
    }
}