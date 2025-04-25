using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PokerGame.Core.Game;
using PokerGame.Core.Models;

namespace PokerGame.Core.Microservices
{
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
        
        /// <summary>
        /// Creates a new console UI service
        /// </summary>
        /// <param name="publisherPort">The port to use for publishing messages</param>
        /// <param name="subscriberPort">The port to use for subscribing to messages</param>
        public ConsoleUIService(int publisherPort, int subscriberPort) 
            : base("PlayerUI", "Console UI", publisherPort, subscriberPort)
        {
        }
        
        /// <summary>
        /// Starts the UI service
        /// </summary>
        public override void Start()
        {
            base.Start();
            
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
                    
                case MessageType.PlayerAction:
                    var actionPlayer = message.GetPayload<PlayerInfo>();
                    if (actionPlayer != null)
                    {
                        _waitingForPlayerAction = true;
                        _activePlayerId = actionPlayer.PlayerId;
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
            Console.WriteLine("===============================================");
            Console.WriteLine("           TEXAS HOLD'EM POKER GAME           ");
            Console.WriteLine("===============================================");
            Console.WriteLine();
            
            // Wait for the game engine to be available
            while (_gameEngineServiceId == null)
            {
                Console.WriteLine("Waiting for game engine to start...");
                await Task.Delay(1000);
            }
            
            // Get number of players
            int numPlayers = 0;
            bool validInput = false;
            
            while (!validInput)
            {
                Console.Write("Enter number of players (2-8): ");
                string input = Console.ReadLine() ?? "";
                
                if (int.TryParse(input, out numPlayers) && numPlayers >= 2 && numPlayers <= 8)
                {
                    validInput = true;
                }
                else
                {
                    Console.WriteLine("Please enter a number between 2 and 8.");
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
            var message = Message.Create(MessageType.GameStart, playerNames);
            SendTo(message, _gameEngineServiceId);
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
                var payload = new PlayerActionPayload
                {
                    PlayerId = _activePlayerId,
                    ActionType = actionType,
                    BetAmount = betAmount
                };
                
                var message = Message.Create(MessageType.PlayerAction, payload);
                SendTo(message, _gameEngineServiceId);
                
                _waitingForPlayerAction = false;
            }
        }
        
        /// <summary>
        /// Displays the current game state
        /// </summary>
        private void DisplayGameState()
        {
            if (_latestGameState == null)
                return;
                
            Console.WriteLine();
            Console.WriteLine("===============================================");
            Console.WriteLine($"GAME STATE: {_latestGameState.CurrentState}");
            
            // Show community cards if any
            if (_latestGameState.CommunityCards.Count > 0)
            {
                Console.WriteLine($"Community cards: {CardListToString(_latestGameState.CommunityCards)}");
            }
            
            // Show pot and current bet
            Console.WriteLine($"Pot: {_latestGameState.Pot}   Current bet: {_latestGameState.CurrentBet}");
            Console.WriteLine();
            
            // Show player information
            Console.WriteLine("PLAYERS:");
            foreach (var player in _latestGameState.Players)
            {
                string status = player.HasFolded ? "Folded" : player.IsAllIn ? "All-In" : "Active";
                string currentBet = player.CurrentBet > 0 ? $" (Bet: {player.CurrentBet})" : "";
                Console.WriteLine($"- {player.Name}: {player.Chips} chips, {status}{currentBet}");
            }
            
            Console.WriteLine("===============================================");
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
    }
}