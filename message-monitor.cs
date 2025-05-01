using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation;
using MSA.Foundation.Messaging;
using MSA.Foundation.ServiceManagement;
using PokerGame.Abstractions.Messaging;
using PokerGame.Core.Messaging;

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
            var context = new ExecutionContext();
            context.Initialize("MessageMonitor");
            
            // Create a broker connector
            _broker = BrokerManager.Instance.GetCentralMessageBroker();
            _subscriberId = Guid.NewGuid().ToString();
            
            // Subscribe to all message types
            _broker.Subscribe(_subscriberId, HandleMessage);
            
            Console.WriteLine($"Subscribed with ID: {_subscriberId}");
            
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

        private static void HandleMessage(IMessage message)
        {
            try
            {
                // Format the message info
                string messageType = message.MessageType.ToString();
                string source = message.SourceId ?? "Unknown";
                string destination = message.DestinationId ?? "Broadcast";
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

        private static string ExtractMessageDetails(IMessage message)
        {
            try
            {
                // Try to extract details based on message type
                if (message is SimpleMessage simpleMessage)
                {
                    var payload = simpleMessage.Payload;
                    if (payload != null)
                    {
                        // Try to serialize the payload for display
                        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
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