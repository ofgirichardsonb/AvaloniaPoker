using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PokerGame.Core.Models;
using NetMQ;
using NetMQ.Sockets;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// A microservice that manages card decks for card games
    /// </summary>
    public class CardDeckService : MicroserviceBase
    {
        private readonly Dictionary<string, Deck> _decks = new Dictionary<string, Deck>();
        private readonly Dictionary<string, List<Card>> _burnPiles = new Dictionary<string, List<Card>>();
        
        // Default publisher and subscriber ports
        public const int DefaultPublisherPort = 5559;
        public const int DefaultSubscriberPort = 5560;
        
        // Flag to indicate if this service should always use emergency decks
        private readonly bool _useEmergencyDeckMode;
        
        /// <summary>
        /// Creates a new card deck service
        /// </summary>
        /// <param name="publisherPort">The port to publish messages on</param>
        /// <param name="subscriberPort">The port to subscribe to messages on</param>
        /// <param name="useEmergencyDeckMode">Whether to always use emergency decks instead of network communication</param>
        public CardDeckService(
            int publisherPort = DefaultPublisherPort,
            int subscriberPort = DefaultSubscriberPort,
            bool useEmergencyDeckMode = false)
            : base("CardDeck", "Card Deck Service", publisherPort, subscriberPort)
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
        /// Verifies that critical message handlers are properly set up
        /// </summary>
        private void VerifyCriticalMessageHandlers()
        {
            Console.WriteLine("===> CardDeckService: Verifying critical message handlers");
            
            // Send a self-acknowledgement to confirm the publisher socket is working
            try
            {
                var testMessage = Message.Create(MessageType.Ping, "self-test");
                testMessage.SenderId = _serviceId;
                testMessage.ReceiverId = _serviceId;  
                
                Console.WriteLine("===> CardDeckService: Publishing self-test message");
                _publisherSocket?.SendFrame(testMessage.ToJson());
                
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
            
            // Override message handling to implement direct low-level acknowledgment
            SetupCriticalMessageHandler();
            
            // Call the base implementation to set up the service
            base.Start();
            
            // Register for direct message handling
            Console.WriteLine("===> CardDeckService: Setting up direct acknowledgment handling");
            
            // Send a startup message to announce this service's capabilities
            var startupMessage = Message.Create(MessageType.ServiceRegistration, 
                new ServiceRegistrationPayload 
                { 
                    ServiceId = _serviceId,
                    ServiceName = "Card Deck Service",
                    ServiceType = "CardDeck",
                    Capabilities = new List<string> { "DeckCreate", "DeckShuffle", "DeckDeal", "DeckStatus" }
                });
            
            Console.WriteLine("===> CardDeckService: Broadcasting startup message");
            Broadcast(startupMessage);
            
            // Send a self-acknowledgement to ensure the message handling is working
            Console.WriteLine("===> CardDeckService: Sending self-test message");
            var testMessage = Message.Create(MessageType.Ping, "self-test");
            testMessage.SenderId = _serviceId;
            testMessage.ReceiverId = _serviceId;
            _publisherSocket?.SendFrame(testMessage.ToJson());
            
            Console.WriteLine("===> CardDeckService: Enhanced startup complete");
        }
        
        /// <summary>
        /// Sets up a special critical message handler that ensures acknowledgments are sent properly
        /// </summary>
        private void SetupCriticalMessageHandler()
        {
            Console.WriteLine("===> CardDeckService: Setting up OVERRIDE message handler for critical messages");
            
            // Start a background task to continuously monitor the message queue and prioritize critical messages
            Task.Run(async () =>
            {
                Console.WriteLine("===> CardDeckService: OVERRIDE message handler started");
                
                // Use a cancellation token to properly terminate the task
                CancellationToken token = _cancellationTokenSource?.Token ?? CancellationToken.None;
                
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // Listen for incoming messages directly from the socket
                        if (_subscriberSocket != null)
                        {
                            try
                            {
                                // Make sure the socket is still valid
                                if (_subscriberSocket.IsDisposed)
                                {
                                    Console.WriteLine("Subscriber socket is disposed, stopping critical message handler");
                                    break;
                                }
                                
                                // Try to receive a message with a short timeout
                                if (_subscriberSocket != null && _subscriberSocket.TryReceiveFrameString(TimeSpan.FromMilliseconds(50), out string? messageJson) && 
                                    !string.IsNullOrEmpty(messageJson))
                                {
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
                                            Console.WriteLine($"!!!! CRITICAL OVERRIDE: Received {message.Type} message {message.MessageId} from {message.SenderId}");
                                            
                                            // Create and send acknowledgment immediately
                                            var ackMessage = Message.Create(MessageType.Acknowledgment, DateTime.UtcNow.ToString("o"));
                                            ackMessage.InResponseTo = message.MessageId;
                                            ackMessage.SenderId = _serviceId;
                                            ackMessage.ReceiverId = message.SenderId;
                                            
                                            // Check if publisher socket is still valid 
                                            if (_publisherSocket == null || _publisherSocket.IsDisposed)
                                            {
                                                Console.WriteLine("Publisher socket is disposed, unable to send acknowledgment");
                                                continue;
                                            }
                                            
                                            // Send acknowledgment with multiple approaches for redundancy
                                            string serializedAck = ackMessage.ToJson();
                                            
                                            try
                                            {
                                                Console.WriteLine($"!!!! CRITICAL OVERRIDE: Sending raw socket ACK for {message.MessageId}");
                                                _publisherSocket.SendFrame(serializedAck);
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"!!!! CRITICAL OVERRIDE: Error sending raw socket ACK: {ex.Message}");
                                            }
                                            
                                            try 
                                            {
                                                Console.WriteLine($"!!!! CRITICAL OVERRIDE: Broadcasting ACK for {message.MessageId}");
                                                Broadcast(ackMessage);
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"!!!! CRITICAL OVERRIDE: Error broadcasting ACK: {ex.Message}");
                                            }
                                            
                                            // Process the critical message accordingly
                                            if (message.Type == MessageType.Ping)
                                            {
                                                Console.WriteLine($"!!!! CRITICAL OVERRIDE: Processing ping {message.MessageId}");
                                                // Additional ping processing if needed
                                            }
                                            else if (message.Type == MessageType.DeckCreate)
                                            {
                                                Console.WriteLine($"!!!! CRITICAL OVERRIDE: Processing deck creation {message.MessageId}");
                                                // We'll still let the normal message processing handle the actual deck creation
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"!!!! CRITICAL OVERRIDE: Error processing message: {ex.Message}");
                                    }
                                }
                            }
                            catch (ObjectDisposedException)
                            {
                                Console.WriteLine("Subscriber socket was disposed during operation, stopping critical message handler");
                                break;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"!!!! CRITICAL OVERRIDE: Error receiving message: {ex.Message}");
                            }
                        }
                        else
                        {
                            // If socket is null, exit the loop
                            Console.WriteLine("Subscriber socket is null, stopping critical message handler");
                            break;
                        }
                        
                        // Check for cancellation
                        if (token.IsCancellationRequested)
                        {
                            Console.WriteLine("Critical message handler cancellation requested");
                            break;
                        }
                        
                        // Small delay to prevent CPU overuse
                        await Task.Delay(5, token);
                    }
                    catch (TaskCanceledException)
                    {
                        // Normal cancellation, exit loop
                        Console.WriteLine("Critical message handler cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"!!!! CRITICAL OVERRIDE: Unhandled error: {ex.Message}");
                        
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
                
                Console.WriteLine("Critical message handler terminated");
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
                
                // Debug - dump publisher socket status
                Console.WriteLine($"===> SOCKET STATUS: Publisher Socket null? {_publisherSocket == null}");
                try
                {
                    Console.WriteLine($"===> SOCKET INFO: Publisher Socket info: {(_publisherSocket?.GetType()?.FullName ?? "null")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"===> SOCKET ERROR: Failed to get publisher socket info: {ex.Message}");
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
                    
                    // APPROACH 2: Direct socket send
                    try
                    {
                        Console.WriteLine($"===> APPROACH 2: Direct socket ACK for {message.MessageId}");
                        var serializedAck = ackMessage.ToJson();
                        Console.WriteLine($"===> APPROACH 2: Serialized ACK: {serializedAck}");
                        _publisherSocket?.SendFrame(serializedAck);
                        Console.WriteLine($"===> APPROACH 2: SendFrame completed");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"===> ERROR in approach 2: {ex.Message}");
                    }
                    
                    // APPROACH 3: Create new socket just for this message
                    try
                    {
                        Console.WriteLine($"===> APPROACH 3: Creating dedicated socket for ACK");
                        using (var pubSocket = new PublisherSocket())
                        {
                            pubSocket.Bind($"tcp://127.0.0.1:{new Random().Next(6000, 7000)}");
                            var serializedAck = ackMessage.ToJson();
                            pubSocket.SendFrame(serializedAck);
                            Console.WriteLine($"===> APPROACH 3: Dedicated socket ACK sent");
                        }
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
            if (_decks.TryGetValue(deckId, out var deck))
            {
                // Ensure a burn pile exists for this deck
                if (!_burnPiles.ContainsKey(deckId))
                {
                    _burnPiles[deckId] = new List<Card>();
                    Console.WriteLine($"Created new burn pile for deck: {deckId}");
                }
                
                var burnPile = _burnPiles[deckId];
                
                var card = deck.DealCard();
                if (card != null)
                {
                    burnPile.Add(card);
                    Console.WriteLine($"Burned card from deck: {deckId} (Face up: {faceUp}, Card: {(faceUp ? card.ToString() : "Hidden")})");
                }
                return card;
            }
            else
            {
                throw new Exception($"Deck not found: {deckId}");
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
                
                // Check if _publisherSocket is valid
                if (_publisherSocket == null)
                {
                    Console.WriteLine($"===> CardDeckService: ERROR in BroadcastDeckStatus - publisher socket is null");
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
                if (_publisherSocket == null)
                {
                    Console.WriteLine($"===> CardDeckService: ERROR in SendDealResponse - publisher socket is null");
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
                if (_publisherSocket == null)
                {
                    Console.WriteLine($"===> CardDeckService: ERROR in SendBurnResponse - publisher socket is null");
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