using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;
using MSA.Foundation.ServiceManagement;
using PokerGame.Abstractions.Messages;
using PokerGame.Abstractions.Models;
using PokerGame.Core.Messaging;
using PokerGame.Core.Microservices;
using System.Linq;

// Use an alias to clarify which Message class we're using
using MSAMessage = MSA.Foundation.Messaging.Message;

namespace PokerGame.Services.Services
{
    public class LobbyService : MicroserviceBase
    {
        private GameSession _currentSession;
        private readonly List<Player> _registeredPlayers = new();
        
        public LobbyService(MSA.Foundation.ServiceManagement.ExecutionContext context, string serviceId = "static_lobby_service") 
            : base("Poker Game Lobby", "LobbyService", context, serviceId)
        {
            RegisterMessageHandler<PlayerRegistrationRequest>(HandlePlayerRegistrationAsync);
            RegisterMessageHandler<JoinGameRequest>(HandleJoinGameAsync);
            RegisterMessageHandler<LeaveGameRequest>(HandleLeaveGameAsync);
            RegisterMessageHandler<GetLobbyStateRequest>(HandleGetLobbyStateAsync);
            RegisterMessageHandler<StartGameRequest>(HandleStartGameAsync);
            
            // Initialize the single game session
            _currentSession = new GameSession
            {
                Name = "Default Poker Table",
                Status = GameSessionStatus.Waiting
            };
            
            Logger.Info($"Lobby service initialized with game session '{_currentSession.Name}' (ID: {_currentSession.Id})");
        }

        private async Task HandlePlayerRegistrationAsync(MSAMessage message)
        {
            var request = message.Payload.ToObject<PlayerRegistrationRequest>();
            
            Logger.Info($"Processing player registration request for '{request.PlayerName}'");
            
            var player = new Player
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.PlayerName,
                Balance = 1000 // Starting balance
            };
            
            _registeredPlayers.Add(player);
            
            var response = new PlayerRegistrationResponse
            {
                Success = true,
                PlayerId = player.Id
            };
            
            Logger.Info($"Player '{player.Name}' registered with ID '{player.Id}'");
            
            await SendResponseAsync(message, response);
        }

        private async Task HandleJoinGameAsync(MSAMessage message)
        {
            var request = message.Payload.ToObject<JoinGameRequest>();
            
            Logger.Info($"Processing join game request for player '{request.PlayerId}'");
            
            // Find the player
            var player = _registeredPlayers.Find(p => p.Id == request.PlayerId);
            if (player == null)
            {
                Logger.Warning($"Player with ID '{request.PlayerId}' not found");
                await SendErrorResponseAsync(message, "Player not registered");
                return;
            }
            
            // Check if player is already in the game
            if (_currentSession.Players.Exists(p => p.Id == player.Id))
            {
                Logger.Warning($"Player '{player.Name}' (ID: {player.Id}) is already in the game");
                await SendErrorResponseAsync(message, "Player already in game");
                return;
            }
            
            // Check if game is joinable
            if (_currentSession.Status != GameSessionStatus.Waiting)
            {
                Logger.Warning($"Game is not in a joinable state (Status: {_currentSession.Status})");
                await SendErrorResponseAsync(message, "Game is not accepting players");
                return;
            }
            
            // Add the player to the session
            _currentSession.Players.Add(player);
            
            Logger.Info($"Player '{player.Name}' joined the game");
            
            // Notify all clients about the new player
            await BroadcastMessageAsync(new PlayerJoinedNotification
            {
                Player = player
            });
            
            var response = new JoinGameResponse
            {
                Success = true,
                CurrentPlayers = _currentSession.Players
            };
            
            await SendResponseAsync(message, response);
        }

        private async Task HandleLeaveGameAsync(MSAMessage message)
        {
            var request = message.Payload.ToObject<LeaveGameRequest>();
            
            Logger.Info($"Processing leave game request for player '{request.PlayerId}'");
            
            // Remove player from session
            var playerIndex = _currentSession.Players.FindIndex(p => p.Id == request.PlayerId);
            if (playerIndex >= 0)
            {
                var player = _currentSession.Players[playerIndex];
                _currentSession.Players.RemoveAt(playerIndex);
                
                Logger.Info($"Player '{player.Name}' left the game");
                
                // Notify all clients
                await BroadcastMessageAsync(new PlayerLeftNotification
                {
                    PlayerId = player.Id,
                    PlayerName = player.Name
                });
                
                await SendResponseAsync(message, new LeaveGameResponse { Success = true });
            }
            else
            {
                Logger.Warning($"Player with ID '{request.PlayerId}' not found in the game");
                await SendErrorResponseAsync(message, "Player not in game");
            }
        }

        private async Task HandleGetLobbyStateAsync(MSAMessage message)
        {
            Logger.Info("Processing request for lobby state");
            
            var response = new LobbyStateResponse
            {
                CurrentPlayers = _currentSession.Players,
                GameStatus = _currentSession.Status,
                CanStart = _currentSession.Players.Count >= 2
            };
            
            await SendResponseAsync(message, response);
        }

        private async Task HandleStartGameAsync(MSAMessage message)
        {
            var request = message.Payload.ToObject<StartGameRequest>();
            
            Logger.Info($"Processing start game request from '{request.RequesterId}'");
            
            // Basic validation
            if (_currentSession.Players.Count < 2)
            {
                Logger.Warning("Cannot start game with fewer than 2 players");
                await SendErrorResponseAsync(message, "Need at least 2 players to start");
                return;
            }
            
            if (_currentSession.Status != GameSessionStatus.Waiting)
            {
                Logger.Warning($"Cannot start game in current state (Status: {_currentSession.Status})");
                await SendErrorResponseAsync(message, "Game is already in progress");
                return;
            }
            
            // Update status
            _currentSession.Status = GameSessionStatus.InProgress;
            
            Logger.Info("Game started successfully");
            
            // Notify game engine to initialize a game with these players
            await SendMessageAsync("static_game_engine_service", new InitializeGameRequest
            {
                Players = _currentSession.Players,
                SmallBlind = _currentSession.SmallBlind,
                BigBlind = _currentSession.BigBlind
            });
            
            // Notify all clients
            await BroadcastMessageAsync(new GameStartedNotification
            {
                Players = _currentSession.Players
            });
            
            await SendResponseAsync(message, new StartGameResponse { Success = true });
        }
        
        private async Task SendErrorResponseAsync(MSAMessage requestMessage, string errorMessage)
        {
            var response = new BaseResponse
            {
                Success = false,
                ErrorMessage = errorMessage
            };
            
            await SendResponseAsync(requestMessage, response);
        }
    }
}