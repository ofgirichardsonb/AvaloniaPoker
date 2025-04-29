using System;
using System.Threading.Tasks;
using PokerGame.Core.Messaging;
using PokerGame.Core.Microservices;
using System.Collections.Generic;
using System.Text.Json;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("===== Improved StartHand Message Test =====");

        try
        {
            // Create test objects with proper JSON serialization
            Console.WriteLine("Testing JSON serialization...");
            
            // Test basic string serialization
            string testString = $"Test message sent at {DateTime.UtcNow}";
            string serializedString = JsonSerializer.Serialize(testString);
            Console.WriteLine($"Original string: {testString}");
            Console.WriteLine($"Serialized string: {serializedString}");
            
            try
            {
                string deserializedString = JsonSerializer.Deserialize<string>(serializedString);
                Console.WriteLine($"Deserialized string: {deserializedString}");
                Console.WriteLine("Basic string serialization: SUCCESS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Basic string deserialization FAILED: {ex.Message}");
            }
            
            Console.WriteLine();
            
            // Test network message payload
            Console.WriteLine("Testing NetworkMessage payload handling...");
            
            var message = new NetworkMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = MessageType.StartHand,
                SenderId = "test_console",
                ReceiverId = "test_game_engine",
                Timestamp = DateTime.UtcNow,
                Payload = serializedString,
                Headers = new Dictionary<string, string>
                {
                    { "MessageSubType", "StartHand" }
                }
            };
            
            Console.WriteLine($"Message created with payload: {message.Payload}");
            
            try
            {
                var payload = message.GetPayload<string>();
                Console.WriteLine($"GetPayload<string>() result: {payload}");
                Console.WriteLine("NetworkMessage payload retrieval: " + 
                    (payload == testString ? "SUCCESS" : "FAILED - strings don't match"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NetworkMessage payload retrieval FAILED: {ex.Message}");
                Console.WriteLine($"Full exception: {ex}");
            }
            
            // Create a broker and test actual message passing
            Console.WriteLine("\nTesting actual message passing through broker...");
            
            var executionContext = new PokerGame.Core.Messaging.ExecutionContext();
            var broker = new CentralMessageBroker(executionContext);
            broker.Start();
            
            bool messageReceived = false;
            string receivedPayload = null;
            
            // Subscribe to message
            Console.WriteLine("Setting up subscription...");
            string subscriptionId = broker.Subscribe(MessageType.StartHand, msg => {
                Console.WriteLine($"Received message: {msg.Type} from {msg.SenderId}");
                
                try
                {
                    receivedPayload = msg.GetPayload<string>();
                    Console.WriteLine($"Received payload: {receivedPayload}");
                    messageReceived = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error retrieving payload: {ex.Message}");
                    // Try direct access
                    Console.WriteLine($"Raw payload: {msg.Payload}");
                    
                    try
                    {
                        if (msg.Payload is string rawPayload)
                        {
                            receivedPayload = JsonSerializer.Deserialize<string>(rawPayload);
                            Console.WriteLine($"Manual deserialization: {receivedPayload}");
                            messageReceived = true;
                        }
                    }
                    catch (Exception innerEx)
                    {
                        Console.WriteLine($"Manual deserialization failed: {innerEx.Message}");
                    }
                }
            });
            
            // Allow subscription to settle
            await Task.Delay(100);
            
            // Publish message
            Console.WriteLine("Publishing message...");
            broker.Publish(message);
            
            // Wait for processing
            await Task.Delay(1000);
            
            // Check results
            if (messageReceived)
            {
                Console.WriteLine("\n✓ SUCCESS: Message was received!");
                if (receivedPayload == testString)
                {
                    Console.WriteLine("✓ PAYLOAD MATCH: The payload was correctly passed through the broker!");
                }
                else
                {
                    Console.WriteLine("✗ PAYLOAD MISMATCH: The payload was modified during transmission.");
                    Console.WriteLine($"  Original: {testString}");
                    Console.WriteLine($"  Received: {receivedPayload}");
                }
            }
            else
            {
                Console.WriteLine("\n✗ FAILED: Message was not received!");
            }
            
            // Clean up
            broker.Unsubscribe(subscriptionId, MessageType.StartHand);
            broker.Stop();
            
            // Report overall success
            Console.WriteLine("\nTest " + (messageReceived ? "PASSED" : "FAILED"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TEST ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        
        Console.WriteLine("\n===== Test Complete =====");
    }
}