using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MessageBroker
{
    /// <summary>
    /// A test client for the message broker
    /// </summary>
    public class TestClient
    {
        private readonly BrokerClient _client;
        private readonly string _clientType;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        
        /// <summary>
        /// Creates a new test client
        /// </summary>
        /// <param name="clientName">The name of the client</param>
        /// <param name="clientType">The type of the client</param>
        /// <param name="capabilities">The capabilities of the client</param>
        /// <param name="brokerAddress">The address of the broker</param>
        /// <param name="brokerPort">The port of the broker</param>
        public TestClient(
            string clientName,
            string clientType,
            List<string>? capabilities = null,
            string brokerAddress = "localhost",
            int brokerPort = 5570)
        {
            _clientType = clientType;
            _client = new BrokerClient(clientName, clientType, capabilities, brokerAddress, brokerPort);
            
            // Set up message handler
            _client.MessageReceived += OnMessageReceived;
        }
        
        /// <summary>
        /// Connects to the broker and starts the client
        /// </summary>
        public void Start()
        {
            try
            {
                Console.WriteLine($"Starting test client: {_client.ClientName} ({_client.ClientType})");
                
                // Connect to the broker
                _client.Connect();
                
                Console.WriteLine("Connected to broker successfully");
                
                // Start a background task to process commands
                Task.Run(ProcessCommandsAsync);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting client: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        /// <summary>
        /// Processes commands from the console
        /// </summary>
        private async Task ProcessCommandsAsync()
        {
            try
            {
                Console.WriteLine("Enter commands (type 'help' for available commands):");
                
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Console.Write("> ");
                    var command = Console.ReadLine();
                    
                    if (string.IsNullOrWhiteSpace(command))
                        continue;
                    
                    await ProcessCommandAsync(command);
                }
            }
            catch (TaskCanceledException)
            {
                // Normal during shutdown
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing commands: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        /// <summary>
        /// Processes a single command
        /// </summary>
        /// <param name="command">The command to process</param>
        private async Task ProcessCommandAsync(string command)
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts[0].ToLowerInvariant();
            
            switch (cmd)
            {
                case "help":
                    DisplayHelp();
                    break;
                
                case "discover":
                    await DiscoverServicesAsync(parts);
                    break;
                
                case "ping":
                    await PingServiceAsync(parts);
                    break;
                
                case "send":
                    await SendMessageAsync(parts);
                    break;
                
                case "broadcast":
                    await BroadcastMessageAsync(parts);
                    break;
                
                case "exit":
                case "quit":
                    Console.WriteLine("Exiting...");
                    _cancellationTokenSource.Cancel();
                    break;
                
                default:
                    Console.WriteLine($"Unknown command: {cmd}");
                    DisplayHelp();
                    break;
            }
        }
        
        /// <summary>
        /// Displays help information
        /// </summary>
        private void DisplayHelp()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("  help                            - Displays this help information");
            Console.WriteLine("  discover [type] [capability]    - Discovers services of the specified type and capability");
            Console.WriteLine("  ping <serviceId>                - Pings the specified service");
            Console.WriteLine("  send <serviceId> <message>      - Sends a message to the specified service");
            Console.WriteLine("  broadcast <message>             - Broadcasts a message to all services");
            Console.WriteLine("  exit                            - Exits the application");
        }
        
        /// <summary>
        /// Discovers services asynchronously
        /// </summary>
        /// <param name="parts">The command parts</param>
        private async Task DiscoverServicesAsync(string[] parts)
        {
            string? type = null;
            string? capability = null;
            
            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
            {
                type = parts[1];
            }
            
            if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
            {
                capability = parts[2];
            }
            
            Console.WriteLine($"Discovering services: Type={type}, Capability={capability}");
            
            var services = await _client.DiscoverServicesAsync(type, capability);
            
            Console.WriteLine($"Discovered {services.Count} services:");
            foreach (var service in services)
            {
                Console.WriteLine($"  {service.ServiceId} ({service.ServiceName}, {service.ServiceType})");
                Console.WriteLine($"    Capabilities: {string.Join(", ", service.Capabilities)}");
            }
        }
        
        /// <summary>
        /// Pings a service asynchronously
        /// </summary>
        /// <param name="parts">The command parts</param>
        private async Task PingServiceAsync(string[] parts)
        {
            if (parts.Length < 2 || string.IsNullOrEmpty(parts[1]))
            {
                Console.WriteLine("Usage: ping <serviceId>");
                return;
            }
            
            var serviceId = parts[1];
            
            Console.WriteLine($"Pinging service: {serviceId}");
            
            var success = await _client.PingServiceAsync(serviceId);
            
            if (success)
            {
                Console.WriteLine($"Service {serviceId} is alive!");
            }
            else
            {
                Console.WriteLine($"Service {serviceId} did not respond");
            }
        }
        
        /// <summary>
        /// Sends a message to a service asynchronously
        /// </summary>
        /// <param name="parts">The command parts</param>
        private async Task SendMessageAsync(string[] parts)
        {
            if (parts.Length < 3 || string.IsNullOrEmpty(parts[1]) || string.IsNullOrEmpty(parts[2]))
            {
                Console.WriteLine("Usage: send <serviceId> <message>");
                return;
            }
            
            var serviceId = parts[1];
            var messageContent = string.Join(' ', parts, 2, parts.Length - 2);
            
            Console.WriteLine($"Sending message to {serviceId}: {messageContent}");
            
            var message = BrokerMessage.Create(BrokerMessageType.Custom, messageContent);
            message.SenderId = _client.ClientId;
            message.ReceiverId = serviceId;
            
            await _client.SendMessageAsync(message);
            
            Console.WriteLine("Message sent");
        }
        
        /// <summary>
        /// Broadcasts a message to all services asynchronously
        /// </summary>
        /// <param name="parts">The command parts</param>
        private async Task BroadcastMessageAsync(string[] parts)
        {
            if (parts.Length < 2 || string.IsNullOrEmpty(parts[1]))
            {
                Console.WriteLine("Usage: broadcast <message>");
                return;
            }
            
            var messageContent = string.Join(' ', parts, 1, parts.Length - 1);
            
            Console.WriteLine($"Broadcasting message: {messageContent}");
            
            var message = BrokerMessage.Create(BrokerMessageType.Custom, messageContent);
            message.SenderId = _client.ClientId;
            
            await _client.SendMessageAsync(message);
            
            Console.WriteLine("Message broadcast");
        }
        
        /// <summary>
        /// Handles received messages
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="message">The received message</param>
        private void OnMessageReceived(object? sender, BrokerMessage message)
        {
            if (message.Type == BrokerMessageType.Custom)
            {
                var content = message.GetPayload<string>();
                Console.WriteLine($"\nReceived message from {message.SenderId}: {content}\n> ");
            }
        }
        
        /// <summary>
        /// Stops the client
        /// </summary>
        public void Stop()
        {
            try
            {
                Console.WriteLine("Stopping test client...");
                
                _cancellationTokenSource.Cancel();
                _client.Dispose();
                
                Console.WriteLine("Test client stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping client: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        /// <summary>
        /// The entry point for the test client
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static void Main(string[] args)
        {
            TestClient? client = null;
            
            try
            {
                // Parse command line arguments
                var clientName = GetArgValue(args, "--name", "TestClient");
                var clientType = GetArgValue(args, "--type", "Test");
                var brokerAddress = GetArgValue(args, "--broker", "localhost");
                var brokerPortStr = GetArgValue(args, "--port", "5570");
                
                if (!int.TryParse(brokerPortStr, out int brokerPort))
                {
                    brokerPort = 5570;
                }
                
                // Create capabilities list
                var capabilities = new List<string> { "Test" };
                
                // Create and start the client
                client = new TestClient(clientName, clientType, capabilities, brokerAddress, brokerPort);
                client.Start();
                
                // Register Ctrl+C handler
                Console.CancelKeyPress += (sender, e) =>
                {
                    Console.WriteLine("Shutting down...");
                    e.Cancel = true;
                    client?.Stop();
                };
                
                // Wait for the client to finish
                while (!client._cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running test client: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                client?.Stop();
            }
        }
        
        /// <summary>
        /// Gets the value of a command line argument
        /// </summary>
        /// <param name="args">The command line arguments</param>
        /// <param name="argName">The argument name to look for</param>
        /// <param name="defaultValue">The default value to use if the argument is not found</param>
        /// <returns>The argument value</returns>
        private static string GetArgValue(string[] args, string argName, string defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(argName, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            
            return defaultValue;
        }
    }
}