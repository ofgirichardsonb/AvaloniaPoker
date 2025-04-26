using System;
using System.Collections.Generic;
using PokerGame.Core.Microservices;
using PokerGame.Abstractions;
using PokerGame.Core.Messaging;
using IGameEngineService = PokerGame.Services.IGameEngineService;

namespace PokerGame.Services
{
    /// <summary>
    /// Factory for creating telemetry decorator instances
    /// </summary>
    public static class TelemetryDecoratorFactory
    {
        // Direct reference to the TelemetryService instance
        private static readonly ITelemetryService _telemetryService;
        
        // Static constructor to safely initialize the telemetry service
        static TelemetryDecoratorFactory()
        {
            try
            {
                var coreTelemetryService = PokerGame.Core.Telemetry.TelemetryService.Instance;
                if (coreTelemetryService != null && coreTelemetryService.IsInitialized)
                {
                    _telemetryService = new TelemetryServiceAdapter(coreTelemetryService);
                    Console.WriteLine("TelemetryDecoratorFactory initialized successfully with core TelemetryService");
                }
                else
                {
                    _telemetryService = new NullTelemetryService();
                    Console.WriteLine("TelemetryDecoratorFactory initialized with NullTelemetryService (core service not initialized)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing TelemetryDecoratorFactory: {ex.Message}");
                // Provide a fallback implementation to avoid null reference exceptions
                _telemetryService = new NullTelemetryService();
            }
        }
        
        /// <summary>
        /// Creates a new telemetry decorator for the specified service
        /// </summary>
        /// <param name="service">The service to decorate</param>
        /// <returns>A decorated service with telemetry capabilities</returns>
        public static IGameEngineService CreateGameEngineDecorator(IGameEngineService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            
            return new GameTelemetryDecorator(service, _telemetryService);
        }
        
        /// <summary>
        /// Creates a telemetry-decorated game engine service
        /// </summary>
        /// <param name="service">The service to decorate</param>
        /// <returns>A decorated service with telemetry capabilities</returns>
        public static IGameEngineService CreateGameEngineServiceWithTelemetry(GameEngineService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            
            // Create an adapter that implements IGameEngineService over the Core GameEngineService
            var serviceAdapter = new GameEngineServiceAdapter(service);
            
            // Now decorate the adapter
            return new GameTelemetryDecorator(serviceAdapter, _telemetryService);
        }
        
        /// <summary>
        /// Adapter class to bridge Core.GameEngineService to Services.IGameEngineService
        /// </summary>
        private class GameEngineServiceAdapter : IGameEngineService
        {
            private readonly GameEngineService _service;
            
            public GameEngineServiceAdapter(GameEngineService service)
            {
                _service = service ?? throw new ArgumentNullException(nameof(service));
            }
            
            public string ServiceId => _service.ServiceId;
            public string ServiceName => _service.ServiceName;
            public string ServiceType => _service.ServiceType;
            public bool IsRunning => true; // Approximate based on service being created
            
            public void AddPlayer(Core.Models.Player player) => _service.AddPlayer(player);
            public void BroadcastGameState() => _service.BroadcastGameState();
            public Task HandleMessageAsync(Core.Microservices.Message message) => _service.HandleMessageAsync(message);
            public void RemovePlayer(string playerId) => _service.RemovePlayer(playerId);
            public Task<bool> ProcessPlayerActionAsync(string playerId, string action, int amount) => _service.ProcessPlayerActionAsync(playerId, action, amount);
            public Task StartHandAsync() => _service.StartHandAsync();
            public Task StartAsync() => _service.StartAsync();
            public Task StopAsync() => _service.StopAsync();
        }
        
        /// <summary>
        /// Creates a telemetry-decorated microservice
        /// </summary>
        /// <param name="service">The service to decorate</param>
        /// <returns>The same service (currently no generic microservice decorator implemented)</returns>
        public static MicroserviceBase CreateMicroserviceWithTelemetry(MicroserviceBase service)
        {
            // Currently, we only have a specific decorator for GameEngineService
            // For other services, we return them as-is
            return service;
        }
        
        /// <summary>
        /// Registers telemetry with the broker
        /// </summary>
        /// <param name="brokerManager">The broker manager instance</param>
        public static void RegisterBrokerTelemetry(BrokerManager brokerManager)
        {
            // Since we can't directly cast between the different TelemetryService types,
            // we'll pass the Core.Telemetry.TelemetryService directly
            var coreTelemetryService = PokerGame.Core.Telemetry.TelemetryService.Instance;
            var telemetryHandler = new MessageBrokerTelemetryHandler(coreTelemetryService);
            
            // Set the telemetry handler on the broker manager
            brokerManager.SetTelemetryHandler(telemetryHandler);
            
            // Enable telemetry for the broker
            brokerManager.InitializeTelemetry();
        }
        
        /// <summary>
        /// Gets the telemetry service instance
        /// </summary>
        public static ITelemetryService TelemetryService => _telemetryService;
    }
}