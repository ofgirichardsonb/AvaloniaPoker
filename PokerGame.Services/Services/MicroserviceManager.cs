using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PokerGame.Core.Microservices;
using MessageBroker;

namespace PokerGame.Services
{
    /// <summary>
    /// Manages the lifecycle of microservices and provides telemetry integration
    /// </summary>
    public class MicroserviceManager : IDisposable
    {
        private readonly BrokerManager _brokerManager;
        private readonly TelemetryService _telemetryService;
        private readonly List<MicroserviceBase> _services = new List<MicroserviceBase>();
        private readonly Dictionary<string, IGameEngineService> _gameEngineServices = new Dictionary<string, IGameEngineService>();
        private bool _isDisposed = false;
        
        /// <summary>
        /// Creates a new instance of the MicroserviceManager
        /// </summary>
        /// <param name="brokerPort">The port for the message broker</param>
        public MicroserviceManager(int brokerPort = 5555)
        {
            // Initialize the broker manager
            _brokerManager = BrokerManager.Instance;
            // Start with port configuration
            _brokerManager.Start();
            
            // Initialize telemetry
            _telemetryService = TelemetryService.Instance;
            
            // Register telemetry with the broker
            TelemetryDecoratorFactory.RegisterBrokerTelemetry(_brokerManager);
            
            // Log the initialization
            _telemetryService.TrackEvent("MicroserviceManagerInitialized", new Dictionary<string, string>
            {
                ["BrokerPort"] = brokerPort.ToString()
            });
        }
        
        /// <summary>
        /// Registers a microservice with the manager
        /// </summary>
        /// <param name="service">The service to register</param>
        /// <returns>The registered service (which may be decorated with telemetry)</returns>
        public MicroserviceBase RegisterService(MicroserviceBase service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));
                
            // Apply telemetry decoration
            var decoratedService = TelemetryDecoratorFactory.CreateMicroserviceWithTelemetry(service);
            
            // Add to the list of services
            _services.Add(decoratedService);
            
            // Special handling for GameEngineService
            if (service is GameEngineService gameEngineService)
            {
                var decoratedGameEngine = TelemetryDecoratorFactory.CreateGameEngineServiceWithTelemetry(gameEngineService);
                _gameEngineServices[service.ServiceId] = decoratedGameEngine;
            }
            
            // Log the registration
            _telemetryService.TrackEvent("ServiceRegistered", new Dictionary<string, string>
            {
                ["ServiceId"] = service.ServiceId,
                ["ServiceName"] = service.ServiceName,
                ["ServiceType"] = service.ServiceType
            });
            
            return decoratedService;
        }
        
        /// <summary>
        /// Gets a registered game engine service by ID
        /// </summary>
        /// <param name="serviceId">The ID of the service to get</param>
        /// <returns>The game engine service, or null if not found</returns>
        public IGameEngineService? GetGameEngineService(string serviceId)
        {
            if (string.IsNullOrEmpty(serviceId))
                return null;
                
            return _gameEngineServices.TryGetValue(serviceId, out var service) ? service : null;
        }
        
        /// <summary>
        /// Starts all registered services
        /// </summary>
        public async Task StartAllServicesAsync()
        {
            // Track the start of all services
            _telemetryService.TrackEvent("StartingAllServices", new Dictionary<string, string>
            {
                ["ServiceCount"] = _services.Count.ToString()
            });
            
            // Start each service
            foreach (var service in _services)
            {
                try
                {
                    await service.StartAsync();
                }
                catch (Exception ex)
                {
                    _telemetryService.TrackException(ex, new Dictionary<string, string>
                    {
                        ["ServiceId"] = service.ServiceId,
                        ["ServiceName"] = service.ServiceName,
                        ["ServiceType"] = service.ServiceType,
                        ["Operation"] = "StartAsync"
                    });
                    
                    Console.WriteLine($"Error starting service {service.ServiceName}: {ex.Message}");
                }
            }
            
            // Track the completion of service startup
            _telemetryService.TrackEvent("AllServicesStarted", new Dictionary<string, string>
            {
                ["ServiceCount"] = _services.Count.ToString()
            });
        }
        
        /// <summary>
        /// Stops all registered services
        /// </summary>
        public async Task StopAllServicesAsync()
        {
            // Track the stop of all services
            _telemetryService.TrackEvent("StoppingAllServices", new Dictionary<string, string>
            {
                ["ServiceCount"] = _services.Count.ToString()
            });
            
            // Stop each service in reverse order
            foreach (var service in _services.AsEnumerable().Reverse())
            {
                try
                {
                    await service.StopAsync();
                }
                catch (Exception ex)
                {
                    _telemetryService.TrackException(ex, new Dictionary<string, string>
                    {
                        ["ServiceId"] = service.ServiceId,
                        ["ServiceName"] = service.ServiceName,
                        ["ServiceType"] = service.ServiceType,
                        ["Operation"] = "StopAsync"
                    });
                    
                    Console.WriteLine($"Error stopping service {service.ServiceName}: {ex.Message}");
                }
            }
            
            // Track the completion of service shutdown
            _telemetryService.TrackEvent("AllServicesStopped", new Dictionary<string, string>
            {
                ["ServiceCount"] = _services.Count.ToString()
            });
        }
        
        /// <summary>
        /// Disposes the microservice manager and all registered services
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;
                
            _isDisposed = true;
            
            // Stop all services
            StopAllServicesAsync().GetAwaiter().GetResult();
            
            // Stop the broker manager
            _brokerManager.Stop();
            
            // Flush telemetry
            _telemetryService.Flush();
        }
    }
}