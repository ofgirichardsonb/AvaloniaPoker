using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using PokerGame.Abstractions;

namespace PokerGame.Services
{
    /// <summary>
    /// Decorator class for GameEngineService that adds telemetry capabilities
    /// </summary>
    public class GameTelemetryDecorator : IGameEngineService
    {
        private readonly IGameEngineService _decoratedService;
        private readonly ITelemetryService _telemetryService;
        
        /// <summary>
        /// Gets the unique identifier for this service instance
        /// </summary>
        public string ServiceId => _decoratedService.ServiceId;
        
        /// <summary>
        /// Gets the human-readable name of this service
        /// </summary>
        public string ServiceName => _decoratedService.ServiceName;
        
        /// <summary>
        /// Gets the type of this service (e.g., "GameEngine", "CardDeck", etc.)
        /// </summary>
        public string ServiceType => _decoratedService.ServiceType;
        
        /// <summary>
        /// Gets a value indicating whether the service is currently running
        /// </summary>
        public bool IsRunning => _decoratedService.IsRunning;

        /// <summary>
        /// Creates a new GameTelemetryDecorator
        /// </summary>
        /// <param name="decoratedService">The service to decorate</param>
        /// <param name="telemetryService">The telemetry service to use</param>
        public GameTelemetryDecorator(IGameEngineService decoratedService, ITelemetryService telemetryService)
        {
            _decoratedService = decoratedService ?? throw new ArgumentNullException(nameof(decoratedService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        /// <summary>
        /// Adds a player to the game, with telemetry tracking
        /// </summary>
        /// <param name="player">The player to add</param>
        public void AddPlayer(object player)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                _decoratedService.AddPlayer(player);
                stopwatch.Stop();

                _telemetryService.TrackRequest(
                    "AddPlayer", 
                    DateTimeOffset.UtcNow.AddMilliseconds(-stopwatch.ElapsedMilliseconds), 
                    stopwatch.Elapsed, 
                    "200", 
                    true,
                    new Dictionary<string, string>
                    {
                        ["PlayerType"] = player?.GetType().Name ?? "Unknown"
                    });
            }
            catch (Exception ex)
            {
                _telemetryService.TrackException(ex, new Dictionary<string, string>
                {
                    ["Operation"] = "AddPlayer",
                    ["PlayerType"] = player?.GetType().Name ?? "Unknown"
                });
                throw;
            }
        }

        /// <summary>
        /// Removes a player from the game, with telemetry tracking
        /// </summary>
        /// <param name="playerId">The ID of the player to remove</param>
        public void RemovePlayer(string playerId)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                _decoratedService.RemovePlayer(playerId);
                stopwatch.Stop();

                _telemetryService.TrackRequest(
                    "RemovePlayer", 
                    DateTimeOffset.UtcNow.AddMilliseconds(-stopwatch.ElapsedMilliseconds), 
                    stopwatch.Elapsed, 
                    "200", 
                    true,
                    new Dictionary<string, string>
                    {
                        ["PlayerId"] = playerId ?? "Unknown"
                    });
            }
            catch (Exception ex)
            {
                _telemetryService.TrackException(ex, new Dictionary<string, string>
                {
                    ["Operation"] = "RemovePlayer",
                    ["PlayerId"] = playerId ?? "Unknown"
                });
                throw;
            }
        }

        /// <summary>
        /// Starts a new hand, with telemetry tracking
        /// </summary>
        public async Task StartHandAsync()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await _decoratedService.StartHandAsync();
                stopwatch.Stop();

                _telemetryService.TrackRequest(
                    "StartHand", 
                    DateTimeOffset.UtcNow.AddMilliseconds(-stopwatch.ElapsedMilliseconds), 
                    stopwatch.Elapsed, 
                    "200", 
                    true);
            }
            catch (Exception ex)
            {
                _telemetryService.TrackException(ex, new Dictionary<string, string>
                {
                    ["Operation"] = "StartHand"
                });
                throw;
            }
        }

        /// <summary>
        /// Processes a player action, with telemetry tracking
        /// </summary>
        /// <param name="playerId">The ID of the player</param>
        /// <param name="action">The action to process</param>
        /// <param name="amount">The amount of the action</param>
        /// <returns>True if the action was processed successfully; otherwise, false</returns>
        public async Task<bool> ProcessPlayerActionAsync(string playerId, string action, int amount)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                bool result = await _decoratedService.ProcessPlayerActionAsync(playerId, action, amount);
                stopwatch.Stop();

                _telemetryService.TrackRequest(
                    "ProcessPlayerAction", 
                    DateTimeOffset.UtcNow.AddMilliseconds(-stopwatch.ElapsedMilliseconds), 
                    stopwatch.Elapsed, 
                    result ? "200" : "400", 
                    result,
                    new Dictionary<string, string>
                    {
                        ["PlayerId"] = playerId ?? "Unknown",
                        ["Action"] = action ?? "Unknown",
                        ["Amount"] = amount.ToString()
                    });

                return result;
            }
            catch (Exception ex)
            {
                _telemetryService.TrackException(ex, new Dictionary<string, string>
                {
                    ["Operation"] = "ProcessPlayerAction",
                    ["PlayerId"] = playerId ?? "Unknown",
                    ["Action"] = action ?? "Unknown",
                    ["Amount"] = amount.ToString()
                });
                throw;
            }
        }

        /// <summary>
        /// Handles a message, with telemetry tracking
        /// </summary>
        /// <param name="message">The message to handle</param>
        public async Task HandleMessageAsync(object message)
        {
            try
            {
                string messageType = "Unknown";
                string messageId = "Unknown";
                
                // Detailed message handling will be implemented in Core.Messaging
                
                var stopwatch = Stopwatch.StartNew();
                await _decoratedService.HandleMessageAsync(message);
                stopwatch.Stop();

                _telemetryService.TrackRequest(
                    $"HandleMessage_{messageType}", 
                    DateTimeOffset.UtcNow.AddMilliseconds(-stopwatch.ElapsedMilliseconds), 
                    stopwatch.Elapsed, 
                    "200", 
                    true,
                    new Dictionary<string, string>
                    {
                        ["MessageType"] = messageType,
                        ["MessageId"] = messageId
                    });
            }
            catch (Exception ex)
            {
                string messageType = "Unknown";
                string messageId = "Unknown";
                
                _telemetryService.TrackException(ex, new Dictionary<string, string>
                {
                    ["Operation"] = "HandleMessage",
                    ["MessageType"] = messageType,
                    ["MessageId"] = messageId
                });
                throw;
            }
        }

        /// <summary>
        /// Broadcasts the current game state to all connected clients, with telemetry tracking
        /// </summary>
        public void BroadcastGameState()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                _decoratedService.BroadcastGameState();
                stopwatch.Stop();

                _telemetryService.TrackRequest(
                    "BroadcastGameState", 
                    DateTimeOffset.UtcNow.AddMilliseconds(-stopwatch.ElapsedMilliseconds), 
                    stopwatch.Elapsed, 
                    "200", 
                    true);
            }
            catch (Exception ex)
            {
                _telemetryService.TrackException(ex, new Dictionary<string, string>
                {
                    ["Operation"] = "BroadcastGameState"
                });
                throw;
            }
        }

        /// <summary>
        /// Starts the service, with telemetry tracking
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await _decoratedService.StartAsync();
                stopwatch.Stop();

                _telemetryService.TrackRequest(
                    "StartService", 
                    DateTimeOffset.UtcNow.AddMilliseconds(-stopwatch.ElapsedMilliseconds), 
                    stopwatch.Elapsed, 
                    "200", 
                    true,
                    new Dictionary<string, string>
                    {
                        ["ServiceType"] = ServiceType,
                        ["ServiceId"] = ServiceId
                    });
            }
            catch (Exception ex)
            {
                _telemetryService.TrackException(ex, new Dictionary<string, string>
                {
                    ["Operation"] = "StartService",
                    ["ServiceType"] = ServiceType,
                    ["ServiceId"] = ServiceId
                });
                throw;
            }
        }

        /// <summary>
        /// Stops the service, with telemetry tracking
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await _decoratedService.StopAsync();
                stopwatch.Stop();

                _telemetryService.TrackRequest(
                    "StopService", 
                    DateTimeOffset.UtcNow.AddMilliseconds(-stopwatch.ElapsedMilliseconds), 
                    stopwatch.Elapsed, 
                    "200", 
                    true,
                    new Dictionary<string, string>
                    {
                        ["ServiceType"] = ServiceType,
                        ["ServiceId"] = ServiceId
                    });
            }
            catch (Exception ex)
            {
                _telemetryService.TrackException(ex, new Dictionary<string, string>
                {
                    ["Operation"] = "StopService",
                    ["ServiceType"] = ServiceType,
                    ["ServiceId"] = ServiceId
                });
                throw;
            }
        }
    }
}