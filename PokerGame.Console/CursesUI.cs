using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PokerGame.Core.Game;
using PokerGame.Core.Interfaces;
using PokerGame.Core.Models;

namespace PokerGame.Console
{
    /// <summary>
    /// Provides an enhanced text-based user interface for the poker game using System.Console.
    /// </summary>
    public class CursesUI : IPokerGameUI, IDisposable
    {
        private readonly int _maxPlayers = 8;
        private bool _initialized;
        private CancellationTokenSource _cancelSource;
        private PokerGameEngine _gameEngine = null!;
        
        // Player and card display positions
        private readonly Dictionary<int, (int Row, int Col)> _playerPositions;
        
        public CursesUI()
        {
            // Define player positions around "the table"
            _playerPositions = new Dictionary<int, (int Row, int Col)>
            {
                { 0, (15, 2) },   // Bottom left
                { 1, (18, 2) },   // Bottom left (below)
                { 2, (18, 30) },  // Bottom center
                { 3, (18, 58) },  // Bottom right
                { 4, (15, 58) },  // Bottom right (above)
                { 5, (6, 58) },   // Top right
                { 6, (6, 30) },   // Top center
                { 7, (6, 2) }     // Top left
            };
            
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
            
            // Display welcome message
            System.Console.Clear();
            DrawBorder();
            WriteAt("TEXAS HOLD'EM POKER GAME", 20, 1);
            WriteAt("===============================================", 1, 3);
            
            // Get number of players
            int numPlayers = GetNumberInRange("Enter number of players (2-8): ", 2, 8, 2, 5);
            
            // Get player names
            string[] playerNames = new string[numPlayers];
            for (int i = 0; i < numPlayers; i++)
            {
                string defaultName = $"Player {i+1}";
                WriteAt($"Enter name for player {i+1} (or press Enter for '{defaultName}'): ", 2, 7 + i);
                SetCursorPosition(60, 7 + i);
                string name = System.Console.ReadLine();
                playerNames[i] = string.IsNullOrWhiteSpace(name) ? defaultName : name;
            }
            
            // Initialize the UI
            _initialized = true;
            
            // Start the game with the provided player names
            _gameEngine.StartGame(playerNames);
            
            // Main game loop (handled by the game engine and callbacks)
            while (true)
            {
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Shows a message to the user
        /// </summary>
        public void ShowMessage(string message)
        {
            if (!_initialized)
                return;
                
            // Display message in a prominent location
            WriteAt(message.PadRight(60), 2, 22);
        }
        
        /// <summary>
        /// Gets an action from the current player
        /// </summary>
        public void GetPlayerAction(Player player, PokerGameEngine gameEngine)
        {
            if (!_initialized)
                return;
            
            // Clear the action area
            ClearArea(2, 20, 60, 3);
            
            WriteAt($"{player.Name}'s turn", 2, 20);
            
            // Show player's hole cards
            string cardsText = FormatCards(player.HoleCards);
            WriteAt($"Your hole cards: {cardsText}", 2, 21);
            
            // Show available actions
            bool canCheck = player.CurrentBet == gameEngine.CurrentBet;
            int callAmount = gameEngine.CurrentBet - player.CurrentBet;
            
            string actionPrompt = "Actions: ";
            if (canCheck)
                actionPrompt += "[C]heck, ";
            else
                actionPrompt += $"[C]all ${callAmount}, ";
                
            actionPrompt += "[F]old, [R]aise";
            WriteAt(actionPrompt, 2, 22);
            
            // Get player action
            bool validAction = false;
            while (!validAction)
            {
                WriteAt("Enter your action: ", 2, 23);
                SetCursorPosition(20, 23);
                string actionInput = System.Console.ReadLine()?.ToUpper() ?? "";
                
                switch (actionInput)
                {
                    case "F":
                        gameEngine.ProcessPlayerAction("fold");
                        validAction = true;
                        break;
                        
                    case "C":
                        if (canCheck)
                            gameEngine.ProcessPlayerAction("check");
                        else
                            gameEngine.ProcessPlayerAction("call");
                        validAction = true;
                        break;
                        
                    case "R":
                        int minRaise = gameEngine.CurrentBet + 10;
                        WriteAt($"Enter raise amount (min {minRaise}): $", 2, 24);
                        SetCursorPosition(35, 24);
                        if (int.TryParse(System.Console.ReadLine(), out int raiseAmount) && 
                            raiseAmount >= minRaise && 
                            raiseAmount <= player.Chips + player.CurrentBet)
                        {
                            gameEngine.ProcessPlayerAction("raise", raiseAmount);
                            validAction = true;
                        }
                        else
                        {
                            WriteAt("Invalid amount. Try again.", 2, 24);
                            Thread.Sleep(1000);
                            WriteAt("                          ", 2, 24);
                        }
                        break;
                        
                    default:
                        WriteAt("Invalid action. Use F (fold), C (check/call), or R (raise).", 2, 24);
                        Thread.Sleep(1000);
                        WriteAt("                                                           ", 2, 24);
                        break;
                }
            }
            
            // Clear action area after processing
            ClearArea(2, 20, 60, 5);
        }
        
        /// <summary>
        /// Updates the UI with the current game state
        /// </summary>
        public void UpdateGameState(PokerGameEngine gameEngine)
        {
            if (!_initialized || gameEngine.State == GameState.HandComplete)
                return;
            
            // Refresh the screen
            System.Console.Clear();
            DrawBorder();
            WriteAt("TEXAS HOLD'EM POKER GAME", 20, 1);
            
            // Show game state
            WriteAt($"GAME STATE: {gameEngine.State}", 2, 3);
            
            // Show community cards if any
            if (gameEngine.CommunityCards.Count > 0)
            {
                string communityCards = FormatCards(new List<Card>(gameEngine.CommunityCards));
                WriteAt($"Community cards: {communityCards}", 2, 5);
            }
            else
            {
                WriteAt("Community cards: [None]", 2, 5);
            }
            
            // Show pot and current bet
            WriteAt($"Pot: ${gameEngine.Pot}   Current bet: ${gameEngine.CurrentBet}", 2, 6);
            
            // Show player information at their table positions
            for (int i = 0; i < gameEngine.Players.Count && i < _maxPlayers; i++)
            {
                var player = gameEngine.Players[i];
                var pos = _playerPositions[i];
                
                string status = player.HasFolded ? "FOLDED" : 
                                player.IsAllIn ? "ALL-IN" : 
                                player == gameEngine.CurrentPlayer ? ">> ACTIVE <<" : "";
                                
                WriteAt($"{player.Name}: ${player.Chips} {status}", pos.Col, pos.Row);
                
                if (player.HoleCards.Count > 0 && !player.HasFolded && player == gameEngine.CurrentPlayer)
                {
                    // Only show cards for the current player
                    string cards = FormatCards(player.HoleCards);
                    WriteAt(cards, pos.Col, pos.Row + 1);
                }
                else if (player.HoleCards.Count > 0 && !player.HasFolded)
                {
                    // Show card backs for other players
                    WriteAt("[??] [??]", pos.Col, pos.Row + 1);
                }
            }
        }
        
        /// <summary>
        /// Formats a list of cards for display
        /// </summary>
        private string FormatCards(List<Card> cards)
        {
            if (cards == null || cards.Count == 0)
                return "[None]";
                
            var cardText = "";
            foreach (var card in cards)
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
                
                cardText += $"[{rank}{suit}] ";
            }
            
            return cardText.Trim();
        }
        
        /// <summary>
        /// Helper method to get a number within a specified range
        /// </summary>
        private int GetNumberInRange(string prompt, int min, int max, int row, int col)
        {
            int number;
            bool valid = false;
            
            do
            {
                WriteAt(prompt, col, row);
                SetCursorPosition(col + prompt.Length, row);
                string input = System.Console.ReadLine();
                
                if (int.TryParse(input, out number) && number >= min && number <= max)
                {
                    valid = true;
                }
                else
                {
                    WriteAt($"Please enter a number between {min} and {max}.", col, row + 1);
                    Thread.Sleep(1000);
                    WriteAt("                                             ", col, row + 1);
                }
            } while (!valid);
            
            return number;
        }
        
        /// <summary>
        /// Draws a border around the console
        /// </summary>
        private void DrawBorder()
        {
            int width = System.Console.WindowWidth - 2;
            int height = System.Console.WindowHeight - 2;
            
            // Draw top and bottom borders
            WriteAt("+" + new string('-', width) + "+", 0, 0);
            WriteAt("+" + new string('-', width) + "+", 0, height);
            
            // Draw left and right borders
            for (int i = 1; i < height; i++)
            {
                WriteAt("|", 0, i);
                WriteAt("|", width + 1, i);
            }
        }
        
        /// <summary>
        /// Writes text at the specified position
        /// </summary>
        private void WriteAt(string text, int left, int top)
        {
            SetCursorPosition(left, top);
            System.Console.Write(text);
        }
        
        /// <summary>
        /// Sets the cursor position safely
        /// </summary>
        private void SetCursorPosition(int left, int top)
        {
            try
            {
                System.Console.SetCursorPosition(left, top);
            }
            catch (Exception)
            {
                // Handle out-of-range cursor positions
                try
                {
                    System.Console.SetCursorPosition(0, 0);
                }
                catch
                {
                    // Last resort, just skip positioning
                }
            }
        }
        
        /// <summary>
        /// Clears a rectangular area of the console
        /// </summary>
        private void ClearArea(int left, int top, int width, int height)
        {
            string blank = new string(' ', width);
            for (int i = 0; i < height; i++)
            {
                WriteAt(blank, left, top + i);
            }
        }
        
        public void Dispose()
        {
            if (_cancelSource != null)
            {
                _cancelSource.Cancel();
                _cancelSource.Dispose();
                _cancelSource = null;
            }
        }
    }
}