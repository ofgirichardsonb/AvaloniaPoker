using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using PokerGame.Core.Messaging;
using PokerGame.Core.Models;
using PokerGame.Core.Game;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Action types for poker player actions
    /// </summary>
    public enum PokerPlayerActionType
    {
        /// <summary>
        /// Player folds their hand
        /// </summary>
        Fold,
        
        /// <summary>
        /// Player checks (passes without betting)
        /// </summary>
        Check,
        
        /// <summary>
        /// Player calls the current bet
        /// </summary>
        Call,
        
        /// <summary>
        /// Player raises the bet
        /// </summary>
        Raise
    }
    
    /// <summary>
    /// Payload for poker player action messages
    /// </summary>
    public class PokerPlayerActionPayload
    {
        /// <summary>
        /// Gets or sets the ID of the player taking the action
        /// </summary>
        [JsonPropertyName("playerId")]
        public string PlayerId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the action type
        /// </summary>
        [JsonPropertyName("action")]
        public PokerPlayerActionType Action { get; set; }
        
        /// <summary>
        /// Gets or sets the bet amount (for call/raise actions)
        /// </summary>
        [JsonPropertyName("amount")]
        public int Amount { get; set; }
    }
    
    /// <summary>
    /// A simplified game engine service that manages the poker game
    /// </summary>
    public class SimpleGameEngineService : SimpleServiceBase
    {
        private readonly List<Player> _players = new List<Player>();
        private readonly List<Card> _communityCards = new List<Card>();
        private readonly Random _random = new Random();
        private GameState _gameState = GameState.NotStarted;
        private int _currentDealerIndex = 0;
        private int _currentPlayerIndex = 0;
        private int _pot = 0;
        private string _deckId = string.Empty;
        private string _cardDeckServiceId = string.Empty;
        private bool _useLocalDeck = false;
        private Deck _localDeck = null;
        private int _maxRetries = 3;
        private bool _microservicesMode = false;
        
        // Timeout values
        private static readonly TimeSpan _messageTimeout = TimeSpan.FromSeconds(5);
        
        /// <summary>
        /// Creates a new simplified game engine service
        /// </summary>
        /// <param name="publisherPort">The port on which this service will publish messages</param>
        /// <param name="subscriberPort">The port on which this service will subscribe to messages</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        /// <param name="microservicesMode">Whether to run in microservices mode</param>
        public SimpleGameEngineService(int publisherPort, int subscriberPort, bool verbose = false, bool microservicesMode = false)
            : base("Game Engine Service", "GameEngine", publisherPort, subscriberPort, verbose)
        {
            _microservicesMode = microservicesMode;
            
            // Create a local deck as fallback
            _localDeck = new Deck();
            _localDeck.Initialize();
            _localDeck.Shuffle();
            
            Logger.Log($"Created game engine service with microservices mode {_microservicesMode}");
        }
        
        /// <summary>
        /// Starts the service and initializes the game
        /// </summary>
        public override void Start()
        {
            base.Start();
            
            Logger.Log("Game engine service started");
            
            // Initialize the game
            InitializeGame();
        }
        
        /// <summary>
        /// Initializes the game
        /// </summary>
        private void InitializeGame()
        {
            if (_microservicesMode)
            {
                // We need to wait for the card deck service to register
                Logger.Log("Waiting for card deck service to register...");
                
                // We'll try to create a deck now and also handle it in the service registration handler
                CreateDeck();
            }
            else
            {
                // In non-microservices mode, use the local deck
                _useLocalDeck = true;
                _deckId = "local-deck";
                _localDeck.Reset();
                Logger.Log("Using local deck");
                
                // Add test players if needed
                if (_players.Count < 2)
                {
                    Logger.LogWarning("Need at least 2 players to start a hand. Creating default players for testing.");
                    AddTestPlayers();
                }
                
                // Start a hand
                StartHand();
            }
        }
        
        /// <summary>
        /// Creates a deck through the card deck service
        /// </summary>
        private void CreateDeck()
        {
            try
            {
                if (!_microservicesMode || string.IsNullOrEmpty(_cardDeckServiceId))
                {
                    // Use local deck as fallback
                    CreateEmergencyLocalDeck();
                    return;
                }
                
                // Create a deck through the card deck service
                Logger.Log($"Creating deck through card deck service {_cardDeckServiceId}...");
                
                var deckId = Guid.NewGuid().ToString();
                var payload = new DeckShufflePayload { DeckId = deckId };
                var message = SimpleMessage.Create(SimpleMessageType.DeckShuffle, payload);
                message.ReceiverId = _cardDeckServiceId;
                
                PublishMessage(message);
                
                // In a real implementation, we would wait for the response from the card deck service
                // For simplicity, we just wait a moment and then use the local deck if needed
                Logger.Log("Waiting a moment for deck creation to complete...");
                Thread.Sleep(1000);
                
                // Use the deck ID
                _deckId = deckId;
                _useLocalDeck = false;
                
                Logger.Log($"Created new deck with ID: {_deckId}");
                
                // Add test players if needed
                if (_players.Count < 2)
                {
                    Logger.LogWarning("Need at least 2 players to start a hand. Creating default players for testing.");
                    AddTestPlayers();
                }
                
                // Start a hand
                StartHand();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error creating deck", ex);
                
                // Use local deck as fallback
                CreateEmergencyLocalDeck();
            }
        }
        
        /// <summary>
        /// Creates an emergency local deck as fallback
        /// </summary>
        private void CreateEmergencyLocalDeck()
        {
            Logger.Log("Created emergency local deck as fallback");
            
            _useLocalDeck = true;
            _deckId = "emergency-local-deck";
            _localDeck.Reset();
            
            Logger.Log($"Emergency deck created with {_localDeck.RemainingCards} cards");
            
            // Add test players if needed
            if (_players.Count < 2)
            {
                Logger.LogWarning("Need at least 2 players to start a hand. Creating default players for testing.");
                AddTestPlayers();
            }
            
            // Start a hand
            Logger.Log("Finished deck creation process");
            StartHand();
        }
        
        /// <summary>
        /// Adds test players for testing
        /// </summary>
        private void AddTestPlayers()
        {
            _players.Clear();
            
            _players.Add(new Player("Test Player 1", 1000));
            _players.Add(new Player("Test Player 2", 1000));
            _players.Add(new Player("Test Player 3", 1000));
            
            Logger.Log($"Added {_players.Count} test players");
        }
        
        /// <summary>
        /// Starts a new hand
        /// </summary>
        private void StartHand()
        {
            // Reset the game state
            _gameState = GameState.NotStarted;
            _pot = 0;
            _communityCards.Clear();
            
            foreach (var player in _players)
            {
                player.ClearHand();
                player.ResetTotalBet();
            }
            
            // Set the dealer
            _currentDealerIndex = (_currentDealerIndex + 1) % _players.Count;
            _players[_currentDealerIndex].IsDealer = true;
            
            // Set the current player (next to the dealer)
            _currentPlayerIndex = (_currentDealerIndex + 1) % _players.Count;
            
            // Deal cards to players
            DealHoleCards();
            
            // Transition to pre-flop
            _gameState = GameState.PreFlop;
            
            // Broadcast the game state
            BroadcastGameState();
            
            Logger.Log($"Game state after force: {_gameState}");
        }
        
        /// <summary>
        /// Deals hole cards to all players
        /// </summary>
        private void DealHoleCards()
        {
            Logger.Log("Dealing hole cards to players");
            
            if (_useLocalDeck)
            {
                // Deal from the local deck
                Logger.Log("Using emergency deck to deal cards to players");
                
                foreach (var player in _players)
                {
                    var cards = _localDeck.DealCards(2);
                    player.HoleCards.AddRange(cards);
                    
                    Logger.Log($"Dealt {cards.Count} cards to {player.Name} from emergency deck: {string.Join(" ", cards)}");
                }
                
                Logger.Log("All players have hole cards from emergency deck, transitioning to PreFlop");
            }
            else if (_microservicesMode)
            {
                // Deal through the card deck service
                // In a real implementation, we would send a message to the card deck service
                // and wait for the response, but for simplicity, we just use the local deck
                Logger.Log("Using emergency deck to deal cards to players");
                
                foreach (var player in _players)
                {
                    var cards = _localDeck.DealCards(2);
                    player.HoleCards.AddRange(cards);
                    
                    Logger.Log($"Dealt {cards.Count} cards to {player.Name} from emergency deck: {string.Join(" ", cards)}");
                }
                
                Logger.Log("All players have hole cards from emergency deck, transitioning to PreFlop");
            }
        }
        
        /// <summary>
        /// Broadcasts the current game state to all clients
        /// </summary>
        private void BroadcastGameState()
        {
            try
            {
                // Create the game state payload
                var gameStatePayload = new GameStatePayload
                {
                    State = _gameState,
                    Pot = _pot,
                    CommunityCards = _communityCards.ToList(),
                    Players = _players.Select(p => new PlayerInfo
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Chips = p.Chips,
                        IsDealer = p.IsDealer,
                        HasFolded = p.HasFolded,
                        IsActive = p.IsActive,
                        CurrentBet = p.CurrentBet,
                        TotalBet = p.TotalBet
                    }).ToList()
                };
                
                // Create and publish the message
                var message = SimpleMessage.Create(SimpleMessageType.GameState, gameStatePayload);
                PublishMessage(message);
                
                Logger.Log("Game state broadcast successful");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error broadcasting game state", ex);
            }
        }
        
        /// <summary>
        /// Handles a received message
        /// </summary>
        /// <param name="message">The message to handle</param>
        protected override void HandleMessage(SimpleMessage message)
        {
            base.HandleMessage(message);
            
            switch (message.Type)
            {
                case SimpleMessageType.ServiceRegistration:
                    // Check if it's the card deck service
                    var registrationPayload = message.GetPayload<ServiceRegistrationPayload>();
                    if (registrationPayload != null && registrationPayload.ServiceType == "CardDeck")
                    {
                        Logger.Log($"Card deck service registered: {registrationPayload.ServiceId}");
                        _cardDeckServiceId = registrationPayload.ServiceId;
                        
                        // Create a deck
                        CreateDeck();
                    }
                    break;
                    
                case SimpleMessageType.CardDeal:
                    // Handle card deal response
                    HandleCardDealResponse(message);
                    break;
                    
                case SimpleMessageType.DeckShuffle:
                    // Handle deck shuffle response
                    HandleDeckShuffleResponse(message);
                    break;
                    
                case SimpleMessageType.PlayerAction:
                    // Handle player action
                    HandlePlayerAction(message);
                    break;
                    
                default:
                    // Let the base class handle other message types
                    break;
            }
        }
        
        /// <summary>
        /// Handles a response from the card deck service for a card deal request
        /// </summary>
        /// <param name="message">The response message</param>
        private void HandleCardDealResponse(SimpleMessage message)
        {
            try
            {
                var responsePayload = message.GetPayload<CardDealResponsePayload>();
                if (responsePayload == null)
                {
                    Logger.LogError("Invalid card deal response payload");
                    return;
                }
                
                if (!responsePayload.Success)
                {
                    Logger.LogError("Card deal failed");
                    return;
                }
                
                if (responsePayload.DeckId != _deckId)
                {
                    Logger.LogError($"Received card deal response for unknown deck ID: {responsePayload.DeckId}");
                    return;
                }
                
                Logger.Log($"Received {responsePayload.Cards.Count} cards from deck {responsePayload.DeckId}");
                
                // Process the dealt cards based on the current game state
                switch (_gameState)
                {
                    case GameState.PreFlop:
                        // These are hole cards for a player
                        // In a real implementation, we would assign these cards to the appropriate player
                        break;
                        
                    case GameState.Flop:
                        // These are the flop cards
                        _communityCards.AddRange(responsePayload.Cards);
                        Logger.Log($"Added {responsePayload.Cards.Count} cards to community cards (flop)");
                        break;
                        
                    case GameState.Turn:
                        // This is the turn card
                        _communityCards.AddRange(responsePayload.Cards);
                        Logger.Log($"Added {responsePayload.Cards.Count} cards to community cards (turn)");
                        break;
                        
                    case GameState.River:
                        // This is the river card
                        _communityCards.AddRange(responsePayload.Cards);
                        Logger.Log($"Added {responsePayload.Cards.Count} cards to community cards (river)");
                        break;
                }
                
                // Broadcast the updated game state
                BroadcastGameState();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling card deal response", ex);
            }
        }
        
        /// <summary>
        /// Handles a response from the card deck service for a deck shuffle request
        /// </summary>
        /// <param name="message">The response message</param>
        private void HandleDeckShuffleResponse(SimpleMessage message)
        {
            try
            {
                var responsePayload = message.GetPayload<DeckShuffleResponsePayload>();
                if (responsePayload == null)
                {
                    Logger.LogError("Invalid deck shuffle response payload");
                    return;
                }
                
                if (!responsePayload.Success)
                {
                    Logger.LogError("Deck shuffle failed");
                    return;
                }
                
                Logger.Log($"Deck {responsePayload.DeckId} shuffled successfully");
                
                // Update the deck ID if needed
                if (string.IsNullOrEmpty(_deckId))
                {
                    _deckId = responsePayload.DeckId;
                    _useLocalDeck = false;
                    
                    Logger.Log($"Using deck ID: {_deckId}");
                    
                    // Start a hand if we have enough players
                    if (_players.Count >= 2)
                    {
                        StartHand();
                    }
                    else
                    {
                        Logger.LogWarning("Need at least 2 players to start a hand");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling deck shuffle response", ex);
            }
        }
        
        /// <summary>
        /// Handles a player action message
        /// </summary>
        /// <param name="message">The player action message</param>
        private void HandlePlayerAction(SimpleMessage message)
        {
            try
            {
                var actionPayload = message.GetPayload<PokerPlayerActionPayload>();
                if (actionPayload == null)
                {
                    Logger.LogError("Invalid player action payload");
                    return;
                }
                
                // Find the player
                var player = _players.FirstOrDefault(p => p.Id == actionPayload.PlayerId);
                if (player == null)
                {
                    Logger.LogError($"Player not found: {actionPayload.PlayerId}");
                    return;
                }
                
                // Process the action based on its type
                switch (actionPayload.Action)
                {
                    case PokerPlayerActionType.Fold:
                        // Player folds
                        player.Fold();
                        Logger.Log($"Player {player.Name} folded");
                        break;
                        
                    case PokerPlayerActionType.Check:
                        // Player checks
                        Logger.Log($"Player {player.Name} checked");
                        break;
                        
                    case PokerPlayerActionType.Call:
                        // Player calls
                        var callAmount = actionPayload.Amount;
                        var actualCallAmount = player.PlaceBet(callAmount);
                        _pot += actualCallAmount;
                        Logger.Log($"Player {player.Name} called ${callAmount} (actual: ${actualCallAmount})");
                        break;
                        
                    case PokerPlayerActionType.Raise:
                        // Player raises
                        var raiseAmount = actionPayload.Amount;
                        var actualRaiseAmount = player.PlaceBet(raiseAmount);
                        _pot += actualRaiseAmount;
                        Logger.Log($"Player {player.Name} raised to ${raiseAmount} (actual: ${actualRaiseAmount})");
                        break;
                }
                
                // Move to the next player
                _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
                
                // Check if the betting round is complete
                bool bettingRoundComplete = CheckBettingRoundComplete();
                if (bettingRoundComplete)
                {
                    // Move to the next game state
                    AdvanceGameState();
                }
                
                // Broadcast the updated game state
                BroadcastGameState();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error handling player action", ex);
            }
        }
        
        /// <summary>
        /// Checks if the current betting round is complete
        /// </summary>
        /// <returns>True if the betting round is complete, false otherwise</returns>
        private bool CheckBettingRoundComplete()
        {
            // A betting round is complete when all active players have either:
            // 1. Folded, or
            // 2. Called the highest bet, or
            // 3. Gone all-in with less than the highest bet
            
            // For simplicity, we'll just return true for now
            return true;
        }
        
        /// <summary>
        /// Advances the game state to the next state
        /// </summary>
        private void AdvanceGameState()
        {
            // Reset current bets
            foreach (var player in _players)
            {
                player.ResetCurrentBet();
            }
            
            // Set the current player to the one after the dealer
            _currentPlayerIndex = (_currentDealerIndex + 1) % _players.Count;
            
            // Advance the game state
            switch (_gameState)
            {
                case GameState.PreFlop:
                    // Deal the flop
                    _gameState = GameState.Flop;
                    DealFlop();
                    break;
                    
                case GameState.Flop:
                    // Deal the turn
                    _gameState = GameState.Turn;
                    DealTurn();
                    break;
                    
                case GameState.Turn:
                    // Deal the river
                    _gameState = GameState.River;
                    DealRiver();
                    break;
                    
                case GameState.River:
                    // Go to showdown
                    _gameState = GameState.Showdown;
                    HandleShowdown();
                    break;
                    
                case GameState.Showdown:
                    // End the hand
                    _gameState = GameState.Complete;
                    EndHand();
                    break;
                    
                case GameState.Complete:
                    // Start a new hand
                    StartHand();
                    break;
            }
        }
        
        /// <summary>
        /// Deals the flop cards
        /// </summary>
        private void DealFlop()
        {
            if (_useLocalDeck)
            {
                // Deal from the local deck
                _localDeck.DealCard(); // Burn card
                var flopCards = _localDeck.DealCards(3);
                _communityCards.AddRange(flopCards);
                
                Logger.Log($"Dealt flop from local deck: {string.Join(" ", flopCards)}");
            }
            else if (_microservicesMode)
            {
                // Deal through the card deck service
                // In a real implementation, we would send a message to the card deck service
                // and wait for the response, but for simplicity, we just use the local deck
                _localDeck.DealCard(); // Burn card
                var flopCards = _localDeck.DealCards(3);
                _communityCards.AddRange(flopCards);
                
                Logger.Log($"Dealt flop from local deck: {string.Join(" ", flopCards)}");
            }
        }
        
        /// <summary>
        /// Deals the turn card
        /// </summary>
        private void DealTurn()
        {
            if (_useLocalDeck)
            {
                // Deal from the local deck
                _localDeck.DealCard(); // Burn card
                var turnCard = _localDeck.DealCard();
                _communityCards.Add(turnCard);
                
                Logger.Log($"Dealt turn from local deck: {turnCard}");
            }
            else if (_microservicesMode)
            {
                // Deal through the card deck service
                // In a real implementation, we would send a message to the card deck service
                // and wait for the response, but for simplicity, we just use the local deck
                _localDeck.DealCard(); // Burn card
                var turnCard = _localDeck.DealCard();
                _communityCards.Add(turnCard);
                
                Logger.Log($"Dealt turn from local deck: {turnCard}");
            }
        }
        
        /// <summary>
        /// Deals the river card
        /// </summary>
        private void DealRiver()
        {
            if (_useLocalDeck)
            {
                // Deal from the local deck
                _localDeck.DealCard(); // Burn card
                var riverCard = _localDeck.DealCard();
                _communityCards.Add(riverCard);
                
                Logger.Log($"Dealt river from local deck: {riverCard}");
            }
            else if (_microservicesMode)
            {
                // Deal through the card deck service
                // In a real implementation, we would send a message to the card deck service
                // and wait for the response, but for simplicity, we just use the local deck
                _localDeck.DealCard(); // Burn card
                var riverCard = _localDeck.DealCard();
                _communityCards.Add(riverCard);
                
                Logger.Log($"Dealt river from local deck: {riverCard}");
            }
        }
        
        /// <summary>
        /// Handles the showdown phase
        /// </summary>
        private void HandleShowdown()
        {
            // Get the active players (those who haven't folded)
            var activePlayers = _players.Where(p => !p.HasFolded).ToList();
            if (activePlayers.Count == 0)
            {
                Logger.LogWarning("No active players in showdown");
                return;
            }
            
            // If there's only one active player, they win by default
            if (activePlayers.Count == 1)
            {
                var winner = activePlayers[0];
                winner.AwardChips(_pot);
                
                Logger.Log($"Player {winner.Name} won ${_pot} by default (all other players folded)");
                
                // Reset the pot
                _pot = 0;
                return;
            }
            
            // Evaluate the best hand for each active player
            var playerHands = new List<Game.Hand>();
            var bestHandsForBroadcast = new List<HandInfo>();
            
            foreach (var player in activePlayers)
            {
                // Evaluate the player's best hand
                var bestHand = Game.HandEvaluator.EvaluateBestHand(player.HoleCards, _communityCards, player.Id);
                
                // Add to the list of hands for determining the winner
                playerHands.Add(bestHand);
                
                // Add to the list of hands for broadcasting
                bestHandsForBroadcast.Add(HandInfo.FromHand(bestHand));
                
                Logger.Log($"Player {player.Name} has {bestHand}");
            }
            
            // Determine the winner(s)
            var winningHands = Game.HandEvaluator.DetermineWinners(playerHands);
            
            // If there's a tie, split the pot
            int winnerCount = winningHands.Count;
            int winningsPerPlayer = _pot / winnerCount;
            int remainder = _pot % winnerCount; // In case pot isn't evenly divisible
            
            // Track winner IDs for broadcasting
            var winnerIds = new List<string>();
            
            // Award chips to the winners
            for (int i = 0; i < winningHands.Count; i++)
            {
                var winningHand = winningHands[i];
                var winner = _players.First(p => p.Id == winningHand.PlayerId);
                
                // Award chips (last winner gets any remainder)
                int winnings = winningsPerPlayer;
                if (i == winningHands.Count - 1)
                {
                    winnings += remainder;
                }
                
                winner.AwardChips(winnings);
                winnerIds.Add(winner.Id);
                
                Logger.Log($"Player {winner.Name} won ${winnings} with {winningHand.Description}");
            }
            
            // Create a game state payload with the showdown results
            var gameStatePayload = new GameStatePayload
            {
                CurrentState = _gameState,
                Pot = _pot,
                CurrentBet = 0,
                CommunityCards = new List<Card>(_communityCards),
                Players = _players.Select(p => new PlayerInfo
                {
                    PlayerId = p.Id,
                    Name = p.Name,
                    Chips = p.Chips,
                    HasFolded = p.HasFolded,
                    IsAllIn = p.IsAllIn,
                    CurrentBet = p.CurrentBet,
                    // Include hole cards for all players since it's showdown time
                    HoleCards = new List<Card>(p.HoleCards)
                }).ToList(),
                DealerPosition = _currentDealerIndex,
                CurrentPlayerIndex = _currentPlayerIndex,
                BestHands = bestHandsForBroadcast,
                WinnerIds = winnerIds
            };
            
            // Broadcast the showdown results
            var message = SimpleMessage.Create(SimpleMessageType.GameState, gameStatePayload);
            PublishMessage(message);
            
            // Reset the pot
            _pot = 0;
        }
        
        /// <summary>
        /// Ends the current hand
        /// </summary>
        private void EndHand()
        {
            // For now, just log that the hand has ended
            Logger.Log("Hand ended");
            
            // Start a new hand after a delay
            Thread.Sleep(1000);
            StartHand();
        }
    }
    
    // These payload classes are now defined in the MessageTypes.cs file
}