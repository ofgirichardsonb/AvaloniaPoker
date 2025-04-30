using System;
using System.Threading.Tasks;
using PokerGame.Core.Microservices;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Extensions for integrating the MessageBroker with existing microservices
    /// </summary>
    public static class MessageBrokerExtensions
    {
        /// <summary>
        /// Converts a legacy message to a message envelope
        /// </summary>
        /// <param name="message">Legacy message to convert</param>
        /// <returns>Equivalent message envelope</returns>
        public static MessageEnvelope ToEnvelope(this Message message)
        {
            var envelope = new MessageEnvelope
            {
                MessageId = message.MessageId,
                Type = message.Type.ToString(),
                SenderServiceId = message.SenderId,
                TargetServiceId = message.ReceiverId,
                Timestamp = message.Timestamp,
                Payload = message.Payload
            };
            
            return envelope;
        }
        
        /// <summary>
        /// Converts a message envelope to a legacy message
        /// </summary>
        /// <param name="envelope">Message envelope to convert</param>
        /// <returns>Equivalent legacy message</returns>
        public static Message ToLegacyMessage(this MessageEnvelope envelope)
        {
            // Try to parse the message type from the string
            // Parse the message type into Microservices.MessageType for the Message
            Microservices.MessageType microservicesMessageType;
            
            if (!Enum.TryParse<Microservices.MessageType>(envelope.Type, out microservicesMessageType))
            {
                microservicesMessageType = Microservices.MessageType.Error; // Default to Error if unknown
            }
            
            var message = new Message
            {
                MessageId = envelope.MessageId,
                Type = microservicesMessageType,
                SenderId = envelope.GetMetadata("SenderId"),
                ReceiverId = envelope.GetMetadata("TargetId"),
                Timestamp = envelope.Timestamp,
                InResponseTo = envelope.GetMetadata("InResponseTo")
            };
            
            // Get payload
            if (envelope.Payload != null)
            {
                // For legacy Message compatibility, convert payload to string if needed
                if (envelope.Payload is string stringPayload)
                {
                    message.Payload = stringPayload;
                }
                else
                {
                    message.Payload = envelope.Payload.ToString() ?? string.Empty;
                }
            }
            
            return message;
        }
        
        /// <summary>
        /// Adapter to handle message envelopes with legacy message handlers
        /// </summary>
        /// <param name="legacyHandler">The legacy message handler</param>
        /// <returns>Adapted handler for message envelopes</returns>
        public static MessageBroker.MessageHandlerDelegate AdaptLegacyHandler(
            Func<Message, Task> legacyHandler)
        {
            return async (envelope) =>
            {
                var legacyMessage = envelope.ToLegacyMessage();
                await legacyHandler(legacyMessage);
            };
        }
        
        /// <summary>
        /// Registers a message broker service with a microservice manager
        /// </summary>
        /// <param name="manager">The microservice manager</param>
        /// <param name="broker">The message broker to register</param>
        public static void RegisterMessageBroker(this MicroserviceManager manager, MessageBroker broker)
        {
            // This will be implemented when we integrate with the existing architecture
            Console.WriteLine("Message broker registered with microservice manager");
        }
        
        /// <summary>
        /// Sends a message with acknowledgment using the CentralMessageBroker to avoid NetMQ context termination issues
        /// </summary>
        /// <param name="service">The microservice</param>
        /// <param name="message">Message to send</param>
        /// <param name="receiverId">ID of the receiver</param>
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <param name="maxRetries">Maximum number of retries if acknowledgment not received</param>
        /// <param name="useExponentialBackoff">Whether to use exponential backoff between retries</param>
        /// <returns>True if the message was acknowledged, false otherwise</returns>
        public static async Task<bool> SendWithAcknowledgmentAsync(
            this MicroserviceBase service, 
            Message message, 
            string receiverId, 
            int timeoutMs = 5000,
            int maxRetries = 3,
            bool useExponentialBackoff = true)
        {
            // Ensure message has sender ID set
            message.SenderId = service.ServiceId;
            
            // Make sure the message has an ID
            if (string.IsNullOrEmpty(message.MessageId))
            {
                message.MessageId = Guid.NewGuid().ToString();
            }
            
            // Initialize retry variables
            int retryCount = 0;
            bool success = false;
            
            // Log what we're doing initially
            Console.WriteLine($"[{service.ServiceId}] Sending message {message.Type} to {receiverId} with acknowledgment (timeout: {timeoutMs}ms, max retries: {maxRetries})");
            
            // Get the central broker from BrokerManager
            var centralBroker = BrokerManager.Instance.CentralBroker;
            if (centralBroker == null)
            {
                Console.WriteLine($"[{service.ServiceId}] Error: Central broker is not available");
                return false;
            }
            
            // Register a one-time acknowledgment handler with the central broker
            var tcs = new TaskCompletionSource<bool>();
            string ackedMessageId = message.MessageId;
            string? subscriptionId = null;
            
            try
            {
                // Set up a handler to detect acknowledgments specifically for this message
                // Convert MessageType enum to the correct type for CentralMessageBroker.Subscribe
                // NOTE: We must convert from MSA.Foundation.Messaging.MessageType to PokerGame.Core.Messaging.MessageType
                var ackMessageType = PokerGame.Core.Messaging.MessageType.Acknowledgment;
                
                // Create a handler that accepts a single NetworkMessage parameter
                Action<NetworkMessage> ackHandler = (ackMessage) => {
                    try
                    {
                        if (ackMessage != null && 
                            ackMessage.Headers != null &&
                            ackMessage.Headers.TryGetValue("InResponseTo", out string? inResponseTo) && 
                            inResponseTo == ackedMessageId)
                        {
                            Console.WriteLine($"[{service.ServiceId}] Received acknowledgment for message ID: {ackedMessageId}");
                            // Signal the waiting task
                            tcs.TrySetResult(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{service.ServiceId}] Error processing acknowledgment: {ex.Message}");
                    }
                };
                
                // Subscribe to acknowledgment messages
                subscriptionId = centralBroker.Subscribe(ackMessageType, ackHandler);
                Console.WriteLine($"[{service.ServiceId}] Subscribed to acknowledgments with ID: {subscriptionId}");
                
                // Try until we succeed or run out of retries
                while (!success && retryCount <= maxRetries)
                {
                    // Log retry information if this isn't the first attempt
                    if (retryCount > 0)
                    {
                        Console.WriteLine($"[{service.ServiceId}] Retry attempt {retryCount}/{maxRetries} for message {message.Type} to {receiverId}");
                    }
                    
                    try
                    {
                        // Create a network message that requires acknowledgment
                        var networkMsg = new NetworkMessage
                        {
                            MessageId = message.MessageId,
                            // Map to the correct enum type
                            Type = MapMessageType(message.Type),
                            SenderId = message.SenderId,
                            ReceiverId = receiverId,
                            Timestamp = DateTime.UtcNow,
                            Payload = message.Payload,
                            Headers = new Dictionary<string, string>
                            {
                                { "RequireAcknowledgment", "true" },
                                { "InResponseTo", message.InResponseTo ?? string.Empty },
                                { "RetryCount", retryCount.ToString() }
                            }
                        };
                        
                        // Send through the central broker
                        Console.WriteLine($"[{service.ServiceId}] Publishing message {message.Type} (ID: {message.MessageId}) through central broker");
                        centralBroker.Publish(networkMsg);
                        
                        // Wait for acknowledgment with timeout
                        var timeoutTask = Task.Delay(timeoutMs);
                        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                        
                        if (completedTask == tcs.Task)
                        {
                            // Success - acknowledgment received
                            success = true;
                            Console.WriteLine($"[{service.ServiceId}] Message {message.Type} to {receiverId} acknowledged successfully" + 
                                            (retryCount > 0 ? $" after {retryCount} retries" : ""));
                            break;
                        }
                        else
                        {
                            // Timeout waiting for acknowledgment
                            retryCount++;
                            
                            // Reset the task completion source for the next attempt
                            tcs = new TaskCompletionSource<bool>();
                            
                            // Exit if we've exceeded max retries
                            if (retryCount > maxRetries)
                            {
                                break;
                            }
                            
                            // Calculate delay before next retry
                            int delayMs;
                            if (useExponentialBackoff)
                            {
                                // Exponential backoff with jitter (max 10 seconds)
                                delayMs = Math.Min(1000 * (int)Math.Pow(2, retryCount - 1), 10000);
                                delayMs += new Random().Next(-delayMs / 4, delayMs / 4); // Add jitter
                            }
                            else
                            {
                                // Linear backoff (500ms per retry)
                                delayMs = 500 * retryCount;
                            }
                            
                            Console.WriteLine($"[{service.ServiceId}] Message {message.Type} not acknowledged, waiting {delayMs}ms before retry");
                            await Task.Delay(delayMs);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log detailed exception info
                        Console.WriteLine($"[{service.ServiceId}] Error sending message with acknowledgment: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"[{service.ServiceId}] Inner exception: {ex.InnerException.Message}");
                        }
                        
                        // Increment retry counter
                        retryCount++;
                        
                        // Reset the task completion source for the next attempt
                        tcs = new TaskCompletionSource<bool>();
                        
                        // Exit if we've exceeded max retries
                        if (retryCount > maxRetries)
                        {
                            break;
                        }
                        
                        // Simple backoff for exceptions (shorter than the normal backoff)
                        int delayMs = 250 * retryCount;
                        await Task.Delay(delayMs);
                    }
                }
                
                return success;
            }
            finally
            {
                // Clean up the subscription
                if (subscriptionId != null)
                {
                    try
                    {
                        Console.WriteLine($"[{service.ServiceId}] Unsubscribing acknowledgment handler (ID: {subscriptionId})");
                        // The central broker's unsubscribe method requires both the ID and message type
                        centralBroker.Unsubscribe(subscriptionId, PokerGame.Core.Messaging.MessageType.Acknowledgment);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{service.ServiceId}] Error unsubscribing: {ex.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Maps between the Microservices.MessageType and PokerGame.Core.Messaging.MessageType enums
        /// </summary>
        /// <param name="messageType">The source message type from Microservices namespace</param>
        /// <returns>The corresponding message type in the PokerGame.Core.Messaging namespace</returns>
        private static PokerGame.Core.Messaging.MessageType MapMessageType(Microservices.MessageType messageType)
        {
            // Map common message types between the two enum namespaces
            switch (messageType)
            {
                case Microservices.MessageType.Acknowledgment:
                    return PokerGame.Core.Messaging.MessageType.Acknowledgment;
                case Microservices.MessageType.ServiceDiscovery:
                    return PokerGame.Core.Messaging.MessageType.ServiceDiscovery;
                case Microservices.MessageType.ServiceRegistration:
                    return PokerGame.Core.Messaging.MessageType.ServiceRegistration;
                case Microservices.MessageType.StartGame:
                    return PokerGame.Core.Messaging.MessageType.StartGame;
                case Microservices.MessageType.StartHand:
                    return PokerGame.Core.Messaging.MessageType.StartHand;
                case Microservices.MessageType.DealCards:
                    return PokerGame.Core.Messaging.MessageType.DeckDeal;
                case Microservices.MessageType.PlayerAction:
                    return PokerGame.Core.Messaging.MessageType.PlayerAction;
                case Microservices.MessageType.Heartbeat:
                    return PokerGame.Core.Messaging.MessageType.Heartbeat;
                default:
                    // For other message types, use generic GameState type
                    return PokerGame.Core.Messaging.MessageType.GameState;
            }
        }
    }
}