using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PokerGame.Core.Messaging;

namespace PokerGame.Core.ServiceManagement
{
    /// <summary>
    /// Manages in-process services for the poker game
    /// This replaces the previous process-based management system
    /// </summary>
    public class ServiceManager
    {
        private static readonly object _lockObject = new object();
        private static ServiceManager? _instance;
        private readonly Dictionary<string, ServiceInfo> _services = new Dictionary<string, ServiceInfo>();
        private readonly Random _random = new Random();
        private bool _initialized = false;
        
        /// <summary>
        /// Singleton instance
        /// </summary>
        public static ServiceManager Instance
        {
            get
            {
                lock (_lockObject)
                {
                    _instance ??= new ServiceManager();
                    return _instance;
                }
            }
        }
        
        /// <summary>
        /// Private constructor to enforce singleton pattern
        /// </summary>
        private ServiceManager()
        {
        }
        
        /// <summary>
        /// Initialize the service manager
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                return;
                
            // Initialize message broker 
            // Note: We're not initializing the broker here because it should be initialized
            // by the services that need it. We'll leave this as a placeholder for future use.
            
            _initialized = true;
        }
        
        /// <summary>
        /// Start all services using the specified port offset
        /// </summary>
        /// <param name="portOffset">Port offset for services, or -1 for random</param>
        /// <param name="useCurses">Whether to use the curses UI</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        /// <returns>The port offset used</returns>
        public int StartAllServices(int portOffset = -1, bool useCurses = false, bool verbose = false)
        {
            // Ensure the manager is initialized
            Initialize();
            
            // Generate a port offset if not provided
            if (portOffset < 0)
            {
                portOffset = _random.Next(900) + 100;
            }
            
            // Start services
            StartServicesHost(portOffset, verbose);
            
            // Wait for services to start
            Thread.Sleep(1000);
            
            // Start the client
            StartConsoleClient(portOffset, useCurses, verbose);
            
            return portOffset;
        }
        
        /// <summary>
        /// Start the services host with the specified port offset
        /// </summary>
        /// <param name="portOffset">Port offset for services, or -1 for random</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        /// <returns>The port offset used</returns>
        public int StartServicesHost(int portOffset = -1, bool verbose = false)
        {
            // Ensure the manager is initialized
            Initialize();
            
            // Generate a port offset if not provided
            if (portOffset < 0)
            {
                portOffset = _random.Next(900) + 100;
            }
            
            try
            {
                // Create a cancellation token source for the service
                var cts = new CancellationTokenSource();
                
                // Define the service name
                string serviceName = "ServicesHost";
                
                // Create the service info
                var serviceInfo = new ServiceInfo
                {
                    Name = serviceName,
                    CancellationTokenSource = cts
                };
                
                // Log the startup
                Console.WriteLine($"Starting services host with port offset {portOffset} (verbose: {verbose})...");
                
                // Get the central broker instance
                var broker = BrokerManager.Instance.CentralBroker;
                if (broker == null)
                {
                    throw new InvalidOperationException("Central broker is not initialized");
                }
                
                // Create and start the required services
                // Instead of spawning external processes, we'll create the services directly
                Task serviceTask = Task.Run(() =>
                {
                    try
                    {
                        // Get service type constants
                        string cardDeckType = PokerGame.Core.ServiceManagement.ServiceConstants.ServiceTypes.CardDeck;
                        string gameEngineType = PokerGame.Core.ServiceManagement.ServiceConstants.ServiceTypes.GameEngine;
                        
                        // Calculate actual ports based on port offset
                        int gameEnginePublisherPort = ServiceConstants.Ports.GetGameEnginePublisherPort(portOffset);
                        int gameEngineSubscriberPort = ServiceConstants.Ports.GetGameEngineSubscriberPort(portOffset);
                        int cardDeckPublisherPort = ServiceConstants.Ports.GetCardDeckPublisherPort(portOffset);
                        int cardDeckSubscriberPort = ServiceConstants.Ports.GetCardDeckSubscriberPort(portOffset);
                        
                        // Create a microservice manager to help with service creation
                        var microserviceManager = new Microservices.MicroserviceManager();
                        
                        // Create the Game Engine service
                        Console.WriteLine("Starting Game Engine Service...");
                        var gameEngineService = new Microservices.GameEngineService(
                            gameEngineType,
                            "Game Engine Service",
                            gameEnginePublisherPort,
                            gameEngineSubscriberPort);
                        
                        Console.WriteLine($"Game Engine Service started with ID: {gameEngineService.ServiceId}");
                            
                        // Create the Card Deck service
                        Console.WriteLine("Starting Card Deck Service...");
                        var cardDeckService = new Microservices.CardDeckService(
                            cardDeckType, 
                            "Card Deck Service", 
                            cardDeckPublisherPort, 
                            cardDeckSubscriberPort,
                            false);  // Don't use emergency deck mode
                            
                        Console.WriteLine($"Card Deck Service started with ID: {cardDeckService.ServiceId}");
                        
                        // Small delay to let the services initialize
                        Thread.Sleep(2000);
                        
                        // Ensure the Card Deck service is registered with the Game Engine
                        Console.WriteLine("Notifying game engine about card deck service...");
                        cardDeckService.PublishServiceRegistration();
                        
                        // Wait indefinitely until cancellation is requested
                        try {
                            while (!cts.Token.IsCancellationRequested) {
                                Thread.Sleep(100);
                            }
                        }
                        catch (OperationCanceledException) {
                            // This is expected when cancellation is requested
                        }
                        
                        Console.WriteLine("All services started successfully");
                    }
                    catch (OperationCanceledException)
                    {
                        // Normal cancellation - this is expected
                        Console.WriteLine("Services host canceled");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in services host: {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
                    }
                }, cts.Token);
                
                // Store the task and process info
                serviceInfo.Task = serviceTask;
                
                // Add the service to the dictionary
                _services[serviceName] = serviceInfo;
                
                // Return the port offset that was used
                return portOffset;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting services host: {ex.Message}");
                return portOffset;
            }
        }
        
        /// <summary>
        /// Start the console client - this is a stub method since we're no longer using the Console UI
        /// </summary>
        /// <param name="portOffset">Port offset for services</param>
        /// <param name="useCurses">Whether to use the curses UI</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        /// <returns>An identifier for the client or -1 if failed</returns>
        public int StartConsoleClient(int portOffset, bool useCurses, bool verbose)
        {
            // We're no longer using the Console UI, but we need to keep this method for compatibility
            Console.WriteLine("Console UI is no longer supported - using Avalonia UI only");
            return -1;
        }
        
        /// <summary>
        /// Stop all services
        /// </summary>
        public void StopAllServices()
        {
            // Stop all registered services
            foreach (var service in _services.Values)
            {
                StopService(service);
            }
            
            _services.Clear();
        }
        
        /// <summary>
        /// Stop a specific service
        /// </summary>
        private void StopService(ServiceInfo service)
        {
            try
            {
                Console.WriteLine($"Stopping service {service.Name}...");
                
                // Handle process-based services first
                if (service.Process != null && !service.Process.HasExited)
                {
                    try
                    {
                        Console.WriteLine($"Stopping process with PID {service.Process.Id}...");
                        service.Process.Kill(entireProcessTree: true);
                        service.Process.WaitForExit(5000);
                        Console.WriteLine($"Process with PID {service.Process.Id} stopped");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error stopping process for service {service.Name}: {ex.Message}");
                    }
                }
                
                // Cancel the token to signal stop for thread-based services
                service.CancellationTokenSource?.Cancel();
                
                // Wait for the task to complete
                if (service.Task != null)
                {
                    Task.WaitAll(new[] { service.Task }, 5000);
                }
                
                Console.WriteLine($"Service {service.Name} stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping service {service.Name}: {ex.Message}");
            }
            finally
            {
                // Dispose of the cancellation token source
                service.CancellationTokenSource?.Dispose();
                service.CancellationTokenSource = null;
                
                // Dispose of the process
                service.Process?.Dispose();
                service.Process = null;
            }
        }
        
        /// <summary>
        /// Check if the services host is running
        /// </summary>
        /// <returns>True if running, false otherwise</returns>
        public bool IsServicesHostRunning()
        {
            return _services.ContainsKey("ServicesHost");
        }
        
        /// <summary>
        /// Check if the console client is running
        /// </summary>
        /// <returns>True if running, false otherwise</returns>
        public bool IsConsoleClientRunning()
        {
            return _services.ContainsKey("ConsoleClient");
        }
        
        /// <summary>
        /// Get the status of all services
        /// </summary>
        /// <returns>A list of running service names</returns>
        public List<string> GetRunningServices()
        {
            List<string> result = new List<string>();
            
            foreach (var service in _services.Values)
            {
                if (service.Task != null && !service.Task.IsCompleted)
                {
                    result.Add(service.Name);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Information about a running service
        /// </summary>
        private class ServiceInfo
        {
            /// <summary>
            /// The service name
            /// </summary>
            public string Name { get; set; } = "";
            
            /// <summary>
            /// The task running the service
            /// </summary>
            public Task? Task { get; set; }
            
            /// <summary>
            /// Cancellation token source for stopping the service
            /// </summary>
            public CancellationTokenSource? CancellationTokenSource { get; set; }
            
            /// <summary>
            /// Process associated with the service (if applicable)
            /// </summary>
            public System.Diagnostics.Process? Process { get; set; }
        }
    }
}