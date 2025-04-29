using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using PokerGame.Core.Messaging;
using PokerGame.Core.Microservices;

namespace StartHandTest
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("===== StartHand Message Fix Test =====");
            
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
                
                // Create test services with minimally required functionality
                string consoleServiceId = "test_ui_service";
                string gameEngineId = "test_engine_service";
                
                // Track received messages
                bool responseReceived = false;
                bool startHandReceived = false;
                
                // Subscribe to HandStarted messages and filter for those meant for the console service
                Console.WriteLine("Setting up HandStarted subscription...");
                string handStartedSubId = broker.Subscribe(MessageType.HandStarted, (message) => {
                    // Only process messages meant for our test UI service
                    if (message.ReceiverId == consoleServiceId)
                    {
                        Console.WriteLine($"UI SERVICE received message: Type={message.Type}, From={message.SenderId}, ID={message.MessageId}");
                        
                        Console.WriteLine("\n!!!! SUCCESS !!!!");
                        Console.WriteLine($"UI SERVICE received HandStarted response to StartHand message!");
                        Console.WriteLine($"Message ID: {message.MessageId}");
                        Console.WriteLine($"In Response To: {message.InResponseTo}");
                        Console.WriteLine($"From: {message.SenderId}");
                        Console.WriteLine("!!!! SUCCESS !!!!\n");
                        
                        responseReceived = true;
                    }
                });
                
                // Subscribe to StartHand messages and filter for those meant for the game engine service
                Console.WriteLine("Setting up StartHand subscription...");
                string startHandSubId = broker.Subscribe(MessageType.StartHand, (message) => {
                    // Only process messages meant for our test game engine service
                    if (message.ReceiverId == gameEngineId)
                    {
                        Console.WriteLine($"GAME ENGINE received message: Type={message.Type}, From={message.SenderId}, ID={message.MessageId}");
                        
                        Console.WriteLine("\n!!!! RECEIVED START HAND !!!!\n");
                        startHandReceived = true;
                        
                        // Create response message directly for consistency
                        var response = new NetworkMessage
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            Type = MessageType.HandStarted,
                            SenderId = gameEngineId,
                            ReceiverId = message.SenderId,
                            InResponseTo = message.MessageId,
                            Timestamp = DateTime.UtcNow,
                            Headers = new Dictionary<string, string>
                            {
                                { "OriginalMessageId", message.MessageId },
                                { "ResponseType", "HandStarted" }
                            }
                        };
                        
                        // Send the response via the broker
                        Console.WriteLine($"GAME ENGINE sending HandStarted response: {response.MessageId}");
                        broker.Publish(response);
                    }
                });
                
                // Wait for subscriptions to initialize
                await Task.Delay(500);
                
                // Create and send a StartHand message from the UI service to game engine
                Console.WriteLine("\nCreating StartHand message...");
                
                // Create StartHand message directly
                var startHandMessage = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = MessageType.StartHand,
                    SenderId = consoleServiceId,
                    ReceiverId = gameEngineId,
                    Timestamp = DateTime.UtcNow,
                    Headers = new Dictionary<string, string>
                    {
                        { "MessageSubType", "StartHand" }
                    }
                };
                
                // Log and send the message
                Console.WriteLine($"Sending StartHand message: ID={startHandMessage.MessageId}, From={startHandMessage.SenderId}, To={startHandMessage.ReceiverId}");
                broker.Publish(startHandMessage);
                
                // Wait for the message round-trip
                Console.WriteLine("Waiting for message processing (5 seconds)...");
                await Task.Delay(5000);
                
                Console.WriteLine("\n===== TEST SUMMARY =====");
                Console.WriteLine($"StartHand received by GameEngine: {startHandReceived}");
                Console.WriteLine($"HandStarted received by ConsoleUI: {responseReceived}");
                
                if (startHandReceived && responseReceived)
                {
                    Console.WriteLine("\nTEST PASSED: StartHand message flow is working correctly!");
                }
                else
                {
                    Console.WriteLine("\nTEST FAILED: StartHand message flow is not working correctly.");
                    if (!startHandReceived) Console.WriteLine("- GameEngine did not receive the StartHand message.");
                    if (!responseReceived) Console.WriteLine("- ConsoleUI did not receive the HandStarted response message.");
                }
                
                // Unsubscribe to clean up properly
                broker.Unsubscribe(handStartedSubId, MessageType.HandStarted);
                broker.Unsubscribe(startHandSubId, MessageType.StartHand);
                
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