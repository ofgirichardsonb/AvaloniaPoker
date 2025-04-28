using System;
using System.Collections.Generic;

namespace PokerGame.Core.Models
{
    /// <summary>
    /// Represents player information for registration and state updates
    /// </summary>
    public class PlayerInfo
    {
        /// <summary>
        /// Gets or sets the player ID
        /// </summary>
        public string PlayerId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the player name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the player's chip count
        /// </summary>
        public int Chips { get; set; }
        
        /// <summary>
        /// Gets or sets the player's current bet
        /// </summary>
        public int CurrentBet { get; set; }
        
        /// <summary>
        /// Gets or sets whether the player has folded
        /// </summary>
        public bool HasFolded { get; set; }
        
        /// <summary>
        /// Gets or sets whether the player is all in
        /// </summary>
        public bool IsAllIn { get; set; }
        
        /// <summary>
        /// Gets or sets the player's hole cards
        /// </summary>
        public List<Card> HoleCards { get; set; } = new List<Card>();
    }
}