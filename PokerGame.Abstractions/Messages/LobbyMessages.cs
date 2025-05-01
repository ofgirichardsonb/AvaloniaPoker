using System.Collections.Generic;
using PokerGame.Abstractions.Models;

namespace PokerGame.Abstractions.Messages
{
    // Base response class
    public class BaseResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    // Player Registration
    public class PlayerRegistrationRequest
    {
        public string PlayerName { get; set; }
    }

    public class PlayerRegistrationResponse : BaseResponse
    {
        public string PlayerId { get; set; }
    }

    // Game Session Management
    public class JoinGameRequest
    {
        public string PlayerId { get; set; }
    }

    public class JoinGameResponse : BaseResponse
    {
        public List<Player> CurrentPlayers { get; set; }
    }

    public class LeaveGameRequest
    {
        public string PlayerId { get; set; }
    }

    public class LeaveGameResponse : BaseResponse
    {
    }

    public class GetLobbyStateRequest
    {
    }

    public class LobbyStateResponse
    {
        public List<Player> CurrentPlayers { get; set; }
        public GameSessionStatus GameStatus { get; set; }
        public bool CanStart { get; set; }
    }

    public class StartGameRequest
    {
        public string RequesterId { get; set; }
    }

    public class StartGameResponse : BaseResponse
    {
    }

    // Notifications
    public class PlayerJoinedNotification
    {
        public Player Player { get; set; }
    }

    public class PlayerLeftNotification
    {
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
    }

    public class GameStartedNotification
    {
        public List<Player> Players { get; set; }
    }

    // Game Initialization
    public class InitializeGameRequest
    {
        public List<Player> Players { get; set; }
        public decimal SmallBlind { get; set; }
        public decimal BigBlind { get; set; }
    }
    
    public class GameInitializedResponse : BaseResponse
    {
    }
}