using System;
using System.Collections.Generic;
using System.Linq;
using MSA.Foundation.Telemetry;
using PokerGame.Core.Models;

namespace PokerGame.Core.Game
{
    /// <summary>
    /// Provides specialized telemetry for poker betting rounds
    /// </summary>
    public static class BettingRoundTelemetry
    {
        private static readonly TelemetryService _telemetry = TelemetryService.Instance;
        
        /// <summary>
        /// Creates an annotation in Application Insights for significant game events
        /// </summary>
        /// <param name="engine">The poker game engine instance</param>
        /// <param name="title">The annotation title</param>
        /// <param name="description">Detailed description of the annotation</param>
        /// <param name="category">The category of the annotation (e.g., Bug, Feature, UI Issue)</param>
        public static void CreateAnnotation(PokerGameEngine engine, string title, string description, string category)
        {
            if (engine == null) return;
            
            try
            {
                if (!_telemetry.Initialize(Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY") ?? ""))
                {
                    Console.WriteLine("Failed to initialize telemetry for annotation");
                    return;
                }
                
                var properties = new Dictionary<string, string>
                {
                    { "Type", "Annotation" },
                    { "Title", title },
                    { "Description", description },
                    { "Category", category },
                    { "GameState", engine.State.ToString() },
                    { "Timestamp", DateTime.UtcNow.ToString("o") },
                    { "ActivePlayers", engine.Players.Count(p => !p.HasFolded).ToString() },
                    { "CurrentBet", engine.CurrentBet.ToString() },
                    { "TotalPot", engine.Pot.ToString() }
                };
                
                // Add details about each player's state
                int playerIndex = 0;
                foreach (var player in engine.Players)
                {
                    properties[$"Player{playerIndex}.Name"] = player.Name;
                    properties[$"Player{playerIndex}.Chips"] = player.Chips.ToString();
                    properties[$"Player{playerIndex}.CurrentBet"] = player.CurrentBet.ToString();
                    properties[$"Player{playerIndex}.HasActed"] = player.HasActed.ToString();
                    properties[$"Player{playerIndex}.IsAllIn"] = player.IsAllIn.ToString();
                    properties[$"Player{playerIndex}.HasFolded"] = player.HasFolded.ToString();
                    playerIndex++;
                }
                
                // Create a specialized event type for annotations to make them easier to find
                _telemetry.TrackEvent("GameAnnotation", properties);
                _telemetry.Flush(); // Force sending the telemetry immediately
                
                Console.WriteLine($"✓ Created annotation: {title} - {category}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating annotation: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tracks a betting round check for completion
        /// </summary>
        /// <param name="engine">The poker game engine instance</param>
        /// <param name="isComplete">Whether the betting round is complete</param>
        /// <param name="reason">The reason for the completion status</param>
        public static void TrackBettingRoundCheck(PokerGameEngine engine, bool isComplete, string reason)
        {
            if (engine == null) return;
            
            try
            {
                if (!_telemetry.Initialize(Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY") ?? ""))
                {
                    // Don't log the telemetry initialization failure here as it would be repetitive
                    return;
                }
                
                // Get all active players (not folded)
                var activePlayers = engine.Players.Where(p => !p.HasFolded).ToList();
                
                // Count players still able to act (not all-in and with chips)
                var playersToAct = activePlayers.Where(p => !p.IsAllIn && p.Chips > 0).ToList();
                
                // Tracking detailed round check
                var properties = new Dictionary<string, string>
                {
                    { "GameState", engine.State.ToString() },
                    { "IsComplete", isComplete.ToString() },
                    { "Reason", reason },
                    { "ActivePlayers", activePlayers.Count.ToString() },
                    { "PlayersToAct", playersToAct.Count.ToString() },
                    { "CurrentBet", engine.CurrentBet.ToString() },
                    { "AllBetsEqual", playersToAct.All(p => p.CurrentBet == engine.CurrentBet).ToString() },
                    { "AllPlayersHaveActed", playersToAct.All(p => p.HasActed).ToString() }
                };
                
                // Add player details
                int playerIndex = 0;
                foreach (var player in activePlayers)
                {
                    properties[$"Player{playerIndex}.Name"] = player.Name;
                    properties[$"Player{playerIndex}.IsAllIn"] = player.IsAllIn.ToString();
                    properties[$"Player{playerIndex}.HasActed"] = player.HasActed.ToString();
                    properties[$"Player{playerIndex}.Chips"] = player.Chips.ToString();
                    properties[$"Player{playerIndex}.CurrentBet"] = player.CurrentBet.ToString();
                    playerIndex++;
                }
                
                _telemetry.TrackEvent("BettingRoundCheck", properties);
                _telemetry.Flush(); // Force sending the telemetry immediately
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking betting round check: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tracks a state transition in the poker game
        /// </summary>
        /// <param name="engine">The poker game engine instance</param>
        /// <param name="fromState">The previous state</param>
        /// <param name="toState">The new state</param>
        public static void TrackStateTransition(PokerGameEngine engine, GameState fromState, GameState toState)
        {
            if (engine == null) return;
            
            try
            {
                if (!_telemetry.Initialize(Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY") ?? ""))
                {
                    return;
                }
                
                var properties = new Dictionary<string, string>
                {
                    { "FromState", fromState.ToString() },
                    { "ToState", toState.ToString() },
                    { "ActivePlayers", engine.Players.Count(p => !p.HasFolded).ToString() },
                    { "TotalPot", engine.Pot.ToString() },
                    { "CurrentBet", engine.CurrentBet.ToString() }
                };
                
                _telemetry.TrackEvent("GameStateTransition", properties);
                _telemetry.Flush(); // Force sending the telemetry immediately
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking state transition: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tracks a player action in the poker game
        /// </summary>
        /// <param name="engine">The poker game engine instance</param>
        /// <param name="player">The player making the action</param>
        /// <param name="action">The action type</param>
        /// <param name="amount">The amount associated with the action (for bet/raise)</param>
        public static void TrackPlayerAction(PokerGameEngine engine, Player player, ActionType action, int amount = 0)
        {
            if (engine == null || player == null) return;
            
            try
            {
                if (!_telemetry.Initialize(Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY") ?? ""))
                {
                    return;
                }
                
                var properties = new Dictionary<string, string>
                {
                    { "GameState", engine.State.ToString() },
                    { "PlayerName", player.Name },
                    { "ActionType", action.ToString() },
                    { "Amount", amount.ToString() },
                    { "PlayerChips", player.Chips.ToString() },
                    { "PlayerBet", player.CurrentBet.ToString() },
                    { "HasActed", player.HasActed.ToString() },
                    { "IsAllIn", player.IsAllIn.ToString() },
                    { "CurrentBet", engine.CurrentBet.ToString() },
                    { "TotalPot", engine.Pot.ToString() }
                };
                
                _telemetry.TrackEvent("PlayerAction", properties);
                _telemetry.Flush(); // Force sending the telemetry immediately
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking player action: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tracks a reset of player action flags
        /// </summary>
        /// <param name="engine">The poker game engine instance</param>
        /// <param name="context">The context in which the reset occurred</param>
        public static void TrackHasActedReset(PokerGameEngine engine, string context)
        {
            if (engine == null) return;
            
            try
            {
                if (!_telemetry.Initialize(Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY") ?? ""))
                {
                    return;
                }
                
                var properties = new Dictionary<string, string>
                {
                    { "GameState", engine.State.ToString() },
                    { "Context", context },
                    { "Players", string.Join(", ", engine.Players.Select(p => p.Name)) }
                };
                
                // Add player details after reset
                int playerIndex = 0;
                foreach (var player in engine.Players)
                {
                    properties[$"Player{playerIndex}.Name"] = player.Name;
                    properties[$"Player{playerIndex}.HasActed"] = player.HasActed.ToString();
                    playerIndex++;
                }
                
                _telemetry.TrackEvent("HasActedReset", properties);
                _telemetry.Flush(); // Force sending the telemetry immediately
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking HasActed reset: {ex.Message}");
            }
        }
    }
}