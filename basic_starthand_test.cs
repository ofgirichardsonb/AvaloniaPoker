using System;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;
using PokerGame.Core.Messaging;

public class BasicStartHandTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Basic StartHand Message Test ===");
        
        try
        {
            // Get the broker instance
            Console.WriteLine("Initializing CentralMessageBroker...");
            var broker = BrokerManager.Instance.Broker;
            broker.Start();
            Console.WriteLine("CentralMessageBroker started.");
            
            // Handle responses
            bool responseReceived = false;
            
            // Set up our message handler
            Console.WriteLine("Setting up message subscription...");
            broker.Subscribe(message =>
            {
                Console.WriteLine($"Received message: Type={message.Type}, Sender={message.SenderId}");
                
                // Check if this is a response to our message
                if (message.Headers.TryGetValue("InResponseTo", out var responseId))
                {
                    Console.WriteLine($"This is a response to message: {responseId}");
                    Console.WriteLine($"Payload: {message.Payload ?? "null"}");
                    responseReceived = true;
                }
            });
            
            // Create a StartHand message
            Console.WriteLine("Creating StartHand message...");
            var startHandMessage = new NetworkMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                SenderId = "test_client",
                ReceiverId = "static_game_engine_service", // Static ID used by the game engine
                Payload = "{}"
            };
            
            // Set specific headers
            startHandMessage.Headers["MessageSubType"] = "StartHand";
            
            // Send the message
            Console.WriteLine($"Sending StartHand message (ID: {startHandMessage.MessageId})...");
            broker.Publish(startHandMessage);
            
            // Wait for a response with a timeout
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