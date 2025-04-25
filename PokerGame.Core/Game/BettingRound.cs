namespace PokerGame.Core.Game
{
    /// <summary>
    /// Represents the different betting rounds in a poker game
    /// </summary>
    public enum BettingRound
    {
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
        River
    }
}
