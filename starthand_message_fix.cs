using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using PokerGame.Core.Messaging;
using PokerGame.Core.Microservices;

namespace MessageFlowFix
{
    /// <summary>
    /// This class implements a fixed StartHand message flow to help debug
    /// the issue with StartHand messages not triggering card shuffling.
    /// 
    /// The key issue is:
    /// 1. ConsoleUIService creates a StartHand message with MessageType.StartHand
    /// 2. This message is published to the CentralMessageBroker
    /// 3. GameEngineService should receive this message and respond
    /// 4. For some reason, the handling chain is broken
    /// 
    /// This class implements a direct test that bypasses most of the infrastructure
    /// to verify the core messaging works, and then proposes fixes.
    /// </summary>
    public class StartHandMessageFix
    {
        public static void ApplyFixesToGameEngineService()
        {
            // 1. Make sure GameEngineService properly responds to StartHand messages
            // - It should look for MessageType.StartHand directly in HandleMessageAsync
            // - When it gets a StartHand message, it should respond with DeckShuffled
            
            // Code update for GameEngineService.HandleMessageAsync:
            // After message conversion (line ~104), add special check:
            
            /*
            // Special handling for StartHand message - check message type explicitly
            if (message.MessageType.ToString().Contains("StartHand") || 
                (message.Headers.ContainsKey("MessageSubType") && 
                 message.Headers["MessageSubType"].Contains("StartHand")))
            {
                Console.WriteLine("Detected StartHand message from message type or headers - setting correct type");
                convertedMessage.Type = PokerGame.Core.Messaging.MessageType.StartHand;
            }
            */
            
            // 2. Fix GameEngineService.HandleMessageInternalAsync switch case for StartHand
            // - Ensure it creates a proper DeckShuffled response
            // - Make sure it sends the response via CentralMessageBroker
            
            /*
            // At around line 960 in HandleMessageInternalAsync
            // Create deck shuffled response
            var deckShuffledResponse = new NetworkMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = MessageType.DeckShuffled,
                SenderId = _serviceId,
                ReceiverId = message.SenderId,
                InResponseTo = message.MessageId,
                Timestamp = DateTime.UtcNow,
                Headers = new Dictionary<string, string>
                {
                    { "OriginalMessageId", message.MessageId },
                    { "ResponseType", "DeckShuffled" }
                }
            };
            
            // Publish via CentralMessageBroker for maximum reliability
            Console.WriteLine($"Publishing DeckShuffled response directly to CentralMessageBroker");
            BrokerManager.Instance.CentralBroker?.Publish(deckShuffledResponse);
            */
            
            // 3. Fix ConsoleUIService to properly process DeckShuffled responses
            // - Add explicit check for MessageType.DeckShuffled in HandleMessageInternalAsync
            
            /*
            // In ConsoleUIService.HandleMessageInternalAsync:
            case MessageType.DeckShuffled:
                Console.WriteLine($"Received DeckShuffled message (ID: {message.MessageId}) in response to {message.InResponseTo}");
                // Process the shuffled deck...
                break;
            */
        }
        
        /// <summary>
        /// Implement a direct test of StartHand message flow
        /// </summary>
        public static async Task TestStartHandMessageFlowAsync()
        {
            // Create execution context
            var context = new ExecutionContext();
            
            // Start broker
            BrokerManager.Instance.Start(context);
            
            // Start central broker
            var broker = BrokerManager.Instance.StartCentralBroker(29777, context, true);
            
            // Define client and server IDs
            var clientId = "test_client";
            var serverId = "game_engine";
            
            // Set up server to respond to StartHand with DeckShuffled
            broker.Subscribe(serverId, (message) => {
                if (message.Type == MessageType.StartHand)
                {
                    Console.WriteLine($"SERVER received StartHand: {message.MessageId}");
                    
                    // Create DeckShuffled response
                    var response = new NetworkMessage
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        Type = MessageType.DeckShuffled,
                        SenderId = serverId,
                        ReceiverId = message.SenderId,
                        InResponseTo = message.MessageId,
                        Timestamp = DateTime.UtcNow
                    };
                    
                    // Send response
                    Console.WriteLine($"SERVER sending DeckShuffled: {response.MessageId}");
                    broker.Publish(response);
                }
                return true;
            });
            
            // Set up client to listen for DeckShuffled
            broker.Subscribe(clientId, (message) => {
                Console.WriteLine($"CLIENT received: Type={message.Type}, ID={message.MessageId}");
                if (message.Type == MessageType.DeckShuffled)
                {
                    Console.WriteLine($"CLIENT got DeckShuffled response: {message.MessageId}");
                }
                return true;
            });
            
            // Wait for subscriptions to initialize
            await Task.Delay(500);
            
            // Send a StartHand message
            var startHand = new NetworkMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = MessageType.StartHand,
                SenderId = clientId,
                ReceiverId = serverId,
                Timestamp = DateTime.UtcNow
            };
            
            // Send the message
            Console.WriteLine($"CLIENT sending StartHand: {startHand.MessageId}");
            broker.Publish(startHand);
            
            // Wait for the round-trip
            await Task.Delay(2000);
            
            // Clean up
            BrokerManager.Instance.Stop();
        }
    }
}