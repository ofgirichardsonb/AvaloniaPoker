namespace PokerGame.Core.Models
{
    /// <summary>
    /// Represents the current state of the poker game
    /// </summary>
    public enum GameState
    {
        /// <summary>
        /// Game has not started yet
        /// </summary>
        NotStarted,
        
        /// <summary>
        /// Waiting for players to join
        /// </summary>
        WaitingForPlayers,
        
        /// <summary>
        /// Preflop betting round (after hole cards are dealt)
        /// </summary>
        Preflop,
        
        /// <summary>
        /// Flop betting round (after 3 community cards are dealt)
        /// </summary>
        Flop,
        
        /// <summary>
        /// Turn betting round (after 4th community card is dealt)
        /// </summary>
        Turn,
        
        /// <summary>
        /// River betting round (after 5th community card is dealt)
        /// </summary>
        River,
        
        /// <summary>
        /// Showdown (determining winner)
        /// </summary>
        Showdown,
        
        /// <summary>
        /// Hand is complete
        /// </summary>
        HandComplete,
        
        /// <summary>
        /// Game is complete
        /// </summary>
        GameOver
    }
}