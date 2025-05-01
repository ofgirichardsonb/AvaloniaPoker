using System.Collections.Generic;
using PokerGame.Abstractions.Models;

namespace PokerGame.Abstractions.Messages
{
    // Base response class
    public class BaseResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        
        // Default constructor to initialize properties
        public BaseResponse()
        {
            Success = false;
            ErrorMessage = null;
        }
    }

    // Player Registration
    public class PlayerRegistrationRequest
    {
        public required string PlayerName { get; set; }
    }

    public class PlayerRegistrationResponse : BaseResponse
    {
        public required string PlayerId { get; set; }
    }

    // Game Session Management
    public class JoinGameRequest
    {
        public required string PlayerId { get; set; }
    }

    public class JoinGameResponse : BaseResponse
    {
        public List<Player> CurrentPlayers { get; set; } = new List<Player>();
    }

    public class LeaveGameRequest
    {
        public required string PlayerId { get; set; }
    }

    public class LeaveGameResponse : BaseResponse
    {
    }

    public class GetLobbyStateRequest
    {
    }

    public class LobbyStateResponse
    {
        public List<Player> CurrentPlayers { get; set; } = new List<Player>();
        public GameSessionStatus GameStatus { get; set; }
        public bool CanStart { get; set; }
    }

    public class StartGameRequest
    {
        public required string RequesterId { get; set; }
    }

    public class StartGameResponse : BaseResponse
    {
    }

    // Notifications
    public class PlayerJoinedNotification
    {
        public required Player Player { get; set; }
    }

    public class PlayerLeftNotification
    {
        public required string PlayerId { get; set; }
        public required string PlayerName { get; set; }
    }

    public class GameStartedNotification
    {
        public List<Player> Players { get; set; } = new List<Player>();
    }

    // Game Initialization
    public class InitializeGameRequest
    {
        public List<Player> Players { get; set; } = new List<Player>();
        public decimal SmallBlind { get; set; }
        public decimal BigBlind { get; set; }
    }
    
    public class GameInitializedResponse : BaseResponse
    {
    }
}