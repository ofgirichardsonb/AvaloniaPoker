using System;

namespace PokerGame.Core.Models
{
    /// <summary>
    /// Represents an action taken by a player during a poker game
    /// </summary>
    public class PlayerAction
    {
        /// <summary>
        /// The type of action (Fold, Check, Call, Bet, Raise)
        /// </summary>
        public ActionType ActionType { get; set; }
        
        /// <summary>
        /// The amount associated with the action (for Bet and Raise)
        /// </summary>
        public int Amount { get; set; }
        
        /// <summary>
        /// Creates a new PlayerAction instance
        /// </summary>
        public PlayerAction()
        {
            // Default constructor
        }
        
        /// <summary>
        /// Creates a new PlayerAction with the specified action type
        /// </summary>
        /// <param name="actionType">The type of action</param>
        public PlayerAction(ActionType actionType)
        {
            ActionType = actionType;
        }
        
        /// <summary>
        /// Creates a new PlayerAction with the specified action type and amount
        /// </summary>
        /// <param name="actionType">The type of action</param>
        /// <param name="amount">The amount associated with the action</param>
        public PlayerAction(ActionType actionType, int amount)
        {
            ActionType = actionType;
            Amount = amount;
        }
        
        /// <summary>
        /// Returns a string that represents the current action
        /// </summary>
        /// <returns>A string representation of the action</returns>
        public override string ToString()
        {
            switch (ActionType)
            {
                case ActionType.Fold:
                    return "Fold";
                case ActionType.Check:
                    return "Check";
                case ActionType.Call:
                    return $"Call (${Amount})";
                case ActionType.Bet:
                    return $"Bet (${Amount})";
                case ActionType.Raise:
                    return $"Raise (${Amount})";
                default:
                    return "Unknown action";
            }
        }
    }
}