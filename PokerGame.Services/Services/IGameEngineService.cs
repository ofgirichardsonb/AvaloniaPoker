using System.Threading.Tasks;
using PokerGame.Core.Messages;
using PokerGame.Core.Models;

namespace PokerGame.Services
{
    /// <summary>
    /// Defines the interface for the game engine service
    /// </summary>
    public interface IGameEngineService
    {
        /// <summary>
        /// Gets the service ID
        /// </summary>
        string ServiceId { get; }
        
        /// <summary>
        /// Gets the service name
        /// </summary>
        string ServiceName { get; }
        
        /// <summary>
        /// Gets the service type
        /// </summary>
        string ServiceType { get; }
        
        /// <summary>
        /// Gets a value indicating whether the service is running
        /// </summary>
        bool IsRunning { get; }
        
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
        /// Adds a player to the game
        /// </summary>
        /// <param name="player">The player to add</param>
        void AddPlayer(Player player);
        
        /// <summary>
        /// Removes a player from the game
        /// </summary>
        /// <param name="playerId">The ID of the player to remove</param>
        void RemovePlayer(string playerId);
        
        /// <summary>
        /// Handles messages received from other services
        /// </summary>
        /// <param name="message">The message to handle</param>
        Task HandleMessageAsync(Message message);
        
        /// <summary>
        /// Broadcasts the current game state
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