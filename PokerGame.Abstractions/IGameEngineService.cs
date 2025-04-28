using System.Threading.Tasks;
using MSA.Foundation.Messaging;

namespace PokerGame.Abstractions
{
    /// <summary>
    /// Interface for the game engine service
    /// </summary>
    public interface IGameEngineService
    {
        /// <summary>
        /// Gets the unique identifier for this service instance
        /// </summary>
        string ServiceId { get; }
        
        /// <summary>
        /// Gets the human-readable name of this service
        /// </summary>
        string ServiceName { get; }
        
        /// <summary>
        /// Gets the type of this service (e.g., "GameEngine", "CardDeck", etc.)
        /// </summary>
        string ServiceType { get; }
        
        /// <summary>
        /// Gets a value indicating whether the service is currently running
        /// </summary>
        bool IsRunning { get; }
        
        /// <summary>
        /// Adds a player to the game
        /// </summary>
        /// <param name="player">The player to add</param>
        void AddPlayer(object player);
        
        /// <summary>
        /// Removes a player from the game
        /// </summary>
        /// <param name="playerId">The ID of the player to remove</param>
        void RemovePlayer(string playerId);
        
        /// <summary>
        /// Processes a player action
        /// </summary>
        /// <param name="playerId">The ID of the player taking the action</param>
        /// <param name="action">The action to take</param>
        /// <param name="amount">The amount of the action (if applicable)</param>
        /// <returns>A task that completes when the action is processed</returns>
        Task<bool> ProcessPlayerActionAsync(string playerId, string action, int amount);
        
        /// <summary>
        /// Starts a new hand
        /// </summary>
        /// <returns>A task that completes when the hand is started</returns>
        Task StartHandAsync();
        
        /// <summary>
        /// Starts the service
        /// </summary>
        /// <returns>A task that completes when the service is started</returns>
        Task StartAsync();
        
        /// <summary>
        /// Stops the service
        /// </summary>
        /// <returns>A task that completes when the service is stopped</returns>
        Task StopAsync();
        
        /// <summary>
        /// Broadcasts the current game state to all clients
        /// </summary>
        void BroadcastGameState();
        
        /// <summary>
        /// Handles a message received from another service
        /// </summary>
        /// <param name="message">The message to handle</param>
        /// <returns>A task that completes when the message is handled</returns>
        Task HandleMessageAsync(Message message);
    }
}