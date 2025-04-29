using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using PokerGame.Core.Messaging;
using PokerGame.Core.Microservices;

namespace MessageFlowTest
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("===== Minimal Message Flow Test =====");
            
            try 
            {
                // Create basic execution context with its own cancellation token
                Console.WriteLine("Creating execution context...");
                var cancellationTokenSource = new CancellationTokenSource();
                var executionContext = new PokerGame.Core.Messaging.ExecutionContext(cancellationTokenSource);
                
                // Initialize broker directly
                Console.WriteLine("Creating CentralMessageBroker...");
                var broker = new CentralMessageBroker(executionContext);
                broker.Start();
                
                // Track received messages
                bool messageReceived = false;
                string messageContent = $"Test message sent at {DateTime.UtcNow}";
                
                // Set up subscription for Debug messages
                Console.WriteLine("Setting up Debug message subscription...");
                string subId = broker.Subscribe(MessageType.Debug, (message) => {
                    Console.WriteLine($"Received message: {message.Type}, From={message.SenderId}, ID={message.MessageId}");
                    
                    try 
                    {
                        // Attempt to get payload content
                        Console.WriteLine($"Raw payload: {message.Payload}");
                        messageReceived = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing payload: {ex.Message}");
                    }
                });
                
                // Wait for subscription to initialize
                await Task.Delay(500);
                
                // Create and send a Debug message 
                Console.WriteLine("\nCreating Debug message...");
                
                // Create message directly
                var debugMessage = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = MessageType.Debug,
                    SenderId = "test_sender",
                    ReceiverId = "",  // Broadcast
                    Timestamp = DateTime.UtcNow,
                    Payload = messageContent,
                    Headers = new Dictionary<string, string>
                    {
                        { "TestHeader", "TestValue" }
                    }
                };
                
                // Log and send the message
                Console.WriteLine($"Sending Debug message: ID={debugMessage.MessageId}, From={debugMessage.SenderId}");
                broker.Publish(debugMessage);
                
                // Wait for the message processing
                Console.WriteLine("Waiting for message processing (2 seconds)...");
                await Task.Delay(2000);
                
                Console.WriteLine("\n===== TEST SUMMARY =====");
                Console.WriteLine($"Message received: {messageReceived}");
                
                if (messageReceived)
                {
                    Console.WriteLine("\nTEST PASSED: Basic message flow is working!");
                }
                else
                {
                    Console.WriteLine("\nTEST FAILED: Message was not received.");
                }
                
                // Unsubscribe to clean up properly
                broker.Unsubscribe(subId, MessageType.Debug);
                
                // Stop the broker
                broker.Stop();
                cancellationTokenSource.Cancel();
                
                Console.WriteLine("Resources cleaned up.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}