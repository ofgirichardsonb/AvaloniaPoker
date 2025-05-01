using System;
using System.Collections.Generic;

namespace PokerGame.Abstractions.Models
{
    /// <summary>
    /// Represents the status of a game session
    /// </summary>
    public enum GameSessionStatus
    {
        /// <summary>
        /// The game session is waiting for players to join
        /// </summary>
        Waiting,
        
        /// <summary>
        /// The game is in progress
        /// </summary>
        InProgress,
        
        /// <summary>
        /// The game is paused
        /// </summary>
        Paused,
        
        /// <summary>
        /// The game has completed
        /// </summary>
        Completed
    }
    
    /// <summary>
    /// Represents a game session that players can join
    /// </summary>
    public class GameSession
    {
        /// <summary>
        /// Gets or sets the unique ID of the game session
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Gets or sets the name of the game session
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the status of the game session
        /// </summary>
        public GameSessionStatus Status { get; set; } = GameSessionStatus.Waiting;
        
        /// <summary>
        /// Gets or sets the list of players in the game session
        /// </summary>
        public List<Player> Players { get; set; } = new List<Player>();
        
        /// <summary>
        /// Gets or sets the creation time of the game session
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets or sets the small blind amount
        /// </summary>
        public decimal SmallBlind { get; set; } = 5;
        
        /// <summary>
        /// Gets or sets the big blind amount
        /// </summary>
        public decimal BigBlind { get; set; } = 10;
        
        /// <summary>
        /// Gets or sets the minimum chips required to join
        /// </summary>
        public decimal MinBuyIn { get; set; } = 200;
        
        /// <summary>
        /// Gets or sets the maximum number of players allowed
        /// </summary>
        public int MaxPlayers { get; set; } = 9;
    }
}