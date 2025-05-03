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
        /// Determines if the current betting round is complete
        /// </summary>
        /// <param name="engine">The poker game engine instance</param>
        /// <returns>True if the betting round is complete, false otherwise</returns>
        public static bool IsBettingRoundComplete(this PokerGameEngine engine)
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
                return true;
            }
            
            // Count players still able to act (not all-in and with chips)
            var playersToAct = activePlayers.Where(p => !p.IsAllIn && p.Chips > 0).ToList();
            
            if (playersToAct.Count == 0)
            {
                // All players are either folded or all-in
                Console.WriteLine("★★★★★ Betting round complete: All remaining players are all-in ★★★★★");
                return true;
            }
            
            // Check if all active players have bet the same amount
            bool allBetsEqual = playersToAct.All(p => p.CurrentBet == engine.CurrentBet);
            
            // In pre-flop, we need to make sure everyone had a chance to act
            // In other rounds, just check if all bets are equal
            bool bettingComplete;
            
            if (engine.State == GameState.PreFlop)
            {
                // The player index tracking happens in the BettingRound class which we don't have access to here,
                // so we'll simplify by just checking if all bets are equal and all players have acted at least once
                bool allPlayersHaveActed = playersToAct.All(p => p.HasActed);
                bettingComplete = allBetsEqual && allPlayersHaveActed;
                
                Console.WriteLine($"★★★★★ PreFlop betting complete check: allBetsEqual={allBetsEqual}, allPlayersHaveActed={allPlayersHaveActed}, result={bettingComplete} ★★★★★");
            }
            else
            {
                // For other rounds, we only need all bets to be equal
                bettingComplete = allBetsEqual;
                
                Console.WriteLine($"★★★★★ Betting complete check for {engine.State}: allBetsEqual={allBetsEqual}, result={bettingComplete} ★★★★★");
            }
            
            return bettingComplete;
        }
    }
}