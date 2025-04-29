using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using PokerGame.Core.Messaging;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("===== Minimal StartHand Message Flow Test =====");
        
        // Create execution context
        var cts = new CancellationTokenSource();
        var executionContext = new PokerGame.Core.Messaging.ExecutionContext(cts);
        
        // Create broker
        var broker = new CentralMessageBroker(executionContext);
        broker.Start();
        
        // Track message receipt
        bool startHandReceived = false;
        bool handStartedReceived = false;
        
        // Set up StartHand subscription (Game Engine side)
        Console.WriteLine("Setting up StartHand subscription (Game Engine)...");
        var startHandSubId = broker.Subscribe(MessageType.StartHand, msg => {
            Console.WriteLine($"GAME ENGINE received StartHand: ID={msg.MessageId}, From={msg.SenderId}");
            startHandReceived = true;
            
            // Create HandStarted response
            var response = new NetworkMessage {
                MessageId = Guid.NewGuid().ToString(),
                Type = MessageType.HandStarted,
                SenderId = "game_engine",
                ReceiverId = msg.SenderId,
                InResponseTo = msg.MessageId,
                Timestamp = DateTime.UtcNow,
                Headers = new Dictionary<string, string> {
                    { "ResponseType", "HandStarted" }
                }
            };
            
            // Send response
            Console.WriteLine($"GAME ENGINE sending HandStarted response: ID={response.MessageId}");
            broker.Publish(response);
        });
        
        // Set up HandStarted subscription (Console UI side)
        Console.WriteLine("Setting up HandStarted subscription (Console UI)...");
        var handStartedSubId = broker.Subscribe(MessageType.HandStarted, msg => {
            Console.WriteLine($"CONSOLE UI received HandStarted: ID={msg.MessageId}, From={msg.SenderId}, InResponseTo={msg.InResponseTo}");
            handStartedReceived = true;
        });
        
        // Wait for subscriptions to initialize
        await Task.Delay(500);
        
        // Send StartHand message from Console UI to Game Engine
        var startHandMsg = new NetworkMessage {
            MessageId = Guid.NewGuid().ToString(),
            Type = MessageType.StartHand,
            SenderId = "console_ui",
            ReceiverId = "game_engine",
            Timestamp = DateTime.UtcNow,
            Headers = new Dictionary<string, string> {
                { "MessageSubType", "StartHand" }
            }
        };
        
        Console.WriteLine($"CONSOLE UI sending StartHand: ID={startHandMsg.MessageId}");
        broker.Publish(startHandMsg);
        
        // Wait for message processing
        Console.WriteLine("Waiting for message processing...");
        await Task.Delay(2000);
        
        // Check results
        Console.WriteLine("\n===== TEST RESULTS =====");
        Console.WriteLine($"StartHand received by Game Engine: {startHandReceived}");
        Console.WriteLine($"HandStarted received by Console UI: {handStartedReceived}");
        
        if (startHandReceived && handStartedReceived) {
            Console.WriteLine("\nSUCCESS: Message flow is working properly!");
        } else {
            Console.WriteLine("\nFAILURE: Message flow is not working properly.");
        }
        
        // Clean up
        broker.Unsubscribe(startHandSubId, MessageType.StartHand);
        broker.Unsubscribe(handStartedSubId, MessageType.HandStarted);
        broker.Stop();
        cts.Cancel();
    }
}
