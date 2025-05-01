using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using MSA.Foundation.Messaging;

// Message Monitor - Diagnostic tool to observe all messages flowing through the system
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
        
        // Filtering options
        private static FilterType _currentFilterType = FilterType.None;
        private static string _currentFilterValue = string.Empty;
        
        // Statistics tracking
        private static Dictionary<MessageType, int> _messageTypeStats = new Dictionary<MessageType, int>();
        private static Dictionary<string, int> _senderStats = new Dictionary<string, int>();
        private static Dictionary<string, int> _receiverStats = new Dictionary<string, int>();
        private static int _totalMessagesReceived = 0;
        private static int _totalMessagesDisplayed = 0;
        private static DateTime _startTime = DateTime.Now;

        static async Task Main(string[] args)
        {
            Console.WriteLine("====================================");
            Console.WriteLine("  PokerGame Message Monitor Tool    ");
            Console.WriteLine("====================================");
            
            // Process command line args
            ProcessCommandLineArgs(args);
            
            Console.WriteLine("Connecting to message broker...");

            try
            {
                await InitializeBroker();
                
                Console.WriteLine("Connected! Monitoring all messages...");
                DisplayHelpText();
                
                // Set up Ctrl+C handler
                Console.CancelKeyPress += (s, e) => {
                    e.Cancel = true;
                    _cts.Cancel();
                };
                
                // Start a background task to handle user input
                var userInputTask = Task.Run(HandleUserInput);
                
                // Keep the program running until Ctrl+C
                await Task.Delay(-1, _cts.Token).ContinueWith(t => { });
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Monitoring stopped.");
                DisplayStatistics();
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
        
        private static void ProcessCommandLineArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();
                
                switch (arg)
                {
                    case "-h":
                    case "--help":
                        DisplayHelpText();
                        Environment.Exit(0);
                        break;
                        
                    case "-t":
                    case "--type":
                        if (i + 1 < args.Length)
                        {
                            _currentFilterType = FilterType.MessageType;
                            _currentFilterValue = args[++i];
                            Console.WriteLine($"Filtering by message type: {_currentFilterValue}");
                        }
                        break;
                        
                    case "-s":
                    case "--sender":
                        if (i + 1 < args.Length)
                        {
                            _currentFilterType = FilterType.SenderId;
                            _currentFilterValue = args[++i];
                            Console.WriteLine($"Filtering by sender ID: {_currentFilterValue}");
                        }
                        break;
                        
                    case "-r":
                    case "--receiver":
                        if (i + 1 < args.Length)
                        {
                            _currentFilterType = FilterType.ReceiverId;
                            _currentFilterValue = args[++i];
                            Console.WriteLine($"Filtering by receiver ID: {_currentFilterValue}");
                        }
                        break;
                        
                    case "-p":
                    case "--payload":
                        if (i + 1 < args.Length)
                        {
                            _currentFilterType = FilterType.Payload;
                            _currentFilterValue = args[++i];
                            Console.WriteLine($"Filtering by payload content: {_currentFilterValue}");
                        }
                        break;
                }
            }
        }
        
        private static void DisplayHelpText()
        {
            Console.WriteLine();
            Console.WriteLine("====================================");
            Console.WriteLine("  Message Monitor - Help           ");
            Console.WriteLine("====================================");
            Console.WriteLine("Interactive commands:");
            Console.WriteLine("Filter commands:");
            Console.WriteLine("  [T] Filter by message type      [S] Filter by sender");
            Console.WriteLine("  [R] Filter by receiver          [P] Filter by payload");
            Console.WriteLine("  [C] Clear all filters           [0] Clear all filters");
            Console.WriteLine();
            Console.WriteLine("Quick filters:");
            Console.WriteLine("  [1] Show only Requests          [2] Show only Responses");
            Console.WriteLine("  [3] Show only Commands          [4] Show only Events");
            Console.WriteLine("  [5] Hide Heartbeat messages");
            Console.WriteLine();
            Console.WriteLine("Display options:");
            Console.WriteLine("  [H] Toggle headers display      [D] Toggle payload display");
            Console.WriteLine("  [X] Display statistics");
            Console.WriteLine();
            Console.WriteLine("Other commands:");
            Console.WriteLine("  [?] Show this help              [Q] Exit monitor");
            Console.WriteLine("  [Ctrl+C] Exit monitor");
            Console.WriteLine();
        }
        
        private static void DisplayStatistics()
        {
            TimeSpan runtime = DateTime.Now - _startTime;
            
            Console.WriteLine();
            Console.WriteLine("====================================");
            Console.WriteLine("  Message Monitor Statistics       ");
            Console.WriteLine("====================================");
            Console.WriteLine($"Runtime: {runtime.Hours:00}:{runtime.Minutes:00}:{runtime.Seconds:00}");
            Console.WriteLine($"Total messages received: {_totalMessagesReceived}");
            Console.WriteLine($"Total messages displayed: {_totalMessagesDisplayed}");
            
            if (_messageTypeStats.Any())
            {
                Console.WriteLine();
                Console.WriteLine("Message types:");
                foreach (var stat in _messageTypeStats.OrderByDescending(s => s.Value))
                {
                    Console.WriteLine($"  {stat.Key}: {stat.Value} messages");
                }
            }
            
            if (_senderStats.Any())
            {
                Console.WriteLine();
                Console.WriteLine("Top senders:");
                foreach (var stat in _senderStats.OrderByDescending(s => s.Value).Take(5))
                {
                    Console.WriteLine($"  {stat.Key}: {stat.Value} messages");
                }
            }
            
            if (_receiverStats.Any())
            {
                Console.WriteLine();
                Console.WriteLine("Top receivers:");
                foreach (var stat in _receiverStats.OrderByDescending(s => s.Value).Take(5))
                {
                    Console.WriteLine($"  {stat.Key}: {stat.Value} messages");
                }
            }
        }
        
        private static async Task HandleUserInput()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        
                        switch (char.ToUpper(key.KeyChar))
                        {
                            case 'T':
                                await SetFilter(FilterType.MessageType);
                                break;
                                
                            case 'S':
                                await SetFilter(FilterType.SenderId);
                                break;
                                
                            case 'R':
                                await SetFilter(FilterType.ReceiverId);
                                break;
                                
                            case 'P':
                                await SetFilter(FilterType.Payload);
                                break;
                                
                            case 'C':
                                ClearFilters();
                                break;
                                
                            case 'X':
                                DisplayStatistics();
                                break;
                                
                            case 'H':
                                _showHeaders = !_showHeaders;
                                Console.WriteLine($"Headers display: {(_showHeaders ? "ON" : "OFF")}");
                                break;
                                
                            case 'D':
                                _showPayloads = !_showPayloads;
                                Console.WriteLine($"Payload display: {(_showPayloads ? "ON" : "OFF")}");
                                break;
                                
                            case '1':
                                // Show only request messages
                                _currentFilterType = FilterType.MessageType;
                                _currentFilterValue = "Request";
                                Console.WriteLine("Filter set: Showing only Request messages");
                                break;
                                
                            case '2':
                                // Show only response messages
                                _currentFilterType = FilterType.MessageType;
                                _currentFilterValue = "Response";
                                Console.WriteLine("Filter set: Showing only Response messages");
                                break;
                                
                            case '3':
                                // Show only command messages
                                _currentFilterType = FilterType.MessageType;
                                _currentFilterValue = "Command";
                                Console.WriteLine("Filter set: Showing only Command messages");
                                break;
                                
                            case '4':
                                // Show only event messages
                                _currentFilterType = FilterType.MessageType;
                                _currentFilterValue = "Event";
                                Console.WriteLine("Filter set: Showing only Event messages");
                                break;
                                
                            case '5':
                                // Show non-heartbeat messages
                                _currentFilterType = FilterType.MessageType;
                                _currentFilterValue = "Heartbeat";
                                Console.WriteLine("Filter set: Hiding Heartbeat messages");
                                break;
                                
                            case '0':
                                ClearFilters();
                                break;
                                
                            case '?':
                                DisplayHelpText();
                                break;
                                
                            case '\r': // Enter key
                                Console.WriteLine();
                                break;
                                
                            case 'Q':
                                Console.WriteLine("Exiting message monitor...");
                                _cts.Cancel();
                                break;
                        }
                    }
                    
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in user input handler: {ex.Message}");
            }
        }
        
        private static async Task SetFilter(FilterType filterType)
        {
            Console.WriteLine();
            Console.Write($"Enter {filterType} filter value (empty to clear): ");
            
            // Read the filter value
            string filterValue = Console.ReadLine()?.Trim() ?? string.Empty;
            
            if (string.IsNullOrEmpty(filterValue))
            {
                _currentFilterType = FilterType.None;
                _currentFilterValue = string.Empty;
                Console.WriteLine("Filter cleared.");
            }
            else
            {
                _currentFilterType = filterType;
                _currentFilterValue = filterValue;
                Console.WriteLine($"Filter set: {filterType} = {filterValue}");
            }
            
            Console.WriteLine();
        }
        
        private static void ClearFilters()
        {
            _currentFilterType = FilterType.None;
            _currentFilterValue = string.Empty;
            Console.WriteLine("All filters cleared.");
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

        // Settings for message display
        private static bool _showHeaders = false;
        private static bool _showPayloads = true;
        
        private static void HandleMessage(MSA.Foundation.Messaging.Message message)
        {
            try
            {
                // Track statistics
                _totalMessagesReceived++;
                
                // Update message type stats
                if (!_messageTypeStats.ContainsKey(message.MessageType))
                {
                    _messageTypeStats[message.MessageType] = 0;
                }
                _messageTypeStats[message.MessageType]++;
                
                // Format the message info
                string messageType = message.MessageType.ToString();
                string source = message.SenderId ?? "Unknown";
                string destination = message.ReceiverId ?? "Broadcast";
                string messageId = message.MessageId ?? Guid.NewGuid().ToString();
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                
                // Update sender stats
                if (!string.IsNullOrEmpty(message.SenderId))
                {
                    if (!_senderStats.ContainsKey(message.SenderId))
                    {
                        _senderStats[message.SenderId] = 0;
                    }
                    _senderStats[message.SenderId]++;
                }
                
                // Update receiver stats
                if (!string.IsNullOrEmpty(message.ReceiverId))
                {
                    if (!_receiverStats.ContainsKey(message.ReceiverId))
                    {
                        _receiverStats[message.ReceiverId] = 0;
                    }
                    _receiverStats[message.ReceiverId]++;
                }
                
                // Apply filtering if enabled
                if (_currentFilterType != FilterType.None && !string.IsNullOrEmpty(_currentFilterValue))
                {
                    bool includeMessage = false;
                    bool invertFilter = false; // For exclusion filters like hiding heartbeats
                    
                    // Special case for heartbeat filtering (when using key 5)
                    if (message.MessageType == MessageType.Heartbeat && 
                        _currentFilterType == FilterType.MessageType && 
                        _currentFilterValue == "Heartbeat")
                    {
                        return; // Skip heartbeats when filtering for them
                    }
                    
                    // For message type filtering, special handling for numeric shortcut keys
                    if (_currentFilterType == FilterType.MessageType)
                    {
                        // Exact match for message type is required (for numeric shortcuts)
                        if (_currentFilterValue == messageType)
                        {
                            includeMessage = true;
                        }
                        // Otherwise, do a contains match (for text filters)
                        else if (messageType.Contains(_currentFilterValue, StringComparison.OrdinalIgnoreCase))
                        {
                            includeMessage = true;
                        }
                    }
                    else
                    {
                        switch (_currentFilterType)
                        {
                            case FilterType.SenderId:
                                includeMessage = source.Contains(_currentFilterValue, StringComparison.OrdinalIgnoreCase);
                                break;
                                
                            case FilterType.ReceiverId:
                                includeMessage = destination.Contains(_currentFilterValue, StringComparison.OrdinalIgnoreCase);
                                break;
                                
                            case FilterType.Payload:
                                includeMessage = message.Payload?.Contains(_currentFilterValue, StringComparison.OrdinalIgnoreCase) ?? false;
                                break;
                        }
                    }
                    
                    if (invertFilter)
                    {
                        includeMessage = !includeMessage;
                    }
                    
                    if (!includeMessage)
                    {
                        return; // Skip displaying this message
                    }
                }
                
                // If we got here, the message passed the filters
                _totalMessagesDisplayed++;
                
                // Get message details
                string details = ExtractMessageDetails(message);
                
                // Format and print with colors
                ConsoleColor originalColor = Console.ForegroundColor;
                
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"[{timestamp}] ");
                
                // Message type with color based on type
                ConsoleColor typeColor = GetColorForMessageType(message.MessageType);
                Console.ForegroundColor = typeColor;
                Console.Write($"{messageType} ");
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"From: {source} ");
                
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write($"To: {destination} ");
                
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"[ID: {messageId.Substring(0, Math.Min(messageId.Length, 8))}...]");
                
                Console.WriteLine();
                
                // Show headers if enabled
                if (_showHeaders && message.Headers != null && message.Headers.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine("  Headers:");
                    foreach (var header in message.Headers)
                    {
                        Console.WriteLine($"    {header.Key}: {header.Value}");
                    }
                }
                
                // Show details if available and payload display is enabled
                if (_showPayloads && !string.IsNullOrEmpty(details))
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"  {details.Replace("\n", "\n  ")}");
                }
                
                Console.ForegroundColor = originalColor;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling message: {ex.Message}");
            }
        }
        
        private static ConsoleColor GetColorForMessageType(MessageType type)
        {
            return type switch
            {
                MessageType.Heartbeat => ConsoleColor.DarkGray,
                MessageType.Request => ConsoleColor.Cyan,
                MessageType.Response => ConsoleColor.Blue,
                MessageType.Command => ConsoleColor.Yellow,
                MessageType.Event => ConsoleColor.Green,
                MessageType.Error => ConsoleColor.Red,
                MessageType.ServiceDiscovery => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };
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
                        var options = new JsonSerializerOptions { 
                            WriteIndented = true,
                            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        };
                        return JsonSerializer.Serialize(jsonObj, options);
                    }
                    catch
                    {
                        // If we can't parse as JSON, just return the payload
                        if (message.Payload.Length > 500)
                        {
                            return message.Payload.Substring(0, 500) + "... [truncated]";
                        }
                        return message.Payload;
                    }
                }
                
                // For other message types, show type info
                return $"Message type: {message.GetType().Name}";
            }
            catch (Exception ex)
            {
                // If we can't extract details, at least show the type and error
                return $"Error extracting message details: {ex.Message}";
            }
        }
    }
}