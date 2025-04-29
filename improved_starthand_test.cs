// Improved StartHand message flow test
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using PokerGame.Core.Messaging;
using PokerGame.Core.Microservices;

// Simple test to validate that the StartHand message flow works
class StartHandTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("===== StartHand Message Flow Test (Improved) =====");
        Console.WriteLine("This test validates the flow of StartHand messages through CentralMessageBroker");
        
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
            
            // Create test services with minimally required functionality
            string consoleServiceId = "test_ui_service";
            string gameEngineId = "test_engine_service";
            
            // Track received messages for verification
            bool startHandReceived = false;
            bool responseReceived = false;
            string startHandMessageId = string.Empty;
            
            // Subscribe to messages for the UI service
            Console.WriteLine($"Setting up subscriber for console service {consoleServiceId}...");
            
            broker.Subscribe(consoleServiceId, (message) => {
                Console.WriteLine($"UI SERVICE received message: Type={message.Type}, From={message.SenderId}, ID={message.MessageId}");
                
                // Check if we got a response to our StartHand message
                if (message.InResponseTo == startHandMessageId)
                {
                    Console.WriteLine("\n!!!! SUCCESS !!!!");
                    Console.WriteLine($"UI SERVICE received response to StartHand message!");
                    Console.WriteLine($"Message Type: {message.Type}");
                    Console.WriteLine($"Message ID: {message.MessageId}");
                    Console.WriteLine($"In Response To: {message.InResponseTo}");
                    Console.WriteLine($"From: {message.SenderId}");
                    Console.WriteLine("!!!! SUCCESS !!!!\n");
                    
                    responseReceived = true;
                }
                
                return true;
            });
            
            // Subscribe to messages for the game engine service
            Console.WriteLine($"Setting up subscriber for game engine {gameEngineId}...");
            
            broker.Subscribe(gameEngineId, (message) => {
                Console.WriteLine($"GAME ENGINE received message: Type={message.Type}, From={message.SenderId}, ID={message.MessageId}");
                
                // If this is a StartHand message, respond to it
                if (message.Type == MessageType.StartHand)
                {
                    Console.WriteLine("\n!!!! RECEIVED START HAND !!!!\n");
                    startHandReceived = true;
                    
                    // Create response message directly as NetworkMessage to avoid conversion issues
                    var response = new NetworkMessage
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        Type = MessageType.HandStarted, // Use HandStarted as response type
                        SenderId = gameEngineId,
                        ReceiverId = message.SenderId,
                        InResponseTo = message.MessageId,
                        Timestamp = DateTime.UtcNow,
                        Headers = new Dictionary<string, string>
                        {
                            { "OriginalMessageId", message.MessageId },
                            { "ResponseType", "HandStarted" },
                            { "MessageSubType", "HandStarted" }
                        }
                    };
                    
                    // Send the response via the broker
                    Console.WriteLine($"GAME ENGINE sending HandStarted response: {response.MessageId}");
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
                Type = MessageType.StartHand, // Use explicit enum value
                SenderId = consoleServiceId,
                ReceiverId = gameEngineId,
                Timestamp = DateTime.UtcNow,
                Headers = new Dictionary<string, string>
                {
                    { "MessageSubType", "StartHand" }
                }
            };
            
            // Store the message ID for verification
            startHandMessageId = startHandMessage.MessageId;
            
            // Log and send the message
            Console.WriteLine($"Sending StartHand message: ID={startHandMessageId}, From={startHandMessage.SenderId}, To={startHandMessage.ReceiverId}");
            broker.Publish(startHandMessage);
            
            // Wait for the message round-trip
            Console.WriteLine("Waiting for message processing (5 seconds)...");
            
            // Wait with periodic checks for completion
            for (int i = 0; i < 10; i++)
            {
                if (startHandReceived && responseReceived)
                {
                    Console.WriteLine("Test completed successfully!");
                    break;
                }
                
                await Task.Delay(500);
            }
            
            // Final verification
            Console.WriteLine("\n===== TEST SUMMARY =====");
            Console.WriteLine($"StartHand received by GameEngine: {startHandReceived}");
            Console.WriteLine($"Response received by ConsoleUI: {responseReceived}");
            
            if (startHandReceived && responseReceived)
            {
                Console.WriteLine("\nTEST PASSED: StartHand message flow is working correctly!");
            }
            else
            {
                Console.WriteLine("\nTEST FAILED: StartHand message flow is not working correctly.");
                if (!startHandReceived) Console.WriteLine("- GameEngine did not receive the StartHand message.");
                if (!responseReceived) Console.WriteLine("- ConsoleUI did not receive the response message.");
            }
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