using System;
using System.Collections.Generic;

namespace PokerGame.Core.Models
{
    /// <summary>
    /// Payload for service registration messages
    /// </summary>
    public class ServiceRegistrationPayload
    {
        /// <summary>
        /// Gets or sets the service ID
        /// </summary>
        public string ServiceId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the service name
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the service type
        /// </summary>
        public string ServiceType { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Payload for player action messages
    /// </summary>
    public class PlayerActionPayload
    {
        /// <summary>
        /// Gets or sets the player ID
        /// </summary>
        public string PlayerId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the action type
        /// </summary>
        public string ActionType { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the bet amount
        /// </summary>
        public int BetAmount { get; set; }
    }
    
    /// <summary>
    /// Payload for game state messages
    /// </summary>
    public class GameStatePayload
    {
        /// <summary>
        /// Gets or sets the current game state
        /// </summary>
        public GameState CurrentState { get; set; }
        
        /// <summary>
        /// Gets or sets the community cards
        /// </summary>
        public List<Card> CommunityCards { get; set; } = new List<Card>();
        
        /// <summary>
        /// Gets or sets the pot
        /// </summary>
        public int Pot { get; set; }
        
        /// <summary>
        /// Gets or sets the current bet
        /// </summary>
        public int CurrentBet { get; set; }
        
        /// <summary>
        /// Gets or sets the players
        /// </summary>
        public List<PlayerInfo> Players { get; set; } = new List<PlayerInfo>();
        
        /// <summary>
        /// Gets or sets the winner IDs
        /// </summary>
        public List<string> WinnerIds { get; set; } = new List<string>();
    }
}