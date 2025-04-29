using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using PokerGame.Core.Messaging;
using PokerGame.Core.Microservices;

namespace SimpleMessageTest
{
    class Program 
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("===== Simple StartHand Message Flow Test =====");
            Console.WriteLine("Testing message flow with current message types");
            
            try 
            {
                // Initialize broker
                Console.WriteLine("Starting BrokerManager...");
                BrokerManager.Instance.Start(null);
                
                // Start central broker
                Console.WriteLine("Starting CentralMessageBroker...");
                var broker = BrokerManager.Instance.StartCentralBroker(25555, null, true);
                
                if (broker == null)
                {
                    Console.WriteLine("ERROR: Failed to start central broker");
                    return;
                }
                
                Console.WriteLine("CentralMessageBroker started successfully");
                
                // Define service IDs
                string consoleServiceId = "test_ui_service";
                string gameEngineId = "test_engine_service";
                
                // Track message receipt
                bool startHandReceived = false;
                bool responseReceived = false;
                string startHandId = string.Empty;
                
                // Subscribe UI service to receive responses
                broker.Subscribe(consoleServiceId, (message) => {
                    Console.WriteLine($"UI received: Type={message.Type}, From={message.SenderId}");
                    
                    if (message.Type == MessageType.HandStarted && message.InResponseTo == startHandId)
                    {
                        Console.WriteLine("\n=== SUCCESS: UI received HandStarted response ===");
                        responseReceived = true;
                    }
                    
                    return true;
                });
                
                // Subscribe game engine to handle StartHand messages
                broker.Subscribe(gameEngineId, (message) => {
                    Console.WriteLine($"Engine received: Type={message.Type}, From={message.SenderId}");
                    
                    if (message.Type == MessageType.StartHand)
                    {
                        Console.WriteLine("\n=== SUCCESS: Engine received StartHand message ===");
                        startHandReceived = true;
                        
                        // Create direct response
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
                                { "MessageSubType", "HandStarted" },
                                { "ResponseType", "HandStarted" }
                            }
                        };
                        
                        Console.WriteLine($"Engine sending HandStarted response");
                        broker.Publish(response);
                    }
                    
                    return true;
                });
                
                // Allow subscriptions to initialize
                await Task.Delay(500);
                
                // Send StartHand message
                Console.WriteLine("\nSending StartHand message...");
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
                
                // Store ID for verification
                startHandId = startHandMessage.MessageId;
                
                // Send the message
                Console.WriteLine($"Sending StartHand (ID: {startHandId})");
                broker.Publish(startHandMessage);
                
                // Wait for processing
                Console.WriteLine("Waiting for message processing...");
                for (int i = 0; i < 10; i++)
                {
                    if (startHandReceived && responseReceived)
                    {
                        break;
                    }
                    await Task.Delay(500);
                }
                
                // Report results
                Console.WriteLine("\n===== TEST RESULTS =====");
                Console.WriteLine($"StartHand received by engine: {startHandReceived}");
                Console.WriteLine($"HandStarted received by UI: {responseReceived}");
                
                if (startHandReceived && responseReceived)
                {
                    Console.WriteLine("\nTEST PASSED: Message flow is working correctly!");
                } 
                else 
                {
                    Console.WriteLine("\nTEST FAILED: Message flow has issues.");
                    if (!startHandReceived) Console.WriteLine("  - Engine did not receive StartHand");
                    if (!responseReceived) Console.WriteLine("  - UI did not receive HandStarted response");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                // Cleanup
                BrokerManager.Instance.Stop();
                Console.WriteLine("Test completed. Resources cleaned up.");
            }
        }
    }
}
