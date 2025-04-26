using System;
using System.Collections.Generic;
using System.Linq;
using PokerGame.Core.Models;

namespace PokerGame.Core.Game
{
    /// <summary>
    /// Manages a single betting round in a poker game
    /// </summary>
    public class BettingRound
    {
        private readonly List<Player> _players;
        private readonly GameState _roundState;
        private int _currentBet;
        private int _startingPlayerIndex;
        private int _currentPlayerIndex;
        private bool _isBettingComplete;
        
        /// <summary>
        /// Creates a new betting round
        /// </summary>
        /// <param name="players">The players participating in the round</param>
        /// <param name="startingPlayerIndex">The index of the player who acts first</param>
        /// <param name="roundState">The current game state (PreFlop, Flop, etc.)</param>
        /// <param name="currentBet">Any existing bet amount (e.g., the big blind)</param>
        public BettingRound(List<Player> players, int startingPlayerIndex, GameState roundState, int currentBet = 0)
        {
            _players = players;
            _startingPlayerIndex = startingPlayerIndex;
            _currentPlayerIndex = startingPlayerIndex;
            _roundState = roundState;
            _currentBet = currentBet;
            _isBettingComplete = false;
            
            // Immediately check if we have enough active players to even start
            if (GetActivePlayers().Count <= 1)
            {
                _isBettingComplete = true;
            }
        }
        
        /// <summary>
        /// Gets the player who is currently acting
        /// </summary>
        public Player CurrentPlayer => _players[_currentPlayerIndex];
        
        /// <summary>
        /// Gets the current bet amount
        /// </summary>
        public int CurrentBet => _currentBet;
        
        /// <summary>
        /// Gets whether the betting round is complete
        /// </summary>
        public bool IsBettingComplete => _isBettingComplete;
        
        /// <summary>
        /// Processes a player action
        /// </summary>
        /// <param name="action">The action to process</param>
        /// <returns>True if the action was valid and processed, false otherwise</returns>
        public bool ProcessAction(PlayerAction action)
        {
            Player player = CurrentPlayer;
            
            switch (action.ActionType)
            {
                case ActionType.Fold:
                    player.Fold();
                    break;
                    
                case ActionType.Check:
                    if (_currentBet > player.CurrentBet)
                    {
                        // Can't check if there's a bet to call
                        return false;
                    }
                    // Check is just passing, no bet change
                    break;
                    
                case ActionType.Call:
                    {
                        int callAmount = _currentBet - player.CurrentBet;
                        player.PlaceBet(callAmount);
                    }
                    break;
                    
                case ActionType.Bet:
                    if (_currentBet > 0)
                    {
                        // Can't bet if there's already a bet (would be a raise)
                        return false;
                    }
                    
                    _currentBet = player.PlaceBet(action.Amount);
                    break;
                    
                case ActionType.Raise:
                    if (_currentBet == 0)
                    {
                        // Can't raise if there's no bet (would be a bet)
                        return false;
                    }
                    
                    // Calculate total amount player needs to put in
                    int raiseAmount = action.Amount;
                    int totalAmount = _currentBet + raiseAmount;
                    
                    // This will account for any existing bet the player has made
                    int actualBetAmount = totalAmount - player.CurrentBet;
                    player.PlaceBet(actualBetAmount);
                    _currentBet = player.CurrentBet;
                    break;
                    
                default:
                    return false;
            }
            
            // Move to next player
            MoveToNextPlayer();
            
            // Check if betting round is complete
            CheckIfBettingComplete();
            
            return true;
        }
        
        /// <summary>
        /// Advances to the next active player
        /// </summary>
        private void MoveToNextPlayer()
        {
            int originalIndex = _currentPlayerIndex;
            
            do
            {
                _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
                
                // If we've gone all the way around, stop
                if (_currentPlayerIndex == originalIndex)
                    break;
                
            } while (!IsPlayerActive(_players[_currentPlayerIndex]));
        }
        
        /// <summary>
        /// Checks if a player is active in the betting round
        /// </summary>
        private bool IsPlayerActive(Player player)
        {
            return !player.HasFolded && !player.IsAllIn && player.Chips > 0;
        }
        
        /// <summary>
        /// Gets all players who are still active
        /// </summary>
        private List<Player> GetActivePlayers()
        {
            return _players.Where(p => !p.HasFolded).ToList();
        }
        
        /// <summary>
        /// Checks if the betting round is complete
        /// </summary>
        private void CheckIfBettingComplete()
        {
            var activePlayers = GetActivePlayers();
            
            // If only one player remains, betting is complete
            if (activePlayers.Count <= 1)
            {
                _isBettingComplete = true;
                return;
            }
            
            // Check if all active (not folded, not all-in) players have bet the same amount
            var playersToAct = activePlayers.Where(p => !p.IsAllIn && p.Chips > 0).ToList();
            
            if (playersToAct.Count == 0)
            {
                // All players are either folded or all-in
                _isBettingComplete = true;
                return;
            }
            
            bool allBetsEqual = playersToAct.All(p => p.CurrentBet == _currentBet);
            
            // Pre-flop is special: we need to ensure everyone has had a chance to act
            // since we start with the big blind
            if (_roundState == GameState.PreFlop)
            {
                // Everyone has acted if we've gone around the table and are back at the starting player
                // or past them, and all bets are equal
                bool fullRoundCompleted = (_currentPlayerIndex >= _startingPlayerIndex || 
                                          _currentPlayerIndex < (_startingPlayerIndex + 1) % _players.Count);
                
                _isBettingComplete = allBetsEqual && fullRoundCompleted;
            }
            else
            {
                // For other rounds, we only need all bets to be equal
                _isBettingComplete = allBetsEqual;
            }
        }
        
        /// <summary>
        /// Calculates the total pot from all bets in this round
        /// </summary>
        public int CalculatePot()
        {
            return _players.Sum(p => p.CurrentBet);
        }
        
        /// <summary>
        /// Resets all players' current bets for a new betting round
        /// </summary>
        public void ResetPlayerBets()
        {
            foreach (var player in _players)
            {
                player.ResetBetForNewRound();
            }
        }
    }
}