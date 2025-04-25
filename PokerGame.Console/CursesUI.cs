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
    /// Provides an enhanced text-based user interface for the poker game using NCurses.
    /// </summary>
    public class CursesUI : IPokerGameUI, IDisposable
    {
        private readonly int _maxPlayers = 8;
        private bool _initialized;
        private CancellationTokenSource _cancelSource;
        private PokerGameEngine _gameEngine = null!;
        
        // NCurses main window and player info windows
        private Window _mainWindow;
        private Window _statusWindow;
        private Window _communityCardsWindow;
        private Window _potWindow;
        private Window _actionWindow;
        private Dictionary<int, Window> _playerWindows;
        
        // Player positions around the table
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
                
            try
            {
                // Initialize NCurses
                Curses.InitScreen();
                Curses.Raw();
                Curses.KeyPad(Curses.StdScr, true);
                Curses.NoEcho();
                Curses.Start_Color();
                Curses.Refresh();
                
                // Initialize color pairs
                InitializeColors();
                
                // Create main window and draw borders
                int height = Curses.Lines;
                int width = Curses.Cols;
                _mainWindow = new Window(height, width, 0, 0);
                _mainWindow.Box(0, 0);
                _mainWindow.Refresh();
                
                // Create status window
                _statusWindow = new Window(3, width - 4, 1, 2);
                _statusWindow.Box(0, 0);
                _statusWindow.MvAddStr(1, 2, "TEXAS HOLD'EM POKER GAME");
                _statusWindow.Refresh();
                
                // Create community cards window
                _communityCardsWindow = new Window(5, width - 4, 5, 2);
                _communityCardsWindow.Box(0, 0);
                _communityCardsWindow.MvAddStr(1, 2, "Community Cards: [None]");
                _communityCardsWindow.Refresh();
                
                // Create pot window
                _potWindow = new Window(3, 20, 5, width - 24);
                _potWindow.Box(0, 0);
                _potWindow.MvAddStr(1, 2, "Pot: $0");
                _potWindow.Refresh();
                
                // Create action window
                _actionWindow = new Window(5, width - 4, height - 6, 2);
                _actionWindow.Box(0, 0);
                _actionWindow.MvAddStr(1, 2, "Actions:");
                _actionWindow.Refresh();
                
                // Display welcome message
                _statusWindow.Clear();
                _statusWindow.Box(0, 0);
                _statusWindow.MvAddStr(1, 2, "Welcome to Texas Hold'em Poker!");
                _statusWindow.Refresh();
                
                // Get number of players
                _actionWindow.Clear();
                _actionWindow.Box(0, 0);
                _actionWindow.MvAddStr(1, 2, "Enter number of players (2-8): ");
                _actionWindow.Refresh();
                Curses.Echo();
                string input = GetInput(_actionWindow, 1, 32, 1);
                Curses.NoEcho();
                int numPlayers;
                if (!int.TryParse(input, out numPlayers) || numPlayers < 2 || numPlayers > 8)
                {
                    numPlayers = 4; // Default to 4 players
                }
                
                // Initialize player windows
                _playerWindows = new Dictionary<int, Window>();
                for (int i = 0; i < numPlayers; i++)
                {
                    var pos = _playerPositions[i];
                    Window playerWindow = new Window(3, 28, pos.Row, pos.Col);
                    playerWindow.Box(0, 0);
                    _playerWindows[i] = playerWindow;
                }
                
                // Get player names
                string[] playerNames = new string[numPlayers];
                for (int i = 0; i < numPlayers; i++)
                {
                    string defaultName = $"Player {i+1}";
                    _actionWindow.Clear();
                    _actionWindow.Box(0, 0);
                    _actionWindow.MvAddStr(1, 2, $"Enter name for player {i+1} (Enter for '{defaultName}'): ");
                    _actionWindow.Refresh();
                    Curses.Echo();
                    string name = GetInput(_actionWindow, 1, 55, 20);
                    Curses.NoEcho();
                    playerNames[i] = string.IsNullOrWhiteSpace(name) ? defaultName : name;
                    
                    // Display player in their window
                    _playerWindows[i].Clear();
                    _playerWindows[i].Box(0, 0);
                    _playerWindows[i].MvAddStr(1, 2, playerNames[i]);
                    _playerWindows[i].Refresh();
                }
                
                // Initialize the UI
                _initialized = true;
                
                // Start the game with the provided player names
                _gameEngine.StartGame(playerNames);
                
                // Main game loop (handled by the game engine and callbacks)
                while (true)
                {
                    Thread.Sleep(100);
                    Curses.Refresh();
                }
            }
            catch (Exception ex)
            {
                EndCurses();
                System.Console.WriteLine($"Error initializing NCurses: {ex.Message}");
                System.Console.WriteLine(ex.StackTrace);
            }
        }
        
        /// <summary>
        /// Initialize color pairs for the UI
        /// </summary>
        private void InitializeColors()
        {
            // Initialize color pairs
            Curses.Init_Pair(1, Curses.COLOR_WHITE, Curses.COLOR_BLUE);   // Title
            Curses.Init_Pair(2, Curses.COLOR_RED, Curses.COLOR_BLACK);    // Hearts/Diamonds
            Curses.Init_Pair(3, Curses.COLOR_WHITE, Curses.COLOR_BLACK);  // Clubs/Spades
            Curses.Init_Pair(4, Curses.COLOR_BLACK, Curses.COLOR_GREEN);  // Table
            Curses.Init_Pair(5, Curses.COLOR_YELLOW, Curses.COLOR_BLACK); // Active player
            Curses.Init_Pair(6, Curses.COLOR_RED, Curses.COLOR_BLACK);    // Folded player
        }
        
        /// <summary>
        /// Gets input from the user within an NCurses window
        /// </summary>
        private string GetInput(Window window, int y, int x, int maxLength)
        {
            Curses.Echo();
            window.Move(y, x);
            window.Refresh();
            
            char[] buffer = new char[maxLength];
            int pos = 0;
            
            while (pos < maxLength)
            {
                int ch = Curses.GetCh();
                
                if (ch == Curses.KEY_ENTER || ch == 10 || ch == 13) // Enter key (different key codes)
                {
                    break;
                }
                else if (ch == Curses.KEY_BACKSPACE || ch == 127) // Backspace
                {
                    if (pos > 0)
                    {
                        pos--;
                        window.MvAddCh(y, x + pos, ' ');
                        window.Move(y, x + pos);
                        window.Refresh();
                    }
                }
                else if (ch >= 32 && ch <= 126) // Printable ASCII
                {
                    buffer[pos] = (char)ch;
                    window.MvAddCh(y, x + pos, ch);
                    pos++;
                    window.Refresh();
                }
            }
            
            Curses.NoEcho();
            return new string(buffer, 0, pos);
        }
        
        /// <summary>
        /// Safely ends curses mode
        /// </summary>
        private void EndCurses()
        {
            try
            {
                Curses.EndWin();
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        /// <summary>
        /// Shows a message to the user
        /// </summary>
        public void ShowMessage(string message)
        {
            if (!_initialized)
                return;
                
            // Display message in status window
            try
            {
                _statusWindow?.Clear();
                _statusWindow?.Box(0, 0);
                _statusWindow?.MvAddStr(1, 2, message.Length > 60 ? message.Substring(0, 60) : message);
                _statusWindow?.Refresh();
            }
            catch (Exception ex)
            {
                // If NCurses fails, fall back to console
                System.Console.WriteLine($"Message: {message}");
            }
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
                // Update status window to show whose turn it is
                _statusWindow.Clear();
                _statusWindow.Box(0, 0);
                _statusWindow.MvAddStr(1, 2, $"{player.Name}'s turn");
                _statusWindow.Refresh();
                
                // Highlight active player's window
                int playerIndex = -1;
                for (int i = 0; i < gameEngine.Players.Count; i++)
                {
                    if (gameEngine.Players[i] == player)
                    {
                        playerIndex = i;
                        break;
                    }
                }
                
                if (playerIndex >= 0 && _playerWindows.ContainsKey(playerIndex))
                {
                    // Highlight the active player's window
                    _playerWindows[playerIndex].AttrOn(Curses.COLOR_PAIR(5));
                    _playerWindows[playerIndex].Box(0, 0);
                    _playerWindows[playerIndex].AttrOff(Curses.COLOR_PAIR(5));
                    _playerWindows[playerIndex].Refresh();
                }
                
                // Show player's hole cards in the action window
                _actionWindow.Clear();
                _actionWindow.Box(0, 0);
                
                string cardsText = FormatCards(player.HoleCards);
                _actionWindow.MvAddStr(1, 2, $"Your hole cards: {cardsText}");
                
                // Show available actions
                bool canCheck = player.CurrentBet == gameEngine.CurrentBet;
                int callAmount = gameEngine.CurrentBet - player.CurrentBet;
                
                string actionPrompt = "Actions: ";
                if (canCheck)
                    actionPrompt += "[C]heck, ";
                else
                    actionPrompt += $"[C]all ${callAmount}, ";
                    
                actionPrompt += "[F]old, [R]aise";
                _actionWindow.MvAddStr(2, 2, actionPrompt);
                _actionWindow.Refresh();
                
                // Get player action
                bool validAction = false;
                while (!validAction)
                {
                    _actionWindow.MvAddStr(3, 2, "Enter your action (C/F/R): ");
                    _actionWindow.Refresh();
                    
                    Curses.Echo();
                    string actionInput = GetInput(_actionWindow, 3, 27, 1).ToUpper();
                    Curses.NoEcho();
                    
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
                            _actionWindow.MvAddStr(3, 2, $"Enter raise amount (min ${minRaise}): $");
                            _actionWindow.Clrtoeol();
                            _actionWindow.Refresh();
                            
                            Curses.Echo();
                            string raiseInput = GetInput(_actionWindow, 3, 37, 5);
                            Curses.NoEcho();
                            
                            if (int.TryParse(raiseInput, out int raiseAmount) && 
                                raiseAmount >= minRaise && 
                                raiseAmount <= player.Chips + player.CurrentBet)
                            {
                                gameEngine.ProcessPlayerAction("raise", raiseAmount);
                                validAction = true;
                            }
                            else
                            {
                                _actionWindow.MvAddStr(3, 2, "Invalid amount. Try again.");
                                _actionWindow.Clrtoeol();
                                _actionWindow.Refresh();
                                Thread.Sleep(1000);
                            }
                            break;
                            
                        default:
                            _actionWindow.MvAddStr(3, 2, "Invalid action. Use F (fold), C (check/call), or R (raise).");
                            _actionWindow.Clrtoeol();
                            _actionWindow.Refresh();
                            Thread.Sleep(1000);
                            break;
                    }
                }
                
                // Clear highlight from player window
                if (playerIndex >= 0 && _playerWindows.ContainsKey(playerIndex))
                {
                    _playerWindows[playerIndex].Box(0, 0);
                    _playerWindows[playerIndex].Refresh();
                }
                
                // Clear action window
                _actionWindow.Clear();
                _actionWindow.Box(0, 0);
                _actionWindow.Refresh();
            }
            catch (Exception ex)
            {
                // Fall back to console if NCurses fails
                System.Console.WriteLine($"Error in GetPlayerAction: {ex.Message}");
                System.Console.WriteLine($"Enter action for {player.Name} (F=fold, C=check/call, R=raise): ");
                string input = System.Console.ReadLine()?.ToUpper() ?? "";
                
                switch (input)
                {
                    case "F":
                        gameEngine.ProcessPlayerAction("fold");
                        break;
                    case "C":
                        if (player.CurrentBet == gameEngine.CurrentBet)
                            gameEngine.ProcessPlayerAction("check");
                        else
                            gameEngine.ProcessPlayerAction("call");
                        break;
                    case "R":
                        System.Console.Write("Enter raise amount: $");
                        if (int.TryParse(System.Console.ReadLine(), out int amount))
                            gameEngine.ProcessPlayerAction("raise", amount);
                        else
                            gameEngine.ProcessPlayerAction("fold");
                        break;
                    default:
                        gameEngine.ProcessPlayerAction("fold");
                        break;
                }
            }
        }
        
        /// <summary>
        /// Updates the UI with the current game state
        /// </summary>
        public void UpdateGameState(PokerGameEngine gameEngine)
        {
            if (!_initialized || gameEngine.State == GameState.HandComplete)
                return;
            
            try
            {
                // Update status window with game state
                _statusWindow.Clear();
                _statusWindow.Box(0, 0);
                _statusWindow.MvAddStr(1, 2, $"TEXAS HOLD'EM POKER - {gameEngine.State}");
                _statusWindow.Refresh();
                
                // Update community cards window
                _communityCardsWindow.Clear();
                _communityCardsWindow.Box(0, 0);
                
                if (gameEngine.CommunityCards.Count > 0)
                {
                    string communityCards = FormatCards(new List<Card>(gameEngine.CommunityCards));
                    _communityCardsWindow.MvAddStr(1, 2, $"Community cards: {communityCards}");
                }
                else
                {
                    _communityCardsWindow.MvAddStr(1, 2, "Community cards: [None]");
                }
                _communityCardsWindow.Refresh();
                
                // Update pot window
                _potWindow.Clear();
                _potWindow.Box(0, 0);
                _potWindow.MvAddStr(1, 2, $"Pot: ${gameEngine.Pot}");
                if (gameEngine.CurrentBet > 0)
                {
                    _potWindow.MvAddStr(2, 2, $"Current bet: ${gameEngine.CurrentBet}");
                }
                _potWindow.Refresh();
                
                // Update player windows
                for (int i = 0; i < gameEngine.Players.Count && i < _maxPlayers; i++)
                {
                    if (!_playerWindows.ContainsKey(i))
                        continue;
                        
                    var player = gameEngine.Players[i];
                    var window = _playerWindows[i];
                    
                    window.Clear();
                    
                    // Set color based on player status
                    if (player.HasFolded)
                        window.AttrOn(Curses.COLOR_PAIR(6));
                    else if (player == gameEngine.CurrentPlayer)
                        window.AttrOn(Curses.COLOR_PAIR(5));
                    
                    window.Box(0, 0);
                    
                    // Reset attributes
                    window.AttrOff(Curses.COLOR_PAIR(5));
                    window.AttrOff(Curses.COLOR_PAIR(6));
                    
                    // Display player info
                    string status = player.HasFolded ? "FOLDED" : 
                                    player.IsAllIn ? "ALL-IN" : 
                                    player == gameEngine.CurrentPlayer ? "ACTIVE" : "";
                                    
                    window.MvAddStr(1, 2, $"{player.Name}: ${player.Chips} {status}");
                    
                    // Show cards for current player or card backs for others
                    if (player.HoleCards.Count > 0 && !player.HasFolded && player == gameEngine.CurrentPlayer)
                    {
                        string cards = FormatCards(player.HoleCards);
                        window.MvAddStr(2, 2, cards);
                    }
                    else if (player.HoleCards.Count > 0 && !player.HasFolded)
                    {
                        window.MvAddStr(2, 2, "[??] [??]");
                    }
                    
                    window.Refresh();
                }
                
                Curses.Refresh();
            }
            catch (Exception ex)
            {
                // Fall back to console if NCurses fails
                System.Console.Clear();
                System.Console.WriteLine($"TEXAS HOLD'EM POKER - {gameEngine.State}");
                System.Console.WriteLine($"Pot: ${gameEngine.Pot}  Current bet: ${gameEngine.CurrentBet}");
                
                if (gameEngine.CommunityCards.Count > 0)
                {
                    string communityCards = FormatCards(new List<Card>(gameEngine.CommunityCards));
                    System.Console.WriteLine($"Community cards: {communityCards}");
                }
                else
                {
                    System.Console.WriteLine("Community cards: [None]");
                }
                
                foreach (var player in gameEngine.Players)
                {
                    string status = player.HasFolded ? "FOLDED" : 
                                    player.IsAllIn ? "ALL-IN" : 
                                    player == gameEngine.CurrentPlayer ? ">> ACTIVE <<" : "";
                                    
                    System.Console.WriteLine($"{player.Name}: ${player.Chips} {status}");
                    
                    if (player == gameEngine.CurrentPlayer)
                    {
                        string cards = FormatCards(player.HoleCards);
                        System.Console.WriteLine($"  Your cards: {cards}");
                    }
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
        
        public void Dispose()
        {
            try
            {
                // Clean up NCurses windows
                _playerWindows?.Values.ToList().ForEach(w => w?.Dispose());
                _actionWindow?.Dispose();
                _potWindow?.Dispose();
                _communityCardsWindow?.Dispose();
                _statusWindow?.Dispose();
                _mainWindow?.Dispose();
                
                // End NCurses mode
                EndCurses();
                
                // Clean up other resources
                if (_cancelSource != null)
                {
                    _cancelSource.Cancel();
                    _cancelSource.Dispose();
                    _cancelSource = null;
                }
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }
}