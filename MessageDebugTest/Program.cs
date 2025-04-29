using System;
using System.Threading.Tasks;
using PokerGame.Core.Messaging;

namespace MessageDebugTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Message Flow Debug Test");
            Console.WriteLine("======================");

            // Create execution context
            var context = new ExecutionContext();
            
            // Start broker manager
            Console.WriteLine("Starting broker manager...");
            BrokerManager.Instance.Start(context);
            
            // Create broker
            Console.WriteLine("Creating central broker...");
            var broker = BrokerManager.Instance.StartCentralBroker(27777, context, true);
            
            // Create a test message
            var message = new NetworkMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = MessageType.Debug,
                SenderId = "debug_test",
                ReceiverId = "test_receiver",
                Payload = "Test debug message"
            };
            
            // Subscribe to messages
            Console.WriteLine("Setting up subscription...");
            broker.Subscribe("debug_test", msg => {
                Console.WriteLine($"Received message: {msg.Type} from {msg.SenderId}");
                return true;
            });
            
            // Wait for subscription to activate
            await Task.Delay(500);
            
            // Send message
            Console.WriteLine($"Sending test message ID: {message.MessageId}");
            broker.Publish(message);
            
            // Wait for processing
            await Task.Delay(2000);
            
            Console.WriteLine("Test complete");
            BrokerManager.Instance.Stop();
        }
    }
}
