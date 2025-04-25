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
        private const int CardDeckPublisherPort = 5559;
        private const int CardDeckSubscriberPort = 5557; // Same as GameEngine's publisher for subscription
        
        /// <summary>
        /// Creates a new microservice manager
        /// </summary>
        public MicroserviceManager()
        {
        }
        
        /// <summary>
        /// Creates and starts all the required microservices
        /// </summary>
        /// <param name="args">Command line arguments to pass UI configuration</param>
        public void StartMicroservices(string[]? args = null)
        {
            // Check if enhanced UI should be used
            bool useCursesUi = args != null && Array.Exists(args, arg => 
                arg.Equals("--curses", StringComparison.OrdinalIgnoreCase) || 
                arg.Equals("-c", StringComparison.OrdinalIgnoreCase));
                
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
                
                Console.WriteLine("Starting Card Deck Service...");
                // Create the card deck service
                var cardDeckService = new CardDeckService(
                    CardDeckPublisherPort,
                    CardDeckSubscriberPort);
                _services.Add(cardDeckService);
                
                // Small delay for initialization
                Thread.Sleep(500);
                
                Console.WriteLine($"Starting Console UI Service{(useCursesUi ? " with enhanced UI" : "")}...");
                // Create the console UI service with enhanced UI preference
                var consoleUIService = new ConsoleUIService(
                    ConsoleUIPublisherPort,
                    GameEnginePublisherPort, // Connect to the game engine's publisher port
                    useCursesUi); // Pass the UI preference
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
                
                // Now that services are running, make sure the Card Deck service is registered with the Game Engine
                Console.WriteLine("Verifying service connections...");
                var deckService = _services.Find(s => s is CardDeckService) as CardDeckService;
                var engineService = _services.Find(s => s is GameEngineService) as GameEngineService;
                
                if (deckService != null && engineService != null)
                {
                    Console.WriteLine("Notifying game engine about card deck service...");
                    // Registration should happen automatically, but let's make sure
                    deckService.PublishServiceRegistration();
                    Thread.Sleep(1000);
                    
                    // Send a direct message to ensure CardDeck service is properly registered
                    var registerMessage = Message.Create(MessageType.ServiceRegistration, 
                        new ServiceRegistrationPayload
                        {
                            ServiceId = deckService.ServiceId,
                            ServiceType = "CardDeck",
                            ServiceName = "Card Deck Service",
                            Capabilities = new List<string>() { "shuffle", "deal" }
                        });
                    engineService.HandleServiceRegistration(registerMessage);
                    
                    // Also trigger a game state broadcast
                    engineService.BroadcastGameState();
                    
                    // Give the console UI service time to process the game start message
                    // and collect player names before starting the hand
                    Thread.Sleep(1000);
                }
                
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