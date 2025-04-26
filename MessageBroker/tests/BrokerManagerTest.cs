using System;
using System.Threading;
using System.Threading.Tasks;

namespace MessageBroker.Tests
{
    /// <summary>
    /// A simple test program for the BrokerManager
    /// </summary>
    public class BrokerManagerTest
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== BROKER MANAGER TEST ===");
            
            try
            {
                // Start the broker
                Console.WriteLine("Starting broker manager...");
                BrokerManager.Instance.Start();
                
                // Create two clients
                Console.WriteLine("Creating clients...");
                var client1 = BrokerManager.Instance.CreateClient("TestClient1", "TestService", new[] { "Test" });
                var client2 = BrokerManager.Instance.CreateClient("TestClient2", "TestService", new[] { "Test" });
                
                // Connect clients
                Console.WriteLine("Connecting clients...");
                await client1.ConnectAsync();
                await client2.ConnectAsync();
                
                // Register services
                Console.WriteLine("Registering services...");
                await client1.RegisterServiceAsync();
                await client2.RegisterServiceAsync();
                
                // Discover services
                Console.WriteLine("Discovering services...");
                var services = await client1.DiscoverServicesAsync();
                
                Console.WriteLine($"Found {services.Count} services:");
                foreach (var service in services)
                {
                    Console.WriteLine($"  - {service.ServiceName} (ID: {service.ServiceId}, Type: {service.ServiceType})");
                }
                
                // Set up message handlers
                Console.WriteLine("Setting up message handlers...");
                client1.MessageReceived += (sender, message) =>
                {
                    Console.WriteLine($"Client1 received message: Type={message.Type}, From={message.SenderId}");
                };
                
                client2.MessageReceived += (sender, message) =>
                {
                    Console.WriteLine($"Client2 received message: Type={message.Type}, From={message.SenderId}");
                    
                    // Send acknowledgment if required
                    if (message.RequiresAcknowledgment)
                    {
                        Task.Run(async () =>
                        {
                            Console.WriteLine($"Client2 sending acknowledgment for message: {message.MessageId}");
                            await client2.SendAcknowledgmentAsync(message);
                        });
                    }
                };
                
                // Send ping message from client1 to client2
                Console.WriteLine("Sending ping from client1 to client2...");
                var response = await client1.SendAndWaitForResponseAsync(
                    BrokerMessageType.Ping,
                    client2.ServiceId,
                    null,
                    TimeSpan.FromSeconds(5)
                );
                
                if (response != null)
                {
                    Console.WriteLine("Received response to ping!");
                }
                else
                {
                    Console.WriteLine("No response received to ping.");
                }
                
                // Broadcast message from client1
                Console.WriteLine("Broadcasting message from client1...");
                await client1.BroadcastMessageAsync(BrokerMessageType.Heartbeat, null);
                
                // Wait for messages to be processed
                Console.WriteLine("Waiting for messages to be processed...");
                await Task.Delay(2000);
                
                // Stop broker and clients
                Console.WriteLine("Stopping clients...");
                await client1.DisconnectAsync();
                await client2.DisconnectAsync();
                
                Console.WriteLine("Stopping broker manager...");
                BrokerManager.Instance.Stop();
                
                Console.WriteLine("Test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}