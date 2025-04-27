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
        /// Sends a message with acknowledgment using the MicroserviceMessageBroker
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
                    // Create a message envelope from the message
                    var envelope = new MessageEnvelope
                    {
                        MessageId = message.MessageId,
                        Type = message.Type.ToString(),
                        SenderServiceId = message.SenderId,
                        TargetServiceId = receiverId,
                        Timestamp = DateTime.UtcNow,
                        Payload = message.Payload
                    };
                    
                    // Add required metadata
                    envelope.Metadata["SenderId"] = message.SenderId;
                    envelope.Metadata["MessageId"] = message.MessageId;
                    envelope.Metadata["TimeStamp"] = DateTime.UtcNow.ToString("o");
                    envelope.Metadata["TargetId"] = receiverId;
                    envelope.Metadata["InResponseTo"] = message.InResponseTo ?? string.Empty;
                    envelope.Metadata["RetryCount"] = retryCount.ToString();
                    
                    // Use the MessageBroker directly to get delivery confirmation
                    // Use ports that are unlikely to conflict with a bit more randomness on retries
                    int randomPortBase = new Random().Next(2000 + (retryCount * 500), 5000 + (retryCount * 500));
                    using (var broker = new MessageBroker(
                        service.ServiceId, 
                        randomPortBase, // publish port
                        randomPortBase + 1)) // subscribe port
                    {
                        // Start the broker explicitly
                        broker.Start();
                        
                        // Send the message and wait for acknowledgment
                        success = await broker.SendWithAcknowledgmentAsync(envelope, timeoutMs);
                        
                        // Make sure to stop the broker
                        broker.Stop();
                        
                        if (success)
                        {
                            // Log success
                            Console.WriteLine($"[{service.ServiceId}] Message {message.Type} to {receiverId} acknowledged successfully" + 
                                             (retryCount > 0 ? $" after {retryCount} retries" : ""));
                            return true;
                        }
                    }
                    
                    // If we're here, the message wasn't acknowledged
                    retryCount++;
                    
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
            
            // If we get here, we failed after all retries
            Console.WriteLine($"[{service.ServiceId}] Failed to get acknowledgment for message {message.Type} to {receiverId} after {maxRetries} retries");
            return false;
        }
    }
}