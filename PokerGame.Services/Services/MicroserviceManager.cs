using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PokerGame.Core.Microservices;
using PokerGame.Core.Messaging;

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
            : this(brokerPort, null)
        {
        }
        
        /// <summary>
        /// Creates a new instance of the MicroserviceManager with a custom execution context
        /// </summary>
        /// <param name="brokerPort">The port for the message broker</param>
        /// <param name="executionContext">The execution context to use</param>
        public MicroserviceManager(int brokerPort = 5555, PokerGame.Core.Messaging.ExecutionContext? executionContext = null)
        {
            // Initialize the broker manager
            _brokerManager = BrokerManager.Instance;
            // Start with port configuration and execution context
            _brokerManager.Start(executionContext);
            
            // Initialize telemetry
            _telemetryService = TelemetryService.Instance;
            
            // Register telemetry with the broker
            TelemetryDecoratorFactory.RegisterBrokerTelemetry(_brokerManager);
            
            // Log the initialization
            _telemetryService.TrackEvent("MicroserviceManagerInitialized", new Dictionary<string, string>
            {
                ["BrokerPort"] = brokerPort.ToString(),
                ["HasExecutionContext"] = (executionContext != null).ToString()
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
            
            // Register the service with the central broker if it exists
            if (_brokerManager.CentralBroker != null)
            {
                _brokerManager.CentralBroker.RegisterService(
                    service.ServiceId,
                    service.ServiceName,
                    service.ServiceType);
            }
            
            // Log the registration
            _telemetryService.TrackEvent("ServiceRegistered", new Dictionary<string, string>
            {
                ["ServiceId"] = service.ServiceId,
                ["ServiceName"] = service.ServiceName,
                ["ServiceType"] = service.ServiceType,
                ["CentralBrokerRegistered"] = (_brokerManager.CentralBroker != null).ToString()
            });
            
            return decoratedService;
        }
        
        /// <summary>
        /// Creates and registers a microservice with a dedicated execution context
        /// </summary>
        /// <typeparam name="T">Type of service to create, must extend MicroserviceBase</typeparam>
        /// <param name="serviceName">Name of the service</param>
        /// <param name="serviceType">Type of the service</param>
        /// <param name="publisherPort">Publisher port for the service</param>
        /// <param name="subscriberPort">Subscriber port for the service</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        /// <param name="constructorArgs">Additional constructor arguments if needed</param>
        /// <returns>The registered service instance</returns>
        public T CreateServiceWithExecutionContext<T>(
            string serviceName,
            string serviceType,
            int publisherPort,
            int subscriberPort,
            bool verbose = false,
            params object[] constructorArgs) where T : MicroserviceBase
        {
            try
            {
                // Create a service-specific execution context from the broker manager's context
                var executionContext = _brokerManager.CreateServiceExecutionContext();
                
                // Try to create the service with the ExecutionContext-based constructor first
                T? instance = null;
                bool useExecutionContextConstructor = true;
                
                try
                {
                    // First try the ExecutionContext constructor
                    var ecParameters = new List<object>();
                    ecParameters.Add(executionContext);
                    
                    // Add any additional constructor arguments
                    if (constructorArgs != null && constructorArgs.Length > 0)
                    {
                        ecParameters.AddRange(constructorArgs);
                    }
                    
                    Console.WriteLine($"Attempting to create {typeof(T).Name} with ExecutionContext constructor");
                    Console.WriteLine($"Constructor parameters: ExecutionContext + {(constructorArgs != null ? constructorArgs.Length : 0)} additional args");
                    instance = (T)Activator.CreateInstance(typeof(T), ecParameters.ToArray());
                    Console.WriteLine($"Successfully created {typeof(T).Name} with ExecutionContext constructor");
                }
                catch (Exception ex)
                {
                    // If the ExecutionContext constructor failed, use the port-based constructor as fallback
                    Console.WriteLine($"ExecutionContext constructor failed: {ex.Message}. Falling back to port-based constructor.");
                    useExecutionContextConstructor = false;
                }
                
                // If ExecutionContext constructor failed, try the port-based constructor
                if (!useExecutionContextConstructor)
                {
                    try
                    {
                        // Note: Some service constructors expect:
                        // (string serviceType, string serviceName, int publisherPort, int subscriberPort, [int heartbeatIntervalMs = 5000])
                        // while others have different parameter signatures - need to try multiple approaches
                        
                        // Prepare the base constructor parameters - note serviceName and serviceType are reversed from our method params
                        var parameters = new List<object>();
                        parameters.Add(serviceType);       // serviceType first
                        parameters.Add(serviceName);       // then serviceName
                        parameters.Add(publisherPort);     // then publisherPort
                        parameters.Add(subscriberPort);    // then subscriberPort
                        
                        // Add any additional constructor arguments
                        if (constructorArgs != null && constructorArgs.Length > 0)
                        {
                            parameters.AddRange(constructorArgs);
                        }
                        
                        // Create the service instance using reflection with port-based constructor
                        Console.WriteLine($"Attempting to create {typeof(T).Name} with port-based constructor");
                        Console.WriteLine($"Parameters: serviceType='{serviceType}', serviceName='{serviceName}', " +
                                         $"publisherPort={publisherPort}, subscriberPort={subscriberPort}, " +
                                         $"plus {(constructorArgs != null ? constructorArgs.Length : 0)} additional args");
                        
                        try
                        {
                            // Try standard parameter order first
                            instance = (T)Activator.CreateInstance(typeof(T), parameters.ToArray());
                            Console.WriteLine($"Successfully created {typeof(T).Name} with standard parameter order");
                        }
                        catch (Exception standardEx)
                        {
                            // If that fails, try with different orders of the parameters
                            Console.WriteLine($"Standard parameter constructor failed: {standardEx.Message}");
                            Console.WriteLine("Trying alternate constructor with ports-only");
                            
                            // Some services may just need the port parameters
                            instance = (T)Activator.CreateInstance(typeof(T), publisherPort, subscriberPort);
                            Console.WriteLine($"Successfully created {typeof(T).Name} with ports-only constructor");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating service with port-based constructor: {ex.Message}");
                        throw new InvalidOperationException($"Failed to create instance of {typeof(T).Name} with either ExecutionContext or port-based constructor", ex);
                    }
                }
                
                if (instance == null)
                {
                    throw new InvalidOperationException($"Failed to create instance of {typeof(T).Name}");
                }
                
                // Register the service
                RegisterService(instance);
                
                // Log the creation with telemetry
                _telemetryService.TrackEvent("ServiceCreatedWithExecutionContext", new Dictionary<string, string>
                {
                    ["ServiceType"] = serviceType,
                    ["ServiceName"] = serviceName,
                    ["PublisherPort"] = publisherPort.ToString(),
                    ["SubscriberPort"] = subscriberPort.ToString(),
                    ["ServiceId"] = instance.ServiceId
                });
                
                return instance;
            }
            catch (Exception ex)
            {
                _telemetryService.TrackException(ex, new Dictionary<string, string>
                {
                    ["ServiceType"] = serviceType,
                    ["ServiceName"] = serviceName,
                    ["Operation"] = "CreateServiceWithExecutionContext"
                });
                
                Console.WriteLine($"Error creating service {serviceName}: {ex.Message}");
                throw;
            }
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