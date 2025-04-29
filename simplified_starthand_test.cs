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
        // Define message types to avoid dependency on the enum
        private const string MESSAGE_TYPE_START_HAND = "StartHand";
        private const string MESSAGE_TYPE_HAND_STARTED = "HandStarted";
        
        public static async Task Main(string[] args)
        {
            Console.WriteLine("===== StartHand Message Flow Test (Simplified) =====");
            
            // Create basic execution context
            var executionContext = new PokerGame.Core.Messaging.ExecutionContext();
            
            try
            {
                // Initialize broker
                Console.WriteLine("Starting CentralMessageBroker...");
                var broker = new CentralMessageBroker(executionContext, 25555, true);
                broker.Start();
                
                if (broker == null)
                {
                    Console.WriteLine("ERROR: Failed to start central broker");
                    return;
                }
                
                Console.WriteLine("CentralMessageBroker started successfully");
                
                // Create test services with minimally required functionality
                string consoleServiceId = "test_ui_service";
                string gameEngineId = "test_engine_service";
                
                // Subscribe to HandStarted messages
                Console.WriteLine($"Setting up subscriber for HandStarted messages...");
                
                bool responseReceived = false;
                string handStartedSubscriptionId = null;
                
                try 
                {
                    handStartedSubscriptionId = broker.Subscribe(MessageType.HandStarted, (message) => {
                        Console.WriteLine($"Received message: Type={message.Type}, From={message.SenderId}, To={message.ReceiverId}, ID={message.MessageId}");
                        
                        // Check if this is a response to our console service
                        if (message.ReceiverId == consoleServiceId)
                        {
                            Console.WriteLine("\n!!!! SUCCESS !!!!");
                            Console.WriteLine($"UI SERVICE received HandStarted response to StartHand message!");
                            Console.WriteLine($"Message ID: {message.MessageId}");
                            Console.WriteLine($"In Response To: {message.InResponseTo}");
                            Console.WriteLine($"From: {message.SenderId}");
                            Console.WriteLine("!!!! SUCCESS !!!!\n");
                            
                            responseReceived = true;
                        }
                    });
                }
                catch (Exception ex) 
                {
                    Console.WriteLine($"Error subscribing to HandStarted messages: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
                
                // Subscribe to StartHand messages
                Console.WriteLine($"Setting up subscriber for StartHand messages...");
                
                bool startHandReceived = false;
                string startHandSubscriptionId = null;
                
                try 
                {
                    startHandSubscriptionId = broker.Subscribe(MessageType.StartHand, (message) => {
                        Console.WriteLine($"Received message: Type={message.Type}, From={message.SenderId}, To={message.ReceiverId}, ID={message.MessageId}");
                        
                        // Check if this is a message meant for our game engine
                        if (message.ReceiverId == gameEngineId)
                        {
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
                }
                catch (Exception ex) 
                {
                    Console.WriteLine($"Error subscribing to StartHand messages: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
                
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
                
                // Unsubscribe if needed
                if (handStartedSubscriptionId != null)
                    broker.Unsubscribe(handStartedSubscriptionId, MessageType.HandStarted);
                    
                if (startHandSubscriptionId != null)
                    broker.Unsubscribe(startHandSubscriptionId, MessageType.StartHand);
                
                // Stop the broker
                broker.Stop();
                Console.WriteLine("Broker stopped. Resources cleaned up.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR during test execution: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}