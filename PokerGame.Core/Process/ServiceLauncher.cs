using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading;
using System.Threading.Tasks;

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
            // Create the root command and options
            var rootCommand = new RootCommand("PokerGame Service Launcher");
            
            // Add global options
            var portOffsetOption = new Option<int>(
                "--port-offset",
                description: "Port offset for all services",
                getDefaultValue: () => -1);
            rootCommand.AddOption(portOffsetOption);
            
            var verboseOption = new Option<bool>(
                "--verbose",
                description: "Enable verbose logging",
                getDefaultValue: () => false);
            rootCommand.AddOption(verboseOption);
            
            var cursesOption = new Option<bool>(
                "--curses",
                description: "Use curses UI for console client",
                getDefaultValue: () => false);
            rootCommand.AddOption(cursesOption);
            
            // Add commands
            var startAllCommand = new Command("start-all", "Start all services (services host and console client)");
            rootCommand.AddCommand(startAllCommand);
            
            var startServicesCommand = new Command("start-services", "Start only the services host");
            rootCommand.AddCommand(startServicesCommand);
            
            var startClientCommand = new Command("start-client", "Start only the console client");
            rootCommand.AddCommand(startClientCommand);
            
            var stopAllCommand = new Command("stop-all", "Stop all running services");
            rootCommand.AddCommand(stopAllCommand);
            
            var stopServicesCommand = new Command("stop-services", "Stop the services host");
            rootCommand.AddCommand(stopServicesCommand);
            
            var stopClientCommand = new Command("stop-client", "Stop the console client");
            rootCommand.AddCommand(stopClientCommand);
            
            var statusCommand = new Command("status", "Show the status of all services");
            rootCommand.AddCommand(statusCommand);
            
            // Set handlers for each command
            startAllCommand.SetHandler((portOffset, verbose, curses) =>
            {
                return StartAllAsync(portOffset, verbose, curses);
            }, portOffsetOption, verboseOption, cursesOption);
            
            startServicesCommand.SetHandler((portOffset, verbose) =>
            {
                return StartServicesAsync(portOffset, verbose);
            }, portOffsetOption, verboseOption);
            
            startClientCommand.SetHandler((portOffset, verbose, curses) =>
            {
                return StartClientAsync(portOffset, verbose, curses);
            }, portOffsetOption, verboseOption, cursesOption);
            
            stopAllCommand.SetHandler(() =>
            {
                return StopAllAsync();
            });
            
            stopServicesCommand.SetHandler(() =>
            {
                return StopServicesAsync();
            });
            
            stopClientCommand.SetHandler(() =>
            {
                return StopClientAsync();
            });
            
            statusCommand.SetHandler(() =>
            {
                return ShowStatusAsync();
            });
            
            // Parse the command line
            return await rootCommand.InvokeAsync(args);
        }
        
        /// <summary>
        /// Start all services
        /// </summary>
        private static Task<int> StartAllAsync(int portOffset, bool verbose, bool curses)
        {
            try
            {
                InitializeCoordinator();
                
                if (portOffset < 0)
                {
                    // Let the coordinator generate a random port offset
                    int offset = _coordinator!.StartAllServices(curses, verbose);
                    if (offset < 0)
                    {
                        Console.WriteLine("Failed to start services");
                        return Task.FromResult(1);
                    }
                    
                    Console.WriteLine($"All services started with port offset {offset}");
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
                    
                    int clientPid = _coordinator.StartConsoleClient(portOffset, curses, verbose);
                    if (clientPid < 0)
                    {
                        Console.WriteLine("Failed to start console client");
                        _coordinator.StopAllServices();
                        return Task.FromResult(1);
                    }
                    
                    Console.WriteLine($"All services started with port offset {portOffset}");
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
                
                int clientPid = _coordinator!.StartConsoleClient(portOffset, curses, verbose);
                if (clientPid < 0)
                {
                    Console.WriteLine("Failed to start console client");
                    return Task.FromResult(1);
                }
                
                Console.WriteLine($"Console client started with port offset {portOffset}");
                
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