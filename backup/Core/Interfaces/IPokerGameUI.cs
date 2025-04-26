using PokerGame.Core.Models;
using PokerGame.Core.Game;

namespace PokerGame.Core.Interfaces
{
    /// <summary>
    /// Interface for UI implementations to interact with the poker game engine
    /// </summary>
    public interface IPokerGameUI
    {
        /// <summary>
        /// Shows a message to the user
        /// </summary>
        /// <param name="message">The message to display</param>
        void ShowMessage(string message);
        
        /// <summary>
        /// Gets an action from the current player
        /// </summary>
        /// <param name="player">The player to get the action from</param>
        /// <param name="gameEngine">The current game engine instance</param>
        void GetPlayerAction(Player player, PokerGameEngine gameEngine);
        
        /// <summary>
        /// Updates the UI with the current game state
        /// </summary>
        /// <param name="gameEngine">The current game engine instance</param>
        void UpdateGameState(PokerGameEngine gameEngine);
    }
}
