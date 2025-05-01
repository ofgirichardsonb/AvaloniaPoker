using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;

// Message Monitor - Diagnostic tool to observe all messages flowing through the system
namespace MessageMonitor
{
    class Program
    {
        private static CancellationTokenSource _cts = new CancellationTokenSource();
        private static IMessageBroker _broker;
        private static string _subscriberId;

        static async Task Main(string[] args)
        {
            Console.WriteLine("====================================");
            Console.WriteLine("  PokerGame Message Monitor Tool    ");
            Console.WriteLine("====================================");
            Console.WriteLine("Connecting to message broker...");

            try
            {
                await InitializeBroker();
                
                Console.WriteLine("Connected! Monitoring all messages...");
                Console.WriteLine("Press Ctrl+C to stop monitoring.");
                
                // Set up Ctrl+C handler
                Console.CancelKeyPress += (s, e) => {
                    e.Cancel = true;
                    _cts.Cancel();
                };

                // Keep the program running until Ctrl+C
                await Task.Delay(-1, _cts.Token).ContinueWith(t => { });
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Monitoring stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                await ShutdownBroker();
            }
        }

        private static async Task InitializeBroker()
        {
            // Try different connection options to match the services
            string address = "127.0.0.1";  // Default broker host 
            int port = 25555;           // Default broker port
            bool verbose = true;
            
            Console.WriteLine("Attempting to connect to message broker...");
            
            // We'll try multiple broker addresses since we're unsure which one the services are using
            string[] connectionOptions = new string[] 
            {
                "127.0.0.1",              // Default TCP host
                "inproc://central-broker", // In-process address from NetMQContextHelper
                "inproc://central_broker"  // Alternative format with underscore
            };
            
            foreach (var connOption in connectionOptions)
            {
                try
                {
                    Console.WriteLine($"Trying to connect to broker at {connOption}...");
                    
                    // For inproc addresses, use port 0
                    int usePort = connOption.StartsWith("inproc://") ? 0 : port;
                    
                    // Create broker instance
                    _broker = new MessageBroker(connOption, usePort, verbose);
                    _broker.Start();
                    
                    // Generate a unique subscriber ID
                    _subscriberId = Guid.NewGuid().ToString();
                    
                    // Subscribe to all message types
                    _subscriberId = _broker.SubscribeAll(HandleMessage);
                    
                    Console.WriteLine($"Successfully connected to {connOption}. Subscribed with ID: {_subscriberId}");
                    
                    // If we reach here, we successfully connected
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error connecting to {connOption}: {ex.Message}");
                    
                    // Close and dispose any partially initialized broker
                    if (_broker != null)
                    {
                        _broker.Dispose();
                        _broker = null;
                    }
                }
            }
            
            // Check if we successfully connected to any broker
            if (_broker == null)
            {
                Console.WriteLine("Failed to connect to any message broker. Monitor will not work.");
                throw new Exception("Could not connect to any message broker");
            }
            
            // Give some time for the subscription to register
            await Task.Delay(1000);
        }

        private static async Task ShutdownBroker()
        {
            if (_broker != null)
            {
                _broker.Unsubscribe(_subscriberId);
                await Task.Delay(500); // Give time for unsubscribe to process
            }
        }

        private static void HandleMessage(MSA.Foundation.Messaging.Message message)
        {
            try
            {
                // Format the message info
                string messageType = message.MessageType.ToString();
                string source = message.SenderId ?? "Unknown";
                string destination = message.ReceiverId ?? "Broadcast";
                string messageId = message.MessageId ?? Guid.NewGuid().ToString();
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                
                // Get message details
                string details = ExtractMessageDetails(message);
                
                // Format and print with colors
                ConsoleColor originalColor = Console.ForegroundColor;
                
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"[{timestamp}] ");
                
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"{messageType} ");
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"From: {source} ");
                
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write($"To: {destination} ");
                
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"[ID: {messageId.Substring(0, 8)}...]");
                
                Console.WriteLine();
                
                // Show details if available
                if (!string.IsNullOrEmpty(details))
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"  {details}");
                }
                
                Console.ForegroundColor = originalColor;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling message: {ex.Message}");
            }
        }

        private static string ExtractMessageDetails(MSA.Foundation.Messaging.Message message)
        {
            try
            {
                // Show payload if available
                if (!string.IsNullOrEmpty(message.Payload))
                {
                    // Try to pretty-print the JSON
                    try
                    {
                        var jsonObj = JsonSerializer.Deserialize<object>(message.Payload);
                        return JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true });
                    }
                    catch
                    {
                        // If we can't parse as JSON, just return the payload
                        return message.Payload;
                    }
                }
                
                // For other message types, show type info
                return message.GetType().Name;
            }
            catch
            {
                // If we can't extract details, at least show the type
                return message.GetType().Name;
            }
        }
    }
}