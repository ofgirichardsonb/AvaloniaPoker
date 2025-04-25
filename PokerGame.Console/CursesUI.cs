using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PokerGame.Core.Game;
using PokerGame.Core.Interfaces;
using PokerGame.Core.Models;

namespace PokerGame.Console
{
    /// <summary>
    /// A simplified implementation of the poker game UI
    /// </summary>
    public class CursesUI : IPokerGameUI, IDisposable
    {
        private readonly int _maxPlayers = 8;
        private bool _initialized;
        private CancellationTokenSource _cancelSource;
        private PokerGameEngine _gameEngine = null!;
        
        public CursesUI()
        {
            _cancelSource = new CancellationTokenSource();
        }
        
        /// <summary>
        /// Sets the game engine reference for this UI
        /// </summary>
        public void SetGameEngine(PokerGameEngine gameEngine)
        {
            _gameEngine = gameEngine;
        }
        
        /// <summary>
        /// Starts the game UI and application flow
        /// </summary>
        public void StartGame()
        {
            if (_gameEngine == null)
                throw new InvalidOperationException("Game engine not set");
                
            try
            {
                System.Console.WriteLine("Starting with enhanced UI...");
                System.Console.WriteLine("Enhanced Console UI is active.");
                System.Console.WriteLine("Using simplified console UI for now...");
                
                // Get number of players and initialize game
                System.Console.Write("Enter number of players (2-8): ");
                string? input = System.Console.ReadLine();
                
                if (!int.TryParse(input, out int numPlayers) || numPlayers < 2 || numPlayers > _maxPlayers)
                {
                    System.Console.WriteLine($"Invalid number of players. Using default (4).");
                    numPlayers = 4;
                }
                
                // Initialize players
                var players = new List<Player>();
                for (int i = 0; i < numPlayers; i++)
                {
                    System.Console.Write($"Enter name for player {i+1} (or press Enter for 'Player {i+1}'): ");
                    string name = System.Console.ReadLine() ?? "";
                    if (string.IsNullOrWhiteSpace(name))
                        name = $"Player {i+1}";
                        
                    players.Add(new Player(name, 1000)); // Start with 1000 chips
                }
                
                // Initialize game
                // Convert player list to names for the engine
                string[] playerNames = players.Select(p => p.Name).ToArray();
                _gameEngine.StartGame(playerNames, 1000);
                System.Console.WriteLine("=============================================");
                System.Console.WriteLine("    TEXAS HOLD'EM POKER GAME (CONSOLE UI)   ");
                System.Console.WriteLine("=============================================");
                
                // Game loop
                while (!_cancelSource.Token.IsCancellationRequested && _gameEngine.Players.Count(p => p.Chips > 0) > 1)
                {
                    _gameEngine.StartHand();
                }
                
                System.Console.WriteLine("Game over!");
                var winner = _gameEngine.Players.OrderByDescending(p => p.Chips).First();
                System.Console.WriteLine($"{winner.Name} wins the game with {winner.Chips} chips!");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error in game: {ex.Message}");
                System.Console.WriteLine(ex.StackTrace);
            }
        }
        
        /// <summary>
        /// Initialize the enhanced UI
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                return;
            
            try
            {
                // Clear the console
                System.Console.Clear();
                
                // Set console colors if supported
                try 
                {
                    System.Console.BackgroundColor = ConsoleColor.Black;
                    System.Console.ForegroundColor = ConsoleColor.Cyan;
                }
                catch
                {
                    // Ignore color setting errors - some terminals might not support it
                }
                
                // Display a fancy header
                System.Console.WriteLine("╔═════════════════════════════════════════════════════════╗");
                System.Console.WriteLine("║        TEXAS HOLD'EM POKER (ENHANCED CONSOLE UI)        ║");
                System.Console.WriteLine("╚═════════════════════════════════════════════════════════╝");
                System.Console.WriteLine();
                System.Console.WriteLine("Waiting for game data...");
                System.Console.WriteLine();
                
                // Reset color
                try 
                {
                    System.Console.ResetColor();
                }
                catch
                {
                    // Ignore color reset errors
                }
                
                _initialized = true;
                System.Console.WriteLine("Enhanced UI initialized successfully");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error initializing enhanced UI: {ex.Message}");
                System.Console.WriteLine("Using simplified console UI");
            }
        }
        
        /// <summary>
        /// Shows a message to the user
        /// </summary>
        public void ShowMessage(string message)
        {
            System.Console.WriteLine(message);
        }
        
        /// <summary>
        /// Gets the ante amount from the user
        /// </summary>
        public int GetAnteAmount()
        {
            try
            {
                System.Console.Write("Enter ante amount: $");
                string? input = System.Console.ReadLine();
                
                if (!int.TryParse(input, out int ante) || ante < 1)
                {
                    System.Console.WriteLine("Invalid ante amount. Using default ($10).");
                    return 10;
                }
                
                return ante;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error getting ante: {ex.Message}");
                return 10; // Default
            }
        }
        
        /// <summary>
        /// Gets the player's action for their turn
        /// </summary>
        public void GetPlayerAction(Player player, PokerGameEngine gameEngine)
        {
            try
            {
                System.Console.WriteLine();
                System.Console.WriteLine($"--- {player.Name}'s turn ---");
                
                if (player.HoleCards.Count == 2)
                {
                    System.Console.WriteLine($"Your cards: {FormatCards(player.HoleCards)}");
                }
                
                bool validAction = false;
                while (!validAction)
                {
                    // Determine what actions are allowed
                    bool canCheck = gameEngine.CurrentBet == 0 || player.CurrentBet == gameEngine.CurrentBet;
                    int callAmount = gameEngine.CurrentBet - player.CurrentBet;
                    
                    System.Console.WriteLine("Available actions:");
                    System.Console.WriteLine("F - Fold");
                    
                    if (canCheck)
                        System.Console.WriteLine("C - Check");
                    else
                        System.Console.WriteLine($"C - Call (${callAmount})");
                        
                    System.Console.WriteLine("R - Raise");
                    
                    System.Console.Write("Enter action: ");
                    string actionText = (System.Console.ReadLine() ?? "").ToUpper();
                    
                    if (actionText == "F")
                    {
                        // Fold
                        gameEngine.ProcessPlayerAction("fold");
                        validAction = true;
                    }
                    else if (actionText == "C")
                    {
                        // Check or Call
                        if (canCheck)
                            gameEngine.ProcessPlayerAction("check");
                        else
                            gameEngine.ProcessPlayerAction("call");
                        validAction = true;
                    }
                    else if (actionText == "R")
                    {
                        // Raise - get the amount
                        System.Console.Write("Enter raise amount: $");
                        string raiseInput = System.Console.ReadLine() ?? "";
                        
                        if (int.TryParse(raiseInput, out int raiseAmount) && raiseAmount > 0)
                        {
                            gameEngine.ProcessPlayerAction("raise", raiseAmount);
                            validAction = true;
                        }
                        else
                        {
                            System.Console.WriteLine("Invalid amount! Try again.");
                        }
                    }
                    else
                    {
                        System.Console.WriteLine("Invalid action! Try again.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error getting player action: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Updates the game state display
        /// </summary>
        public void UpdateGameState(PokerGameEngine gameEngine)
        {
            try
            {
                System.Console.WriteLine();
                System.Console.WriteLine("=============================================");
                System.Console.WriteLine($"CURRENT STATE: {gameEngine.State}");
                
                // Show community cards
                string communityCardsText = gameEngine.CommunityCards.Count > 0 
                    ? FormatCards(gameEngine.CommunityCards) 
                    : "[None]";
                System.Console.WriteLine($"Community Cards: {communityCardsText}");
                
                // Show pot
                System.Console.WriteLine($"Pot: ${gameEngine.Pot}");
                if (gameEngine.CurrentBet > 0)
                {
                    System.Console.WriteLine($"Current bet: ${gameEngine.CurrentBet}");
                }
                System.Console.WriteLine();
                
                // Show player info
                System.Console.WriteLine("PLAYERS:");
                for (int i = 0; i < gameEngine.Players.Count; i++)
                {
                    var player = gameEngine.Players[i];
                    string status = "";
                    if (player.HasFolded) status = " (Folded)";
                    else if (player.IsAllIn) status = " (All-In)";
                    
                    System.Console.WriteLine($"- {player.Name}{status}: ${player.Chips} chips");
                }
                System.Console.WriteLine("=============================================");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error updating game state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Shows the winning player(s) for the hand
        /// </summary>
        public void ShowWinner(List<Player> winners, PokerGameEngine gameEngine)
        {
            try
            {
                System.Console.WriteLine();
                System.Console.WriteLine("*** HAND COMPLETE ***");
                
                if (winners.Count == 1)
                {
                    var winner = winners[0];
                    System.Console.WriteLine($"{winner.Name} wins the pot!");
                    if (winner.CurrentHand != null)
                    {
                        System.Console.WriteLine($"Winning hand: {winner.CurrentHand.Rank}");
                        System.Console.WriteLine($"Cards: {FormatCards(winner.CurrentHand.Cards)}");
                    }
                }
                else
                {
                    System.Console.WriteLine("Split pot between: " + string.Join(", ", winners.Select(w => w.Name)));
                    var firstWinner = winners[0];
                    if (firstWinner.CurrentHand != null)
                    {
                        System.Console.WriteLine($"Winning hand: {firstWinner.CurrentHand.Rank}");
                    }
                }
                
                System.Console.WriteLine("Press Enter to continue...");
                System.Console.ReadLine();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error showing winner: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Formats a list of cards for display
        /// </summary>
        private string FormatCards(IEnumerable<Card> cards)
        {
            if (cards == null || !cards.Any())
                return "";
                
            return string.Join(" ", cards.Select(card => FormatCard(card)));
        }
        
        /// <summary>
        /// Formats a single card for display
        /// </summary>
        private string FormatCard(Card card)
        {
            string rank;
            switch (card.Rank)
            {
                case Rank.Ten:
                    rank = "10";
                    break;
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
                    rank = ((int)card.Rank + 2).ToString();
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
        /// Releases resources
        /// </summary>
        public void Dispose()
        {
            _cancelSource.Cancel();
            _cancelSource.Dispose();
        }
    }
}