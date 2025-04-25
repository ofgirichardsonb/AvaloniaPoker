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
                if (payload != null && payload.ServiceType == "CardDeck")
                {
                    _cardDeckServiceId = payload.ServiceId;
                    Console.WriteLine($"Directly registered card deck service: {payload.ServiceName} (ID: {payload.ServiceId})");
                }
            }
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
                        _gameEngine.StartGame(playerNames);
                        
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
                    
                    // Ensure we have a card deck service
                    if (_cardDeckServiceId == null)
                    {
                        Console.WriteLine("WARNING: Card deck service not registered yet. Attempting to locate it...");
                        var cardDeckServices = GetServicesOfType("CardDeck");
                        if (cardDeckServices.Count > 0)
                        {
                            _cardDeckServiceId = cardDeckServices[0];
                            Console.WriteLine($"Found card deck service with ID: {_cardDeckServiceId}");
                        }
                        else
                        {
                            Console.WriteLine("ERROR: Cannot find card deck service. Hand cannot be started.");
                            BroadcastGameState(); // Still broadcast current state
                            break;
                        }
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
                    
                    // Deal hole cards to players
                    Console.WriteLine("Dealing hole cards to players");
                    await DealCardsToPlayersAsync();
                    
                    // Ensure we start the hand if not already in progress
                    if (_gameEngine.State == GameState.Setup)
                    {
                        Console.WriteLine("Starting hand");
                        _gameEngine.StartHand();
                        Console.WriteLine("New game state: " + _gameEngine.State);
                    }
                    
                    // Always broadcast the current state after processing
                    BroadcastGameState();
                    break;
                    
                case MessageType.PlayerAction:
                    var actionPayload = message.GetPayload<PlayerActionPayload>();
                    if (actionPayload != null)
                    {
                        _gameEngine.ProcessPlayerAction(actionPayload.ActionType, actionPayload.BetAmount);
                        
                        // If we need to deal community cards after this action
                        if (_gameEngine.State == Game.GameState.Flop)
                        {
                            await DealCommunityCardsAsync(3); // Flop
                        }
                        else if (_gameEngine.State == Game.GameState.Turn)
                        {
                            await DealCommunityCardsAsync(1); // Turn
                        }
                        else if (_gameEngine.State == Game.GameState.River)
                        {
                            await DealCommunityCardsAsync(1); // River
                        }
                        
                        BroadcastGameState();
                    }
                    break;
                    
                case MessageType.DeckDealResponse:
                    var dealResponse = message.GetPayload<DeckDealResponsePayload>();
                    if (dealResponse != null)
                    {
                        HandleDealtCards(dealResponse.Cards);
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
            if (_cardDeckServiceId == null)
            {
                Console.WriteLine("Card deck service not available");
                return;
            }
                
            // Generate a unique ID for this deck
            _currentDeckId = $"deck-{Guid.NewGuid()}";
            
            // Create a new deck and shuffle it
            var createPayload = new DeckCreatePayload
            {
                DeckId = _currentDeckId,
                Shuffle = true
            };
            
            var message = Message.Create(MessageType.DeckCreate, createPayload);
            SendTo(message, _cardDeckServiceId);
            
            // Small delay to let the deck be created
            await Task.Delay(100);
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