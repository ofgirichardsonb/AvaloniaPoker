using System;
using System.Collections.Generic;
using PokerGame.Core.Microservices;
using PokerGame.Abstractions;

namespace PokerGame.Services
{
    /// <summary>
    /// Factory for creating telemetry decorator instances
    /// </summary>
    public static class TelemetryDecoratorFactory
    {
        private static readonly Lazy<ITelemetryService> _telemetryService = 
            new Lazy<ITelemetryService>(() => (ITelemetryService)TelemetryService.Instance);
        
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
            
            return new GameTelemetryDecorator(service, _telemetryService.Value);
        }
        
        /// <summary>
        /// Gets the telemetry service instance
        /// </summary>
        public static ITelemetryService TelemetryService => _telemetryService.Value;
    }
}