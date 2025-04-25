using System;
using System.Collections.Generic;
using System.Threading;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Manages the startup and shutdown of microservices
    /// </summary>
    public class MicroserviceManager : IDisposable
    {
        private readonly List<MicroserviceBase> _services = new List<MicroserviceBase>();
        
        // Default port allocations
        private const int GameEnginePublisherPort = 5556;
        private const int GameEngineSubscriberPort = 5557;
        private const int ConsoleUIPublisherPort = 5558;
        private const int ConsoleUISubscriberPort = 5557; // Same as GameEngine's publisher for subscription
        
        /// <summary>
        /// Creates a new microservice manager
        /// </summary>
        public MicroserviceManager()
        {
        }
        
        /// <summary>
        /// Creates and starts all the required microservices
        /// </summary>
        public void StartMicroservices()
        {
            try
            {
                Console.WriteLine("Starting Game Engine Service...");
                // Create the game engine service
                var gameEngineService = new GameEngineService(
                    GameEnginePublisherPort, 
                    GameEngineSubscriberPort);
                _services.Add(gameEngineService);
                
                // Small delay for initialization
                Thread.Sleep(500);
                
                Console.WriteLine("Starting Console UI Service...");
                // Create the console UI service
                var consoleUIService = new ConsoleUIService(
                    ConsoleUIPublisherPort,
                    GameEnginePublisherPort); // Connect to the game engine's publisher port
                _services.Add(consoleUIService);
                
                // Start all services (in order)
                Console.WriteLine("Starting services...");
                foreach (var service in _services)
                {
                    service.Start();
                    // Give each service time to initialize
                    Thread.Sleep(1000);
                }
                
                // Small delay to let the services initialize and connect
                Thread.Sleep(2000);
                
                Console.WriteLine("All microservices started successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting microservices: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        /// <summary>
        /// Stops all running microservices
        /// </summary>
        public void StopMicroservices()
        {
            foreach (var service in _services)
            {
                service.Stop();
            }
            
            Console.WriteLine("All microservices stopped");
        }
        
        /// <summary>
        /// Disposes of all resources used by the microservices
        /// </summary>
        public void Dispose()
        {
            foreach (var service in _services)
            {
                service.Dispose();
            }
            
            _services.Clear();
        }
    }
}