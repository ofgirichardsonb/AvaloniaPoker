using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using MSA.Foundation.Messaging;

// Message Monitor - Simplified diagnostic tool to observe all messages flowing through the system
namespace MessageMonitor
{
    public enum FilterType
    {
        None,
        MessageType, 
        SenderId,
        ReceiverId,
        Payload
    }

    class Program
    {
        private static CancellationTokenSource _cts = new CancellationTokenSource();
        private static IMessageBroker _broker;
        private static string _subscriberId;
        
        // Filtering options - default to filtering out heartbeat messages
        private static FilterType _currentFilterType = FilterType.MessageType;
        private static string _currentFilterValue = "!Heartbeat";
        
        // Display options - always show headers and payloads
        private static bool _showHeaders = true;
        private static bool _showPayloads = true;
        
        // Statistics tracking
        private static Dictionary<string, int> _messageTypeStats = new Dictionary<string, int>();
        private static Dictionary<string, int> _senderStats = new Dictionary<string, int>();
        private static Dictionary<string, int> _receiverStats = new Dictionary<string, int>();
        private static int _totalMessagesReceived = 0;
        private static int _totalMessagesDisplayed = 0;
        private static DateTime _startTime = DateTime.Now;
        private static DateTime _lastStatsTime = DateTime.Now;

        static async Task Main(string[] args)
        {
            Console.WriteLine("====================================");
            Console.WriteLine("  PokerGame Message Monitor Tool    ");
            Console.WriteLine("====================================");
            
            // Set up cancellation on Ctrl+C
            Console.CancelKeyPress += (sender, e) => {
                e.Cancel = true;
                _cts.Cancel();
            };
            
            Console.WriteLine("Connecting to message broker...");

            try
            {
                // Initialize and start the broker connection
                await InitializeBroker();
                
                // Start monitoring messages
                await MonitorMessages();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in message monitor: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Cleanup
                await CleanupBroker();
                Console.WriteLine("Message monitor stopped.");
            }
        }

        private static async Task MonitorMessages()
        {
            Console.WriteLine("Connected! Monitoring all messages...");
            Console.WriteLine("NOTE: Interactive mode disabled.");
            Console.WriteLine("- Using filter to hide Heartbeat messages.");
            Console.WriteLine("- Statistics will display every 15 seconds.");
            Console.WriteLine("- Press Ctrl+C to exit the monitor.");
            Console.WriteLine("====================================");
            
            int lastStatSeconds = -1;
            
            // Poll for messages continually
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    // Get all pending messages (non-blocking)
                    var messages = await _broker.ReceiveAsync(_subscriberId, TimeSpan.FromMilliseconds(10));
                    
                    if (messages != null)
                    {
                        // Increment stats counters
                        _totalMessagesReceived++;
                        
                        // Extract message type as string
                        string messageType = messages.MessageType.ToString();
                        
                        // Track message type stats
                        if (!_messageTypeStats.ContainsKey(messageType))
                            _messageTypeStats[messageType] = 0;
                        _messageTypeStats[messageType]++;
                        
                        // Track sender stats
                        if (!string.IsNullOrEmpty(messages.SenderId))
                        {
                            if (!_senderStats.ContainsKey(messages.SenderId))
                                _senderStats[messages.SenderId] = 0;
                            _senderStats[messages.SenderId]++;
                        }
                        
                        // Track receiver stats  
                        if (!string.IsNullOrEmpty(messages.ReceiverId))
                        {
                            if (!_receiverStats.ContainsKey(messages.ReceiverId))
                                _receiverStats[messages.ReceiverId] = 0;
                            _receiverStats[messages.ReceiverId]++;
                        }
                        
                        // Check if this message should be displayed based on current filter
                        bool shouldDisplay = ShouldDisplayMessage(messages);
                        
                        // Display message if it passes filter
                        if (shouldDisplay)
                        {
                            _totalMessagesDisplayed++;
                            DisplayMessage(messages, _showHeaders, _showPayloads);
                        }
                    }
                    
                    // Display stats periodically (every 15 seconds)
                    int currentSeconds = (int)DateTime.Now.Subtract(_startTime).TotalSeconds;
                    if (currentSeconds % 15 == 0 && currentSeconds != lastStatSeconds && currentSeconds > 0)
                    {
                        DisplayStatistics();
                        lastStatSeconds = currentSeconds;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error receiving messages: {ex.Message}");
                }
                
                // Brief pause to avoid CPU thrashing
                await Task.Delay(100);
            }
        }

        private static bool ShouldDisplayMessage(Message message)
        {
            if (_currentFilterType == FilterType.None)
                return true;
                
            switch (_currentFilterType)
            {
                case FilterType.MessageType:
                    // Special handling for negative filter (exclude)
                    if (_currentFilterValue.StartsWith("!"))
                    {
                        string typeToExclude = _currentFilterValue.Substring(1);
                        return !message.MessageType.ToString().Contains(typeToExclude);
                    }
                    return message.MessageType.ToString().Contains(_currentFilterValue);
                    
                case FilterType.SenderId:
                    return message.SenderId?.Contains(_currentFilterValue) ?? false;
                    
                case FilterType.ReceiverId:
                    return message.ReceiverId?.Contains(_currentFilterValue) ?? false;
                    
                case FilterType.Payload:
                    if (message.Payload == null)
                        return false;
                    
                    try
                    {
                        string payloadStr = message.Payload.ToString() ?? "";
                        return payloadStr.Contains(_currentFilterValue);
                    }
                    catch
                    {
                        return false;
                    }
                    
                default:
                    return true;
            }
        }

        private static void DisplayMessage(Message message, bool showHeaders, bool showPayloads)
        {
            // Determine message category
            var category = GetMessageCategory(message.MessageType.ToString());
            
            // Header section
            if (showHeaders)
            {
                Console.WriteLine($"=== {message.MessageType} from {message.SenderId ?? "<no-sender>"} ===");
                Console.WriteLine($"To: {message.ReceiverId ?? "<broadcast>"}");
                Console.WriteLine($"ID: {message.MessageId}");
                
                if (!string.IsNullOrEmpty(message.CorrelationId))
                {
                    Console.WriteLine($"Response To: {message.CorrelationId}");
                }
                
                Console.WriteLine($"Timestamp: {message.Timestamp}");
            }
            
            // Payload section
            if (showPayloads && message.Payload != null)
            {
                Console.WriteLine("Payload:");
                try
                {
                    // Try to format the payload as JSON if possible
                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    var jsonString = JsonSerializer.Serialize(message.Payload, jsonOptions);
                    Console.WriteLine(jsonString);
                }
                catch
                {
                    // Fall back to basic ToString() if can't serialize as JSON
                    Console.WriteLine(message.Payload.ToString());
                }
            }
            
            Console.WriteLine(); // Empty line for readability
        }

        private static string GetMessageCategory(string messageType)
        {
            if (messageType.Contains("Request") || messageType.Contains("Response"))
                return "REQUEST/RESPONSE";
                
            if (messageType.Contains("Command"))
                return "COMMAND";
                
            if (messageType.Contains("Event") || messageType.Contains("Heartbeat"))
                return "EVENT";
                
            return "OTHER";
        }

        private static void DisplayStatistics()
        {
            var runTime = DateTime.Now - _startTime;
            
            Console.WriteLine("\n====================================");
            Console.WriteLine($"  Message Monitor Statistics       ");
            Console.WriteLine("====================================");
            Console.WriteLine($"Running for: {runTime.Hours:00}:{runTime.Minutes:00}:{runTime.Seconds:00}");
            Console.WriteLine($"Total messages received: {_totalMessagesReceived}");
            Console.WriteLine($"Messages displayed: {_totalMessagesDisplayed}");
            
            if (_messageTypeStats.Count > 0)
            {
                Console.WriteLine("\nMessage Types:");
                foreach (var entry in _messageTypeStats.OrderByDescending(x => x.Value))
                {
                    Console.WriteLine($"  {entry.Key}: {entry.Value}");
                }
            }
            
            if (_senderStats.Count > 0)
            {
                Console.WriteLine("\nTop Senders:");
                foreach (var entry in _senderStats.OrderByDescending(x => x.Value).Take(5))
                {
                    Console.WriteLine($"  {entry.Key}: {entry.Value}");
                }
            }
            
            Console.WriteLine("====================================\n");
        }

        private static async Task InitializeBroker()
        {
            try
            {
                Console.WriteLine("Attempting to connect to message broker...");
                
                // Create a broker instance using the central broker manager
                _broker = new MessageBroker();
                _subscriberId = Guid.NewGuid().ToString();
                
                // Subscribe to all messages
                await _broker.SubscribeAsync(_subscriberId, null);
                
                // Try a test receive to make sure it's working
                var testMessage = await _broker.ReceiveAsync(_subscriberId, TimeSpan.FromMilliseconds(10));
                
                Console.WriteLine($"Successfully connected to broker. Subscribed with ID: {_subscriberId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to message broker: {ex.Message}");
                throw new Exception("Failed to connect to message broker. Check if services are running.");
            }
        }

        private static async Task CleanupBroker()
        {
            // Unsubscribe from all messages and clean up
            if (_broker != null && !string.IsNullOrEmpty(_subscriberId))
            {
                try
                {
                    await _broker.UnsubscribeAsync(_subscriberId, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during cleanup: {ex.Message}");
                }
            }
        }
    }
}