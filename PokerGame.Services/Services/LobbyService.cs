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
using Newtonsoft.Json;

// Use an alias to clarify which Message class we're using
using MSAMessage = MSA.Foundation.Messaging.Message;

namespace PokerGame.Services.Services
{
    public class LobbyService : MicroserviceBase
    {
        private GameSession _currentSession;
        private readonly List<Player> _registeredPlayers = new();
        private readonly Logger _logger;
        private readonly MSA.Foundation.Messaging.MessageBroker _messageBroker;
        
        // Dictionary to store message handlers by payload type
        private readonly Dictionary<Type, Func<MSAMessage, Task>> _messageHandlers = new();
        
        public LobbyService(MSA.Foundation.ServiceManagement.ExecutionContext context, string serviceId = "static_lobby_service") 
            : base("Poker Game Lobby", "LobbyService", context)
        {
            // Initialize the logger
            _logger = new Logger("LobbyService", true);
            
            // Initialize the message broker
            _messageBroker = new MSA.Foundation.Messaging.MessageBroker();
            _messageBroker.Start();
            
            // Register message handlers
            _messageHandlers[typeof(PlayerRegistrationRequest)] = HandlePlayerRegistrationAsync;
            _messageHandlers[typeof(JoinGameRequest)] = HandleJoinGameAsync;
            _messageHandlers[typeof(LeaveGameRequest)] = HandleLeaveGameAsync;
            _messageHandlers[typeof(GetLobbyStateRequest)] = HandleGetLobbyStateAsync;
            _messageHandlers[typeof(StartGameRequest)] = HandleStartGameAsync;
            
            // Initialize the single game session
            _currentSession = new GameSession
            {
                Name = "Default Poker Table",
                Status = GameSessionStatus.Waiting
            };
            
            _logger.Log($"Lobby service initialized with game session '{_currentSession.Name}' (ID: {_currentSession.Id})");
        }
        
        // Override the base class HandleMessageAsync to route messages to our handlers
        public override async Task HandleMessageAsync(PokerGame.Core.Microservices.Message message)
        {
            if (message?.Payload == null)
            {
                _logger.LogWarning("Received null message or message with null payload");
                return;
            }
            
            // Try to determine the payload type
            string payloadTypeStr = message.Payload.GetType().Name;
            _logger.Log($"Received message with payload type: {payloadTypeStr}");
            
            // Convert the Core message to MSA message format to work with our handlers
            var msaMessage = new MSA.Foundation.Messaging.Message
            {
                MessageId = message.MessageId,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                Payload = message.Payload
            };
            
            // Handle message based on payload type
            foreach (var handler in _messageHandlers)
            {
                if (message.Payload.GetType() == handler.Key || 
                    message.Payload.GetType().Name.Contains(handler.Key.Name))
                {
                    await handler.Value(msaMessage);
                    return;
                }
            }
            
            _logger.LogWarning($"No handler found for message with payload type: {payloadTypeStr}");
        }

        private async Task HandlePlayerRegistrationAsync(MSAMessage message)
        {
            PlayerRegistrationRequest request;
            
            // Extract the payload from string
            if (string.IsNullOrEmpty(message.Payload))
            {
                _logger.LogWarning("Received message with null or empty payload");
                return;
            }
            
            try
            {
                request = JsonConvert.DeserializeObject<PlayerRegistrationRequest>(message.Payload);
                
                if (request == null)
                {
                    _logger.LogWarning("Failed to deserialize player registration request");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error deserializing player registration request: {ex.Message}");
                return;
            }
            
            _logger.Log($"Processing player registration request for '{request.PlayerName}'");
            
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
            
            _logger.Log($"Player '{player.Name}' registered with ID '{player.Id}'");
            
            await SendResponseAsync(message, response);
        }
        
        // Helper method to send a response to the requester
        private async Task SendResponseAsync<T>(MSAMessage requestMessage, T responsePayload) where T : class
        {
            var responseMessage = new MSAMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                SenderId = ServiceId,
                ReceiverId = requestMessage.SenderId,
                MessageType = MSA.Foundation.Messaging.MessageType.Response
            };
            
            // Store the original message ID in headers for correlation
            responseMessage.Headers["OriginalMessageId"] = requestMessage.MessageId;
            
            // Serialize the payload to JSON
            string jsonPayload = JsonConvert.SerializeObject(responsePayload);
            responseMessage.Payload = jsonPayload;
            
            await _messageBroker.PublishMessageAsync(responseMessage);
        }
        
        // Helper method to send a message to a specific service
        private async Task SendMessageAsync<T>(string targetServiceId, T payload) where T : class
        {
            var message = new MSAMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                SenderId = ServiceId,
                ReceiverId = targetServiceId,
                MessageType = MSA.Foundation.Messaging.MessageType.Request
            };
            
            // Serialize the payload to JSON
            string jsonPayload = JsonConvert.SerializeObject(payload);
            message.Payload = jsonPayload;
            
            await _messageBroker.PublishMessageAsync(message);
        }
        
        // Helper method to broadcast a message to all services
        private async Task BroadcastMessageAsync<T>(T payload) where T : class
        {
            var message = new MSAMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                SenderId = ServiceId,
                MessageType = MSA.Foundation.Messaging.MessageType.Event
            };
            
            // Serialize the payload to JSON
            string jsonPayload = JsonConvert.SerializeObject(payload);
            message.Payload = jsonPayload;
            
            await _messageBroker.PublishMessageAsync(message);
        }

        private async Task HandleJoinGameAsync(MSAMessage message)
        {
            JoinGameRequest request;
            
            // Extract the payload from string
            if (string.IsNullOrEmpty(message.Payload))
            {
                _logger.LogWarning("Received message with null or empty payload");
                return;
            }
            
            try
            {
                request = JsonConvert.DeserializeObject<JoinGameRequest>(message.Payload);
                
                if (request == null)
                {
                    _logger.LogWarning("Failed to deserialize join game request");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error deserializing join game request: {ex.Message}");
                return;
            }
            
            _logger.Log($"Processing join game request for player '{request.PlayerId}'");
            
            // Find the player
            var player = _registeredPlayers.Find(p => p.Id == request.PlayerId);
            if (player == null)
            {
                _logger.LogWarning($"Player with ID '{request.PlayerId}' not found");
                await SendErrorResponseAsync(message, "Player not registered");
                return;
            }
            
            // Check if player is already in the game
            if (_currentSession.Players.Exists(p => p.Id == player.Id))
            {
                _logger.LogWarning($"Player '{player.Name}' (ID: {player.Id}) is already in the game");
                await SendErrorResponseAsync(message, "Player already in game");
                return;
            }
            
            // Check if game is joinable
            if (_currentSession.Status != GameSessionStatus.Waiting)
            {
                _logger.LogWarning($"Game is not in a joinable state (Status: {_currentSession.Status})");
                await SendErrorResponseAsync(message, "Game is not accepting players");
                return;
            }
            
            // Add the player to the session
            _currentSession.Players.Add(player);
            
            _logger.Log($"Player '{player.Name}' joined the game");
            
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
            LeaveGameRequest request;
            
            // Extract the payload from string
            if (string.IsNullOrEmpty(message.Payload))
            {
                _logger.LogWarning("Received message with null or empty payload");
                return;
            }
            
            try
            {
                request = JsonConvert.DeserializeObject<LeaveGameRequest>(message.Payload);
                
                if (request == null)
                {
                    _logger.LogWarning("Failed to deserialize leave game request");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error deserializing leave game request: {ex.Message}");
                return;
            }
            
            _logger.Log($"Processing leave game request for player '{request.PlayerId}'");
            
            // Remove player from session
            var playerIndex = _currentSession.Players.FindIndex(p => p.Id == request.PlayerId);
            if (playerIndex >= 0)
            {
                var player = _currentSession.Players[playerIndex];
                _currentSession.Players.RemoveAt(playerIndex);
                
                _logger.Log($"Player '{player.Name}' left the game");
                
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
                _logger.LogWarning($"Player with ID '{request.PlayerId}' not found in the game");
                await SendErrorResponseAsync(message, "Player not in game");
            }
        }

        private async Task HandleGetLobbyStateAsync(MSAMessage message)
        {
            _logger.Log("Processing request for lobby state");
            
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
            StartGameRequest request;
            
            // Extract the payload from string
            if (string.IsNullOrEmpty(message.Payload))
            {
                _logger.LogWarning("Received message with null or empty payload");
                return;
            }
            
            try
            {
                request = JsonConvert.DeserializeObject<StartGameRequest>(message.Payload);
                
                if (request == null)
                {
                    _logger.LogWarning("Failed to deserialize start game request");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error deserializing start game request: {ex.Message}");
                return;
            }
            
            _logger.Log($"Processing start game request from '{request.RequesterId}'");
            
            // Basic validation
            if (_currentSession.Players.Count < 2)
            {
                _logger.LogWarning("Cannot start game with fewer than 2 players");
                await SendErrorResponseAsync(message, "Need at least 2 players to start");
                return;
            }
            
            if (_currentSession.Status != GameSessionStatus.Waiting)
            {
                _logger.LogWarning($"Cannot start game in current state (Status: {_currentSession.Status})");
                await SendErrorResponseAsync(message, "Game is already in progress");
                return;
            }
            
            // Update status
            _currentSession.Status = GameSessionStatus.InProgress;
            
            _logger.Log("Game started successfully");
            
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