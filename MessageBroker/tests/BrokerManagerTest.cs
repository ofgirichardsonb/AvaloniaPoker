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
                client1.Connect();
                client2.Connect();
                
                // Send registration message - note: we don't have RegisterService method directly
                Console.WriteLine("Sending service registration messages...");
                var payload1 = new ServiceRegistrationPayload
                {
                    ServiceId = client1.ClientId,
                    ServiceName = client1.ClientName,
                    ServiceType = client1.ClientType,
                    Capabilities = client1.Capabilities.ToList()
                };
                
                var regMessage1 = BrokerMessage.Create(BrokerMessageType.ServiceRegistration, payload1);
                regMessage1.SenderId = client1.ClientId;
                
                var payload2 = new ServiceRegistrationPayload
                {
                    ServiceId = client2.ClientId,
                    ServiceName = client2.ClientName,
                    ServiceType = client2.ClientType,
                    Capabilities = client2.Capabilities.ToList()
                };
                
                var regMessage2 = BrokerMessage.Create(BrokerMessageType.ServiceRegistration, payload2);
                regMessage2.SenderId = client2.ClientId;
                
                await client1.SendMessageAsync(regMessage1);
                await client2.SendMessageAsync(regMessage2);
                
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
                            var ackMessage = new BrokerMessage
                            {
                                Type = BrokerMessageType.Acknowledgment,
                                SenderId = client2.ClientId,
                                ReceiverId = message.SenderId,
                                InResponseTo = message.MessageId
                            };
                            await client2.SendMessageAsync(ackMessage);
                        });
                    }
                };
                
                // Test ping functionality
                Console.WriteLine("Sending ping from client1 to client2...");
                var pingResult = await client1.PingServiceAsync(client2.ClientId, TimeSpan.FromSeconds(5));
                
                if (pingResult)
                {
                    Console.WriteLine("Ping successful!");
                }
                else
                {
                    Console.WriteLine("Ping failed.");
                }
                
                // Send direct message from client1 to client2
                Console.WriteLine("Sending direct message from client1 to client2...");
                var heartbeatMessage = new BrokerMessage
                {
                    Type = BrokerMessageType.Heartbeat,
                    SenderId = client1.ClientId,
                    ReceiverId = client2.ClientId
                };
                await client1.SendMessageAsync(heartbeatMessage);
                
                // Wait for messages to be processed
                Console.WriteLine("Waiting for messages to be processed...");
                await Task.Delay(2000);
                
                // Disconnect clients
                Console.WriteLine("Stopping clients...");
                client1.Dispose();
                client2.Dispose();
                
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