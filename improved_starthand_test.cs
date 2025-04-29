using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PokerGame.Core.Messaging;
using PokerGame.Core.Microservices;
using MSAEC = MSA.Foundation.ServiceManagement.ExecutionContext;
using MSA.Foundation.Messaging;
using System.Text.Json;

namespace PokerGame.Test
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Improved StartHand Message Flow Test");
            Console.WriteLine("====================================");
            
            // Initialize CentralMessageBroker through BrokerManager
            Console.WriteLine("Initializing BrokerManager...");
            var executionContext = new MSAEC();
            BrokerManager.Instance.Start(executionContext);
            
            // Get or create the central broker
            Console.WriteLine("Starting central broker...");
            var centralBroker = BrokerManager.Instance.StartCentralBroker(25555, executionContext, true);
            
            Console.WriteLine("Broker initialized and started.");
            
            // Create a test client
            var clientId = "test_client_" + Guid.NewGuid().ToString().Substring(0, 8);
            Console.WriteLine($"Created test client ID: {clientId}");
            
            // Dictionary to track received messages
            var receivedMessages = new List<NetworkMessage>();
            
            // Set up a message listener for all messages
            centralBroker.Subscribe(clientId, (message) =>
            {
                receivedMessages.Add(message);
                Console.WriteLine($">> RECEIVED: Type={message.Type}, From={message.SenderId}, To={message.ReceiverId}, MsgId={message.MessageId}");
                
                // Add debug info for the message payload
                if (message.Payload != null)
                {
                    Console.WriteLine($"   Payload: {message.Payload}");
                }
            });
            
            // Also listen for broadcast messages
            centralBroker.Subscribe("", (message) =>
            {
                if (message.ReceiverId == null || message.ReceiverId == "")
                {
                    receivedMessages.Add(message);
                    Console.WriteLine($">> BROADCAST: Type={message.Type}, From={message.SenderId}, MsgId={message.MessageId}");
                    
                    // Add debug info for the message payload
                    if (message.Payload != null)
                    {
                        Console.WriteLine($"   Payload: {message.Payload}");
                    }
                }
            });
            
            // Wait a moment for subscription to take effect and services to stabilize
            await Task.Delay(1000);
            
            // Now send a fake registration to test connection to broker
            var testRegistration = new NetworkMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = MessageType.ServiceRegistration,
                SenderId = clientId,
                ReceiverId = "",  // broadcast
                Payload = JsonSerializer.Serialize(new { ServiceId = clientId, ServiceName = "Test Client", ServiceType = "TestService" })
            };
            
            Console.WriteLine("Sending test registration...");
            centralBroker.Publish(testRegistration);
            
            // Wait for services to respond to our registration
            await Task.Delay(1000);
            
            // Now create a direct NetworkMessage for StartHand
            var startHandMessage = new NetworkMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = MessageType.StartHand,
                SenderId = clientId,
                ReceiverId = "static_game_engine_service",  // targeted at the Game Engine Service
                Timestamp = DateTime.UtcNow,
                // Add MessageSubType in headers for better recognition
                Headers = new Dictionary<string, string>
                {
                    { "MessageSubType", "StartHand" }
                }
            };
            
            Console.WriteLine("==============================================");
            Console.WriteLine($"Sending StartHand message ID: {startHandMessage.MessageId}");
            Console.WriteLine($"Type: {startHandMessage.Type}");
            Console.WriteLine($"From: {startHandMessage.SenderId}");
            Console.WriteLine($"To: {startHandMessage.ReceiverId}");
            Console.WriteLine("==============================================");
            
            // Send the message
            centralBroker.Publish(startHandMessage);
            
            // Wait for responses
            Console.WriteLine("Waiting for responses...");
            await Task.Delay(5000);
            
            // Summary of received messages
            Console.WriteLine("\nSUMMARY OF MESSAGE FLOW:");
            Console.WriteLine("-------------------------");
            Console.WriteLine($"Total messages received: {receivedMessages.Count}");
            Console.WriteLine($"Our StartHand message ID: {startHandMessage.MessageId}");
            
            // Look for responses to our StartHand
            var responses = receivedMessages.FindAll(msg => msg.InResponseTo == startHandMessage.MessageId);
            Console.WriteLine($"Direct responses to our StartHand: {responses.Count}");
            
            foreach (var response in responses)
            {
                Console.WriteLine($"Response from {response.SenderId}: Type={response.Type}, MsgId={response.MessageId}");
            }
            
            // Look for any StartHand related messages by type
            var startHandRelated = receivedMessages.FindAll(msg => 
                msg.Type == MessageType.StartHand || 
                msg.Type == MessageType.DeckShuffled ||
                msg.Type.ToString().Contains("Hand") ||
                (msg.Headers != null && msg.Headers.ContainsKey("MessageSubType") && 
                 msg.Headers["MessageSubType"].Contains("Hand"))
            );
            
            Console.WriteLine($"\nAny StartHand-related messages: {startHandRelated.Count}");
            foreach (var msg in startHandRelated)
            {
                Console.WriteLine($"Related message from {msg.SenderId}: Type={msg.Type}, MsgId={msg.MessageId}");
            }
            
            Console.WriteLine("\nTest completed.");
            BrokerManager.Instance.Stop();
        }
    }
}