using System;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;
using MSA.Foundation.ServiceManagement;
using PokerGame.Core.Messaging;
using PokerGame.Core.Microservices;
using PokerGame.Abstractions;
using PokerGame.Core.Game;
using PokerGame.Core.Models;

namespace StartHandTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== StartHand Message Flow Test ===");
            
            try
            {
                await RunStartHandTest();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static async Task RunStartHandTest()
        {
            Console.WriteLine("Starting CentralMessageBroker...");
            // Initialize broker with an explicit ID for testing
            var broker = CentralMessageBroker.GetInstance();
            broker.Start();
            
            Console.WriteLine("Setting up test message handlers...");
            
            // Create a basic execution context for services
            var executionContext = new MSA.Foundation.ServiceManagement.ExecutionContext();
            
            // Create a simple handler for StartHand message responses
            bool responseReceived = false;
            
            void HandleStartHandResponse(NetworkMessage msg)
            {
                if (msg.Headers.TryGetValue("InResponseTo", out var responseId))
                {
                    Console.WriteLine($"Received response to message {responseId}");
                    Console.WriteLine($"Response message type: {msg.MessageType}");
                    Console.WriteLine($"Response payload: {msg.Payload}");
                    responseReceived = true;
                }
            }
            
            Console.WriteLine("Initializing game engine service...");
            // Initialize the GameEngineService (target for our StartHand message)
            var gameEngine = new PokerGameEngine();
            var gameEngineService = new GameEngineService(executionContext, gameEngine);
            gameEngineService.Start();
            Thread.Sleep(1000); // Wait for service to initialize
            
            Console.WriteLine("GameEngineService started with ID: " + gameEngineService.ServiceId);
            
            Console.WriteLine("Initializing card deck service...");
            // Initialize CardDeckService (required by GameEngineService)
            var cardDeckService = new CardDeckService(executionContext);
            cardDeckService.Start();
            Thread.Sleep(1000); // Wait for service to initialize
            
            Console.WriteLine("CardDeckService started with ID: " + cardDeckService.ServiceId);
            
            // Register with broker to receive StartHand responses
            broker.Subscribe((msg) => 
            {
                if (msg.MessageType == MessageType.Generic)
                {
                    try
                    {
                        HandleStartHandResponse(msg);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error handling message: {ex.Message}");
                    }
                    return;
                }
                
                Console.WriteLine($"Received message of type: {msg.MessageType}");
            });

            Console.WriteLine("Sending StartHand message...");
            
            // Create a StartHand message directly (similar to ConsoleUIService)
            var startHandMessage = new NetworkMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                MessageType = MessageType.Generic,
                SenderId = "test_client",
                ReceiverId = gameEngineService.ServiceId,
                Timestamp = DateTime.UtcNow,
                Payload = "{}"
            };
            
            // Add headers to help with routing
            startHandMessage.Headers["MessageSubType"] = "StartHand";
            
            Console.WriteLine($"Sending StartHand message (ID: {startHandMessage.MessageId}) to {gameEngineService.ServiceId}");
            
            // Send the message through the broker
            broker.Publish(startHandMessage);
            
            // Wait for response with timeout
            int maxWaitTime = 5; // seconds
            for (int i = 0; i < maxWaitTime && !responseReceived; i++)
            {
                Console.WriteLine($"Waiting for response... ({i+1}/{maxWaitTime})");
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
            Console.WriteLine("Stopping services...");
            gameEngineService.Stop();
            cardDeckService.Stop();
            broker.Stop();
            Console.WriteLine("Test completed.");
        }
    }
}