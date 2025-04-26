using System;
using System.Threading.Tasks;
using PokerGame.Core.Messaging;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Extension methods for MicroserviceBase to integrate with MessageBroker
    /// </summary>
    public static class MicroserviceBaseExtensions
    {
        /// <summary>
        /// Gets the service name from a microservice
        /// </summary>
        /// <param name="service">The microservice</param>
        /// <returns>The service name</returns>
        public static string GetServiceName(MicroserviceBase service)
        {
            // Use reflection to get the service name
            var field = typeof(MicroserviceBase).GetField("_serviceName", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            return field?.GetValue(service)?.ToString() ?? "UnknownService";
        }
        
        /// <summary>
        /// Handles service registration messages to register new services
        /// </summary>
        /// <param name="service">The microservice</param>
        /// <param name="message">The service registration message</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task HandleServiceRegistrationAsync(MicroserviceBase service, Message message)
        {
            await Task.Run(() => {
                // Extract the registration information
                var payload = message.GetPayload<ServiceRegistrationPayload>();
                if (payload != null)
                {
                    // Call the OnServiceRegistered method via reflection
                    var method = typeof(MicroserviceBase).GetMethod("OnServiceRegistered", 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance);
                        
                    method?.Invoke(service, new object[] { payload });
                }
            });
        }
        /// <summary>
        /// Sends a message to a specific service with guaranteed delivery via the MessageBroker
        /// </summary>
        /// <param name="service">The microservice sending the message</param>
        /// <param name="message">The message to send</param>
        /// <param name="receiverId">The ID of the receiving service</param>
        /// <param name="timeoutMs">Timeout in milliseconds for the acknowledgment</param>
        /// <param name="maxRetries">Maximum number of retries if acknowledgment not received</param>
        /// <param name="useExponentialBackoff">Whether to use exponential backoff between retries</param>
        /// <returns>A task representing the acknowledgment status</returns>
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
                    // Create a temporary message broker for this operation with specific ports
                    // Use high port numbers to avoid conflicts with a bit more randomness on retries
                    int brokerPublishPort = 25560 + new Random().Next(100) + (retryCount * 10);  
                    int brokerSubscribePort = 25570 + new Random().Next(100) + (retryCount * 10);
                    
                    using var messageBroker = new MicroserviceMessageBroker(service, brokerPublishPort, brokerSubscribePort);
                    messageBroker.Start();
                    
                    // Set up acknowledgment tracking
                    var ackReceived = new TaskCompletionSource<bool>();
                    
                    // Define the acknowledgment pattern - what message type confirms receipt
                    MessageType expectedAckType = GetAcknowledgmentType(message.Type);
                    
                    // Register a handler for the acknowledgment message
                    messageBroker.RegisterMessageHandler(expectedAckType, async (ackMessage) => {
                        // Verify this is an acknowledgment for our specific message
                        if (ackMessage.InResponseTo == message.MessageId)
                        {
                            Console.WriteLine($"Received acknowledgment for message {message.MessageId}");
                            ackReceived.TrySetResult(true);
                        }
                        await Task.CompletedTask;
                    });
                    
                    // Send the message
                    messageBroker.SendTo(message, receiverId);
                    
                    // Wait for acknowledgment or timeout
                    var timeoutTask = Task.Delay(timeoutMs);
                    var completedTask = await Task.WhenAny(ackReceived.Task, timeoutTask);
                    
                    // Check if we got acknowledgment or timed out
                    if (completedTask == ackReceived.Task)
                    {
                        // We got the acknowledgment
                        success = await ackReceived.Task;
                        if (success)
                        {
                            // Log success
                            Console.WriteLine($"[{service.ServiceId}] Message {message.Type} to {receiverId} acknowledged successfully" + 
                                            (retryCount > 0 ? $" after {retryCount} retries" : ""));
                            return true;
                        }
                    }
                    else
                    {
                        // Timed out waiting for acknowledgment
                        Console.WriteLine($"Timed out waiting for acknowledgment of message {message.MessageId}");
                    }
                    
                    // Cleanup the message broker
                    messageBroker.Stop();
                    
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
                    Console.WriteLine($"[{service.ServiceId}] Error in SendWithAcknowledgmentAsync: {ex.Message}");
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
        
        /// <summary>
        /// Determine what message type serves as acknowledgment for a given request type
        /// </summary>
        /// <param name="requestType">The type of the request message</param>
        /// <returns>The expected acknowledgment message type</returns>
        private static MessageType GetAcknowledgmentType(MessageType requestType)
        {
            // Pattern matching for request/response message pairs
            switch (requestType)
            {
                case MessageType.DeckCreate:
                    return MessageType.DeckCreated;
                    
                case MessageType.DeckShuffle:
                    return MessageType.DeckShuffled;
                    
                case MessageType.DeckDeal:
                    return MessageType.DeckDealt;
                    
                case MessageType.DeckStatus:
                    return MessageType.DeckStatusResponse;
                    
                case MessageType.StartHand:
                    return MessageType.HandStarted;
                    
                case MessageType.EndHand:
                    return MessageType.HandEnded;
                    
                case MessageType.StartGame:
                    return MessageType.GameStarted;
                    
                case MessageType.EndGame:
                    return MessageType.GameEnded;
                    
                // For messages that don't have a specific acknowledgment type
                default:
                    return MessageType.Acknowledgment;
            }
        }
    }
}