using System;
using System.Threading;
using System.Threading.Tasks;
using System.CommandLine;

namespace PokerGame.Core.Process
{
    /// <summary>
    /// Utility class for launching poker game services from the command line
    /// </summary>
    public static class ServiceLauncher
    {
        private static ProcessCoordinator? _coordinator;
        private static CancellationTokenSource? _cancellationTokenSource;
        
        /// <summary>
        /// Run the service launcher with the specified command line arguments
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static async Task<int> RunAsync(string[] args)
        {
            try
            {
                Console.WriteLine("DEBUG: Command line arguments received: " + string.Join(", ", args));
                
                if (args.Length == 0)
                {
                    ShowHelp();
                    return 1;
                }
                
                // Parse arguments using simple approach without System.CommandLine
                string command = args[0];
                int portOffset = -1;
                bool verbose = false;
                bool curses = false;
                
                // Parse options
                for (int i = 1; i < args.Length; i++)
                {
                    string arg = args[i];
                    
                    if (arg == "--port-offset" || arg == "-p")
                    {
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int port))
                        {
                            portOffset = port;
                            i++; // Skip the value
                        }
                        else
                        {
                            Console.WriteLine("Error: --port-offset requires a numeric value.");
                            return 1;
                        }
                    }
                    else if (arg.StartsWith("--port-offset="))
                    {
                        string value = arg.Substring("--port-offset=".Length);
                        if (int.TryParse(value, out int port))
                        {
                            portOffset = port;
                        }
                        else
                        {
                            Console.WriteLine("Error: --port-offset requires a numeric value.");
                            return 1;
                        }
                    }
                    else if (arg == "--verbose" || arg == "-v")
                    {
                        verbose = true;
                    }
                    else if (arg == "--curses" || arg == "-c")
                    {
                        curses = true;
                    }
                    else
                    {
                        Console.WriteLine($"Unrecognized argument: {arg}");
                        ShowHelp();
                        return 1;
                    }
                }
                
                Console.WriteLine($"DEBUG: Using command={command}, portOffset={portOffset}, verbose={verbose}, curses={curses}");
                
                // Execute the appropriate command
                switch (command)
                {
                    case "start-all":
                        return await StartAllAsync(portOffset, verbose, curses);
                        
                    case "start-services":
                        return await StartServicesAsync(portOffset, verbose);
                        
                    case "start-client":
                        return await StartClientAsync(portOffset, verbose, curses);
                        
                    case "stop-all":
                        return await StopAllAsync();
                        
                    case "stop-services":
                        return await StopServicesAsync();
                        
                    case "stop-client":
                        return await StopClientAsync();
                        
                    case "status":
                        return await ShowStatusAsync();
                        
                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        ShowHelp();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in ServiceLauncher.RunAsync: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }
        
        /// <summary>
        /// Display help information for the command-line interface
        /// </summary>
        private static void ShowHelp()
        {
            Console.WriteLine("PokerGame Service Launcher");
            Console.WriteLine("Usage: PokerGame.Launcher <command> [options]");
            Console.WriteLine("");
            Console.WriteLine("Commands:");
            Console.WriteLine("  start-all        Start all services (services host and console client)");
            Console.WriteLine("  start-services   Start only the services host");
            Console.WriteLine("  start-client     Start only the console client");
            Console.WriteLine("  stop-all         Stop all running services");
            Console.WriteLine("  stop-services    Stop the services host");
            Console.WriteLine("  stop-client      Stop the console client");
            Console.WriteLine("  status           Show the status of all services");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("  --port-offset, -p <value>   Port offset for services (required for start-client)");
            Console.WriteLine("  --verbose, -v               Enable verbose logging");
            Console.WriteLine("  --curses, -c                Use curses UI for console client");
        }
        
        /// <summary>
        /// Start all services
        /// </summary>
        private static Task<int> StartAllAsync(int portOffset, bool verbose, bool curses)
        {
            try
            {
                InitializeCoordinator();
                
                // With our new standardized approach, curses parameter directly determines the UI mode
                bool useCurses = curses;
                
                if (portOffset < 0)
                {
                    // Let the coordinator generate a random port offset
                    int offset = _coordinator!.StartAllServices(useCurses, verbose);
                    if (offset < 0)
                    {
                        Console.WriteLine("Failed to start services");
                        return Task.FromResult(1);
                    }
                    
                    Console.WriteLine($"All services started with port offset {offset} using {(useCurses ? "curses" : "enhanced")} UI");
                }
                else
                {
                    // Start services with the specified port offset
                    int servicesOffset = _coordinator!.StartServicesHost(portOffset, verbose);
                    if (servicesOffset < 0)
                    {
                        Console.WriteLine("Failed to start services host");
                        return Task.FromResult(1);
                    }
                    
                    // Give services time to start up
                    Thread.Sleep(3000);
                    
                    int clientPid = _coordinator.StartConsoleClient(portOffset, useCurses, verbose);
                    if (clientPid < 0)
                    {
                        Console.WriteLine("Failed to start console client");
                        _coordinator.StopAllServices();
                        return Task.FromResult(1);
                    }
                    
                    Console.WriteLine($"All services started with port offset {portOffset} using {(useCurses ? "curses" : "enhanced")} UI");
                }
                
                // Keep running until Ctrl+C
                WaitForCancellation();
                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting services: {ex.Message}");
                return Task.FromResult(1);
            }
        }
        
        /// <summary>
        /// Start only the services host
        /// </summary>
        private static Task<int> StartServicesAsync(int portOffset, bool verbose)
        {
            try
            {
                InitializeCoordinator();
                
                int offset = _coordinator!.StartServicesHost(portOffset, verbose);
                if (offset < 0)
                {
                    Console.WriteLine("Failed to start services host");
                    return Task.FromResult(1);
                }
                
                Console.WriteLine($"Services host started with port offset {offset}");
                
                // Keep running until Ctrl+C
                WaitForCancellation();
                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting services host: {ex.Message}");
                return Task.FromResult(1);
            }
        }
        
        /// <summary>
        /// Start only the console client
        /// </summary>
        private static Task<int> StartClientAsync(int portOffset, bool verbose, bool curses)
        {
            try
            {
                InitializeCoordinator();
                
                if (portOffset < 0)
                {
                    Console.WriteLine("Port offset is required to start the client");
                    return Task.FromResult(1);
                }
                
                // With our new standardized approach, curses parameter directly determines the UI mode
                bool useCurses = curses;
                
                int clientPid = _coordinator!.StartConsoleClient(portOffset, useCurses, verbose);
                if (clientPid < 0)
                {
                    Console.WriteLine("Failed to start console client");
                    return Task.FromResult(1);
                }
                
                Console.WriteLine($"Console client started with port offset {portOffset} using {(useCurses ? "curses" : "enhanced")} UI");
                
                // Client doesn't need to keep running, it has its own process
                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting console client: {ex.Message}");
                return Task.FromResult(1);
            }
        }
        
        /// <summary>
        /// Stop all services
        /// </summary>
        private static Task<int> StopAllAsync()
        {
            try
            {
                InitializeCoordinator();
                _coordinator!.StopAllServices();
                Console.WriteLine("All services stopped");
                
                // Cancel the wait operation if active
                _cancellationTokenSource?.Cancel();
                
                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping services: {ex.Message}");
                return Task.FromResult(1);
            }
        }
        
        /// <summary>
        /// Stop the services host
        /// </summary>
        private static Task<int> StopServicesAsync()
        {
            try
            {
                InitializeCoordinator();
                
                if (_coordinator!.IsServicesHostRunning())
                {
                    _coordinator.StopAllServices(); // Will stop both host and client
                    Console.WriteLine("Services host stopped");
                }
                else
                {
                    Console.WriteLine("Services host is not running");
                }
                
                // Cancel the wait operation if active
                _cancellationTokenSource?.Cancel();
                
                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping services host: {ex.Message}");
                return Task.FromResult(1);
            }
        }
        
        /// <summary>
        /// Stop the console client
        /// </summary>
        private static Task<int> StopClientAsync()
        {
            try
            {
                InitializeCoordinator();
                
                if (_coordinator!.IsConsoleClientRunning())
                {
                    // This will stop only the client, not the services host
                    var manager = ProcessManager.Instance;
                    manager.StopProcessesByName("ConsoleUI");
                    manager.StopProcessesByName("CursesUI");
                    Console.WriteLine("Console client stopped");
                }
                else
                {
                    Console.WriteLine("Console client is not running");
                }
                
                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping console client: {ex.Message}");
                return Task.FromResult(1);
            }
        }
        
        /// <summary>
        /// Show the status of all services
        /// </summary>
        private static Task<int> ShowStatusAsync()
        {
            try
            {
                InitializeCoordinator();
                
                bool servicesRunning = _coordinator!.IsServicesHostRunning();
                bool clientRunning = _coordinator.IsConsoleClientRunning();
                
                Console.WriteLine("PokerGame Services Status:");
                Console.WriteLine($"Services Host: {(servicesRunning ? "Running" : "Not Running")}");
                Console.WriteLine($"Console Client: {(clientRunning ? "Running" : "Not Running")}");
                
                // Show all running processes for more detail
                var manager = ProcessManager.Instance;
                var services = manager.GetRunningServices();
                
                Console.WriteLine("\nRunning Services:");
                bool anyServices = false;
                
                foreach (var service in services)
                {
                    Console.WriteLine($"- {service}");
                    anyServices = true;
                }
                
                if (!anyServices)
                {
                    Console.WriteLine("No services are currently running");
                }
                
                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing status: {ex.Message}");
                return Task.FromResult(1);
            }
        }
        
        /// <summary>
        /// Initialize the process coordinator if not already initialized
        /// </summary>
        private static void InitializeCoordinator()
        {
            if (_coordinator == null)
            {
                _coordinator = ProcessCoordinator.Instance;
            }
        }
        
        /// <summary>
        /// Wait for cancellation (Ctrl+C)
        /// </summary>
        private static void WaitForCancellation()
        {
            // Create a cancellation token source
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Handle Ctrl+C
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _cancellationTokenSource.Cancel();
            };
            
            Console.WriteLine("Press Ctrl+C to stop services...");
            
            try
            {
                // Wait indefinitely until cancellation is requested
                Task.Delay(-1, _cancellationTokenSource.Token).Wait();
            }
            catch (OperationCanceledException)
            {
                // Cancellation was requested, this is expected
                Console.WriteLine("Cancellation requested");
            }
            
            // Clean up resources
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }
}