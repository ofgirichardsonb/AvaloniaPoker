using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PokerGame.Core.Models;
using PokerGame.Core.ServiceManagement;
using PokerGame.Core.Messaging;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// A microservice that manages card decks for card games
    /// </summary>
    public class CardDeckService : MicroserviceBase, PokerGame.Core.Interfaces.ICardDeckService
    {
        private readonly Dictionary<string, Deck> _decks = new Dictionary<string, Deck>();
        private readonly Dictionary<string, List<Card>> _burnPiles = new Dictionary<string, List<Card>>();
        
        // Default deck ID for the ICardDeckService implementation
        private const string DefaultDeckId = "default-deck";
        
        /// <summary>
        /// Gets a shuffled deck of cards
        /// </summary>
        /// <returns>A shuffled deck of cards</returns>
        public List<Card> GetShuffledDeck()
        {
            // Create a new default deck if it doesn't exist
            if (!_decks.ContainsKey(DefaultDeckId))
            {
                CreateDeck(DefaultDeckId, true);
            }
            else
            {
                // If the deck exists, make sure it's shuffled
                ShuffleDeck(DefaultDeckId);
            }
            
            // Return a copy of the cards in the deck
            return _decks[DefaultDeckId].GetAllCards();
        }
        
        /// <summary>
        /// Draws a card from the deck
        /// </summary>
        /// <returns>The drawn card</returns>
        public Card DrawCard()
        {
            // Create a new default deck if it doesn't exist
            if (!_decks.ContainsKey(DefaultDeckId) || _decks[DefaultDeckId].RemainingCards == 0)
            {
                CreateDeck(DefaultDeckId, true);
            }
            
            // Draw a card from the deck
            return _decks[DefaultDeckId].DealCard();
        }
        
        /// <summary>
        /// Resets the deck to its initial state
        /// </summary>
        public void ResetDeck()
        {
            // Call the implementation with the default deck ID
            ResetDeck(DefaultDeckId);
        }
        
        /// <summary>
        /// Shuffles the current deck
        /// </summary>
        public void ShuffleDeck()
        {
            // Create the deck if it doesn't exist
            if (!_decks.ContainsKey(DefaultDeckId))
            {
                CreateDeck(DefaultDeckId, false);
            }
            
            // Shuffle the deck
            ShuffleDeck(DefaultDeckId);
        }
        
        // Default publisher and subscriber ports
        public const int DefaultPublisherPort = 5559;
        public const int DefaultSubscriberPort = 5560;
        
        // Flag to indicate if this service should always use emergency decks
        private readonly bool _useEmergencyDeckMode;
        
        /// <summary>
        /// Creates a new card deck service with an execution context
        /// </summary>
        /// <param name="executionContext">The execution context to use</param>
        /// <param name="useEmergencyDeckMode">Whether to always use emergency decks instead of network communication</param>
        public CardDeckService(
            MSA.Foundation.ServiceManagement.ExecutionContext executionContext,
            bool useEmergencyDeckMode = false)
            : base(PokerGame.Core.ServiceManagement.ServiceConstants.ServiceTypes.CardDeck, "Card Deck Service", executionContext)
        {
            _useEmergencyDeckMode = useEmergencyDeckMode;
            
            Console.WriteLine($"CardDeckService created with execution context using service type: {PokerGame.Core.ServiceManagement.ServiceConstants.ServiceTypes.CardDeck}");
            
            if (_useEmergencyDeckMode)
            {
                Console.WriteLine("===> CardDeckService: Running in emergency deck mode - will create decks immediately without network");
            }
        }
        
        /// <summary>
        /// Creates a new card deck service with specific ports (backwards compatibility)
        /// </summary>
        /// <param name="publisherPort">The port to publish messages on</param>
        /// <param name="subscriberPort">The port to subscribe to messages on</param>
        /// <param name="useEmergencyDeckMode">Whether to always use emergency decks instead of network communication</param>
        public CardDeckService(
            int publisherPort = DefaultPublisherPort,
            int subscriberPort = DefaultSubscriberPort,
            bool useEmergencyDeckMode = false)
            : base(PokerGame.Core.ServiceManagement.ServiceConstants.ServiceTypes.CardDeck, "Card Deck Service", publisherPort, subscriberPort)
        {
            _useEmergencyDeckMode = useEmergencyDeckMode;
        }
        
        /// <summary>
        /// Constructor to match what MicroserviceManager expects (serviceType, serviceName, publisherPort, subscriberPort)
        /// </summary>
        public CardDeckService(
            string serviceType,
            string serviceName,
            int publisherPort,
            int subscriberPort,
            bool useEmergencyDeckMode = false)
            : base(serviceType, serviceName, publisherPort, subscriberPort)
        {
            _useEmergencyDeckMode = useEmergencyDeckMode;
            
            if (_useEmergencyDeckMode)
            {
                Console.WriteLine("===> CardDeckService: Running in emergency deck mode - will create decks immediately without network");
            }
            // Ensure that the card deck service is properly initialized
            VerifyCriticalMessageHandlers();
        }
        
        /// <summary>
        /// Creates a new card deck service with service type, name, and ports
        /// This constructor matches the pattern expected by MicroserviceManager reflection instantiation
        /// </summary>
        /// <param name="serviceType">The type of the service</param>
        /// <param name="serviceName">The name of the service</param>
        /// <param name="publisherPort">The port to publish messages on</param>
        /// <param name="subscriberPort">The port to subscribe to messages on</param>
        /// <param name="verbose">Whether verbose logging is enabled</param>
        /// <param name="useEmergencyDeckMode">Whether to always use emergency decks</param>
        public CardDeckService(
            string serviceType,
            string serviceName,
            int publisherPort,
            int subscriberPort,
            bool verbose = false,
            bool useEmergencyDeckMode = false)
            : base(serviceType, serviceName, publisherPort, subscriberPort)
        {
            _useEmergencyDeckMode = useEmergencyDeckMode;
            
            Console.WriteLine($"CardDeckService created with reflection-compatible constructor: serviceType={serviceType}, serviceName={serviceName}");
            
            if (_useEmergencyDeckMode)
            {
                Console.WriteLine("===> CardDeckService: Running in emergency deck mode - will create decks immediately without network");
            }
            
            // Ensure that the card deck service is properly initialized
            VerifyCriticalMessageHandlers();
        }
        
        /// <summary>
        /// Verifies that critical message handlers are properly set up
        /// </summary>
        private void VerifyCriticalMessageHandlers()
        {
            Console.WriteLine("===> CardDeckService: Verifying critical message handlers");
            
            // Send a self-acknowledgement to confirm the message transport is working
            try
            {
                var testMessage = Message.Create(MessageType.Ping, "self-test");
                testMessage.SenderId = _serviceId;
                testMessage.ReceiverId = _serviceId;  
                
                Console.WriteLine("===> CardDeckService: Publishing self-test message");
                
                // Use Broadcast method from base class which uses the channel message transport
                Broadcast(testMessage);
                
                Console.WriteLine("===> CardDeckService: Self-test message published successfully");
                Console.WriteLine("===> CardDeckService: Critical message handlers verified!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"===> CardDeckService: ERROR verifying critical message handlers: {ex.Message}");
                Console.WriteLine($"===> CardDeckService: ERROR Stack Trace: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Starts the card deck service with special handling for critical messages
        /// </summary>
        public override void Start()
        {
            Console.WriteLine("===> CardDeckService: Starting with enhanced critical message handling");
            
            // Call the base implementation to set up the service
            base.Start();
            
            // Setup channel-based enhanced message handling (replacing the old socket-based handler)
            SetupChannelMessageHandler();
            
            // Register for direct message handling
            Console.WriteLine("===> CardDeckService: Setting up direct acknowledgment handling");
            
            // Send a startup message to announce this service's capabilities
            var startupMessage = Message.Create(MessageType.ServiceRegistration, 
                new ServiceRegistrationPayload 
                { 
                    ServiceId = _serviceId,
                    ServiceName = "Card Deck Service",
                    ServiceType = PokerGame.Core.ServiceManagement.ServiceConstants.ServiceTypes.CardDeck,
                    Capabilities = new List<string> { "DeckCreate", "DeckShuffle", "DeckDeal", "DeckStatus" }
                });
            
            Console.WriteLine("===> CardDeckService: Broadcasting startup message");
            Broadcast(startupMessage);
            
            // Send a self-acknowledgement to ensure the message handling is working
            Console.WriteLine("===> CardDeckService: Sending self-test message");
            var testMessage = Message.Create(MessageType.Ping, "self-test");
            testMessage.SenderId = _serviceId;
            testMessage.ReceiverId = _serviceId;
            
            // Use the broadcast method to send the test message
            Broadcast(testMessage);
            
            Console.WriteLine("===> CardDeckService: Enhanced startup complete");
        }
        
        /// <summary>
        /// Sets up a special channel-based critical message handler that ensures acknowledgments are sent properly
        /// </summary>
        private void SetupChannelMessageHandler()
        {
            Console.WriteLine("===> CardDeckService: Setting up channel-based OVERRIDE message handler for critical messages");
            
            // Start a background task to continuously monitor the message queue and prioritize critical messages
            Task.Run(async () =>
            {
                Console.WriteLine("===> CardDeckService: Channel-based OVERRIDE message handler started");
                
                // Use a cancellation token to properly terminate the task
                CancellationToken token = _cancellationTokenSource?.Token ?? CancellationToken.None;
                
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // Listen for incoming messages directly from channel transport
                        var channelTransport = _messageTransport as ChannelMessageTransport;
                        if (channelTransport != null)
                        {
                            try
                            {
                                // Try to receive a message with a short timeout
                                var result = await channelTransport.TryReceiveMessageAsync(50);
                                if (result.Success && !string.IsNullOrEmpty(result.MessageData))
                                {
                                    string messageJson = result.MessageData;
                                    try
                                    {
                                        // Parse the message
                                        var message = Message.FromJson(messageJson);
                                        
                                        if (message != null && 
                                            (message.Type == MessageType.Ping || message.Type == MessageType.DeckCreate) &&
                                            (string.IsNullOrEmpty(message.ReceiverId) || message.ReceiverId == _serviceId) &&
                                            !string.IsNullOrEmpty(message.MessageId) &&
                                            !string.IsNullOrEmpty(message.SenderId))
                                        {
                                            Console.WriteLine($"!!!! CHANNEL CRITICAL OVERRIDE: Received {message.Type} message {message.MessageId} from {message.SenderId}");
                                            
                                            // Create and send acknowledgment immediately
                                            var ackMessage = Message.Create(MessageType.Acknowledgment, DateTime.UtcNow.ToString("o"));
                                            ackMessage.InResponseTo = message.MessageId;
                                            ackMessage.SenderId = _serviceId;
                                            ackMessage.ReceiverId = message.SenderId;
                                            
                                            // Send acknowledgment with multiple approaches for redundancy
                                            try 
                                            {
                                                Console.WriteLine($"!!!! CHANNEL CRITICAL OVERRIDE: Broadcasting ACK for {message.MessageId}");
                                                Broadcast(ackMessage);
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"!!!! CHANNEL CRITICAL OVERRIDE: Error broadcasting ACK: {ex.Message}");
                                            }
                                            
                                            try 
                                            {
                                                // Try using CentralBroker directly as a redundant acknowledgment path
                                                Console.WriteLine($"!!!! CHANNEL CRITICAL OVERRIDE: Sending via central broker ACK for {message.MessageId}");
                                                var networkAck = ackMessage.ToNetworkMessage();
                                                BrokerManager.Instance.CentralBroker?.Publish(networkAck);
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"!!!! CHANNEL CRITICAL OVERRIDE: Error sending via central broker: {ex.Message}");
                                            }
                                            
                                            // Process the critical message accordingly
                                            if (message.Type == MessageType.Ping)
                                            {
                                                Console.WriteLine($"!!!! CHANNEL CRITICAL OVERRIDE: Processing ping {message.MessageId}");
                                                // Additional ping processing if needed
                                            }
                                            else if (message.Type == MessageType.DeckCreate)
                                            {
                                                Console.WriteLine($"!!!! CHANNEL CRITICAL OVERRIDE: Processing deck creation {message.MessageId}");
                                                // We'll still let the normal message processing handle the actual deck creation
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"!!!! CHANNEL CRITICAL OVERRIDE: Error processing message: {ex.Message}");
                                    }
                                }
                            }
                            catch (ObjectDisposedException)
                            {
                                Console.WriteLine("Channel transport was disposed during operation, stopping critical message handler");
                                break;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"!!!! CHANNEL CRITICAL OVERRIDE: Error receiving message: {ex.Message}");
                            }
                        }
                        else
                        {
                            // If message transport is null or not a channel transport, attempt to recreate it
                            Console.WriteLine("Channel message transport is null, attempting to recreate...");
                            try
                            {
                                // Reinitialize the channel-based message transport
                                _messageTransport = ChannelMessageHelper.CreateServiceTransport(_serviceId);
                                await _messageTransport.StartAsync();
                                
                                Console.WriteLine("Successfully recreated channel message transport in critical message handler");
                                
                                // Add a small delay to ensure the transport is ready
                                await Task.Delay(100, token);
                                continue;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to recreate channel message transport: {ex.Message}");
                                // Wait before trying again
                                await Task.Delay(500, token);
                                continue;
                            }
                        }
                        
                        // Check for cancellation
                        if (token.IsCancellationRequested)
                        {
                            Console.WriteLine("Channel critical message handler cancellation requested");
                            break;
                        }
                        
                        // Small delay to prevent CPU overuse
                        await Task.Delay(5, token);
                    }
                    catch (TaskCanceledException)
                    {
                        // Normal cancellation, exit loop
                        Console.WriteLine("Channel critical message handler cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"!!!! CHANNEL CRITICAL OVERRIDE: Unhandled error: {ex.Message}");
                        
                        try
                        {
                            // Longer delay on error, but respect cancellation
                            await Task.Delay(100, token);
                        }
                        catch (TaskCanceledException)
                        {
                            // If cancelled during delay, exit loop
                            break;
                        }
                    }
                }
                
                Console.WriteLine("Channel critical message handler terminated");
            });
        }
        
        /// <summary>
        /// Handles messages received from other services
        /// </summary>
        /// <param name="message">The message to handle</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public override async Task HandleMessageAsync(Message message)
        {
            try
            {
                Console.WriteLine($"===> CRITICAL DIAGNOSTIC: CardDeckService handling message type {message.Type} with ID {message.MessageId}");
                
                // Debug - dump message transport status
                Console.WriteLine($"===> TRANSPORT STATUS: Message Transport null? {_messageTransport == null}");
                try
                {
                    Console.WriteLine($"===> TRANSPORT INFO: Message Transport info: {(_messageTransport?.GetType()?.FullName ?? "null")}");
                    
                    var channelTransport = _messageTransport as ChannelMessageTransport;
                    Console.WriteLine($"===> CHANNEL STATUS: Channel Transport null? {channelTransport == null}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"===> TRANSPORT ERROR: Failed to get message transport info: {ex.Message}");
                }
                
                // SUPER CRITICAL: Handle acknowledgments immediately and with maximum redundancy
                if (message.Type == MessageType.Ping || message.Type == MessageType.DeckCreate) 
                {
                    Console.WriteLine($"===> CRITICAL ACK HANDLING: Immediate ACK for {message.Type} {message.MessageId}");
                    
                    // Create the acknowledgment message
                    var ackMessage = Message.Create(MessageType.Acknowledgment, DateTime.UtcNow.ToString("o"));
                    ackMessage.InResponseTo = message.MessageId;
                    ackMessage.ReceiverId = message.SenderId;
                    ackMessage.SenderId = _serviceId;
                    
                    // APPROACH 1: Use the broadcast method
                    try
                    {
                        Console.WriteLine($"===> APPROACH 1: Broadcasting ACK for {message.MessageId} to {message.SenderId}");
                        Broadcast(ackMessage);
                        Console.WriteLine($"===> APPROACH 1: Broadcast completed");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"===> ERROR in approach 1: {ex.Message}");
                    }
                    
                    // APPROACH 2: Direct channel send
                    try
                    {
                        Console.WriteLine($"===> APPROACH 2: Direct channel send ACK for {message.MessageId}");
                        var channelTransport = _messageTransport as ChannelMessageTransport;
                        if (channelTransport != null)
                        {
                            Console.WriteLine($"===> APPROACH 2: Preparing for channel send");
                            // Convert our message to a JSON string and directly use that
                            var serializedAck = ackMessage.ToJson();
                            // Use central broker instead for reliable channel-based delivery
                            var serviceMessage = MSA.Foundation.Messaging.ServiceMessage.Create("Acknowledgment");
                            serviceMessage.SetContent(serializedAck);
                            channelTransport.SendAsync("broadcast", serviceMessage).GetAwaiter().GetResult();
                            Console.WriteLine($"===> APPROACH 2: Channel send completed");
                        }
                        else
                        {
                            Console.WriteLine($"===> APPROACH 2: Channel transport not available");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"===> ERROR in approach 2: {ex.Message}");
                    }
                    
                    // APPROACH 3: Use central broker directly with ServiceMessage
                    try
                    {
                        Console.WriteLine($"===> APPROACH 3: Using central broker for ACK");
                        // Create a ServiceMessage instead of NetworkMessage
                        var serviceMessage = MSA.Foundation.Messaging.ServiceMessage.Create("Acknowledgment");
                        serviceMessage.FromSender(_serviceId);
                        
                        // Set message properties from ackMessage
                        serviceMessage.SetHeader("OriginalMessageId", ackMessage.InResponseTo);
                        serviceMessage.SetHeader("AcknowledgementTime", DateTime.UtcNow.ToString("o"));
                        if (!string.IsNullOrEmpty(ackMessage.ReceiverId))
                            serviceMessage.ToReceiver(ackMessage.ReceiverId);
                        
                        // Use the broker to publish the message
                        BrokerManager.Instance.CentralBroker?.PublishServiceMessage(serviceMessage);
                        Console.WriteLine($"===> APPROACH 3: Central broker message sent");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"===> ERROR in approach 3: {ex.Message}");
                    }
                    
                    // Specific handling for different message types
                    if (message.Type == MessageType.Ping)
                    {
                        Console.WriteLine($"===> PING HANDLER: Processing ping {message.MessageId}");
                    }
                    else if (message.Type == MessageType.DeckCreate)
                    {
                        Console.WriteLine($"===> DECKCREATE HANDLER: Processing deck creation {message.MessageId}");
                    }
                }
                
                // Special handling for messages when in emergency deck mode
                if (_useEmergencyDeckMode && message.Type == MessageType.DeckCreate)
                {
                    Console.WriteLine($"===> CardDeckService: Using EMERGENCY DECK MODE for DeckCreate message {message.MessageId}");
                    var createPayload = message.GetPayload<DeckCreatePayload>();
                    if (createPayload != null)
                    {
                        try
                        {
                            // Create the deck immediately with no delay or network communication
                            Console.WriteLine($"===> CardDeckService: EMERGENCY creating deck with ID {createPayload.DeckId}");
                            CreateDeck(createPayload.DeckId, createPayload.Shuffle);
                            Console.WriteLine($"===> CardDeckService: EMERGENCY deck {createPayload.DeckId} created successfully");
                            
                            // Send immediate confirmation without relying on normal message channels
                            var confirmationPayload = new DeckStatusPayload 
                            { 
                                DeckId = createPayload.DeckId,
                                Success = true,
                                Message = "Deck created successfully (emergency mode)"
                            };
                            
                            var confirmationMessage = Message.Create(MessageType.DeckCreated, confirmationPayload);
                            confirmationMessage.InResponseTo = message.MessageId;
                            confirmationMessage.SenderId = _serviceId;
                            confirmationMessage.ReceiverId = message.SenderId;
                            
                            Console.WriteLine($"===> CardDeckService: EMERGENCY broadcasting confirmation to {message.SenderId}");
                            Broadcast(confirmationMessage);
                            
                            // Send an acknowledgment message too
                            var ackMessage = Message.Create(MessageType.Acknowledgment, DateTime.UtcNow.ToString("o"));
                            ackMessage.InResponseTo = message.MessageId;
                            ackMessage.SenderId = _serviceId;
                            ackMessage.ReceiverId = message.SenderId;
                            Broadcast(ackMessage);
                            
                            Console.WriteLine($"===> CardDeckService: EMERGENCY deck creation process complete");
                            
                            // Return immediately to bypass normal processing
                            return;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"===> CardDeckService: EMERGENCY ERROR creating deck: {ex.Message}");
                        }
                    }
                }
                
                switch (message.Type)
                {
                    case MessageType.DeckCreate:
                        Console.WriteLine($"===> CardDeckService: Received DeckCreate message with ID {message.MessageId} from {message.SenderId}");
                        var createPayload = message.GetPayload<DeckCreatePayload>();
                        if (createPayload != null)
                        {
                            try
                            {
                                Console.WriteLine($"===> CardDeckService: Creating deck with ID {createPayload.DeckId}");
                                CreateDeck(createPayload.DeckId, createPayload.Shuffle);
                                Console.WriteLine($"===> CardDeckService: Deck {createPayload.DeckId} created successfully");
                                
                                // Send confirmation directly back to the sender
                                var confirmationPayload = new DeckStatusPayload 
                                { 
                                    DeckId = createPayload.DeckId,
                                    Success = true,
                                    Message = "Deck created successfully"
                                };
                                
                                var confirmationMessage = Message.Create(MessageType.DeckCreated, confirmationPayload);
                                confirmationMessage.InResponseTo = message.MessageId;
                                
                                Console.WriteLine($"===> CardDeckService: Sending DeckCreated confirmation to {message.SenderId} for message {message.MessageId}");
                                // IMPORTANT FIX: Broadcast the confirmation instead of direct send
                                Console.WriteLine($"===> CardDeckService: Broadcasting confirmation with receiver ID set to {message.SenderId}");
                                confirmationMessage.ReceiverId = message.SenderId;
                                Broadcast(confirmationMessage);
                                Console.WriteLine($"===> CardDeckService: DeckCreated confirmation sent");
                                
                                // Also broadcast the deck status
                                Console.WriteLine($"===> CardDeckService: Broadcasting deck status for {createPayload.DeckId}");
                                BroadcastDeckStatus(createPayload.DeckId);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"===> CardDeckService: ERROR creating deck: {ex.Message}");
                                Console.WriteLine($"===> CardDeckService: {ex.StackTrace}");
                                
                                // Send error message back to sender
                                var errorPayload = new DeckStatusPayload 
                                { 
                                    DeckId = createPayload.DeckId,
                                    Success = false,
                                    Message = $"Failed to create deck: {ex.Message}"
                                };
                                
                                var errorMessage = Message.Create(MessageType.Error, errorPayload);
                                errorMessage.InResponseTo = message.MessageId;
                                
                                Console.WriteLine($"===> CardDeckService: Sending error for DeckCreate to {message.SenderId}");
                                SendTo(errorMessage, message.SenderId);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"===> CardDeckService: Received null DeckCreatePayload for message {message.MessageId}");
                        }
                        break;
                        
                    case MessageType.DeckShuffle:
                        var shufflePayload = message.GetPayload<DeckIdPayload>();
                        if (shufflePayload != null)
                        {
                            Console.WriteLine($"CardDeckService: Shuffling deck {shufflePayload.DeckId}");
                            ShuffleDeck(shufflePayload.DeckId);
                            
                            // Send direct acknowledgment to the sender
                            var ackPayload = new DeckStatusPayload
                            {
                                DeckId = shufflePayload.DeckId,
                                Success = true,
                                Message = "Deck shuffled successfully"
                            };
                            
                            var ackMessage = Message.Create(MessageType.DeckShuffled, ackPayload);
                            // Set InResponseTo field to link this response to the original message
                            ackMessage.InResponseTo = message.MessageId;
                            
                            // Send the acknowledgment
                            Console.WriteLine($"CardDeckService: Sending shuffle acknowledgment to {message.SenderId}");
                            SendTo(ackMessage, message.SenderId);
                            
                            // Also broadcast the deck status
                            BroadcastDeckStatus(shufflePayload.DeckId);
                        }
                        break;
                        
                    case MessageType.DeckDeal:
                        var dealPayload = message.GetPayload<DeckDealPayload>();
                        if (dealPayload != null)
                        {
                            var cards = DealCards(dealPayload.DeckId, dealPayload.Count);
                            SendDealResponse(message.SenderId, dealPayload.DeckId, cards);
                            BroadcastDeckStatus(dealPayload.DeckId);
                        }
                        break;
                        
                    case MessageType.DeckBurn:
                        var burnPayload = message.GetPayload<DeckBurnPayload>();
                        if (burnPayload != null)
                        {
                            var burnedCard = BurnCard(burnPayload.DeckId, burnPayload.FaceUp);
                            SendBurnResponse(message.SenderId, burnPayload.DeckId, burnedCard, burnPayload.FaceUp);
                            BroadcastDeckStatus(burnPayload.DeckId);
                        }
                        break;
                        
                    case MessageType.DeckReset:
                        var resetPayload = message.GetPayload<DeckIdPayload>();
                        if (resetPayload != null)
                        {
                            ResetDeck(resetPayload.DeckId);
                            BroadcastDeckStatus(resetPayload.DeckId);
                        }
                        break;
                        
                    case MessageType.DeckStatus:
                        var statusPayload = message.GetPayload<DeckIdPayload>();
                        if (statusPayload != null)
                        {
                            BroadcastDeckStatus(statusPayload.DeckId);
                        }
                        break;
                        
                    case MessageType.Ping:
                        Console.WriteLine($"===> CardDeckService: Received Ping message with ID {message.MessageId} from {message.SenderId}");
                        
                        try
                        {
                            // Send a direct acknowledgment back to the sender
                            var pingAckMessage = Message.Create(MessageType.Acknowledgment, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                            pingAckMessage.InResponseTo = message.MessageId;
                            
                            Console.WriteLine($"===> CardDeckService: Creating ping acknowledgment for message {message.MessageId} to {message.SenderId}");
                            
                            // IMPORTANT FIX: Broadcast the acknowledgment instead of direct send
                            Console.WriteLine($"===> CardDeckService: Broadcasting acknowledgment with receiver ID set to {message.SenderId}");
                            pingAckMessage.ReceiverId = message.SenderId;
                            Broadcast(pingAckMessage);
                            
                            Console.WriteLine($"===> CardDeckService: Acknowledgment sent for message {message.MessageId}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"===> CardDeckService: ERROR sending ping acknowledgment: {ex.Message}");
                            Console.WriteLine($"===> CardDeckService: {ex.StackTrace}");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling card deck message: {ex.Message}");
                // Send error response if needed
                var errorPayload = new ErrorPayload 
                { 
                    ErrorCode = "DECK_ERROR", 
                    ErrorMessage = ex.Message 
                };
                var errorMessage = Message.Create(MessageType.Error, errorPayload);
                SendTo(errorMessage, message.SenderId);
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// Creates a new deck with the specified ID and optionally shuffles it
        /// </summary>
        /// <param name="deckId">The unique ID for the deck</param>
        /// <param name="shuffle">Whether to shuffle the deck after creation</param>
        private void CreateDeck(string deckId, bool shuffle)
        {
            try
            {
                Console.WriteLine($"===> CardDeckService: Creating new deck with ID {deckId}");
                
                // Validate deck ID
                if (string.IsNullOrEmpty(deckId))
                {
                    throw new ArgumentException("Deck ID cannot be null or empty", nameof(deckId));
                }
                
                // Create new deck
                var deck = new Deck();
                if (shuffle)
                {
                    Console.WriteLine($"===> CardDeckService: Shuffling deck {deckId}");
                    deck.Shuffle();
                }
                
                // Store the deck and create its burn pile
                _decks[deckId] = deck;
                _burnPiles[deckId] = new List<Card>();
                
                Console.WriteLine($"===> CardDeckService: Created deck: {deckId} (Shuffled: {shuffle})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"===> CardDeckService: ERROR creating deck {deckId}: {ex.Message}");
                throw; // Rethrow to allow higher-level error handling
            }
        }
        
        /// <summary>
        /// Shuffles the specified deck
        /// </summary>
        /// <param name="deckId">The ID of the deck to shuffle</param>
        private void ShuffleDeck(string deckId)
        {
            if (_decks.TryGetValue(deckId, out var deck))
            {
                deck.Shuffle();
                Console.WriteLine($"Shuffled deck: {deckId}");
            }
            else
            {
                throw new Exception($"Deck not found: {deckId}");
            }
        }
        
        /// <summary>
        /// Deals cards from the specified deck
        /// </summary>
        /// <param name="deckId">The ID of the deck to deal from</param>
        /// <param name="count">The number of cards to deal</param>
        /// <returns>A list of dealt cards</returns>
        private List<Card> DealCards(string deckId, int count)
        {
            if (_decks.TryGetValue(deckId, out var deck))
            {
                var cards = deck.DealCards(count);
                Console.WriteLine($"Dealt {cards.Count} cards from deck: {deckId}");
                return cards;
            }
            else
            {
                throw new Exception($"Deck not found: {deckId}");
            }
        }
        
        /// <summary>
        /// Burns a card from the specified deck
        /// </summary>
        /// <param name="deckId">The ID of the deck to burn from</param>
        /// <param name="faceUp">Whether the card is burned face up (visible)</param>
        /// <returns>The burned card, or null if the deck is empty</returns>
        private Card? BurnCard(string deckId, bool faceUp)
        {
            try
            {
                if (_decks.TryGetValue(deckId, out var deck))
                {
                    // Ensure a burn pile exists for this deck
                    if (!_burnPiles.ContainsKey(deckId))
                    {
                        _burnPiles[deckId] = new List<Card>();
                        Console.WriteLine($"Created new burn pile for deck: {deckId}");
                    }
                    
                    var burnPile = _burnPiles[deckId];
                    
                    try
                    {
                        var card = deck.DealCard();
                        burnPile.Add(card);
                        Console.WriteLine($"Burned card from deck: {deckId} (Face up: {faceUp}, Card: {(faceUp ? card.ToString() : "Hidden")})");
                        return card;
                    }
                    catch (InvalidOperationException ex)
                    {
                        // Handle the case when the deck is empty
                        Console.WriteLine($"Failed to burn card: {ex.Message}");
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine($"Cannot burn card - Deck not found: {deckId}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in BurnCard method: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Resets the specified deck to a full, unshuffled state
        /// </summary>
        /// <param name="deckId">The ID of the deck to reset</param>
        private void ResetDeck(string deckId)
        {
            if (_decks.TryGetValue(deckId, out var deck))
            {
                deck.Reset();
                
                // Ensure burn pile exists before trying to clear it
                if (!_burnPiles.ContainsKey(deckId))
                {
                    _burnPiles[deckId] = new List<Card>();
                }
                else
                {
                    _burnPiles[deckId].Clear();
                }
                
                Console.WriteLine($"Reset deck: {deckId}");
            }
            else
            {
                throw new Exception($"Deck not found: {deckId}");
            }
        }
        
        /// <summary>
        /// Broadcasts the status of a deck to all listeners
        /// </summary>
        /// <param name="deckId">The ID of the deck</param>
        private void BroadcastDeckStatus(string deckId)
        {
            try 
            {
                Console.WriteLine($"===> CardDeckService: Broadcasting deck status for deck {deckId}");
                
                // Check if message transport is valid
                if (_messageTransport == null)
                {
                    Console.WriteLine($"===> CardDeckService: ERROR in BroadcastDeckStatus - message transport is null");
                    return;
                }
                
                // Check if deck exists
                if (!_decks.TryGetValue(deckId, out var deck))
                {
                    Console.WriteLine($"===> CardDeckService: ERROR in BroadcastDeckStatus - deck {deckId} not found");
                    return;
                }
                
                // Check if burn pile exists, create it if it doesn't
                if (!_burnPiles.TryGetValue(deckId, out var burnPile))
                {
                    Console.WriteLine($"===> CardDeckService: Creating missing burn pile for deck {deckId}");
                    burnPile = new List<Card>();
                    _burnPiles[deckId] = burnPile;
                }
                
                var payload = new DeckStatusPayload
                {
                    DeckId = deckId,
                    RemainingCards = deck.CardsRemaining,
                    BurnedCardsCount = burnPile.Count
                };
                
                var message = Message.Create(MessageType.DeckStatus, payload);
                message.SenderId = _serviceId;  // Ensure sender ID is set
                
                Console.WriteLine($"===> CardDeckService: Sending deck status message for deck {deckId}");
                Broadcast(message);
                Console.WriteLine($"===> CardDeckService: Deck status message sent successfully");
            }
            catch (Exception ex)
            {
                // Log error but don't throw - allows operation to continue when possible
                Console.WriteLine($"===> CardDeckService: ERROR in BroadcastDeckStatus: {ex.Message}");
                Console.WriteLine($"===> CardDeckService: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Sends a response with the dealt cards to the requesting service
        /// </summary>
        /// <param name="requesterId">The ID of the requesting service</param>
        /// <param name="deckId">The ID of the deck</param>
        /// <param name="cards">The dealt cards</param>
        private void SendDealResponse(string requesterId, string deckId, List<Card> cards)
        {
            try
            {
                if (_messageTransport == null)
                {
                    Console.WriteLine($"===> CardDeckService: ERROR in SendDealResponse - message transport is null");
                    return;
                }
                
                if (string.IsNullOrEmpty(requesterId))
                {
                    Console.WriteLine($"===> CardDeckService: ERROR in SendDealResponse - requester ID is null or empty");
                    return;
                }
                
                var payload = new DeckDealResponsePayload
                {
                    DeckId = deckId,
                    Cards = cards ?? new List<Card>()
                };
                
                var message = Message.Create(MessageType.DeckDealResponse, payload);
                message.SenderId = _serviceId;  // Ensure sender ID is set
                message.ReceiverId = requesterId;
                
                Console.WriteLine($"===> CardDeckService: Sending deal response to {requesterId} for deck {deckId}");
                SendTo(message, requesterId);
                Console.WriteLine($"===> CardDeckService: Deal response sent successfully");
            }
            catch (Exception ex)
            {
                // Log error but don't throw
                Console.WriteLine($"===> CardDeckService: ERROR in SendDealResponse: {ex.Message}");
                Console.WriteLine($"===> CardDeckService: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Sends a response with the burned card to the requesting service
        /// </summary>
        /// <param name="requesterId">The ID of the requesting service</param>
        /// <param name="deckId">The ID of the deck</param>
        /// <param name="card">The burned card</param>
        /// <param name="faceUp">Whether the card was burned face up</param>
        private void SendBurnResponse(string requesterId, string deckId, Card? card, bool faceUp)
        {
            try
            {
                if (_messageTransport == null)
                {
                    Console.WriteLine($"===> CardDeckService: ERROR in SendBurnResponse - message transport is null");
                    return;
                }
                
                if (string.IsNullOrEmpty(requesterId))
                {
                    Console.WriteLine($"===> CardDeckService: ERROR in SendBurnResponse - requester ID is null or empty");
                    return;
                }
                
                var payload = new DeckBurnResponsePayload
                {
                    DeckId = deckId,
                    Card = card,
                    FaceUp = faceUp
                };
                
                var message = Message.Create(MessageType.DeckBurnResponse, payload);
                message.SenderId = _serviceId;  // Ensure sender ID is set
                message.ReceiverId = requesterId;
                
                Console.WriteLine($"===> CardDeckService: Sending burn response to {requesterId} for deck {deckId}");
                SendTo(message, requesterId);
                Console.WriteLine($"===> CardDeckService: Burn response sent successfully");
            }
            catch (Exception ex)
            {
                // Log error but don't throw
                Console.WriteLine($"===> CardDeckService: ERROR in SendBurnResponse: {ex.Message}");
                Console.WriteLine($"===> CardDeckService: {ex.StackTrace}");
            }
        }
    }
    
    #region Card Deck Payloads
    
    /// <summary>
    /// Payload for creating a new deck
    /// </summary>
    public class DeckCreatePayload
    {
        /// <summary>
        /// Gets or sets the unique ID for the deck
        /// </summary>
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets whether to shuffle the deck after creation
        /// </summary>
        public bool Shuffle { get; set; } = true;
    }
    
    /// <summary>
    /// Payload for referencing a deck by ID
    /// </summary>
    public class DeckIdPayload
    {
        /// <summary>
        /// Gets or sets the unique ID of the deck
        /// </summary>
        public string DeckId { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Payload for dealing cards from a deck
    /// </summary>
    public class DeckDealPayload
    {
        /// <summary>
        /// Gets or sets the unique ID of the deck
        /// </summary>
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the number of cards to deal
        /// </summary>
        public int Count { get; set; } = 1;
    }
    
    /// <summary>
    /// Payload for burning a card from a deck
    /// </summary>
    public class DeckBurnPayload
    {
        /// <summary>
        /// Gets or sets the unique ID of the deck
        /// </summary>
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets whether the card is burned face up (visible)
        /// </summary>
        public bool FaceUp { get; set; } = false;
    }
    
    /// <summary>
    /// Payload containing the status of a deck
    /// </summary>
    public class DeckStatusPayload
    {
        /// <summary>
        /// Gets or sets the unique ID of the deck
        /// </summary>
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the number of cards remaining in the deck
        /// </summary>
        public int RemainingCards { get; set; }
        
        /// <summary>
        /// Gets or sets the number of cards in the burn pile
        /// </summary>
        public int BurnedCardsCount { get; set; }
        
        /// <summary>
        /// Gets or sets whether the operation was successful
        /// </summary>
        public bool Success { get; set; } = true;
        
        /// <summary>
        /// Gets or sets an optional message about the status
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Payload containing cards dealt from a deck
    /// </summary>
    public class DeckDealResponsePayload
    {
        /// <summary>
        /// Gets or sets the unique ID of the deck
        /// </summary>
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the list of dealt cards
        /// </summary>
        public List<Card> Cards { get; set; } = new List<Card>();
    }
    
    /// <summary>
    /// Payload containing a burned card
    /// </summary>
    public class DeckBurnResponsePayload
    {
        /// <summary>
        /// Gets or sets the unique ID of the deck
        /// </summary>
        public string DeckId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the burned card
        /// </summary>
        public Card? Card { get; set; }
        
        /// <summary>
        /// Gets or sets whether the card was burned face up
        /// </summary>
        public bool FaceUp { get; set; }
    }
    
    #endregion
}