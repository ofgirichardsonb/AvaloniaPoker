// test_table_limits.cs - Test program for verifying table limits in poker game
using System;
using System.Collections.Generic;
using System.Linq;
using PokerGame.Core.Game;
using PokerGame.Core.Models;
using PokerGame.Core.Interfaces;

namespace TableLimitsTest
{
    /// <summary>
    /// Simple UI implementation for testing
    /// </summary>
    public class TestUI : IPokerGameUI
    {
        public void ShowMessage(string message)
        {
            Console.WriteLine("UI Message: " + message);
        }

        public void UpdateGameState(PokerGameEngine gameEngine)
        {
            Console.WriteLine($"Game State: {gameEngine.State}, Pot: {gameEngine.Pot}, Current Bet: {gameEngine.CurrentBet}");
            
            // Display players
            foreach (var player in gameEngine.Players)
            {
                Console.WriteLine($"  Player: {player.Name}, Chips: {player.Chips}, Current Bet: {player.CurrentBet}, Active: {player.IsActive}");
            }
        }

        public void GetPlayerAction(Player player, PokerGameEngine gameEngine)
        {
            // Automatically fold for simplicity in testing
            gameEngine.ProcessPlayerAction("fold");
        }
    }

    /// <summary>
    /// Main program to test table limits
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("POKER GAME TABLE LIMITS TEST");
            Console.WriteLine("============================");
            
            // Create test UI and game engine
            var ui = new TestUI();
            var gameEngine = new PokerGameEngine(ui);
            
            Console.WriteLine($"Table Limits Configuration:");
            Console.WriteLine($" - Maximum bet per round: ${gameEngine.MaxBet}");
            Console.WriteLine($" - Maximum player count: {gameEngine.MaxPlayers} players");
            Console.WriteLine($" - Maximum chips per player: ${gameEngine.MaxTableLimit}");
            Console.WriteLine();
            
            // Test 1: Try to start a game with too many players
            Console.WriteLine("TEST 1: Starting game with 10 players (exceeds maximum)");
            var playerNames = new string[10];
            for (int i = 0; i < 10; i++)
            {
                playerNames[i] = $"Player {i+1}";
            }
            gameEngine.StartGame(playerNames);
            
            Console.WriteLine($"Game started with {gameEngine.Players.Count} players (should be {gameEngine.MaxPlayers} or fewer)");
            Console.WriteLine();
            
            // Test 2: Try to start with excessive starting chips
            Console.WriteLine("TEST 2: Starting game with excessive chips ($2000 per player)");
            gameEngine.StartGame(new[] { "Player A", "Player B" }, 2000);
            
            Console.WriteLine($"First player's chips: ${gameEngine.Players[0].Chips} (should be {gameEngine.MaxTableLimit} or less)");
            Console.WriteLine();
            
            // Test 3: Test bet limits in the game
            Console.WriteLine("TEST 3: Testing bet limits by attempting to raise to $500");
            
            // Create a specialized game engine instance that overrides GetPlayerAction
            // to test the raise limits
            var betTestUI = new BetTestUI();
            var betTestEngine = new PokerGameEngine(betTestUI);
            
            // Start a simple game
            betTestEngine.StartGame(new[] { "High Roller", "Opponent" });
            
            // Start a hand to initiate betting
            betTestEngine.StartHand();
            
            Console.WriteLine();
            Console.WriteLine("TABLE LIMITS TEST COMPLETE");
        }
    }
    
    /// <summary>
    /// Specialized UI for testing bet limits
    /// </summary>
    public class BetTestUI : IPokerGameUI
    {
        private bool _raiseTested = false;
        
        public void ShowMessage(string message)
        {
            Console.WriteLine("UI Message: " + message);
        }

        public void UpdateGameState(PokerGameEngine gameEngine)
        {
            Console.WriteLine($"Game State: {gameEngine.State}, Pot: {gameEngine.Pot}, Current Bet: {gameEngine.CurrentBet}");
            
            // Display players
            foreach (var player in gameEngine.Players)
            {
                Console.WriteLine($"  Player: {player.Name}, Chips: {player.Chips}, Current Bet: {player.CurrentBet}, Active: {player.IsActive}");
            }
        }

        public void GetPlayerAction(Player player, PokerGameEngine gameEngine)
        {
            if (!_raiseTested)
            {
                _raiseTested = true;
                Console.WriteLine($"Player {player.Name} is attempting to raise to $500 (well above maximum bet of ${gameEngine.MaxBet})");
                gameEngine.ProcessPlayerAction("raise", 500);
            }
            else
            {
                // After testing the raise, just fold to simplify
                gameEngine.ProcessPlayerAction("fold");
            }
        }
    }
}