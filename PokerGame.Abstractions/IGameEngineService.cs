using System;
using System.Threading.Tasks;

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
        /// Starts a new hand
        /// </summary>
        Task StartHandAsync();
        
        /// <summary>
        /// Processes a player action
        /// </summary>
        /// <param name="playerId">The ID of the player</param>
        /// <param name="action">The action to process</param>
        /// <param name="amount">The amount of the action</param>
        /// <returns>True if the action was processed successfully; otherwise, false</returns>
        Task<bool> ProcessPlayerActionAsync(string playerId, string action, int amount);
        
        /// <summary>
        /// Handles a message
        /// </summary>
        /// <param name="message">The message to handle</param>
        Task HandleMessageAsync(object message);
        
        /// <summary>
        /// Broadcasts the current game state to all connected clients
        /// </summary>
        void BroadcastGameState();
        
        /// <summary>
        /// Starts the service
        /// </summary>
        Task StartAsync();
        
        /// <summary>
        /// Stops the service
        /// </summary>
        Task StopAsync();
    }
}