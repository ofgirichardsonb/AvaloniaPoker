using System;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;
using PokerGame.Core.Messaging;

namespace StartHandTestMinimal
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== StartHand Message Test (Minimal) ===");
            
            try
            {
                // Get broker instance
                Console.WriteLine("Obtaining broker instance...");
                var broker = BrokerManager.Instance.Broker;
                broker.Start();
                Console.WriteLine("Broker started successfully.");
                
                // Set up message tracking
                bool responseReceived = false;
                
                // Subscribe to messages
                Console.WriteLine("Setting up message subscription...");
                broker.Subscribe(message =>
                {
                    Console.WriteLine($"Received message: Type={message.Type}, Sender={message.SenderId}");
                    
                    // Check if this is a response
                    if (message.Headers.TryGetValue("InResponseTo", out var responseId))
                    {
                        Console.WriteLine($"This is a response to message: {responseId}");
                        Console.WriteLine($"Response payload: {message.Payload ?? "null"}");
                        responseReceived = true;
                    }
                });
                
                // Create a StartHand message
                Console.WriteLine("Creating StartHand message...");
                var startHandMessage = new NetworkMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderId = "test_client",
                    ReceiverId = "static_game_engine_service", // Using a known static ID
                    Payload = "{}"
                };
                
                // Add message subtype header to help with routing
                startHandMessage.Headers["MessageSubType"] = "StartHand";
                
                // Send the message
                Console.WriteLine($"Sending StartHand message (ID: {startHandMessage.MessageId})...");
                broker.Publish(startHandMessage);
                
                // Wait for response with timeout
                Console.WriteLine("Waiting for response...");
                for (int i = 0; i < 10 && !responseReceived; i++)
                {
                    Console.WriteLine($"Waiting... ({i+1}/10)");
                    await Task.Delay(1000);
                }
                
                if (responseReceived)
                {
                    Console.WriteLine("SUCCESS: Received response to StartHand message!");
                }
                else
                {
                    Console.WriteLine("ERROR: No response received within timeout period.");
                }
                
                // Cleanup
                Console.WriteLine("Stopping broker...");
                broker.Stop();
                Console.WriteLine("Test completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}