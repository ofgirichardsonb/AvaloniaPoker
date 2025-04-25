using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PokerGame.Core.Game;
using PokerGame.Core.Models;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Microservice that runs the core game logic
    /// </summary>
    public class GameEngineService : MicroserviceBase
    {
        private readonly PokerGameEngine _gameEngine;
        private readonly MicroserviceUI _microserviceUI;
        
        /// <summary>
        /// Creates a new game engine service
        /// </summary>
        /// <param name="publisherPort">The port to use for publishing messages</param>
        /// <param name="subscriberPort">The port to use for subscribing to messages</param>
        public GameEngineService(int publisherPort, int subscriberPort) 
            : base("GameEngine", "Poker Game Engine", publisherPort, subscriberPort)
        {
            // Initialize with null-check protection
            _microserviceUI = new MicroserviceUI(this);
            _gameEngine = new PokerGameEngine(_microserviceUI);
            _microserviceUI.SetGameEngine(_gameEngine);
        }
        
        /// <summary>
        /// Handles messages received from other microservices
        /// </summary>
        /// <param name="message">The message to handle</param>
        protected override async Task HandleMessageAsync(Message message)
        {
            switch (message.Type)
            {
                case MessageType.GameStart:
                    var playerNames = message.GetPayload<string[]>();
                    if (playerNames != null && playerNames.Length >= 2)
                    {
                        _gameEngine.StartGame(playerNames);
                        BroadcastGameState();
                    }
                    break;
                    
                case MessageType.StartHand:
                    _gameEngine.StartHand();
                    BroadcastGameState();
                    break;
                    
                case MessageType.PlayerAction:
                    var actionPayload = message.GetPayload<PlayerActionPayload>();
                    if (actionPayload != null)
                    {
                        _gameEngine.ProcessPlayerAction(actionPayload.ActionType, actionPayload.BetAmount);
                        BroadcastGameState();
                    }
                    break;
                    
                // Add more message handlers as needed
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// Broadcasts the current game state to all listeners
        /// </summary>
        private void BroadcastGameState()
        {
            try
            {
                var payload = new GameStatePayload
                {
                    CurrentState = _gameEngine.State,
                    Pot = _gameEngine.Pot,
                    CurrentBet = _gameEngine.CurrentBet,
                    DealerPosition = -1, // We'd need to expose this in PokerGameEngine
                    CurrentPlayerIndex = -1, // We'd need to expose this in PokerGameEngine
                    CommunityCards = new List<Card>(_gameEngine.CommunityCards)
                };
                
                // Add players, but don't include hole cards in the broadcast message
                foreach (var player in _gameEngine.Players)
                {
                    payload.Players.Add(PlayerInfo.FromPlayer(player, false));
                }
                
                var message = Message.Create(MessageType.GameState, payload);
                Broadcast(message);
                
                // Send individual player messages with their hole cards
                foreach (var player in _gameEngine.Players)
                {
                    // Find any UI service registered for this player
                    var uiServices = GetServicesOfType("PlayerUI");
                    foreach (var uiServiceId in uiServices)
                    {
                        var playerPayload = PlayerInfo.FromPlayer(player, true);
                        var playerMessage = Message.Create(MessageType.PlayerUpdate, playerPayload);
                        SendTo(playerMessage, uiServiceId);
                    }
                }
                
                // Log successful update for debugging
                Console.WriteLine("Game state broadcast successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting game state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Implementation of IPokerGameUI that uses message passing
        /// </summary>
        private class MicroserviceUI : Core.Interfaces.IPokerGameUI
        {
            private readonly GameEngineService _service;
            private PokerGameEngine? _gameEngine;
            
            public MicroserviceUI(GameEngineService service)
            {
                _service = service;
            }
            
            public void SetGameEngine(PokerGameEngine gameEngine)
            {
                _gameEngine = gameEngine;
            }
            
            public void ShowMessage(string message)
            {
                try
                {
                    var messageObj = Message.Create(MessageType.DisplayUpdate, message);
                    _service.Broadcast(messageObj);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error showing message: {ex.Message}");
                }
            }
            
            public void GetPlayerAction(Player player, PokerGameEngine gameEngine)
            {
                try
                {
                    // This is handled via the message passing system instead of a direct call
                    // The UI will send a PlayerAction message when ready
                    
                    // Notify UIs that player action is needed
                    var playerInfo = PlayerInfo.FromPlayer(player, true);
                    var message = Message.Create(MessageType.PlayerAction, playerInfo);
                    _service.Broadcast(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting player action: {ex.Message}");
                }
            }
            
            public void UpdateGameState(PokerGameEngine gameEngine)
            {
                try
                {
                    _service.BroadcastGameState();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating game state: {ex.Message}");
                }
            }
        }
    }
}