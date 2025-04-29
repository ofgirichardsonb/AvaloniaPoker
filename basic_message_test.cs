using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using PokerGame.Core.Messaging;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("===== Basic Message Test =====");
        
        try
        {
            // Create basic execution context with its own cancellation token
            var cancellationTokenSource = new CancellationTokenSource();
            var executionContext = new PokerGame.Core.Messaging.ExecutionContext(cancellationTokenSource);
            
            // Create a central message broker
            Console.WriteLine("Creating CentralMessageBroker...");
            var broker = new CentralMessageBroker(executionContext);
            broker.Start();
            
            // Flag to track if the message was received
            bool messageReceived = false;
            var messageContent = $"Test message sent at {DateTime.UtcNow}";
            
            // Subscribe to debug messages
            Console.WriteLine("Subscribing to Debug messages...");
            string subscriptionId = broker.Subscribe(MessageType.Debug, message => {
                Console.WriteLine($"Received message: {message.Type}");
                Console.WriteLine($"  From: {message.SenderId}");
                Console.WriteLine($"  To: {message.ReceiverId ?? "broadcast"}");
                Console.WriteLine($"  ID: {message.MessageId}");
                
                // Check the message payload for our test content
                try 
                {
                    var payloadJson = message.Payload as string;
                    Console.WriteLine($"  Raw Payload: {payloadJson ?? "null"}");
                    
                    // First try direct string deserialization if the format is valid
                    string payload = null;
                    try
                    {
                        payload = System.Text.Json.JsonSerializer.Deserialize<string>(payloadJson);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  JSON deserialization error: {ex.Message}");
                        // Fallback - maybe it's already a string
                        payload = payloadJson;
                    }
                    
                    Console.WriteLine($"  Decoded Payload: {payload ?? "null"}");
                    
                    if (payload != null && payload.Contains(DateTime.UtcNow.ToString("yyyy-MM-dd")))
                    {
                        Console.WriteLine("✓ Message content matches!");
                        messageReceived = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Payload processing error: {ex.Message}");
                }
            });
            
            // Create and send a debug message
            Console.WriteLine("\nSending test message...");
            
            var message = new NetworkMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = MessageType.Debug,
                SenderId = "test_sender",
                Timestamp = DateTime.UtcNow,
                // Serialize the payload properly as JSON
                Payload = System.Text.Json.JsonSerializer.Serialize(messageContent)
            };
            
            broker.Publish(message);
            
            // Wait a bit for message processing
            Console.WriteLine("Waiting for message to be processed...");
            await Task.Delay(1000);
            
            // Check results
            if (messageReceived)
            {
                Console.WriteLine("\n✓ SUCCESS: Message was successfully sent and received!");
            }
            else
            {
                Console.WriteLine("\n✗ FAILED: Message was not received!");
            }
            
            // Clean up
            broker.Unsubscribe(subscriptionId, MessageType.Debug);
            broker.Stop();
            cancellationTokenSource.Cancel();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        
        Console.WriteLine("\n===== Test Complete =====");
    }
}