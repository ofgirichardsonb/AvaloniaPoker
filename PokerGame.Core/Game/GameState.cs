namespace PokerGame.Core.Game
{
    /// <summary>
    /// Represents the different states of a poker game
    /// </summary>
    public enum GameState
    {
        /// <summary>
        /// The game has not started yet
        /// </summary>
        NotStarted,
        
        /// <summary>
        /// The game is being set up
        /// </summary>
        Setup,
        
        /// <summary>
        /// The game is waiting to start a new hand
        /// </summary>
        WaitingToStart,
        
        /// <summary>
        /// The pre-flop betting round (after hole cards are dealt)
        /// </summary>
        PreFlop,
        
        /// <summary>
        /// The flop betting round (after 3 community cards are dealt)
        /// </summary>
        Flop,
        
        /// <summary>
        /// The turn betting round (after 4th community card is dealt)
        /// </summary>
        Turn,
        
        /// <summary>
        /// The river betting round (after 5th community card is dealt)
        /// </summary>
        River,
        
        /// <summary>
        /// The showdown phase where players reveal their hands
        /// </summary>
        Showdown,
        
        /// <summary>
        /// The hand is complete
        /// </summary>
        HandComplete,
        
        /// <summary>
        /// The game is complete
        /// </summary>
        Complete
    }
}
