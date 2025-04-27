using System;
using System.Collections.Generic;
using System.Threading;
using PokerGame.Core.ServiceManagement;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Manages the startup and shutdown of microservices
    /// </summary>
    public class MicroserviceManager : IDisposable
    {
        private readonly List<MicroserviceBase> _services = new List<MicroserviceBase>();
        
        // Port configurations using constants for consistency
        private int GameEnginePublisherPort;
        private int GameEngineSubscriberPort;
        private int ConsoleUIPublisherPort;
        private int ConsoleUISubscriberPort;
        private int CardDeckPublisherPort;
        private int CardDeckSubscriberPort;
        private int _portOffset;
        
        // Initialize with provided or random port offset to avoid conflicts when restarting
        private void InitializePorts(int providedOffset = -1)
        {
            // Store the port offset for later reference
            if (providedOffset >= 0)
            {
                // Use the provided offset
                _portOffset = providedOffset;
                Console.WriteLine($"Using provided port offset: {_portOffset}");
            }
            else
            {
                // Generate a random offset between 0-100 to avoid port conflicts on restart
                Random random = new Random();
                _portOffset = random.Next(0, 100);
                Console.WriteLine($"Using random port offset: {_portOffset}");
            }
            
            // Use the ServiceConstants to calculate consistent port numbers
            GameEnginePublisherPort = ServiceConstants.Ports.GetGameEnginePublisherPort(_portOffset);
            GameEngineSubscriberPort = ServiceConstants.Ports.GetGameEngineSubscriberPort(_portOffset);
            ConsoleUIPublisherPort = ServiceConstants.Ports.GetConsoleUIPublisherPort(_portOffset);
            ConsoleUISubscriberPort = ServiceConstants.Ports.GetConsoleUISubscriberPort(_portOffset);
            CardDeckPublisherPort = ServiceConstants.Ports.GetCardDeckPublisherPort(_portOffset);
            CardDeckSubscriberPort = ServiceConstants.Ports.GetCardDeckSubscriberPort(_portOffset);
            
            // Debug port configuration
            Console.WriteLine($"====> PORT CONFIG: GameEngine Publisher: {GameEnginePublisherPort}, Subscriber: {GameEngineSubscriberPort}");
            Console.WriteLine($"====> PORT CONFIG: ConsoleUI Publisher: {ConsoleUIPublisherPort}, Subscriber: {ConsoleUISubscriberPort}");
            Console.WriteLine($"====> PORT CONFIG: CardDeck Publisher: {CardDeckPublisherPort}, Subscriber: {CardDeckSubscriberPort}");
        }
        
        /// <summary>
        /// Creates a new microservice manager
        /// </summary>
        /// <param name="portOffset">Optional fixed port offset (if not provided, a random offset will be used)</param>
        public MicroserviceManager(int portOffset = -1)
        {
            // Initialize ports with provided offset or random offset to avoid conflicts
            InitializePorts(portOffset);
        }
        
        /// <summary>
        /// Creates and starts all the required microservices
        /// </summary>
        /// <param name="args">Command line arguments to pass UI configuration</param>
        public void StartMicroservices(string[]? args = null)
        {
            // Check if curses UI should be used (--enhanced-ui flag is supported for backward compatibility)
            bool useCursesUi = args != null && Array.Exists(args, arg => 
                arg.Equals("--curses", StringComparison.OrdinalIgnoreCase) || 
                arg.Equals("-c", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--enhanced-ui", StringComparison.OrdinalIgnoreCase)); // Deprecated but supported for backward compatibility
                
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
                
                Console.WriteLine($"Starting Console UI Service{(useCursesUi ? " with curses UI" : "")}...");
                // Create the console UI service with curses UI preference
                var consoleUIService = new ConsoleUIService(
                    ConsoleUIPublisherPort,
                    ConsoleUISubscriberPort, // Use subscriber port
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
                    
                    // Give time for registration to be processed
                    Console.WriteLine("Waiting for card deck service registration to be processed...");
                    Thread.Sleep(2000);
                    
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
                    
                    // Make sure players are properly registered with the game engine
                    Console.WriteLine("Ensuring players are properly registered with the game engine...");
                    Thread.Sleep(1000);
                    
                    // Give the engine service a direct command to start a hand after initializing
                    Console.WriteLine("Sending forced StartHand command to engine service");
                    var startHandMsg = Message.Create(MessageType.StartHand);
                    engineService.HandleMessageAsync(startHandMsg).Wait();
                    
                    // Then trigger a game state broadcast
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
            try
            {
                // Stop each service (which cancels tasks but doesn't release ports)
                foreach (var service in _services)
                {
                    service.Stop();
                }
                
                // Small delay to ensure tasks have stopped
                Thread.Sleep(200);
                
                // Now dispose them properly (which releases ports)
                foreach (var service in _services)
                {
                    service.Dispose();
                }
                
                // Explicitly clean up NetMQ to ensure ports are released
                try
                {
                    NetMQ.NetMQConfig.Cleanup(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"NetMQ cleanup error: {ex.Message}");
                }
                
                Console.WriteLine("All microservices stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping microservices: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Disposes of all resources used by the microservices
        /// </summary>
        public void Dispose()
        {
            try
            {
                StopMicroservices();
                _services.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing microservice manager: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Start only the Game Engine service
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public void StartGameEngineService(string[]? args = null)
        {
            try
            {
                Console.WriteLine("Starting Game Engine Service in standalone mode...");
                // Create the game engine service
                var gameEngineService = new GameEngineService(
                    GameEnginePublisherPort, 
                    GameEngineSubscriberPort);
                _services.Add(gameEngineService);
                
                // Start the service
                gameEngineService.Start();
                
                Console.WriteLine("Game Engine Service started successfully");
                Console.WriteLine($"Publisher Port: {GameEnginePublisherPort}, Subscriber Port: {GameEngineSubscriberPort}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting Game Engine Service: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        /// <summary>
        /// Start only the Card Deck service
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public void StartCardDeckService(string[]? args = null)
        {
            try
            {
                // Check if emergency deck mode is enabled
                bool useEmergencyDeck = args != null && Array.Exists(args, arg => 
                    arg.Equals("--emergency-deck", StringComparison.OrdinalIgnoreCase));
                    
                Console.WriteLine($"Starting Card Deck Service in standalone mode{(useEmergencyDeck ? " with emergency deck" : "")}...");
                
                // Create the card deck service (with emergency deck if specified)
                var cardDeckService = new CardDeckService(
                    CardDeckPublisherPort,
                    CardDeckSubscriberPort,
                    useEmergencyDeck);
                _services.Add(cardDeckService);
                
                // Start the service
                cardDeckService.Start();
                
                // Publish service registration immediately
                cardDeckService.PublishServiceRegistration();
                
                Console.WriteLine("Card Deck Service started successfully");
                Console.WriteLine($"Publisher Port: {CardDeckPublisherPort}, Subscriber Port: {CardDeckSubscriberPort}");
                if (useEmergencyDeck)
                {
                    Console.WriteLine("Using emergency deck mode - will create decks immediately without network communication");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting Card Deck Service: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        /// <summary>
        /// Start only the Console UI service
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <param name="useEnhancedUi">Whether to use the curses UI</param>
        public void StartConsoleUIService(string[]? args = null, bool useEnhancedUi = false)
        {
            try
            {
                // Check for UI override in args if not explicitly specified
                if (!useEnhancedUi && args != null)
                {
                    // Check for both curses and enhanced-ui flags (the latter is deprecated)
                    bool hasEnhancedUiFlag = Array.Exists(args, arg => arg.Equals("--enhanced-ui", StringComparison.OrdinalIgnoreCase));
                    if (hasEnhancedUiFlag)
                    {
                        Console.WriteLine("Note: --enhanced-ui is deprecated, please use --curses instead");
                    }
                    
                    useEnhancedUi = Array.Exists(args, arg =>
                        arg.Equals("--curses", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("-c", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("--enhanced-ui", StringComparison.OrdinalIgnoreCase)); // Deprecated but supported for backward compatibility
                }
                
                Console.WriteLine($"Starting Console UI Service in standalone mode{(useEnhancedUi ? " with curses UI" : "")}...");
                
                // Create the console UI service with curses UI preference
                var consoleUIService = new ConsoleUIService(
                    ConsoleUIPublisherPort,
                    ConsoleUISubscriberPort,
                    useEnhancedUi);
                _services.Add(consoleUIService);
                
                // Start the service
                consoleUIService.Start();
                
                Console.WriteLine("Console UI Service started successfully");
                Console.WriteLine($"Publisher Port: {ConsoleUIPublisherPort}, Subscriber Port: {ConsoleUISubscriberPort}");
                Console.WriteLine($"Curses UI: {(useEnhancedUi ? "Enabled" : "Disabled")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting Console UI Service: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}