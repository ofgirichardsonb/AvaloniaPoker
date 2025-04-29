using System;
using System.Threading.Tasks;
using PokerGame.Core.Messaging;
using PokerGame.Core.Microservices;
using MSA.Foundation.ServiceManagement;
using MSA.Foundation.Messaging;
using System.Text.Json;

namespace PokerGame.Test
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("StartHand Message Test");
            Console.WriteLine("=====================");
            
            // Initialize CentralMessageBroker through BrokerManager
            Console.WriteLine("Initializing BrokerManager...");
            var executionContext = new ExecutionContext();
            BrokerManager.Instance.Start(executionContext);
            
            // Get or create the central broker
            Console.WriteLine("Starting central broker...");
            var centralBroker = BrokerManager.Instance.StartCentralBroker(25555, executionContext, true);
            
            Console.WriteLine("Broker initialized and started.");
            
            // Create a test client
            var clientId = "test_client";
            
            // Create a direct NetworkMessage for StartHand
            // First create a message class with the MessageType.StartHand (constant value = 9)
            var startHandMessage = new NetworkMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = (PokerGame.Core.Messaging.MessageType)9,  // StartHand has value 9
                SenderId = clientId,
                ReceiverId = "static_game_engine_service",
                Timestamp = DateTime.UtcNow
            };
            
            Console.WriteLine($"Created StartHand message with ID: {startHandMessage.MessageId}");
            
            // Set up a message listener for responses
            centralBroker.OnMessageReceived += (message) =>
            {
                if (message.ReceiverId == clientId || string.IsNullOrEmpty(message.ReceiverId))
                {
                    Console.WriteLine($"Received message: Type={message.Type}, From={message.SenderId}, To={message.ReceiverId}, Payload={message.Payload}");
                    return true;
                }
                return false; // Not handled
            };
            
            // Send the message
            Console.WriteLine("Sending StartHand message...");
            centralBroker.Publish(startHandMessage);
            
            // Wait a bit for responses
            Console.WriteLine("Waiting for responses...");
            await Task.Delay(5000);
            
            Console.WriteLine("Test completed.");
            BrokerManager.Instance.Stop();
        }
    }
}