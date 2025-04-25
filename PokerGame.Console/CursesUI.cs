using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mindmagma.Curses;
using PokerGame.Core.Game;
using PokerGame.Core.Interfaces;
using PokerGame.Core.Models;

namespace PokerGame.Console
{
    /// <summary>
    /// Provides a basic console-based user interface for the poker game.
    /// This is a simplified implementation until the full NCurses implementation is complete.
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
                System.Console.WriteLine("Enhanced UI (NCurses) is still in development.");
                System.Console.WriteLine("Using simplified console UI for now...");
                System.Console.WriteLine("=============================================");
                System.Console.WriteLine("    TEXAS HOLD'EM POKER GAME (CONSOLE UI)   ");
                System.Console.WriteLine("=============================================");
                System.Console.WriteLine();
                
                // Get number of players
                int numPlayers = 0;
                bool validInput = false;
                
                while (!validInput)
                {
                    System.Console.Write("Enter number of players (2-8): ");
                    string input = System.Console.ReadLine() ?? "";
                    
                    if (int.TryParse(input, out numPlayers) && numPlayers >= 2 && numPlayers <= 8)
                    {
                        validInput = true;
                    }
                    else
                    {
                        System.Console.WriteLine("Please enter a number between 2 and 8.");
                    }
                }
                
                // Get player names
                string[] playerNames = new string[numPlayers];
                for (int i = 0; i < numPlayers; i++)
                {
                    string defaultName = $"Player {i+1}";
                    System.Console.Write($"Enter name for player {i+1} (or press Enter for '{defaultName}'): ");
                    string name = System.Console.ReadLine() ?? "";
                    playerNames[i] = string.IsNullOrWhiteSpace(name) ? defaultName : name;
                }
                
                // Initialize the UI
                _initialized = true;
                
                // Start the game with the provided player names
                _gameEngine.StartGame(playerNames);
                
                // Main game loop (handled by the game engine and callbacks)
                System.Console.WriteLine("Game started! Press Ctrl+C to exit.");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error initializing game: {ex.Message}");
                System.Console.WriteLine(ex.StackTrace);
            }
        }
        
        /// <summary>
        /// Shows a message to the user
        /// </summary>
        public void ShowMessage(string message)
        {
            if (!_initialized)
                return;
                
            System.Console.WriteLine($">>> {message}");
        }
        
        /// <summary>
        /// Gets an action from the current player
        /// </summary>
        public void GetPlayerAction(Player player, PokerGameEngine gameEngine)
        {
            if (!_initialized)
                return;
            
            try
            {
                System.Console.WriteLine();
                System.Console.WriteLine($"=== {player.Name}'s turn ===");
                
                // Show player's hole cards
                System.Console.WriteLine($"Your hole cards: {FormatCards(player.HoleCards)}");
                
                // Show available actions
                bool canCheck = player.CurrentBet == gameEngine.CurrentBet;
                int callAmount = gameEngine.CurrentBet - player.CurrentBet;
                
                System.Console.WriteLine("Available actions:");
                
                if (canCheck)
                    System.Console.WriteLine("C = Check");
                else
                    System.Console.WriteLine($"C = Call (${callAmount})");
                    
                System.Console.WriteLine("F = Fold");
                System.Console.WriteLine("R = Raise");
                
                // Get player action
                bool validAction = false;
                while (!validAction)
                {
                    System.Console.Write("Enter your action (C/F/R): ");
                    string actionText = (System.Console.ReadLine() ?? "").ToUpper();
                    
                    if (actionText == "F")
                    {
                        // Fold
                        gameEngine.HandlePlayerAction(player, ActionType.Fold);
                        validAction = true;
                    }
                    else if (actionText == "C")
                    {
                        // Check or Call
                        if (canCheck)
                            gameEngine.HandlePlayerAction(player, ActionType.Check);
                        else
                            gameEngine.HandlePlayerAction(player, ActionType.Call);
                        validAction = true;
                    }
                    else if (actionText == "R")
                    {
                        // Raise - get the amount
                        System.Console.Write("Enter raise amount: $");
                        string raiseInput = System.Console.ReadLine() ?? "";
                        
                        if (int.TryParse(raiseInput, out int raiseAmount) && raiseAmount > 0)
                        {
                            gameEngine.HandlePlayerAction(player, ActionType.Raise, raiseAmount);
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
            if (!_initialized)
                return;
                
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
            if (!_initialized)
                return;
                
            try
            {
                System.Console.WriteLine();
                System.Console.WriteLine("*** HAND COMPLETE ***");
                
                if (winners.Count == 1)
                {
                    var winner = winners[0];
                    System.Console.WriteLine($"{winner.Name} wins with {winner.BestHand.HandRank}!");
                    System.Console.WriteLine($"Winning hand: {FormatCards(winner.BestHand.Cards)}");
                }
                else
                {
                    System.Console.WriteLine("Split pot between: " + string.Join(", ", winners.Select(w => w.Name)));
                    System.Console.WriteLine($"Winning hand: {winners[0].BestHand.HandRank}");
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
        private string FormatCards(List<Card> cards)
        {
            if (cards == null || cards.Count == 0)
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