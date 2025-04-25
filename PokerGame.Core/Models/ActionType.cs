namespace PokerGame.Core.Models
{
    /// <summary>
    /// Enum representing the types of actions a player can take in a poker game
    /// </summary>
    public enum ActionType
    {
        /// <summary>
        /// Player folds their hand, forfeiting any chips they've put in the pot
        /// </summary>
        Fold,
        
        /// <summary>
        /// Player checks, passing the action to the next player without betting
        /// (only allowed if no one has bet in the current round)
        /// </summary>
        Check,
        
        /// <summary>
        /// Player calls, matching the current bet
        /// </summary>
        Call,
        
        /// <summary>
        /// Player bets, placing a new bet (when no one has bet yet)
        /// </summary>
        Bet,
        
        /// <summary>
        /// Player raises, increasing the current bet amount
        /// </summary>
        Raise
    }
}