using System;
using PokerGame.Core.Models;
using System.Linq;

namespace PokerGame.Core.Game
{
    /// <summary>
    /// Extension methods for determining if a betting round is complete
    /// </summary>
    public static class IsBettingRoundCompleteExtension
    {
        /// <summary>
        /// Enhanced checking of betting round completion with detailed diagnostics
        /// </summary>
        /// <param name="engine">The poker game engine instance</param>
        /// <returns>True if the betting round is complete, false otherwise</returns>
        public static bool CheckBettingRoundComplete(this PokerGameEngine engine)
        {
            if (engine == null)
            {
                Console.WriteLine("★★★★★ ERROR: Cannot check betting round completion for null engine ★★★★★");
                return false;
            }
            
            // Log the current state for debugging
            Console.WriteLine($"★★★★★ Checking if betting round is complete for state: {engine.State} ★★★★★");
            
            // Get all active players (not folded)
            var activePlayers = engine.Players.Where(p => !p.HasFolded).ToList();
            
            // If only one player remains, betting is complete
            if (activePlayers.Count <= 1)
            {
                Console.WriteLine("★★★★★ Betting round complete: Only one player remains active ★★★★★");
                // Track the decision in telemetry
                BettingRoundTelemetry.TrackBettingRoundCheck(engine, true, "OnlyOnePlayerRemains");
                return true;
            }
            
            // Count players still able to act (not all-in and with chips)
            var playersToAct = activePlayers.Where(p => !p.IsAllIn && p.Chips > 0).ToList();
            
            // Log the current state of players for debugging
            Console.WriteLine($"★★★★★ Active players: {activePlayers.Count}, Players able to act: {playersToAct.Count} ★★★★★");
            foreach (var player in activePlayers)
            {
                Console.WriteLine($"★★★★★ Player {player.Name}: IsAllIn={player.IsAllIn}, HasActed={player.HasActed}, Chips={player.Chips}, CurrentBet={player.CurrentBet} ★★★★★");
            }
            
            if (playersToAct.Count == 0)
            {
                // All players are either folded or all-in
                Console.WriteLine("★★★★★ Betting round complete: All remaining players are all-in ★★★★★");
                // Track the decision in telemetry
                BettingRoundTelemetry.TrackBettingRoundCheck(engine, true, "AllPlayersAreAllIn");
                return true;
            }
            
            // Special case: If only one player can act, and that player has the biggest bet, round is complete
            if (playersToAct.Count == 1 && playersToAct[0].CurrentBet >= engine.CurrentBet)
            {
                Console.WriteLine($"★★★★★ Special case: Only one player can act ({playersToAct[0].Name}) with bet {playersToAct[0].CurrentBet} >= current bet {engine.CurrentBet} ★★★★★");
                // Track the decision in telemetry
                BettingRoundTelemetry.TrackBettingRoundCheck(engine, true, "OnePlayerCanActWithHighestBet");
                return true;
            }
            
            // Safety check for abnormal game states
            if (engine.CurrentBet < 0)
            {
                Console.WriteLine($"★★★★★ WARNING: Detected negative current bet: {engine.CurrentBet}. This shouldn't happen! ★★★★★");
                // In case of a corrupt game state, don't block the game - allow it to continue
                // Track the decision in telemetry
                BettingRoundTelemetry.TrackBettingRoundCheck(engine, true, "AbnormalGameState_NegativeCurrentBet");
                return true;
            }
            
            // Check if all active players have bet the same amount
            bool allBetsEqual = playersToAct.All(p => p.CurrentBet == engine.CurrentBet);
            
            // In pre-flop, we need to make sure everyone had a chance to act
            // In other rounds, just check if all bets are equal
            bool bettingComplete;
            string reason;
            
            if (engine.State == GameState.PreFlop)
            {
                // The player index tracking happens in the BettingRound class which we don't have access to here,
                // so we'll simplify by just checking if all bets are equal and all players have acted at least once
                bool allPlayersHaveActed = playersToAct.All(p => p.HasActed);
                bettingComplete = allBetsEqual && allPlayersHaveActed;
                
                reason = bettingComplete ? 
                    "PreFlop_AllBetsEqualAndAllPlayersActed" : 
                    (!allBetsEqual ? "PreFlop_BetsNotEqual" : "PreFlop_NotAllPlayersHaveActed");
                
                Console.WriteLine($"★★★★★ PreFlop betting complete check: allBetsEqual={allBetsEqual}, allPlayersHaveActed={allPlayersHaveActed}, result={bettingComplete} ★★★★★");
            }
            else
            {
                // For other rounds, we need all bets to be equal AND all players must have acted
                bool allPlayersHaveActed = playersToAct.All(p => p.HasActed);
                bettingComplete = allBetsEqual && allPlayersHaveActed;
                
                reason = bettingComplete ? 
                    $"{engine.State}_AllBetsEqualAndAllPlayersActed" : 
                    (!allBetsEqual ? $"{engine.State}_BetsNotEqual" : $"{engine.State}_NotAllPlayersHaveActed");
                
                Console.WriteLine($"★★★★★ Betting complete check for {engine.State}: allBetsEqual={allBetsEqual}, allPlayersHaveActed={allPlayersHaveActed}, result={bettingComplete} ★★★★★");
            }
            
            // Track the decision in telemetry
            BettingRoundTelemetry.TrackBettingRoundCheck(engine, bettingComplete, reason);
            
            return bettingComplete;
        }
    }
}