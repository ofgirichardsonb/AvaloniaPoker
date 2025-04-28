using System;
using System.Collections.Generic;
using System.Linq;
using PokerGame.Core.Game;
using PokerGame.Core.Interfaces;
using PokerGame.Core.Models;
using GameStateEnum = PokerGame.Core.Game.GameState;

namespace PokerGame.Console
{
    /// <summary>
    /// Console-based UI implementation for the poker game
    /// </summary>
    public class ConsoleUI : IPokerGameUI
    {
        private PokerGameEngine? _gameEngine;
        
        /// <summary>
        /// Sets the game engine for this UI
        /// </summary>
        /// <param name="gameEngine">The game engine to use</param>
        public void SetGameEngine(PokerGameEngine gameEngine)
        {
            _gameEngine = gameEngine;
        }
        
        /// <summary>
        /// Starts the game by setting up players and beginning the main loop
        /// </summary>
        public void StartGame()
        {
            if (_gameEngine == null)
                throw new InvalidOperationException("Game engine not set");
                
            // Display welcome message
            System.Console.Clear();
            System.Console.WriteLine("===============================================");
            System.Console.WriteLine("           TEXAS HOLD'EM POKER GAME           ");
            System.Console.WriteLine("===============================================");
            System.Console.WriteLine();
            
            // Get number of players
            int numPlayers = GetNumberInRange("Enter number of players (2-8): ", 2, 8);
            
            // Get player names
            string[] playerNames = new string[numPlayers];
            for (int i = 0; i < numPlayers; i++)
            {
                string defaultName = $"Player {i+1}";
                System.Console.Write($"Enter name for player {i+1} (or press Enter for '{defaultName}'): ");
                string? name = System.Console.ReadLine();
                playerNames[i] = string.IsNullOrWhiteSpace(name) ? defaultName : name;
            }
            
            // Start the game with the provided player names
            _gameEngine.StartGame(playerNames);
            
            // Main game loop
            bool exit = false;
            while (!exit)
            {
                switch (_gameEngine.State)
                {
                    case GameStateEnum.WaitingToStart:
                    case GameStateEnum.HandComplete:
                        // Ask to start a new hand or exit
                        System.Console.WriteLine();
                        System.Console.WriteLine("Press Enter to start a new hand or 'Q' to quit.");
                        string? input = System.Console.ReadLine();
                        
                        if (input?.ToUpper() == "Q")
                            exit = true;
                        else
                            _gameEngine.StartHand();
                        break;
                    
                    // Other states are handled by the game engine and callbacks
                    default:
                        System.Threading.Thread.Sleep(100); // Small pause to prevent CPU overuse
                        break;
                }
            }
            
            // Game ended
            System.Console.WriteLine("Thanks for playing!");
        }
        
        /// <summary>
        /// Shows a message to the user
        /// </summary>
        /// <param name="message">The message to display</param>
        public void ShowMessage(string message)
        {
            System.Console.WriteLine(message);
        }
        
        /// <summary>
        /// Gets an action from the current player
        /// </summary>
        /// <param name="player">The player to get the action from</param>
        /// <param name="gameEngine">The current game engine instance</param>
        public void GetPlayerAction(Player player, PokerGameEngine gameEngine)
        {
            System.Console.WriteLine();
            System.Console.WriteLine($"=== {player.Name}'s turn ===");
            
            // Show player's hole cards
            System.Console.WriteLine($"Your hole cards: {CardListToString(player.HoleCards)}");
            
            // Show available actions
            System.Console.WriteLine("Available actions:");
            
            bool canCheck = player.CurrentBet == gameEngine.CurrentBet;
            int callAmount = gameEngine.CurrentBet - player.CurrentBet;
            
            if (canCheck)
                System.Console.WriteLine("- Check (C)");
            else
                System.Console.WriteLine($"- Call {callAmount} (C)");
                
            System.Console.WriteLine("- Fold (F)");
            System.Console.WriteLine($"- Raise (R) (Minimum raise: {gameEngine.CurrentBet + 10})");
            
            // Get player action
            bool validAction = false;
            while (!validAction)
            {
                System.Console.Write("Enter your action: ");
                string? actionInput = System.Console.ReadLine()?.ToUpper();
                
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
                        int raiseAmount = GetNumberInRange($"Enter raise amount (min {minRaise}): ", minRaise, player.Chips + player.CurrentBet);
                        gameEngine.ProcessPlayerAction("raise", raiseAmount);
                        validAction = true;
                        break;
                        
                    default:
                        System.Console.WriteLine("Invalid action. Try again.");
                        break;
                }
            }
        }
        
        /// <summary>
        /// Updates the UI with the current game state
        /// </summary>
        /// <param name="gameEngine">The current game engine instance</param>
        public void UpdateGameState(PokerGameEngine gameEngine)
        {
            if (gameEngine.State == GameStateEnum.HandComplete)
                return;
                
            System.Console.WriteLine();
            System.Console.WriteLine("===============================================");
            System.Console.WriteLine($"GAME STATE: {gameEngine.State}");
            
            // Show community cards if any
            if (gameEngine.CommunityCards.Count > 0)
            {
                System.Console.WriteLine($"Community cards: {CardListToString(gameEngine.CommunityCards.ToList())}");
            }
            
            // Show pot and current bet
            System.Console.WriteLine($"Pot: {gameEngine.Pot}   Current bet: {gameEngine.CurrentBet}");
            System.Console.WriteLine();
            
            // Show player information
            System.Console.WriteLine("PLAYERS:");
            foreach (var player in gameEngine.Players)
            {
                string status = player.HasFolded ? "Folded" : player.IsAllIn ? "All-In" : "Active";
                string currentBet = player.CurrentBet > 0 ? $" (Bet: {player.CurrentBet})" : "";
                System.Console.WriteLine($"- {player.Name}: {player.Chips} chips, {status}{currentBet}");
            }
            
            System.Console.WriteLine("===============================================");
        }
        
        /// <summary>
        /// Helper method to get a number within a specified range
        /// </summary>
        private int GetNumberInRange(string prompt, int min, int max)
        {
            int number;
            bool valid = false;
            
            do
            {
                System.Console.Write(prompt);
                string? input = System.Console.ReadLine();
                
                if (int.TryParse(input, out number) && number >= min && number <= max)
                {
                    valid = true;
                }
                else
                {
                    System.Console.WriteLine($"Please enter a number between {min} and {max}.");
                }
            } while (!valid);
            
            return number;
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
