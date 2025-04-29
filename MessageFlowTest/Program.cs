using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using PokerGame.Core.Messaging;
using PokerGame.Core.Microservices;

namespace MessageFlowTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Simple Message Flow Test ===");
            
            // Create execution context
            var executionContext = new PokerGame.Core.Messaging.ExecutionContext();
            Console.WriteLine("Created execution context");
            
            // Initialize broker manager
            BrokerManager.Instance.Start(executionContext);
            Console.WriteLine("Started broker manager");
            
            // Start the central broker (on a different port to avoid conflicts)
            var centralBroker = BrokerManager.Instance.StartCentralBroker(26555, executionContext, true);
            Console.WriteLine("Started central broker on port 26555");
            
            // Create a client
            var clientId = "test_client_" + Guid.NewGuid().ToString().Substring(0, 6);
            Console.WriteLine($"Client ID: {clientId}");
            
            // Create message listener
            Console.WriteLine("Setting up message subscription");
            centralBroker.Subscribe(clientId, (message) => {
                Console.WriteLine($"Received message: Type={message.Type}, From={message.SenderId}, To={message.ReceiverId}");
                return true;
            });
            
            // Wait for subscription to take effect
            await Task.Delay(500);
            
            // Send a test message to the broker
            var testMessage = new NetworkMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = MessageType.Debug,
                SenderId = clientId,
                ReceiverId = "test_receiver",
                Payload = "This is a test message"
            };
            
            Console.WriteLine($"Sending test message with ID: {testMessage.MessageId}");
            centralBroker.Publish(testMessage);
            
            // Wait to see if the message gets routed back
            await Task.Delay(1000);
            
            // Now test a StartHand message
            var startHandMessage = new NetworkMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = MessageType.StartHand,
                SenderId = clientId,
                ReceiverId = "static_game_engine_service",
                Headers = new Dictionary<string, string>
                {
                    { "MessageSubType", "StartHand" }
                }
            };
            
            Console.WriteLine($"Sending StartHand message with ID: {startHandMessage.MessageId}");
            centralBroker.Publish(startHandMessage);
            
            // Wait for responses
            Console.WriteLine("Waiting for responses...");
            await Task.Delay(3000);
            
            Console.WriteLine("Test completed.");
            BrokerManager.Instance.Stop();
        }
    }
}
