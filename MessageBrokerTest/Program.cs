using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using PokerGame.Core.Messaging;
using PokerGame.Core.Microservices;

namespace MessageBrokerTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("========== CENTRAL MESSAGE BROKER TEST ==========");
            
            // Create execution context
            var executionContext = new PokerGame.Core.Messaging.ExecutionContext();
            
            try
            {
                // Start the broker manager
                Console.WriteLine("Starting BrokerManager...");
                BrokerManager.Instance.Start(executionContext);
                
                // Initialize the central broker
                Console.WriteLine("Starting CentralMessageBroker...");
                var broker = BrokerManager.Instance.StartCentralBroker(28555, executionContext, true);
                Console.WriteLine($"Broker initialized on port 28555: {broker != null}");
                
                // Create test services
                var clientId = "test_client";
                var serverId = "test_server";
                
                // Set up client listener
                Console.WriteLine("Setting up client subscriber...");
                broker.Subscribe(clientId, (message) =>
                {
                    Console.WriteLine($"CLIENT received: Type={message.Type}, From={message.SenderId}, ID={message.MessageId}");
                    
                    // If this is a StartHand response, log it specially
                    if (message.Type == MessageType.DeckShuffled)
                    {
                        Console.WriteLine($"!!! FOUND DECK SHUFFLED MESSAGE: {message.MessageId} !!!");
                    }
                    
                    return true;
                });
                
                // Set up server listener
                Console.WriteLine("Setting up server subscriber...");
                broker.Subscribe(serverId, (message) =>
                {
                    Console.WriteLine($"SERVER received: Type={message.Type}, From={message.SenderId}, ID={message.MessageId}");
                    
                    // If this is a StartHand message, respond with DeckShuffled
                    if (message.Type == MessageType.StartHand)
                    {
                        Console.WriteLine($"!!! RECEIVED START HAND: {message.MessageId} !!!");
                        
                        // Create response message
                        var response = new NetworkMessage
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            Type = MessageType.DeckShuffled,
                            SenderId = serverId,
                            ReceiverId = message.SenderId,
                            Timestamp = DateTime.UtcNow,
                            InResponseTo = message.MessageId,
                            Headers = new Dictionary<string, string>
                            {
                                { "ResponseType", "DeckShuffled" },
                                { "OriginalMessageId", message.MessageId }
                            }
                        };
                        
                        // Send response
                        Console.WriteLine($"Sending DeckShuffled response: {response.MessageId}");
                        broker.Publish(response);
                    }
                    
                    return true;
                });
                
                // Wait for subscriptions to activate
                await Task.Delay(500);
                
                // Send a StartHand message from client to server
                var startHandMessage = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = MessageType.StartHand,
                    SenderId = clientId,
                    ReceiverId = serverId,
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
                Console.WriteLine("Waiting for message processing...");
                await Task.Delay(2000);
                
                // Test the full flow again
                Console.WriteLine("\n--- Testing again with direct message creation ---\n");
                
                var directStartHand = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Type = MessageType.StartHand,
                    SenderId = clientId,
                    ReceiverId = serverId,
                    Timestamp = DateTime.UtcNow
                };
                
                Console.WriteLine($"Sending direct StartHand: ID={directStartHand.MessageId}");
                broker.Publish(directStartHand);
                
                // Wait for processing
                await Task.Delay(2000);
                
                Console.WriteLine("Test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                // Clean up
                BrokerManager.Instance.Stop();
            }
        }
    }
}
