using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using PokerGame.Core.Messaging;
using PokerGame.Core.Microservices;
using MSA.Foundation.ServiceManagement;

// Use aliases to avoid ambiguity between message types
using PokerMessageType = PokerGame.Core.Messaging.MessageType;
using MSAEC = MSA.Foundation.ServiceManagement.ExecutionContext;

namespace MessageFlowTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("===== StartHand Message Flow Test =====");
            Console.WriteLine("This test validates the flow of StartHand messages and DeckShuffled responses");
            
            // Use a central execution context
            var context = new MSAEC();
            
            try
            {
                // Initialize broker
                Console.WriteLine("Starting BrokerManager...");
                BrokerManager.Instance.Start(context);
                
                // Start central broker
                Console.WriteLine("Starting CentralMessageBroker...");
                var broker = BrokerManager.Instance.StartCentralBroker(25555, context, true);
                
                if (broker == null)
                {
                    Console.WriteLine("ERROR: Failed to start central broker");
                    return;
                }
                
                Console.WriteLine("CentralMessageBroker started successfully");
                
                // Create test services with minimally required functionality
                string consoleServiceId = "test_ui_service";
                string gameEngineId = "test_engine_service";
                
                // Subscribe to messages for the UI service
                Console.WriteLine($"Setting up subscriber for console service {consoleServiceId}...");
                
                broker.Subscribe(consoleServiceId, (message) => {
                    Console.WriteLine($"UI SERVICE received message: Type={message.Type}, From={message.SenderId}, ID={message.MessageId}");
                    
                    if (message.Type == PokerMessageType.DeckShuffled)
                    {
                        Console.WriteLine("\n!!!! SUCCESS !!!!");
                        Console.WriteLine($"UI SERVICE received DeckShuffled response to StartHand!");
                        Console.WriteLine($"Message ID: {message.MessageId}");
                        Console.WriteLine($"In Response To: {message.InResponseTo}");
                        Console.WriteLine($"From: {message.SenderId}");
                        Console.WriteLine("!!!! SUCCESS !!!!\n");
                    }
                    
                    return true;
                });
                
                // Subscribe to messages for the game engine service
                Console.WriteLine($"Setting up subscriber for game engine {gameEngineId}...");
                
                broker.Subscribe(gameEngineId, (message) => {
                    Console.WriteLine($"GAME ENGINE received message: Type={message.Type}, From={message.SenderId}, ID={message.MessageId}");
                    
                    // If this is a StartHand message, respond with DeckShuffled
                    if (message.Type == PokerMessageType.StartHand)
                    {
                        Console.WriteLine("\n!!!! RECEIVED START HAND !!!!\n");
                        
                        // Create DeckShuffled response - DIRECT NetworkMessage creation to avoid conversion issues
                        var response = new NetworkMessage
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            Type = PokerMessageType.DeckShuffled, // Use explicit enum value with alias
                            SenderId = gameEngineId,
                            ReceiverId = message.SenderId,
                            InResponseTo = message.MessageId,
                            Timestamp = DateTime.UtcNow,
                            Headers = new Dictionary<string, string>
                            {
                                { "OriginalMessageId", message.MessageId },
                                { "ResponseType", "DeckShuffled" }
                            }
                        };
                        
                        // Send the response via the broker
                        Console.WriteLine($"GAME ENGINE sending DeckShuffled response: {response.MessageId}");
                        broker.Publish(response);
                    }
                    
                    return true;
                });
                
                // Wait for subscriptions to initialize
                await Task.Delay(500);
                
                // Create and send a StartHand message from the UI service to game engine
                Console.WriteLine("\nCreating StartHand message...");
                
                // Create StartHand message directly to avoid conversion issues
                var startHandMessage = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = PokerMessageType.StartHand, // Use explicit enum value with alias
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
                
                Console.WriteLine("\nTest completed. Check the results above to determine success.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR during test execution: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                // Cleanup
                BrokerManager.Instance.Stop();
                Console.WriteLine("Resources cleaned up.");
            }
        }
    }
}
