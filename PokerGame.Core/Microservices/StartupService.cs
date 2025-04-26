using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// A service that coordinates and verifies the startup of other microservices
    /// </summary>
    public class StartupService : MicroserviceBase
    {
        private readonly List<string> _requiredServiceTypes = new List<string> { "GameEngine", "CardDeck", "ConsoleUI" };
        private readonly Dictionary<string, bool> _serviceAvailability = new Dictionary<string, bool>();
        private readonly ManualResetEvent _allServicesAvailable = new ManualResetEvent(false);
        private string? _gameEngineServiceId;
        private string? _cardDeckServiceId;
        private string? _uiServiceId;
        
        /// <summary>
        /// Creates a new startup service
        /// </summary>
        /// <param name="publisherPort">The port for publishing messages</param>
        /// <param name="subscriberPort">The port for subscribing to messages</param>
        public StartupService(int publisherPort, int subscriberPort)
            : base("Startup", "Startup Coordinator", publisherPort, subscriberPort)
        {
            // Initialize service availability tracking
            foreach (var serviceType in _requiredServiceTypes)
            {
                _serviceAvailability[serviceType] = false;
            }
        }
        
        /// <summary>
        /// Waits for all required services to be available before proceeding
        /// </summary>
        /// <param name="timeoutMs">Maximum time to wait in milliseconds</param>
        /// <returns>True if all services are available, false if timeout occurs</returns>
        public bool WaitForAllServices(int timeoutMs = 10000)
        {
            // Start the verification process
            VerifyServices();
            
            // Wait for the event to be signaled or timeout
            return _allServicesAvailable.WaitOne(timeoutMs);
        }
        
        /// <summary>
        /// Verifies that all required services are available and properly registered
        /// </summary>
        public void VerifyServices()
        {
            Console.WriteLine("Verifying service availability...");
            
            // Broadcast a service discovery message
            var discoveryMessage = Message.Create(MessageType.ServiceDiscovery);
            Broadcast(discoveryMessage);
        }
        
        /// <summary>
        /// Initiates the game setup process
        /// </summary>
        public async Task InitiateGameSetup()
        {
            if (_gameEngineServiceId == null)
            {
                Console.WriteLine("Cannot start game setup - Game Engine service not available");
                return;
            }
            
            Console.WriteLine("Sending StartGame message to Game Engine");
            
            // Initiate the game start process
            var startMessage = Message.Create(MessageType.StartGame);
            SendTo(startMessage, _gameEngineServiceId);
            
            // Wait for the game engine to process the start
            await Task.Delay(1000);
            
            // Check if card deck service is available
            if (_cardDeckServiceId != null)
            {
                Console.WriteLine("Sending explicit registration from Card Deck to Game Engine");
                
                // Send an explicit registration message from card deck to game engine
                var registrationMessage = Message.Create(MessageType.ServiceRegistration, 
                    new ServiceRegistrationPayload
                    {
                        ServiceId = _cardDeckServiceId,
                        ServiceName = "Card Deck Service",
                        ServiceType = "CardDeck",
                        Capabilities = new List<string> { "DeckManagement" }
                    });
                    
                SendTo(registrationMessage, _gameEngineServiceId);
                
                // Wait a moment for processing
                await Task.Delay(500);
            }
            
            // Now start the hand
            Console.WriteLine("Sending StartHand message to Game Engine");
            var startHandMessage = Message.Create(MessageType.StartHand);
            SendTo(startHandMessage, _gameEngineServiceId);
        }
        
        /// <summary>
        /// Handles when a service is registered
        /// </summary>
        /// <param name="registrationInfo">Registration information</param>
        protected override void OnServiceRegistered(ServiceRegistrationPayload registrationInfo)
        {
            base.OnServiceRegistered(registrationInfo);
            
            // Track services as they register
            if (_requiredServiceTypes.Contains(registrationInfo.ServiceType))
            {
                _serviceAvailability[registrationInfo.ServiceType] = true;
                
                Console.WriteLine($"Required service now available: {registrationInfo.ServiceName} ({registrationInfo.ServiceType})");
                
                // Keep track of service IDs for direct communication
                switch (registrationInfo.ServiceType)
                {
                    case "GameEngine":
                        _gameEngineServiceId = registrationInfo.ServiceId;
                        break;
                    case "CardDeck":
                        _cardDeckServiceId = registrationInfo.ServiceId;
                        break;
                    case "ConsoleUI":
                        _uiServiceId = registrationInfo.ServiceId;
                        break;
                }
                
                // Check if all required services are now available
                bool allAvailable = true;
                foreach (var pair in _serviceAvailability)
                {
                    if (!pair.Value)
                    {
                        allAvailable = false;
                        break;
                    }
                }
                
                if (allAvailable && !_allServicesAvailable.WaitOne(0))
                {
                    Console.WriteLine("All required services are now available!");
                    _allServicesAvailable.Set();
                    
                    // Send a notification to all services
                    var notificationPayload = new NotificationPayload
                    {
                        Message = "All required services are now available and registered",
                        Level = "info"
                    };
                    
                    var notificationMessage = Message.Create(MessageType.Notification, notificationPayload);
                    Broadcast(notificationMessage);
                }
            }
        }
        
        /// <summary>
        /// Handles messages received from other services
        /// </summary>
        /// <param name="message">The received message</param>
        protected internal override async Task HandleMessageAsync(Message message)
        {
            switch (message.Type)
            {
                case MessageType.DeckCreated:
                    Console.WriteLine("Startup service received DeckCreated confirmation");
                    var deckCreatedPayload = message.GetPayload<DeckStatusPayload>();
                    if (deckCreatedPayload != null && deckCreatedPayload.Success)
                    {
                        Console.WriteLine($"Deck {deckCreatedPayload.DeckId} was created successfully");
                        
                        // Wait a moment before proceeding with dealing cards
                        await Task.Delay(500);
                        
                        // We'll let the game engine handle its own flow now
                    }
                    break;
                    
                case MessageType.GameState:
                    // Handle game state updates
                    var statePayload = message.GetPayload<GameStatePayload>();
                    if (statePayload != null)
                    {
                        Console.WriteLine($"Current game state: {statePayload.CurrentState}");
                    }
                    break;
            }
            
            await base.HandleMessageAsync(message);
        }
    }
}