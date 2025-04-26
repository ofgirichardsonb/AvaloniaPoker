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
            // Create the root command
            var rootCommand = new RootCommand("PokerGame Service Launcher");
            
            // NOTE: We don't add global options to the root command
            // because System.CommandLine beta4 has issues with global options being inherited by subcommands.
            // Instead, we define separate option instances for each command.
            
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
            
            // Create one set of options for each subcommand to avoid sharing
            // This is required in beta4 of System.CommandLine
            
            // StartAll command options
            var startAllPortOffsetOption = new Option<int>(
                name: "--port-offset",
                description: "Port offset for all services",
                getDefaultValue: () => -1);
            startAllPortOffsetOption.AddAlias("-p");
            
            var startAllVerboseOption = new Option<bool>(
                name: "--verbose",
                description: "Enable verbose logging",
                getDefaultValue: () => false);
            startAllVerboseOption.AddAlias("-v");
            
            var startAllCursesOption = new Option<bool>(
                name: "--curses",
                description: "Use curses UI for console client",
                getDefaultValue: () => false);
            startAllCursesOption.AddAlias("-c");
            
            var startAllEnhancedUiOption = new Option<bool>(
                name: "--enhanced-ui",
                description: "Use enhanced UI for console client",
                getDefaultValue: () => false);
            startAllEnhancedUiOption.AddAlias("-e");
            
            // Add options to the startAll command
            startAllCommand.AddOption(startAllPortOffsetOption);
            startAllCommand.AddOption(startAllVerboseOption);
            startAllCommand.AddOption(startAllCursesOption);
            startAllCommand.AddOption(startAllEnhancedUiOption);
            
            // StartServices command options
            var startServicesPortOffsetOption = new Option<int>(
                name: "--port-offset",
                description: "Port offset for all services",
                getDefaultValue: () => -1);
            startServicesPortOffsetOption.AddAlias("-p");
            
            var startServicesVerboseOption = new Option<bool>(
                name: "--verbose",
                description: "Enable verbose logging",
                getDefaultValue: () => false);
            startServicesVerboseOption.AddAlias("-v");
            
            // Add options to the startServices command
            startServicesCommand.AddOption(startServicesPortOffsetOption);
            startServicesCommand.AddOption(startServicesVerboseOption);
            
            // StartClient command options
            var startClientPortOffsetOption = new Option<int>(
                name: "--port-offset",
                description: "Port offset for all services",
                getDefaultValue: () => -1);
            startClientPortOffsetOption.AddAlias("-p");
            
            var startClientVerboseOption = new Option<bool>(
                name: "--verbose",
                description: "Enable verbose logging",
                getDefaultValue: () => false);
            startClientVerboseOption.AddAlias("-v");
            
            var startClientCursesOption = new Option<bool>(
                name: "--curses",
                description: "Use curses UI for console client",
                getDefaultValue: () => false);
            startClientCursesOption.AddAlias("-c");
            
            var startClientEnhancedUiOption = new Option<bool>(
                name: "--enhanced-ui",
                description: "Use enhanced UI for console client",
                getDefaultValue: () => false);
            startClientEnhancedUiOption.AddAlias("-e");
            
            // Add options to the startClient command
            startClientCommand.AddOption(startClientPortOffsetOption);
            startClientCommand.AddOption(startClientVerboseOption);
            startClientCommand.AddOption(startClientCursesOption);
            startClientCommand.AddOption(startClientEnhancedUiOption);
            
            // Set handlers for each command
            startAllCommand.SetHandler((portOffset, verbose, curses, enhancedUi) =>
            {
                return StartAllAsync(portOffset, verbose, curses, enhancedUi);
            }, startAllPortOffsetOption, startAllVerboseOption, startAllCursesOption, startAllEnhancedUiOption);
            
            startServicesCommand.SetHandler((portOffset, verbose) =>
            {
                return StartServicesAsync(portOffset, verbose);
            }, startServicesPortOffsetOption, startServicesVerboseOption);
            
            startClientCommand.SetHandler((portOffset, verbose, curses, enhancedUi) =>
            {
                return StartClientAsync(portOffset, verbose, curses, enhancedUi);
            }, startClientPortOffsetOption, startClientVerboseOption, startClientCursesOption, startClientEnhancedUiOption);
            
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
        private static Task<int> StartAllAsync(int portOffset, bool verbose, bool curses, bool enhancedUi = false)
        {
            try
            {
                InitializeCoordinator();
                
                // If both curses and enhancedUi are specified, prioritize curses
                bool useCurses = curses || (!curses && !enhancedUi); // Default to curses if neither is specified
                
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
        private static Task<int> StartClientAsync(int portOffset, bool verbose, bool curses, bool enhancedUi = false)
        {
            try
            {
                InitializeCoordinator();
                
                if (portOffset < 0)
                {
                    Console.WriteLine("Port offset is required to start the client");
                    return Task.FromResult(1);
                }
                
                // If both curses and enhancedUi are specified, prioritize curses
                bool useCurses = curses || (!curses && !enhancedUi); // Default to curses if neither is specified
                
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