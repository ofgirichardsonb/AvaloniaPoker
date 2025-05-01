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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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
            
            Logger.LogInformation($"Lobby service initialized with game session '{_currentSession.Name}' (ID: {_currentSession.Id})");
        }

        private async Task HandlePlayerRegistrationAsync(MSAMessage message)
        {
            var request = JsonConvert.DeserializeObject<PlayerRegistrationRequest>(message.Payload.ToString());
            
            Logger.LogInformation($"Processing player registration request for '{request.PlayerName}'");
            
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
            
            Logger.LogInformation($"Player '{player.Name}' registered with ID '{player.Id}'");
            
            await SendResponseAsync(message, response);
        }
        
        // Helper method to send a response to the requester
        private async Task SendResponseAsync<T>(MSAMessage requestMessage, T responsePayload) where T : class
        {
            var responseMessage = MessageBroker.CreateResponseMessage(requestMessage, responsePayload);
            await MessageBroker.PublishMessageAsync(responseMessage);
        }
        
        // Helper method to send a message to a specific service
        private async Task SendMessageAsync<T>(string targetServiceId, T payload) where T : class
        {
            var message = MessageBroker.CreateMessage(targetServiceId, payload);
            await MessageBroker.PublishMessageAsync(message);
        }
        
        // Helper method to broadcast a message to all services
        private async Task BroadcastMessageAsync<T>(T payload) where T : class
        {
            var message = MessageBroker.CreateBroadcastMessage(payload);
            await MessageBroker.PublishMessageAsync(message);
        }

        private async Task HandleJoinGameAsync(MSAMessage message)
        {
            var request = JsonConvert.DeserializeObject<JoinGameRequest>(message.Payload.ToString());
            
            Logger.LogInformation($"Processing join game request for player '{request.PlayerId}'");
            
            // Find the player
            var player = _registeredPlayers.Find(p => p.Id == request.PlayerId);
            if (player == null)
            {
                Logger.LogWarning($"Player with ID '{request.PlayerId}' not found");
                await SendErrorResponseAsync(message, "Player not registered");
                return;
            }
            
            // Check if player is already in the game
            if (_currentSession.Players.Exists(p => p.Id == player.Id))
            {
                Logger.LogWarning($"Player '{player.Name}' (ID: {player.Id}) is already in the game");
                await SendErrorResponseAsync(message, "Player already in game");
                return;
            }
            
            // Check if game is joinable
            if (_currentSession.Status != GameSessionStatus.Waiting)
            {
                Logger.LogWarning($"Game is not in a joinable state (Status: {_currentSession.Status})");
                await SendErrorResponseAsync(message, "Game is not accepting players");
                return;
            }
            
            // Add the player to the session
            _currentSession.Players.Add(player);
            
            Logger.LogInformation($"Player '{player.Name}' joined the game");
            
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
            var request = JsonConvert.DeserializeObject<LeaveGameRequest>(message.Payload.ToString());
            
            Logger.LogInformation($"Processing leave game request for player '{request.PlayerId}'");
            
            // Remove player from session
            var playerIndex = _currentSession.Players.FindIndex(p => p.Id == request.PlayerId);
            if (playerIndex >= 0)
            {
                var player = _currentSession.Players[playerIndex];
                _currentSession.Players.RemoveAt(playerIndex);
                
                Logger.LogInformation($"Player '{player.Name}' left the game");
                
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
                Logger.LogWarning($"Player with ID '{request.PlayerId}' not found in the game");
                await SendErrorResponseAsync(message, "Player not in game");
            }
        }

        private async Task HandleGetLobbyStateAsync(MSAMessage message)
        {
            Logger.LogInformation("Processing request for lobby state");
            
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
            var request = JsonConvert.DeserializeObject<StartGameRequest>(message.Payload.ToString());
            
            Logger.LogInformation($"Processing start game request from '{request.RequesterId}'");
            
            // Basic validation
            if (_currentSession.Players.Count < 2)
            {
                Logger.LogWarning("Cannot start game with fewer than 2 players");
                await SendErrorResponseAsync(message, "Need at least 2 players to start");
                return;
            }
            
            if (_currentSession.Status != GameSessionStatus.Waiting)
            {
                Logger.LogWarning($"Cannot start game in current state (Status: {_currentSession.Status})");
                await SendErrorResponseAsync(message, "Game is already in progress");
                return;
            }
            
            // Update status
            _currentSession.Status = GameSessionStatus.InProgress;
            
            Logger.LogInformation("Game started successfully");
            
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