using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;
using PokerGame.Core.Game;
using PokerGame.Core.Models;
using PokerGame.Core.Messaging;
using PokerGame.Core.ServiceManagement;
using PokerGame.Abstractions;
using System.Threading;
using MSA.Foundation.Messaging;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Microservice that runs the core game logic
    /// </summary>
    public class GameEngineService : MicroserviceBase, PokerGame.Abstractions.IGameEngineService
    {
        // Test logging line to verify build
        static GameEngineService() {
            Console.WriteLine("********** GAME ENGINE SERVICE LOADED - NEW VERSION WITH STARTHAND RESPONSE **********");
        }
        private readonly PokerGameEngine _gameEngine;
        private readonly MicroserviceUI _microserviceUI;
        private string? _cardDeckServiceId;
        private string _currentDeckId = string.Empty;
        private Models.Deck? _emergencyDeck;
        
        /// <summary>
        /// Gets a value indicating whether the service is currently running
        /// </summary>
        public bool IsRunning { get; private set; }
        
        /// <summary>
        /// Handles a message received from another service asynchronously
        /// </summary>
        /// <param name="message">The message to handle</param>
        /// <returns>A task that completes when the message is handled</returns>
        public async Task HandleMessageAsync(MSA.Foundation.Messaging.Message message)
        {
            Console.WriteLine($"GameEngineService.HandleMessageAsync received message {message.MessageId} of type {message.MessageType}");
            
            try
            {
                string inResponseTo = null;
                // Check for original message ID in headers (MSA.Foundation uses Headers instead of InResponseTo)
                if (message.Headers.TryGetValue("OriginalMessageId", out string originalMsgId))
                {
                    inResponseTo = originalMsgId;
                    Console.WriteLine($"Found OriginalMessageId in headers: {originalMsgId}");
                }
                // Also check AcknowledgmentId (used for ack messages)
                else if (!string.IsNullOrEmpty(message.AcknowledgmentId))
                {
                    inResponseTo = message.AcknowledgmentId;
                    Console.WriteLine($"Using AcknowledgmentId as InResponseTo: {inResponseTo}");
                }
                
                // Convert MSA message to a NetworkMessage format we can handle
                var convertedMessage = new PokerGame.Core.Messaging.NetworkMessage
                {
                    MessageId = message.MessageId,
                    SenderId = message.SenderId,
                    ReceiverId = message.ReceiverId,
                    InResponseTo = inResponseTo,
                    Timestamp = DateTime.UtcNow
                };
                
                // Handle special message types explicitly to avoid string conversion issues
                if (message.MessageType == MSA.Foundation.Messaging.MessageType.Event)
                {
                    // For Event type messages, look at payload to determine if it's StartHand
                    if (!string.IsNullOrEmpty(message.Payload) && 
                        message.Payload.Contains("StartHand", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Detected StartHand event from payload check - setting correct type");
                        convertedMessage.Type = PokerGame.Core.Messaging.MessageType.StartHand;
                    }
                    // Check headers for additional clues
                    else if (message.Headers.ContainsKey("MessageSubType") && 
                             message.Headers["MessageSubType"].Contains("StartHand", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Detected StartHand from message headers - setting correct type");
                        convertedMessage.Type = PokerGame.Core.Messaging.MessageType.StartHand;
                    }
                }
                else if (Enum.TryParse<PokerGame.Core.Messaging.MessageType>(message.MessageType.ToString(), out var msgType))
                {
                    convertedMessage.Type = msgType;
                }
                else
                {
                    // Default fallback
                    convertedMessage.Type = PokerGame.Core.Messaging.MessageType.Debug;
                    Console.WriteLine($"WARNING: Could not map message type {message.MessageType}, defaulting to Debug");
                }
                
                // Add any payload data if present
                if (message.Payload != null)
                {
                    convertedMessage.Payload = message.Payload;
                }
                
                // Special debug logging for StartHand messages
                if (convertedMessage.Type == PokerGame.Core.Messaging.MessageType.StartHand)
                {
                    Console.WriteLine("\n\n");
                    Console.WriteLine("##########################################################");
                    Console.WriteLine("#                                                        #");
                    Console.WriteLine("#         GAME ENGINE CONVERTING STARTHAND MESSAGE       #");
                    Console.WriteLine("#                                                        #");
                    Console.WriteLine("##########################################################");
                    Console.WriteLine($"# Original MSA Message Type: {message.MessageType}");
                    Console.WriteLine($"# Converted To Network Message Type: {convertedMessage.Type}");
                    Console.WriteLine($"# Message ID: {convertedMessage.MessageId}");
                    Console.WriteLine($"# From Sender: {convertedMessage.SenderId}");
                    Console.WriteLine("##########################################################");
                    Console.WriteLine("\n\n");
                }
                
                // Convert NetworkMessage to Message format using our utility method
                var microserviceMessage = ConvertToMicroserviceMessage(convertedMessage);
                
                // Additional debug logging for StartHand in Microservice format
                if (microserviceMessage.Type == PokerGame.Core.Microservices.MessageType.StartHand)
                {
                    Console.WriteLine("\n\n");
                    Console.WriteLine("##########################################################");
                    Console.WriteLine("#                                                        #");
                    Console.WriteLine("#       MICROSERVICE MESSAGE CONVERTED SUCCESSFULLY      #");
                    Console.WriteLine("#                                                        #");
                    Console.WriteLine("##########################################################");
                    Console.WriteLine($"# Final Microservice Message Type: {microserviceMessage.Type}");
                    Console.WriteLine("##########################################################");
                    Console.WriteLine("\n\n");
                }
                
                // Process message based on converted type
                await HandleMessageInternalAsync(microserviceMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling MSA message: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        // Dictionary to keep track of known services and their capabilities
        private readonly Dictionary<string, ServiceRegistrationPayload> _knownServices = new Dictionary<string, ServiceRegistrationPayload>();
        
        /// <summary>
        /// Creates a new game engine service with an execution context
        /// </summary>
        /// <param name="executionContext">The execution context to use</param>
        public GameEngineService(Messaging.ExecutionContext executionContext) 
            : base(ServiceConstants.ServiceTypes.GameEngine, "Poker Game Engine", executionContext)
        {
            // Initialize with null-check protection
            _microserviceUI = new MicroserviceUI(this);
            _gameEngine = new PokerGameEngine(_microserviceUI);
            _microserviceUI.SetGameEngine(_gameEngine);
            Console.WriteLine($"GameEngineService created with execution context using service type: {ServiceConstants.ServiceTypes.GameEngine}");
        }
        
        /// <summary>
        /// Creates a new game engine service with an execution context and verbose flag
        /// </summary>
        /// <param name="executionContext">The execution context to use</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        public GameEngineService(Messaging.ExecutionContext executionContext, bool verbose) 
            : base(ServiceConstants.ServiceTypes.GameEngine, "Poker Game Engine", executionContext)
        {
            // Initialize with null-check protection
            _microserviceUI = new MicroserviceUI(this);
            _gameEngine = new PokerGameEngine(_microserviceUI);
            _microserviceUI.SetGameEngine(_gameEngine);
            Console.WriteLine($"GameEngineService created with execution context (verbose={verbose}) using service type: {ServiceConstants.ServiceTypes.GameEngine}");
            
            if (verbose)
            {
                Console.WriteLine("Verbose logging enabled for GameEngineService");
            }
        }
        
        /// <summary>
        /// Creates a new game engine service with specific ports (backwards compatibility)
        /// </summary>
        /// <param name="publisherPort">The port to use for publishing messages</param>
        /// <param name="subscriberPort">The port to use for subscribing to messages</param>
        public GameEngineService(int publisherPort, int subscriberPort) 
            : base(ServiceConstants.ServiceTypes.GameEngine, "Poker Game Engine", publisherPort, subscriberPort)
        {
            // Initialize with null-check protection
            _microserviceUI = new MicroserviceUI(this);
            _gameEngine = new PokerGameEngine(_microserviceUI);
            _microserviceUI.SetGameEngine(_gameEngine);
        }
        
        /// <summary>
        /// Constructor to match what MicroserviceManager expects (serviceType, serviceName, publisherPort, subscriberPort)
        /// </summary>
        public GameEngineService(
            string serviceType,
            string serviceName,
            int publisherPort,
            int subscriberPort,
            bool verbose = false)
            : base(serviceType, serviceName, publisherPort, subscriberPort)
        {
            // Initialize with null-check protection
            _microserviceUI = new MicroserviceUI(this);
            _gameEngine = new PokerGameEngine(_microserviceUI);
            _microserviceUI.SetGameEngine(_gameEngine);
            
            Console.WriteLine($"GameEngineService created with reflection-compatible constructor: serviceType={serviceType}, serviceName={serviceName}");
        }
        
        /// <summary>
        /// Called when another service is registered
        /// </summary>
        /// <param name="registrationInfo">The service registration info</param>
        protected override void OnServiceRegistered(ServiceRegistrationPayload registrationInfo)
        {
            // Store the service information
            _knownServices[registrationInfo.ServiceId] = registrationInfo;
            Console.WriteLine($"GameEngine registered service: {registrationInfo.ServiceName} (ID: {registrationInfo.ServiceId}, Type: {registrationInfo.ServiceType})");
            
            // Track the card deck service when it comes online
            if (registrationInfo.ServiceType == ServiceConstants.ServiceTypes.CardDeck)
            {
                _cardDeckServiceId = registrationInfo.ServiceId;
                Console.WriteLine($"Connected to card deck service: {registrationInfo.ServiceName}");
                
                // If we were using an emergency deck, check if we want to switch back
                if (_currentDeckId == "emergency-local-deck" && _emergencyDeck != null)
                {
                    // Since we just connected to a real card deck service, make a note but don't switch automatically
                    // This avoids disruption in the middle of a hand
                    Console.WriteLine("Card deck service is now available, but continuing with emergency deck for current hand");
                    
                    // We'll create a new deck on the next hand instead
                }
                
                // Send a ping message to verify the service is fully operational
                Task.Run(async () => {
                    try 
                    {
                        var pingMessage = Message.Create(MessageType.Ping, DateTime.UtcNow.ToString());
                        pingMessage.MessageId = Guid.NewGuid().ToString();
                        
                        bool pingResponse = await PokerGame.Core.Messaging.MessageBrokerExtensions.SendWithAcknowledgmentAsync(
                            this,
                            pingMessage,
                            registrationInfo.ServiceId,
                            timeoutMs: 2000,
                            maxRetries: 1,
                            useExponentialBackoff: false);
                            
                        if (pingResponse)
                        {
                            Console.WriteLine($"Card deck service {registrationInfo.ServiceId} responded to ping successfully");
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Card deck service {registrationInfo.ServiceId} did not respond to ping");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error pinging card deck service: {ex.Message}");
                    }
                });
            }
            // Track console UI services and respond by broadcasting again
            else if (registrationInfo.ServiceType == ServiceConstants.ServiceTypes.ConsoleUI || 
                    registrationInfo.ServiceType == "PlayerUI") // Support both naming conventions for UI
            {
                Console.WriteLine($"ConsoleUI service registered with ID: {registrationInfo.ServiceId}");
                
                // When a console UI registers, immediately broadcast our registration back
                // This helps with the bidirectional discovery
                Task.Run(() => {
                    try 
                    {
                        // First send a targeted registration directly to the console UI
                        SendTargetedRegistrationTo(registrationInfo.ServiceId);
                        
                        // Then also broadcast generally for redundancy
                        for (int i = 0; i < 3; i++) // Send multiple registration messages for redundancy
                        {
                            Console.WriteLine($"Broadcasting GameEngine registration to ConsoleUI service (attempt {i+1}/3)");
                            PublishServiceRegistration();
                            Thread.Sleep(300); // Small delay between broadcasts
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error broadcasting to ConsoleUI: {ex.Message}");
                    }
                });
            }
        }
        
        /// <summary>
        /// Sends a targeted registration message to a specific service ID through the central broker
        /// </summary>
        /// <param name="targetServiceId">The ID of the service to send registration to</param>
        private new void SendTargetedRegistrationTo(string targetServiceId)
        {
            try
            {
                Console.WriteLine($"Sending targeted registration to service ID: {targetServiceId} via central broker");
                
                // Create registration payload
                var payload = new ServiceRegistrationPayload
                {
                    ServiceId = _serviceId,
                    ServiceName = _serviceName,
                    ServiceType = ServiceConstants.ServiceTypes.GameEngine,
                    Endpoint = $"tcp://127.0.0.1:{_publisherPort}",
                    Capabilities = GetServiceCapabilities()
                };
                
                // Create and send the message
                var registrationMessage = Message.Create(MessageType.ServiceRegistration, payload);
                registrationMessage.SenderId = _serviceId;
                registrationMessage.ReceiverId = targetServiceId;
                registrationMessage.MessageId = Guid.NewGuid().ToString();
                
                // Log the attempt for debugging
                Console.WriteLine($"====> [GameEngine {_serviceId}] Sending registration via central broker to {targetServiceId}");
                
                // Route through the central message broker instead of direct connection
                Broadcast(registrationMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending targeted registration: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Directly handle a service registration message
        /// </summary>
        /// <param name="message">The service registration message</param>
        public void HandleServiceRegistration(PokerGame.Core.Microservices.Message message)
        {
            if (message.Type == PokerGame.Core.Microservices.MessageType.ServiceRegistration)
            {
                var payload = message.GetPayload<ServiceRegistrationPayload>();
                if (payload != null)
                {
                    // Use our standard registration handler to keep logic consistent
                    OnServiceRegistered(payload);
                    Console.WriteLine($"Directly registered service: {payload.ServiceName} (ID: {payload.ServiceId}, Type: {payload.ServiceType})");
                }
            }
        }
        
        /// <summary>
        /// Converts between NetworkMessage and Microservice Message types
        /// </summary>
        private PokerGame.Core.Microservices.Message ConvertToMicroserviceMessage(PokerGame.Core.Messaging.NetworkMessage networkMessage)
        {
            var microserviceMessage = new PokerGame.Core.Microservices.Message
            {
                MessageId = networkMessage.MessageId,
                SenderId = networkMessage.SenderId,
                ReceiverId = networkMessage.ReceiverId,
                InResponseTo = networkMessage.InResponseTo,
                Payload = networkMessage.Payload
            };
            
            // Set the message type appropriately based on a string mapping
            if (Enum.TryParse<PokerGame.Core.Microservices.MessageType>(
                networkMessage.Type.ToString(), out var msgType))
            {
                microserviceMessage.Type = msgType;
            }
            
            return microserviceMessage;
        }
        
        /// <summary>
        /// Gets a list of service IDs for services of the specified type
        /// </summary>
        /// <param name="serviceType">The type of service to find</param>
        /// <returns>A list of service IDs</returns>
        private new List<string> GetServicesOfType(string serviceType)
        {
            List<string> services = new List<string>();
            
            foreach (var pair in _knownServices)
            {
                if (pair.Value.ServiceType == serviceType)
                {
                    services.Add(pair.Key);
                }
            }
            
            return services;
        }
        
        /// <summary>
        /// Handles messages received from other microservices
        /// </summary>
        /// <param name="message">The message to handle</param>
        public override async Task HandleMessageAsync(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            
            // Call our object-based implementation to handle the message
            await HandleMessageAsync((object)message);
        }

/// <summary>
/// Handles messages received from other microservices via the IGameEngineService interface
/// </summary>
/// <param name="messageObj">The message to handle</param>

/// <summary>
/// Adds a player to the game
/// </summary>
/// <param name="player">The player to add</param>
public void AddPlayer(object player)
{
    if (player is Models.Player typedPlayer)
    {
        // PokerGameEngine doesn't have AddPlayer directly, we need to add manually to the player list
        Console.WriteLine($"Adding player {typedPlayer.Name} with {typedPlayer.Chips} chips");
        
        // Get the private _players field via reflection
        var playersField = typeof(PokerGameEngine).GetField("_players", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (playersField != null)
        {
            var playersList = playersField.GetValue(_gameEngine) as List<Models.Player>;
            if (playersList != null)
            {
                // Make sure the player isn't already in the list
                if (!playersList.Any(p => p.Id == typedPlayer.Id))
                {
                    playersList.Add(typedPlayer);
                    Console.WriteLine($"Player {typedPlayer.Name} added successfully");
                }
                else
                {
                    Console.WriteLine($"Player {typedPlayer.Name} already exists in the game");
                }
            }
            else
            {
                Console.WriteLine("Could not access player list from game engine");
            }
        }
        else
        {
            Console.WriteLine("Could not find _players field in PokerGameEngine");
        }
    }
    else
    {
        Console.WriteLine($"Error: AddPlayer called with invalid player type: {player?.GetType().Name ?? "null"}");
    }
}

/// <summary>
/// Removes a player from the game
/// </summary>
/// <param name="playerId">The ID of the player to remove</param>
public void RemovePlayer(string playerId)
{
    // PokerGameEngine doesn't have RemovePlayer directly, we need to remove manually from player list
    Console.WriteLine($"Removing player with ID: {playerId}");
    
    // Get the private _players field via reflection
    var playersField = typeof(PokerGameEngine).GetField("_players", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    if (playersField != null)
    {
        var playersList = playersField.GetValue(_gameEngine) as List<Models.Player>;
        if (playersList != null)
        {
            // Find and remove the player
            var playerToRemove = playersList.FirstOrDefault(p => p.Id == playerId);
            if (playerToRemove != null)
            {
                playersList.Remove(playerToRemove);
                Console.WriteLine($"Player {playerToRemove.Name} removed successfully");
            }
            else
            {
                Console.WriteLine($"Player with ID {playerId} not found in game");
            }
        }
        else
        {
            Console.WriteLine("Could not access player list from game engine");
        }
    }
    else
    {
        Console.WriteLine("Could not find _players field in PokerGameEngine");
    }
}

/// <summary>
/// Starts a new hand
/// </summary>
public async Task StartHandAsync()
{
    Console.WriteLine("StartHandAsync called from IGameEngineService interface");
    
    // Create a StartHand message and handle it
    var startHandMessage = Message.Create(MessageType.StartHand);
    
    // Use our override implementation to process the message
    await base.HandleMessageAsync(startHandMessage);
}

/// <summary>
/// Processes a player action
/// </summary>
/// <param name="playerId">The ID of the player</param>
/// <param name="action">The action to process</param>
/// <param name="amount">The amount of the action</param>
/// <returns>True if the action was processed successfully; otherwise, false</returns>
public async Task<bool> ProcessPlayerActionAsync(string playerId, string action, int amount)
{
    Console.WriteLine($"ProcessPlayerActionAsync called for player {playerId} with action {action} and amount {amount}");
    
    // Create an action payload
    var payload = new PlayerActionPayload
    {
        PlayerId = playerId,
        ActionType = action,
        BetAmount = amount
    };
    
    // Create and handle the player action message
    var actionMessage = Message.Create(MessageType.PlayerAction, payload);
    
    // Process the action using our base class method
    await base.HandleMessageAsync(actionMessage);
    
    // For now, assume success
    return true;
}

/// <summary>
        /// Handles messages received from other services - implementation of IGameEngineService interface
        /// </summary>
        /// <param name="messageObj">The message to handle</param>
        public async Task HandleMessageAsync(object messageObj)
        {
            if (messageObj == null)
            {
                throw new ArgumentNullException(nameof(messageObj));
            }
            
            if (messageObj is Message message)
            {
                // Specially handle discovery messages to respond aggressively
                if (message.Type == MessageType.ServiceDiscovery)
                {
                    Console.WriteLine($"Received service discovery message from {message.SenderId}");
                    
                    // Send a targeted registration directly to the requesting service
                    if (!string.IsNullOrEmpty(message.SenderId))
                    {
                        Console.WriteLine($"Responding with targeted registration to {message.SenderId}");
                        SendTargetedRegistrationTo(message.SenderId);
                        
                        // Send multiple times for reliability
                        _ = Task.Run(async () => {
                            for (int i = 0; i < 3; i++)
                            {
                                await Task.Delay(200 * (i + 1));
                                Console.WriteLine($"Sending delayed targeted registration attempt {i+1} to {message.SenderId}");
                                SendTargetedRegistrationTo(message.SenderId);
                            }
                        });
                    }
                    
                    // Also broadcast our registration for anyone else who might be listening
                    Console.WriteLine("Also broadcasting general registration");
                    PublishServiceRegistration();
                    
                    // Send multiple broadcasts for reliability
                    _ = Task.Run(async () => {
                        for (int i = 0; i < 2; i++)
                        {
                            await Task.Delay(300 * (i + 1));
                            Console.WriteLine($"Sending delayed broadcast registration attempt {i+1}");
                            PublishServiceRegistration();
                        }
                    });
                }
                
                await HandleMessageInternalAsync(message);
            }
            else
            {
                Console.WriteLine($"Error: HandleMessageAsync called with invalid message type: {messageObj.GetType().Name}");
            }
        }
        
        public async Task HandleMessageInternalAsync(Message message)
        {
            
            switch (message.Type)
            {
                case MessageType.ServiceDiscovery:
                    // Already handled in HandleMessageAsync, but we'll provide extra logging here
                    Console.WriteLine($"Processing service discovery message from {message.SenderId} in HandleMessageInternalAsync");
                    // No need to do additional processing as the main handler already sent registrations
                    break;
                    
                case MessageType.StartGame:
                    var playerNames = message.GetPayload<string[]>();
                    if (playerNames != null && playerNames.Length >= 2)
                    {
                        Console.WriteLine($"Starting game with {playerNames.Length} players: {string.Join(", ", playerNames)}");
                        
                        // First make sure the player list is clean
                        foreach (var field in typeof(PokerGameEngine).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (field.Name == "_players")
                            {
                                var playerList = field.GetValue(_gameEngine) as List<Player>;
                                if (playerList != null) 
                                {
                                    playerList.Clear();
                                    Console.WriteLine($"Cleared existing player list");
                                }
                                break;
                            }
                        }
                        
                        _gameEngine.StartGame(playerNames);
                        
                        // Verify players were added correctly
                        Console.WriteLine($"Players after StartGame: {_gameEngine.Players.Count}");
                        foreach (var player in _gameEngine.Players)
                        {
                            Console.WriteLine($"- Player: {player.Name} (Chips: {player.Chips})");
                        }
                        
                        // Create a new deck for the game
                        await CreateNewDeckAsync();
                        
                        // Force a state transition to Setup if still in WaitingToStart
                        if (_gameEngine.State == Game.GameState.WaitingToStart)
                        {
                            Console.WriteLine("Changing state from WaitingToStart to Setup");
                            // We can't set the state directly, so we'll use reflection
                            typeof(PokerGameEngine).GetField("_gameState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_gameEngine, Game.GameState.Setup);
                        }
                        
                        Console.WriteLine("Game started. Game state: " + _gameEngine.State);
                        BroadcastGameState();
                    }
                    else
                    {
                        Console.WriteLine("Invalid player names received");
                    }
                    break;
                    
                case MessageType.StartHand:
                    // ULTRA HIGH VISIBILITY MARKER - This helps debug message routing issues
                    Console.WriteLine("\n\n");
                    Console.WriteLine("##########################################################");
                    Console.WriteLine("#                                                        #");
                    Console.WriteLine("#             GAME ENGINE RECEIVED STARTHAND             #");
                    Console.WriteLine("#                                                        #");
                    Console.WriteLine("#                 MARKED: V3 (FIXED)                     #");
                    Console.WriteLine("#                                                        #");
                    Console.WriteLine("##########################################################");
                    Console.WriteLine($"# GAME ENGINE: Received StartHand message (ID: {message.MessageId})");
                    Console.WriteLine($"# From: {message.SenderId}");
                    Console.WriteLine($"# To: {message.ReceiverId ?? "broadcast"}");
                    Console.WriteLine($"# Time: {DateTime.Now.ToString("HH:mm:ss.fff")}");
                    Console.WriteLine("##########################################################");
                    Console.WriteLine("\n\n");
                    
                    // Log to standard debug and message trace files
                    PokerGame.Core.Logging.FileLogger.Debug("GameEngine", 
                        $"!!!! VERY IMPORTANT !!!! RECEIVED STARTHAND MESSAGE - ID: {message.MessageId}, From: {message.SenderId}, Time: {DateTime.Now.ToString("HH:mm:ss.fff")}");
                    
                    PokerGame.Core.Logging.FileLogger.MessageTrace("GameEngine", 
                        $"####### RECEIVED STARTHAND MESSAGE - ID: {message.MessageId}, From: {message.SenderId}, Time: {DateTime.Now.ToString("HH:mm:ss.fff")} #######");
                    
                    // Log to the file system too
                    PokerGame.Core.Logging.FileLogger.MessageTrace("GameEngine", 
                        $"RECEIVED STARTHAND - From: {message.SenderId}, MessageID: {message.MessageId}");
                    
                    // FIRST, send an immediate acknowledgment before doing any processing
                    // This is critical for reliable communication
                    Console.WriteLine("\n\n");
                    Console.WriteLine("**********************************************************");
                    Console.WriteLine("*                                                        *");
                    Console.WriteLine("*       IMMEDIATELY SENDING STARTHAND ACKNOWLEDGMENT     *");
                    Console.WriteLine("*                                                        *");
                    Console.WriteLine("**********************************************************");
                    Console.WriteLine("\n\n");
                    
                    // Create acknowledgment message
                    var ackMessage = Message.Create(MessageType.Acknowledgment);
                    ackMessage.InResponseTo = message.MessageId;
                    ackMessage.SenderId = _serviceId;
                    ackMessage.ReceiverId = message.SenderId;
                    
                    // Log details of the acknowledgment message
                    Console.WriteLine($"ACK Message ID: {ackMessage.MessageId}");
                    Console.WriteLine($"ACK In Response To: {ackMessage.InResponseTo}");
                    Console.WriteLine($"ACK From: {ackMessage.SenderId}");
                    Console.WriteLine($"ACK To: {ackMessage.ReceiverId}");
                    
                    try {
                        // Send using all available methods for maximum reliability
                        Console.WriteLine("1. Direct sending acknowledgment to original sender");
                        SendTo(ackMessage, message.SenderId);
                        
                        Console.WriteLine("2. Broadcasting acknowledgment as backup");
                        Broadcast(ackMessage);
                        
                        Console.WriteLine("3. Sending explicit generic response");
                        // Also send a GenericResponse message for triple redundancy
                        var genericResponseMessage = Message.Create(MessageType.GenericResponse);
                        genericResponseMessage.InResponseTo = message.MessageId;
                        genericResponseMessage.SenderId = _serviceId;
                        genericResponseMessage.ReceiverId = message.SenderId;
                        
                        // Create a response payload
                        var genericResponsePayload = new GenericResponsePayload
                        {
                            Success = true,
                            OriginalMessageType = MessageType.StartHand,
                            Message = $"StartHand message {message.MessageId} received successfully",
                            ResponseType = "StartHandAcknowledgment", 
                            OriginalMessageId = message.MessageId
                        };
                        genericResponseMessage.SetPayload(genericResponsePayload);
                        
                        // Send direct and broadcast
                        Console.WriteLine($"Sending GenericResponse with ID {genericResponseMessage.MessageId}");
                        SendTo(genericResponseMessage, message.SenderId);
                        Broadcast(genericResponseMessage);
                        
                        // Log all acknowledgments
                        PokerGame.Core.Logging.FileLogger.MessageTrace("GameEngine", 
                            $"SENT ACKNOWLEDGMENT - For: {message.MessageId}, To: {message.SenderId}, AckID: {ackMessage.MessageId}");
                        PokerGame.Core.Logging.FileLogger.MessageTrace("GameEngine", 
                            $"SENT GENERIC RESPONSE - For: {message.MessageId}, To: {message.SenderId}, ResponseID: {genericResponseMessage.MessageId}");
                        
                        // NEW ADDITION - SEND DECK SHUFFLED MESSAGE DIRECTLY
                        // This ensures the game flow continues properly
                        Console.WriteLine("\n");
                        Console.WriteLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
                        Console.WriteLine("@                                                         @");
                        Console.WriteLine("@        SENDING DIRECT DECKSHUFFLED RESPONSE            @");
                        Console.WriteLine("@                                                         @");
                        Console.WriteLine("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
                        Console.WriteLine("\n");
                        
                        // Create a DeckShuffled response using NetworkMessage to avoid conversion issues
                        var deckShuffledResponse = new NetworkMessage
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            Type = MessageType.DeckShuffled,  // Use explicit type
                            SenderId = _serviceId,
                            ReceiverId = message.SenderId,
                            InResponseTo = message.MessageId,
                            Timestamp = DateTime.UtcNow,
                            Headers = new Dictionary<string, string>
                            {
                                { "OriginalMessageId", message.MessageId },
                                { "ResponseType", "DeckShuffled" }
                            }
                        };
                        
                        // Send through the central broker directly for maximum reliability
                        Console.WriteLine($"Publishing DeckShuffled response directly through CentralMessageBroker");
                        Console.WriteLine($"DeckShuffled Message ID: {deckShuffledResponse.MessageId}");
                        Console.WriteLine($"In Response To: {deckShuffledResponse.InResponseTo}");
                        Console.WriteLine($"From: {deckShuffledResponse.SenderId}");
                        Console.WriteLine($"To: {deckShuffledResponse.ReceiverId}");
                        
                        // Publish via central broker
                        BrokerManager.Instance.CentralBroker?.Publish(deckShuffledResponse);
                        
                        // Log the DeckShuffled message
                        PokerGame.Core.Logging.FileLogger.MessageTrace("GameEngine", 
                            $"SENT DECKSHUFFLED - For: {message.MessageId}, To: {message.SenderId}, ID: {deckShuffledResponse.MessageId}");
                    }
                    catch (Exception ex) {
                        Console.WriteLine($"ERROR sending acknowledgment: {ex.Message}");
                        PokerGame.Core.Logging.FileLogger.Error("GameEngine", 
                            $"FAILED TO SEND ACKNOWLEDGMENT: {ex.Message}");
                    }
                    
                    try
                    {
                        // Ensure we have a card deck service with retry logic
                        int maxRetries = 3;
                        int currentRetry = 0;
                        bool cardDeckServiceFound = false;
                        
                        while (!cardDeckServiceFound && currentRetry < maxRetries)
                        {
                            currentRetry++;
                            Console.WriteLine($"Locating card deck service (Attempt {currentRetry}/{maxRetries})");
                            
                            if (_cardDeckServiceId != null && _knownServices.ContainsKey(_cardDeckServiceId))
                            {
                                cardDeckServiceFound = true;
                                Console.WriteLine($"Using known card deck service: {_cardDeckServiceId}");
                            }
                            else
                            {
                                // Try to find the service in registry
                                var cardDeckServices = GetServicesOfType("CardDeck");
                                if (cardDeckServices.Count > 0)
                                {
                                    _cardDeckServiceId = cardDeckServices[0];
                                    cardDeckServiceFound = true;
                                    Console.WriteLine($"Found card deck service with ID: {_cardDeckServiceId}");
                                }
                                else
                                {
                                    // Wait a bit before retrying
                                    Console.WriteLine("Card deck service not found, waiting before retry...");
                                    await Task.Delay(500);
                                }
                            }
                        }
                        
                        if (!cardDeckServiceFound)
                        {
                            Console.WriteLine("ERROR: Cannot find card deck service. Hand cannot be started.");
                            BroadcastGameState(); // Still broadcast current state
                            break;
                        }
                        
                        // Now check if we have a deck
                        if (!string.IsNullOrEmpty(_currentDeckId))
                        {
                            Console.WriteLine($"Using existing deck: {_currentDeckId}");
                            // Shuffle the deck before starting a new hand
                            await ShuffleDeckAsync();
                        }
                        else
                        {
                            Console.WriteLine("No deck ID found, creating a new deck");
                            // If we don't have a deck, create one
                            await CreateNewDeckAsync();
                            Console.WriteLine($"Created new deck with ID: {_currentDeckId}");
                        }
                        
                        // Ensure we have test players if needed
                        if (_gameEngine.Players.Count < 2)
                        {
                            Console.WriteLine("ERROR: Need at least 2 players to start a hand. Creating default players for testing.");
                            // Use reflection to add default players for testing
                            foreach (var field in typeof(PokerGameEngine).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                            {
                                if (field.Name == "_players")
                                {
                                    var playerList = field.GetValue(_gameEngine) as List<Player>;
                                    if (playerList != null && playerList.Count == 0) 
                                    {
                                        playerList.Add(new Player { Name = "Test Player 1", Chips = 1000 });
                                        playerList.Add(new Player { Name = "Test Player 2", Chips = 1000 });
                                        playerList.Add(new Player { Name = "Test Player 3", Chips = 1000 });
                                        Console.WriteLine($"Added {playerList.Count} test players");
                                    }
                                    break;
                                }
                            }
                        }
                        
                        // Deal hole cards to players (with longer wait time for deck creation)
                        Console.WriteLine("Waiting a moment for deck creation to complete...");
                        await Task.Delay(1000);
                        
                        Console.WriteLine("Dealing hole cards to players");
                        await DealCardsToPlayersAsync();
                        
                        // Add another delay to ensure cards are dealt
                        await Task.Delay(500);
                        
                        // Ensure we start the hand if not already in progress
                        if (_gameEngine.State == Game.GameState.Setup || _gameEngine.State == Game.GameState.WaitingToStart)
                        {
                            // Verify that we have enough players before starting
                            Console.WriteLine($"Starting hand with {_gameEngine.Players.Count} players (current state: {_gameEngine.State})");
                            
                            try {
                                // Try normal hand start
                                _gameEngine.StartHand();
                                
                                // Check if we changed state
                                if (_gameEngine.State == Game.GameState.Setup || _gameEngine.State == Game.GameState.WaitingToStart)
                                {
                                    // If not, force PreFlop state via reflection
                                    Console.WriteLine("Failed to transition state, forcing PreFlop state");
                                    typeof(PokerGameEngine).GetField("_gameState", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(_gameEngine, Game.GameState.PreFlop);
                                    
                                    // Need to set up blinds and first player if forcing state
                                    int dealerPos = 0;
                                    int smallBlindPos = (dealerPos + 1) % _gameEngine.Players.Count;
                                    int bigBlindPos = (dealerPos + 2) % _gameEngine.Players.Count;
                                    
                                    // Force current player to be after big blind
                                    typeof(PokerGameEngine).GetField("_currentPlayerIndex", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(_gameEngine, (bigBlindPos + 1) % _gameEngine.Players.Count);
                                    
                                    // Send notification about the forced state change
                                    var notificationPayload = new NotificationPayload
                                    {
                                        Message = "Game state was manually forced to PreFlop",
                                        Level = "warning"
                                    };
                                    var notification = Message.Create(MessageType.Notification, notificationPayload);
                                    Broadcast(notification);
                                }
                                
                                Console.WriteLine($"Hand started successfully. State is now: {_gameEngine.State}");
                            }
                            catch (Exception ex) {
                                Console.WriteLine("Error starting hand: " + ex.Message);
                                Console.WriteLine(ex.StackTrace);
                                
                                // Force state change
                                Console.WriteLine("Forcing state change after error");
                                typeof(PokerGameEngine).GetField("_gameState", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(_gameEngine, Game.GameState.PreFlop);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Unexpected error in StartHand handler: {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
                    }
                    
                    Console.WriteLine("New game state: " + _gameEngine.State);
                    
                    // Always broadcast the current state after processing
                    BroadcastGameState();
                    
                    // Send a direct response to the sender
                    Console.WriteLine("\n\n");
                    Console.WriteLine("**********************************************************");
                    Console.WriteLine("*                                                        *");
                    Console.WriteLine("*          GAME ENGINE SENDING STARTHAND RESPONSE        *");
                    Console.WriteLine("*                                                        *");
                    Console.WriteLine("**********************************************************");
                    Console.WriteLine("\n\n");
                    
                    var responseMessage = Message.Create(MessageType.GenericResponse);
                    
                    // Set message ID to ensure uniqueness
                    responseMessage.MessageId = Guid.NewGuid().ToString();
                    
                    // Set in response to property for tracking the relationship
                    responseMessage.InResponseTo = message.MessageId;
                    
                    // Set sender/receiver IDs for more reliable delivery
                    responseMessage.SenderId = _serviceId;
                    if (!string.IsNullOrEmpty(message.SenderId))
                    {
                        responseMessage.ReceiverId = message.SenderId;
                    }
                    
                    // Make sure the sender ID is set to our service ID
                    responseMessage.SenderId = _serviceId;
                    
                    // IMPORTANT: Set the receiver ID to the original sender
                    responseMessage.ReceiverId = message.SenderId;
                    
                    Console.WriteLine($"Original message ID: {message.MessageId}");
                    Console.WriteLine($"Original sender ID: {message.SenderId}");
                    Console.WriteLine($"Response message ID: {responseMessage.MessageId}");
                    Console.WriteLine($"Response sender ID: {responseMessage.SenderId}");
                    Console.WriteLine($"Response receiver ID: {responseMessage.ReceiverId}");
                    Console.WriteLine($"Response in response to: {responseMessage.InResponseTo}");
                    
                    // Super visible debug message
                    Console.WriteLine("\n\n");
                    Console.WriteLine("**********************************************************");
                    Console.WriteLine("*                                                        *");
                    Console.WriteLine("*  GAME ENGINE SENDING STARTHAND RESPONSE TO CONSOLE UI  *");
                    Console.WriteLine("*                                                        *");
                    Console.WriteLine("**********************************************************");
                    Console.WriteLine("\n\n");
                    
                    // Log to file for debugging - no need to specify path with enhanced FileLogger
                    PokerGame.Core.Logging.FileLogger.MessageTrace("GameEngine", 
                        $"SENDING StartHand RESPONSE - Original: {message.MessageId}, Response: {responseMessage.MessageId}, To: {message.SenderId}");
                    
                    // Also echo to console with more visibility
                    Console.WriteLine($">>>>>> MESSAGE TRACE: [GameEngine] SENDING StartHand RESPONSE - Original: {message.MessageId}, Response: {responseMessage.MessageId}, To: {message.SenderId} <<<<<<");
                    
                    // First, send a direct acknowledgment for the original message
                    // This is extremely important for reliable communication
                    Console.WriteLine("\n\n");
                    Console.WriteLine("**********************************************************");
                    Console.WriteLine("*                                                        *");
                    Console.WriteLine("*    GAME ENGINE SENDING DIRECT ACKNOWLEDGMENT FIRST     *");
                    Console.WriteLine("*                                                        *");
                    Console.WriteLine("**********************************************************");
                    Console.WriteLine("\n\n");
                    
                    // Create explicit acknowledgment message for the response
                    var responseAckMessage = Message.Create(MessageType.Acknowledgment);
                    responseAckMessage.SenderId = _serviceId;
                    responseAckMessage.ReceiverId = message.SenderId;
                    responseAckMessage.InResponseTo = message.MessageId;
                    
                    // Log details of response acknowledgment
                    Console.WriteLine($"RESPONSE ACK Message ID: {responseAckMessage.MessageId}");
                    Console.WriteLine($"RESPONSE ACK In Response To: {responseAckMessage.InResponseTo}");
                    Console.WriteLine($"RESPONSE ACK From: {responseAckMessage.SenderId}");
                    Console.WriteLine($"RESPONSE ACK To: {responseAckMessage.ReceiverId}");
                    
                    try {
                        // Send using multiple delivery methods for redundancy
                        Console.WriteLine("1. Direct sending RESPONSE acknowledgment");
                        SendTo(responseAckMessage, message.SenderId);
                        
                        Console.WriteLine("2. Broadcasting RESPONSE acknowledgment as backup");
                        Broadcast(responseAckMessage);
                        
                        // Log the acknowledgment to trace log
                        PokerGame.Core.Logging.FileLogger.MessageTrace("GameEngine", 
                            $"SENT RESPONSE ACKNOWLEDGMENT - For: {message.MessageId}, To: {message.SenderId}, AckID: {responseAckMessage.MessageId}");
                    }
                    catch (Exception ex) {
                        Console.WriteLine($"ERROR sending response acknowledgment: {ex.Message}");
                        PokerGame.Core.Logging.FileLogger.Error("GameEngine", 
                            $"FAILED TO SEND RESPONSE ACKNOWLEDGMENT: {ex.Message}");
                    }
                        
                    // Add small delay after acknowledgment to ensure it's processed first
                    await Task.Delay(100);
                    
                    // Now set up the main response payload
                    var mainResponsePayload = new GenericResponsePayload
                    {
                        Success = true,
                        OriginalMessageType = MessageType.StartHand,
                        Message = $"Hand started successfully. Current state: {_gameEngine.State}"
                    };
                    responseMessage.SetPayload(mainResponsePayload);
                    
                    PokerGame.Core.Logging.FileLogger.MessageTrace("GameEngine", 
                        $"Response payload: Success={mainResponsePayload.Success}, Message={mainResponsePayload.Message}");
                        
                    Console.WriteLine($"Response payload set: Success={mainResponsePayload.Success}, Message={mainResponsePayload.Message}");
                    
                    if (!string.IsNullOrEmpty(message.SenderId))
                    {
                        Console.WriteLine($"Sending StartHand response directly to {message.SenderId}");
                        Console.WriteLine($"RESPONSE PAYLOAD: {responseMessage.GetPayload<GenericResponsePayload>()?.Message}");
                        
                        // Always use MicroserviceBase's SendTo method which routes through CentralMessageBroker
                        Console.WriteLine("Sending StartHand response via MicroserviceBase.SendTo");
                        
                        // This will ensure the response is routed through CentralMessageBroker
                        SendTo(responseMessage, message.SenderId);
                        
                        Console.WriteLine("StartHand response sent via CentralMessageBroker!");
                        
                        // Log the successful sending
                        PokerGame.Core.Logging.FileLogger.MessageTrace("GameEngine", 
                            $"StartHand response sent to {message.SenderId} - MessageID: {responseMessage.MessageId}, InResponseTo: {responseMessage.InResponseTo}");
                        
                        Console.WriteLine("StartHand response sent using multiple delivery methods!");
                    }
                    else
                    {
                        Console.WriteLine("WARNING: Cannot send targeted StartHand response - sender ID is missing");
                        Console.WriteLine("Falling back to broadcast-only for StartHand response");
                        Broadcast(responseMessage); // Fallback to broadcast
                        Console.WriteLine("StartHand response broadcasted!");
                    }
                    Console.WriteLine("================================================");
                    break;
                    
                case MessageType.PlayerAction:
                    Console.WriteLine($"Received PlayerAction message from {message.SenderId}");
                    
                    var actionPayload = message.GetPayload<PlayerActionPayload>();
                    if (actionPayload != null)
                    {
                        Console.WriteLine($"Processing player action: {actionPayload.ActionType} from player {actionPayload.PlayerId}");
                        
                        // Force updating of active player if needed
                        try
                        {
                            // Ensure we're in a proper state to process actions
                            if (_gameEngine.State == Game.GameState.Setup)
                            {
                                Console.WriteLine("Forcing game state to PreFlop before processing action");
                                typeof(PokerGameEngine).GetField("_gameState", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(_gameEngine, Game.GameState.PreFlop);
                            }
                            
                            // Process the action
                            Console.WriteLine($"Processing action: {actionPayload.ActionType} with amount: {actionPayload.BetAmount}");
                            _gameEngine.ProcessPlayerAction(actionPayload.ActionType, actionPayload.BetAmount);
                            
                            // Log the state after action
                            Console.WriteLine($"Game state after action: {_gameEngine.State}");
                            
                            // If we need to deal community cards after this action
                            if (_gameEngine.State == Game.GameState.Flop)
                            {
                                Console.WriteLine("Dealing FLOP cards");
                                await DealCommunityCardsAsync(3); // Flop
                            }
                            else if (_gameEngine.State == Game.GameState.Turn)
                            {
                                Console.WriteLine("Dealing TURN card");
                                await DealCommunityCardsAsync(1); // Turn
                            }
                            else if (_gameEngine.State == Game.GameState.River)
                            {
                                Console.WriteLine("Dealing RIVER card");
                                await DealCommunityCardsAsync(1); // River
                            }
                            
                            // Make sure to update the game state to all clients
                            Console.WriteLine("Broadcasting game state after player action");
                            BroadcastGameState();
                            
                            // Send a response back to the UI through CentralMessageBroker
                            var actionResponseMessage = Message.Create(MessageType.ActionResponse);
                            actionResponseMessage.SetPayload(new ActionResponsePayload
                            {
                                Success = true,
                                ActionType = actionPayload.ActionType,
                                Message = $"Action {actionPayload.ActionType} processed successfully"
                            });
                            // Set the response as being in response to the original message
                            actionResponseMessage.InResponseTo = message.MessageId;
                            // Use SendTo which now properly routes through CentralMessageBroker
                            SendTo(actionResponseMessage, message.SenderId);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing player action: {ex.Message}");
                            Console.WriteLine(ex.StackTrace);
                            
                            // Send failure response through CentralMessageBroker
                            var actionErrorResponseMessage = Message.Create(MessageType.ActionResponse);
                            actionErrorResponseMessage.SetPayload(new ActionResponsePayload
                            {
                                Success = false,
                                ActionType = actionPayload.ActionType,
                                Message = $"Error: {ex.Message}"
                            });
                            // Set the response as being in response to the original message
                            actionErrorResponseMessage.InResponseTo = message.MessageId;
                            // Use SendTo which now properly routes through CentralMessageBroker
                            SendTo(actionErrorResponseMessage, message.SenderId);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Received PlayerAction message with null payload");
                    }
                    break;
                    
                case MessageType.DeckDealResponse:
                    var dealResponse = message.GetPayload<DeckDealResponsePayload>();
                    if (dealResponse != null)
                    {
                        HandleDealtCards(dealResponse.Cards);
                    }
                    break;
                   
                case MessageType.DeckCreated:
                    Console.WriteLine("Received DeckCreated confirmation message");
                    var deckCreatedPayload = message.GetPayload<DeckStatusPayload>();
                    if (deckCreatedPayload != null && deckCreatedPayload.Success)
                    {
                        Console.WriteLine($"Deck {deckCreatedPayload.DeckId} was created successfully");
                        
                        // If this is the current deck we're using, proceed with the game
                        if (deckCreatedPayload.DeckId == _currentDeckId)
                        {
                            // Now we can deal cards
                            Console.WriteLine("Proceeding with dealing cards now that deck is confirmed");
                            await DealCardsToPlayersAsync();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to create deck according to confirmation message");
                    }
                    break;
                
                case MessageType.Acknowledgment:
                    Console.WriteLine("\n\n");
                    Console.WriteLine("**********************************************************");
                    Console.WriteLine("*                                                        *");
                    Console.WriteLine("*         GAME ENGINE RECEIVED ACKNOWLEDGMENT            *");
                    Console.WriteLine("*                                                        *");
                    Console.WriteLine("**********************************************************");
                    Console.WriteLine("\n\n");
                    
                    Console.WriteLine($"Acknowledgment message ID: {message.MessageId}");
                    Console.WriteLine($"In response to message: {message.InResponseTo}");
                    Console.WriteLine($"From: {message.SenderId}");
                    Console.WriteLine($"To: {message.ReceiverId ?? "broadcast"}");
                    
                    // Use FileLogger for better tracing
                    PokerGame.Core.Logging.FileLogger.MessageTrace("GameEngine", 
                        $"RECEIVED ACKNOWLEDGMENT - ID: {message.MessageId}, For: {message.InResponseTo}, From: {message.SenderId}");
                    
                    // Don't need to respond to acknowledgments - that would create an infinite loop
                    Console.WriteLine("Acknowledgment processed successfully.");
                    Console.WriteLine("================================================");
                    break;
                
                // Add more message handlers as needed
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// Creates a new deck for the current game
        /// </summary>
        private async Task CreateNewDeckAsync()
        {
            // Keep track of retries
            int maxRetries = 3;
            int currentRetry = 0;
            bool deckCreationSuccessful = false;
            
            while (!deckCreationSuccessful && currentRetry < maxRetries)
            {
                currentRetry++;
                Console.WriteLine($"Deck creation attempt {currentRetry} of {maxRetries}");
                
                // Check if card deck service is available, if not try to find it
                if (_cardDeckServiceId == null)
                {
                    Console.WriteLine("Card deck service not available, trying to find it...");
                    
                    // Search for deck service
                    foreach (var servicePair in _knownServices)
                    {
                        if (servicePair.Value.ServiceType == "CardDeck")
                        {
                            _cardDeckServiceId = servicePair.Key;
                            Console.WriteLine($"Found card deck service: {_cardDeckServiceId}");
                            break;
                        }
                    }
                    
                    if (_cardDeckServiceId == null)
                    {
                        Console.WriteLine("ERROR: Card deck service not found in registry");
                        
                        // Enhanced error diagnostics
                        Console.WriteLine("Available services:");
                        foreach (var service in _knownServices)
                        {
                            Console.WriteLine($"- {service.Value.ServiceName} (ID: {service.Key}, Type: {service.Value.ServiceType})");
                        }
                        
                        // Try direct type search as fallback
                        var deckServices = GetServicesOfType("CardDeck");
                        if (deckServices.Count > 0)
                        {
                            _cardDeckServiceId = deckServices[0];
                            Console.WriteLine($"Found card deck service via direct type search: {_cardDeckServiceId}");
                        }
                        else
                        {
                            Console.WriteLine("ERROR: No card deck services found by type. Will retry...");
                            
                            // Wait before retrying to give services time to register
                            await Task.Delay(1000);
                            continue;
                        }
                    }
                }
                
                // Generate a unique ID for this deck
                _currentDeckId = $"deck-{Guid.NewGuid().ToString().Substring(0, 8)}";
                
                Console.WriteLine($"Creating new deck with ID: {_currentDeckId} using reliable messaging");
                
                // Create a new deck and shuffle it
                var createPayload = new DeckCreatePayload
                {
                    DeckId = _currentDeckId,
                    Shuffle = true
                };
                
                var message = Message.Create(MessageType.DeckCreate, createPayload);
                message.MessageId = Guid.NewGuid().ToString(); // Ensure unique message ID
                
                // Use our enhanced extension method to send with acknowledgment
                Console.WriteLine($"Sending DeckCreate message to {_cardDeckServiceId} with reliable delivery");
                // Use exponential backoff and 5 retries for critical deck creation
                bool ackReceived = await PokerGame.Core.Messaging.MessageBrokerExtensions.SendWithAcknowledgmentAsync(
                    this, 
                    message, 
                    _cardDeckServiceId, 
                    timeoutMs: 5000,
                    maxRetries: 2, // Reduced retries for faster failover
                    useExponentialBackoff: true);
                
                if (!ackReceived)
                {
                    Console.WriteLine("Failed to receive acknowledgment for deck creation. Retries exhausted.");
                    
                    // If we've already tried a couple of times, go straight to emergency deck
                    if (currentRetry >= 1) // Changed from 2 to 1 for faster failover
                    {
                        Console.WriteLine("Immediate failover to emergency deck after first failure.");
                        break; // Exit the loop to create emergency deck immediately
                    }
                    
                    await Task.Delay(500); // Reduced delay for faster failover
                    continue;
                }
                
                // If we got here, deck creation was successful
                deckCreationSuccessful = true;
                Console.WriteLine($"Deck {_currentDeckId} successfully created and confirmed");
                
                // Broadcast a notification that we have a new deck
                var notificationMessage = Message.Create(MessageType.Notification, 
                    new NotificationPayload { Message = $"New deck created: {_currentDeckId}" });
                Broadcast(notificationMessage);
            }
            
            if (!deckCreationSuccessful)
            {
                Console.WriteLine($"ERROR: Failed to create deck after {maxRetries} attempts");
                
                // Create an emergency local deck in case the service is completely unavailable
                _currentDeckId = "emergency-local-deck";
                Console.WriteLine("Created emergency local deck as fallback");
                
                // Create and store the actual emergency deck in our service
                _emergencyDeck = new Models.Deck();
                _emergencyDeck.Shuffle();
                
                Console.WriteLine($"Emergency deck created with {_emergencyDeck.CardsRemaining} cards");
            }
            
            Console.WriteLine($"Finished deck creation process");
        }
        
        /// <summary>
        /// Shuffles the current deck with reliable messaging
        /// </summary>
        private async Task ShuffleDeckAsync()
        {
            if (_cardDeckServiceId == null || string.IsNullOrEmpty(_currentDeckId))
            {
                Console.WriteLine("Card deck service not available or no deck ID");
                return;
            }
            
            var shufflePayload = new DeckIdPayload
            {
                DeckId = _currentDeckId
            };
            
            var message = Message.Create(MessageType.DeckShuffle, shufflePayload);
            message.MessageId = Guid.NewGuid().ToString();
            
            Console.WriteLine($"Shuffling deck {_currentDeckId} with reliable messaging");
            
            // Use our enhanced reliable messaging system with fully qualified namespace
            bool ackReceived = await PokerGame.Core.Messaging.MessageBrokerExtensions.SendWithAcknowledgmentAsync(
                this, 
                message, 
                _cardDeckServiceId, 
                timeoutMs: 3000,
                maxRetries: 3,
                useExponentialBackoff: true);
            
            if (!ackReceived)
            {
                Console.WriteLine("Warning: Did not receive shuffle confirmation after retries");
                
                // If using emergency deck, just re-shuffle it locally
                if (_currentDeckId == "emergency-local-deck" && _emergencyDeck != null)
                {
                    Console.WriteLine("Re-shuffling emergency deck locally");
                    _emergencyDeck.Shuffle();
                    Console.WriteLine("Emergency deck shuffled");
                }
                else
                {
                    Console.WriteLine("Continuing without shuffle confirmation");
                }
            }
            else
            {
                Console.WriteLine($"Deck {_currentDeckId} successfully shuffled and confirmed");
            }
        }
        
        /// <summary>
        /// Deals hole cards to all players
        /// </summary>
        private async Task DealCardsToPlayersAsync()
        {
            // Check if we need to use the emergency deck
            if (_currentDeckId == "emergency-local-deck" && _emergencyDeck != null)
            {
                Console.WriteLine("Using emergency deck to deal cards to players");
                
                // Deal 2 cards to each player directly from emergency deck
                foreach (var player in _gameEngine.Players)
                {
                    if (player.HoleCards.Count < 2)
                    {
                        List<Card> cards = _emergencyDeck.DealCards(2);
                        player.HoleCards.AddRange(cards);
                        Console.WriteLine($"Dealt 2 cards to {player.Name} from emergency deck: {cards[0]} {cards[1]}");
                    }
                }
                
                // Notify that all players have hole cards
                if (_gameEngine.Players.All(p => p.HoleCards.Count == 2))
                {
                    Console.WriteLine("All players have hole cards from emergency deck, transitioning to PreFlop");
                    
                    // Force state transition
                    if (_gameEngine.State != Game.GameState.PreFlop)
                    {
                        try
                        {
                            typeof(PokerGameEngine).GetField("_gameState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                ?.SetValue(_gameEngine, Game.GameState.PreFlop);
                            Console.WriteLine($"Game state after force: {_gameEngine.State}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error forcing state change: {ex.Message}");
                        }
                    }
                }
                
                return;
            }
            
            // Normal operation with card deck service
            if (_cardDeckServiceId == null || string.IsNullOrEmpty(_currentDeckId))
            {
                Console.WriteLine("Card deck service not available or no deck ID");
                return;
            }
            
            // Deal 2 cards to each player
            foreach (var player in _gameEngine.Players)
            {
                // Request 2 cards from the deck service with reliable delivery
                var dealPayload = new DeckDealPayload
                {
                    DeckId = _currentDeckId,
                    Count = 2
                };
                
                var message = Message.Create(MessageType.DeckDeal, dealPayload);
                message.MessageId = Guid.NewGuid().ToString();
                
                // FAST FAILOVER: Check if we've had ANY problems with the deck service
                // and immediately use emergency deck for faster gameplay
                if (_emergencyDeck != null)
                {
                    // We've had problems before, so just use the emergency deck immediately
                    Console.WriteLine("Using existing emergency deck immediately for faster gameplay");
                    _currentDeckId = "emergency-local-deck";
                    
                    // Go back and use the emergency deck path
                    await Task.Delay(50); // Small delay before recursive call
                    await DealCardsToPlayersAsync();
                    return;
                }
                
                // Try with the card service, but only once with minimal timeout
                bool ackReceived = await PokerGame.Core.Messaging.MessageBrokerExtensions.SendWithAcknowledgmentAsync(
                    this, 
                    message, 
                    _cardDeckServiceId, 
                    timeoutMs: 1500, // Reduced timeout for faster failover
                    maxRetries: 0,   // No retries for immediate failover
                    useExponentialBackoff: false);
                
                if (!ackReceived)
                {
                    Console.WriteLine("Warning: Failed to get acknowledgment for dealing hole cards");
                    
                    // Fall back to emergency deck immediately if available
                    if (_emergencyDeck == null)
                    {
                        // Create an emergency deck if we don't have one yet
                        Console.WriteLine("Creating emergency deck for hole cards");
                        _emergencyDeck = new Models.Deck();
                        _emergencyDeck.Shuffle();
                        _currentDeckId = "emergency-local-deck";
                        
                        // Recursively call this method now that we have an emergency deck
                        await DealCardsToPlayersAsync();
                        return;
                    }
                    else if (_currentDeckId != "emergency-local-deck")
                    {
                        // Use the existing emergency deck
                        _currentDeckId = "emergency-local-deck";
                        await DealCardsToPlayersAsync();
                        return;
                    }
                }
                
                // Small delay between successful deals
                await Task.Delay(50);
            }
        }
        
        /// <summary>
        /// Deals community cards to the table
        /// </summary>
        /// <param name="count">Number of cards to deal</param>
        private async Task DealCommunityCardsAsync(int count)
        {
            // Check if we need to use the emergency deck
            if (_currentDeckId == "emergency-local-deck" && _emergencyDeck != null)
            {
                Console.WriteLine($"Using emergency deck to deal {count} community cards");
                
                // Burn card if needed (for flop, turn, or river)
                if (count == 3 || count == 1)
                {
                    _emergencyDeck.DealCard(); // Burn a card
                    Console.WriteLine("Burned a card from emergency deck");
                }
                
                // Deal community cards directly from emergency deck
                List<Card> cards = _emergencyDeck.DealCards(count);
                foreach (var card in cards)
                {
                    _gameEngine.AddCommunityCard(card);
                    Console.WriteLine($"Added community card from emergency deck: {card}");
                }
                
                // Broadcast updated game state
                BroadcastGameState();
                return;
            }
            
            // Normal operation with card deck service
            if (_cardDeckServiceId == null || string.IsNullOrEmpty(_currentDeckId))
            {
                Console.WriteLine("Card deck service not available or no deck ID");
                return;
            }
            
            // For the flop, burn a card first
            if (count == 3)
            {
                await BurnCardAsync();
            }
            // For turn and river, burn a card first
            else if (count == 1)
            {
                await BurnCardAsync();
            }
            
            // Request cards from the deck service with reliable delivery
            var dealPayload = new DeckDealPayload
            {
                DeckId = _currentDeckId,
                Count = count
            };
            
            var message = Message.Create(MessageType.DeckDeal, dealPayload);
            message.MessageId = Guid.NewGuid().ToString();
            
            // FAST FAILOVER: Check if we've had ANY problems with the deck service
            // and immediately use emergency deck for faster gameplay
            if (_emergencyDeck != null)
            {
                // We've had problems before, so just use the emergency deck immediately
                Console.WriteLine("Using existing emergency deck immediately for community cards");
                _currentDeckId = "emergency-local-deck";
                
                // Go back and use the emergency deck path
                await Task.Delay(50); // Small delay before recursive call
                await DealCommunityCardsAsync(count);
                return;
            }
            
            // Try once with minimal timeout for fast failover
            bool ackReceived = await PokerGame.Core.Messaging.MessageBrokerExtensions.SendWithAcknowledgmentAsync(
                this, 
                message, 
                _cardDeckServiceId, 
                timeoutMs: 1500, // Reduced timeout for faster failover
                maxRetries: 0,   // No retries for immediate failover
                useExponentialBackoff: false);
                
            if (!ackReceived)
            {
                Console.WriteLine($"Warning: Failed to get acknowledgment for dealing {count} community cards");
                
                // Fall back to emergency deck if available
                if (_emergencyDeck != null && _currentDeckId != "emergency-local-deck")
                {
                    Console.WriteLine("Switching to emergency deck for community cards due to communication failure");
                    _currentDeckId = "emergency-local-deck";
                    
                    // Recursively call this method now that we're using the emergency deck
                    await DealCommunityCardsAsync(count);
                }
            }
        }
        
        /// <summary>
        /// Burns a card from the deck
        /// </summary>
        private async Task BurnCardAsync()
        {
            // Check if we need to use the emergency deck
            if (_currentDeckId == "emergency-local-deck" && _emergencyDeck != null)
            {
                Console.WriteLine("Burning a card from emergency deck");
                _emergencyDeck.DealCard(); // Just deal and discard a card
                return;
            }
            
            // FAST FAILOVER: Check if we've had ANY problems with the deck service
            // and immediately use emergency deck for faster gameplay
            if (_emergencyDeck != null)
            {
                // We've had problems before, so just use the emergency deck immediately
                Console.WriteLine("Using existing emergency deck immediately for burning a card");
                _currentDeckId = "emergency-local-deck";
                
                // Go back and use the emergency deck path
                await Task.Delay(20); // Very small delay before recursive call
                await BurnCardAsync();
                return;
            }
            
            // Normal operation with card deck service
            if (_cardDeckServiceId == null || string.IsNullOrEmpty(_currentDeckId))
            {
                Console.WriteLine("Card deck service not available or no deck ID");
                return;
            }
            
            var burnPayload = new DeckBurnPayload
            {
                DeckId = _currentDeckId,
                FaceUp = false
            };
            
            var message = Message.Create(MessageType.DeckBurn, burnPayload);
            message.MessageId = Guid.NewGuid().ToString();
            
            // Try once with minimal timeout for fast failover
            bool ackReceived = await PokerGame.Core.Messaging.MessageBrokerExtensions.SendWithAcknowledgmentAsync(
                this, 
                message, 
                _cardDeckServiceId, 
                timeoutMs: 1000, // Reduced timeout for faster failover
                maxRetries: 0,   // No retries for immediate failover
                useExponentialBackoff: false);
                
            if (!ackReceived)
            {
                Console.WriteLine("Warning: Failed to get acknowledgment for burn card operation");
                
                // Fall back to emergency deck if available
                if (_emergencyDeck != null && _currentDeckId != "emergency-local-deck")
                {
                    Console.WriteLine("Switching to emergency deck due to burn card communication failure");
                    _currentDeckId = "emergency-local-deck";
                    
                    // Recursively call this method now that we're using the emergency deck
                    await BurnCardAsync();
                    return;
                }
            }
            else
            {
                // Small delay to let the burn operation complete
                await Task.Delay(50);
            }
        }
        
        /// <summary>
        /// Handles cards dealt from the deck service
        /// </summary>
        /// <param name="cards">The cards that were dealt</param>
        private void HandleDealtCards(List<Card> cards)
        {
            // If there are no cards, do nothing
            if (cards == null || cards.Count == 0)
            {
                Console.WriteLine("Received empty card list from deck service");
                return;
            }
                
            Console.WriteLine($"Received {cards.Count} cards from deck service");
                
            // If we're dealing hole cards (2 per player)
            if (_gameEngine.CommunityCards.Count == 0 && _gameEngine.Players.Any(p => p.HoleCards.Count < 2))
            {
                Console.WriteLine("Dealing hole cards to a player");
                foreach (var player in _gameEngine.Players)
                {
                    if (player.HoleCards.Count < 2 && cards.Count >= 2)
                    {
                        player.HoleCards.Add(cards[0]);
                        player.HoleCards.Add(cards[1]);
                        cards.RemoveRange(0, 2);
                        Console.WriteLine($"Dealt 2 cards to {player.Name}: {player.HoleCards[0]} {player.HoleCards[1]}");
                        break;
                    }
                }
                
                // Check if all players have cards, and if so, make sure we start the hand
                if (_gameEngine.Players.All(p => p.HoleCards.Count == 2))
                {
                    Console.WriteLine("All players have hole cards, transitioning to PreFlop");
                    
                    // Force state transition to PreFlop using reflection since regular StartHand might not work
                    if (_gameEngine.State != Game.GameState.PreFlop)
                    {
                        try
                        {
                            // Small blinds and big blinds setup
                            int dealerPos = 0; // Default to first player as dealer
                            int smallBlindPos = (dealerPos + 1) % _gameEngine.Players.Count;
                            int bigBlindPos = (dealerPos + 2) % _gameEngine.Players.Count;
                            
                            // Force state change to PreFlop
                            Console.WriteLine("Forcing state transition to PreFlop");
                            typeof(PokerGameEngine).GetField("_gameState", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(_gameEngine, Game.GameState.PreFlop);
                            Console.WriteLine($"Game state after force: {_gameEngine.State}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error forcing state change: {ex.Message}");
                        }
                    }
                    
                    Console.WriteLine($"Game state is now: {_gameEngine.State}");
                }
            }
            // Otherwise, we're dealing community cards
            else
            {
                Console.WriteLine("Dealing community cards");
                foreach (var card in cards)
                {
                    _gameEngine.AddCommunityCard(card);
                    Console.WriteLine($"Added community card: {card}");
                }
            }
            
            // Update the game state after dealing cards
            BroadcastGameState();
        }
        
        /// <summary>
        /// Starts the service
        /// </summary>
        public override async Task StartAsync()
        {
            if (IsRunning)
            {
                Console.WriteLine("Game engine service is already running");
                return;
            }
            
            try
            {
                // Initialize our file logger as early as possible
                PokerGame.Core.Logging.FileLogger.Initialize();
                string logPath = PokerGame.Core.Logging.FileLogger.GetLogFilePath();
                Console.WriteLine($"******************************************");
                Console.WriteLine($"* GameEngineService initialized FileLogger");
                Console.WriteLine($"* Log path: {logPath}");
                Console.WriteLine($"******************************************");
                
                PokerGame.Core.Logging.FileLogger.Info("GameEngine", "Service starting up");
                
                Console.WriteLine("Starting game engine service with DIRECT DISCOVERY...");
                
                // Use the base StartAsync method which does more thorough initialization
                await base.StartAsync();
                
                // Set service state
                IsRunning = true;
                
                Console.WriteLine("Game engine service started successfully");
                
                // Broadcast our existence once for service discovery
                _ = Task.Run(async () => 
                {
                    // Wait a moment for everything to initialize
                    await Task.Delay(1000);
                    
                    Console.WriteLine("Broadcasting service registration...");
                    
                    try
                    {
                        // Send service registration broadcast - three times for reliability
                        for (int i = 0; i < 3; i++)
                        {
                            PublishServiceRegistration();
                            await Task.Delay(300);
                        }
                        
                        // Check for any already registered Console UI services
                        var consoleServices = GetServicesOfType(ServiceConstants.ServiceTypes.ConsoleUI);
                        if (consoleServices.Count > 0)
                        {
                            Console.WriteLine($"Found {consoleServices.Count} Console UI services, sending direct registrations");
                            foreach (var consoleId in consoleServices)
                            {
                                // Send targeted registration directly to each console service
                                SendTargetedRegistrationTo(consoleId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in service registration broadcasting: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting game engine service: {ex.Message}");
                IsRunning = false;
                throw;
            }
        }
        
        /// <summary>
        /// Stops the service
        /// </summary>
        public override async Task StopAsync()
        {
            if (!IsRunning)
            {
                Console.WriteLine("Game engine service is not running");
                return;
            }
            
            try
            {
                Console.WriteLine("Stopping game engine service...");
                
                // There's no IsRunningMessageLoop property or StopMessageLoop method 
                // in the MicroserviceBase class. Use the base Stop() method instead.
                base.Stop();
                
                // Set service state
                IsRunning = false;
                
                Console.WriteLine("Game engine service stopped successfully");
                
                // No specific cleanup tasks to await
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping game engine service: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Broadcasts the current game state to all clients
        /// </summary>
        public void BroadcastGameState()
        {
            try
            {
                var payload = new GameStatePayload
                {
                    CurrentState = _gameEngine.State,
                    Pot = _gameEngine.Pot,
                    CurrentBet = _gameEngine.CurrentBet,
                    DealerPosition = -1, // We'd need to expose this in PokerGameEngine
                    CurrentPlayerIndex = -1, // We'd need to expose this in PokerGameEngine
                    CommunityCards = new List<Card>(_gameEngine.CommunityCards)
                };
                
                // Add players, but don't include hole cards in the broadcast message
                foreach (var player in _gameEngine.Players)
                {
                    payload.Players.Add(PlayerInfo.FromPlayer(player, false));
                }
                
                var message = Message.Create(MessageType.GameState, payload);
                Broadcast(message);
                
                // Send individual player messages with their hole cards
                foreach (var player in _gameEngine.Players)
                {
                    // Find any UI service registered for this player
                    var uiServices = GetServicesOfType("PlayerUI");
                    foreach (var uiServiceId in uiServices)
                    {
                        var playerPayload = PlayerInfo.FromPlayer(player, true);
                        var playerMessage = Message.Create(MessageType.PlayerUpdate, playerPayload);
                        SendTo(playerMessage, uiServiceId);
                    }
                }
                
                // Log successful update for debugging
                Console.WriteLine("Game state broadcast successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting game state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Implementation of IPokerGameUI that uses message passing
        /// </summary>
        private class MicroserviceUI : Core.Interfaces.IPokerGameUI
        {
            private readonly GameEngineService _service;
            private PokerGameEngine? _gameEngine;
            
            public MicroserviceUI(GameEngineService service)
            {
                _service = service;
            }
            
            public void SetGameEngine(PokerGameEngine gameEngine)
            {
                _gameEngine = gameEngine;
            }
            
            public void ShowMessage(string message)
            {
                try
                {
                    var messageObj = Message.Create(MessageType.DisplayUpdate, message);
                    _service.Broadcast(messageObj);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error showing message: {ex.Message}");
                }
            }
            
            public void GetPlayerAction(Player player, PokerGameEngine gameEngine)
            {
                try
                {
                    // This is handled via the message passing system instead of a direct call
                    // The UI will send a PlayerAction message when ready
                    
                    // Notify UIs that player action is needed
                    var playerInfo = PlayerInfo.FromPlayer(player, true);
                    var message = Message.Create(MessageType.PlayerAction, playerInfo);
                    _service.Broadcast(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting player action: {ex.Message}");
                }
            }
            
            public void UpdateGameState(PokerGameEngine gameEngine)
            {
                try
                {
                    _service.BroadcastGameState();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating game state: {ex.Message}");
                }
            }
        }
    }
}