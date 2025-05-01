using System;
using System.Collections.Generic;

namespace PokerGame.Abstractions.Models
{
    /// <summary>
    /// Represents a player in the poker game
    /// </summary>
    public class Player
    {
        /// <summary>
        /// Gets or sets the player's unique identifier
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the player's display name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the player's chip balance
        /// </summary>
        public decimal Balance { get; set; }
        
        /// <summary>
        /// Gets or sets the player's current bet in this round
        /// </summary>
        public decimal CurrentBet { get; set; }
        
        /// <summary>
        /// Gets or sets whether the player has folded in this hand
        /// </summary>
        public bool HasFolded { get; set; }
        
        /// <summary>
        /// Gets or sets whether the player is all in for this hand
        /// </summary>
        public bool IsAllIn { get; set; }
        
        /// <summary>
        /// Gets or sets whether the player is the dealer
        /// </summary>
        public bool IsDealer { get; set; }
        
        /// <summary>
        /// Gets or sets whether the player is active in the current game session
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Gets or sets whether this player is the current user
        /// </summary>
        public bool IsCurrentUser { get; set; }
    }
}