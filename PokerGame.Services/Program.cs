using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PokerGame.Core.Microservices;
using PokerGame.Core.Messaging;
using PokerGame.Core.ServiceManagement;
using PokerGame.Core.Telemetry;
using PokerGame.Abstractions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;

namespace PokerGame.Services
{
    /// <summary>
    /// Entry point for PokerGame.Services executable
    /// Allows running any combination of microservices independently of UI clients
    /// </summary>
    public class Program
    {
        private static ITelemetryService? _telemetryService;
        private static MicroserviceManager? _microserviceManager;
        private static readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static BrokerManager? _brokerManager;

        public static async Task<int> Main(string[] args)
        {
            try
            {
                Console.WriteLine("PokerGame.Services Microservice Host");
                Console.WriteLine("===================================");

                // Build configuration from appsettings.json
                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.development.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables() // Also read from environment variables
                    .Build();

                // Initialize telemetry with configuration
                Console.WriteLine("Starting telemetry initialization...");
                
                // Always try environment variable first as it's the most reliable approach
                string? instrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
                Console.WriteLine($"Environment variable APPINSIGHTS_INSTRUMENTATIONKEY exists: {(instrumentationKey != null ? "Yes" : "No")}");
                if (instrumentationKey != null)
                {
                    Console.WriteLine($"Environment variable key length: {instrumentationKey.Length}");
                }
                
                // Check for configuration values
                var configKey = configuration["ApplicationInsights:InstrumentationKey"];
                Console.WriteLine($"Configuration key exists: {(!string.IsNullOrEmpty(configKey) ? "Yes" : "No")}");
                if (!string.IsNullOrEmpty(configKey))
                {
                    Console.WriteLine($"Configuration key length: {configKey.Length}");
                }
                
                // Only fall back to configuration if environment variable isn't available
                if (string.IsNullOrEmpty(instrumentationKey))
                {
                    instrumentationKey = configKey;
                    Console.WriteLine("Using instrumentation key from configuration (appsettings.json)");
                }
                else
                {
                    Console.WriteLine("Using instrumentation key from environment variable");
                }
                
                // Print working directory for reference
                Console.WriteLine($"Current directory: {Directory.GetCurrentDirectory()}");
                
                // Get the TelemetryService instance and initialize it if possible
                var coreTelemetryService = PokerGame.Core.Telemetry.TelemetryService.Instance;
                
                if (!string.IsNullOrEmpty(instrumentationKey))
                {
                    Console.WriteLine("Initializing telemetry with Application Insights key...");
                    var initialized = coreTelemetryService.Initialize(instrumentationKey);
                    Console.WriteLine($"Telemetry initialization {(initialized ? "successful" : "failed")}");
                    
                    if (initialized)
                    {
                        _telemetryService = new TelemetryServiceAdapter(coreTelemetryService);
                    }
                    else
                    {
                        _telemetryService = new NullTelemetryService();
                        Console.WriteLine("Falling back to NullTelemetryService due to initialization failure");
                    }
                }
                else
                {
                    Console.WriteLine("No Application Insights key found, telemetry will be disabled");
                    _telemetryService = new NullTelemetryService();
                }
                
                _telemetryService.TrackEvent("ServiceHostStarted");

                // Create the root command and options
                var rootCommand = new RootCommand("PokerGame Services Host");

                // Port offset option
                var portOffsetOption = new Option<int>(
                    "--port-offset",
                    description: "Port offset for all services",
                    getDefaultValue: () => 0);
                rootCommand.AddOption(portOffsetOption);

                // Verbose option
                var verboseOption = new Option<bool>(
                    "--verbose",
                    description: "Enable verbose logging",
                    getDefaultValue: () => false);
                rootCommand.AddOption(verboseOption);

                // Service selection options
                var gameEngineOption = new Option<bool>(
                    "--game-engine",
                    description: "Run the game engine service",
                    getDefaultValue: () => false);
                rootCommand.AddOption(gameEngineOption);

                var cardDeckOption = new Option<bool>(
                    "--card-deck",
                    description: "Run the card deck service",
                    getDefaultValue: () => false);
                rootCommand.AddOption(cardDeckOption);

                // Option to run all services
                var allServicesOption = new Option<bool>(
                    "--all-services",
                    description: "Run all services",
                    getDefaultValue: () => false);
                rootCommand.AddOption(allServicesOption);

                // Handle the command
                rootCommand.SetHandler(async (portOffset, verbose, gameEngine, cardDeck, allServices) =>
                {
                    await RunServices(portOffset, verbose, gameEngine, cardDeck, allServices);
                }, portOffsetOption, verboseOption, gameEngineOption, cardDeckOption, allServicesOption);

                // Parse the command line
                return await rootCommand.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical error in program entry point: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                
                if (_telemetryService != null)
                {
                    _telemetryService.TrackException(ex, new Dictionary<string, string>
                    {
                        ["Location"] = "Program.Main"
                    });
                    _telemetryService.Flush();
                }
                
                return 1;
            }
        }

        private static async Task RunServices(int portOffset, bool verbose, bool gameEngine, bool cardDeck, bool allServices)
        {
            Console.WriteLine($"Running with port offset: {portOffset}");
            if (verbose)
            {
                Console.WriteLine("Verbose logging enabled");
            }

            // Track configuration
            _telemetryService?.TrackEvent("ServiceConfiguration", new Dictionary<string, string>
            {
                ["PortOffset"] = portOffset.ToString(),
                ["Verbose"] = verbose.ToString(),
                ["GameEngine"] = gameEngine.ToString(),
                ["CardDeck"] = cardDeck.ToString(),
                ["AllServices"] = allServices.ToString()
            });

            try
            {
                // Create a main execution context that will be used by all services
                var mainExecutionContext = new PokerGame.Core.Messaging.ExecutionContext();
                Console.WriteLine($"Created main execution context with thread ID: {mainExecutionContext.ThreadId}");
                
                // Initialize the broker manager with our execution context
                _brokerManager = BrokerManager.Instance;
                _brokerManager.Start(mainExecutionContext);
                
                // Initialize and start the CentralMessageBroker
                Console.WriteLine("Starting CentralMessageBroker...");
                var centralBrokerPort = ServiceConstants.Ports.GetCentralBrokerPort(portOffset);
                _brokerManager.StartCentralBroker(centralBrokerPort, mainExecutionContext, verbose);
                Console.WriteLine($"CentralMessageBroker started on port {centralBrokerPort}");

                // Initialize telemetry for the broker
                TelemetryDecoratorFactory.RegisterBrokerTelemetry(_brokerManager);

                // Initialize the microservice manager with our context
                _microserviceManager = new MicroserviceManager(portOffset, mainExecutionContext);

                // Determine which services to run
                bool runGameEngine = gameEngine || allServices;
                bool runCardDeck = cardDeck || allServices;

                // Start the requested services
                if (runGameEngine)
                {
                    try
                    {
                        // Get port numbers from constants for consistency
                        int publisherPort = ServiceConstants.Ports.GetGameEnginePublisherPort(portOffset);
                        int subscriberPort = ServiceConstants.Ports.GetGameEngineSubscriberPort(portOffset);
                        Console.WriteLine($"Starting Game Engine Service (publisher: {publisherPort}, subscriber: {subscriberPort})...");
                        
                        // Create the service with an execution context
                        var gameEngineService = _microserviceManager.CreateServiceWithExecutionContext<GameEngineService>(
                            "Poker Game Engine", 
                            ServiceConstants.ServiceTypes.GameEngine, 
                            publisherPort, 
                            subscriberPort, 
                            verbose);
                            
                        Console.WriteLine($"Game Engine Service started with ID: {gameEngineService.ServiceId}");
                        Console.WriteLine($"Game Engine Service type: {gameEngineService.GetType().FullName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error starting Game Engine Service: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    }
                }

                if (runCardDeck)
                {
                    try
                    {
                        // Get port numbers from constants for consistency
                        int publisherPort = ServiceConstants.Ports.GetCardDeckPublisherPort(portOffset);
                        int subscriberPort = ServiceConstants.Ports.GetCardDeckSubscriberPort(portOffset);
                        Console.WriteLine($"Starting Card Deck Service (publisher: {publisherPort}, subscriber: {subscriberPort})...");
                        
                        // Create the service with an execution context
                        var cardDeckService = _microserviceManager.CreateServiceWithExecutionContext<CardDeckService>(
                            "Card Deck Service", 
                            ServiceConstants.ServiceTypes.CardDeck, 
                            publisherPort, 
                            subscriberPort, 
                            verbose);  // Don't pass useEmergencyDeckMode parameter as it's optional with default value of false
                            
                        Console.WriteLine($"Card Deck Service started with ID: {cardDeckService.ServiceId}");
                        Console.WriteLine($"Card Deck Service type: {cardDeckService.GetType().FullName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error starting Card Deck Service: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    }
                }

                if (!runGameEngine && !runCardDeck)
                {
                    Console.WriteLine("No services specified to run. Use --game-engine, --card-deck, or --all-services.");
                    return;
                }

                // Start all registered services
                await _microserviceManager.StartAllServicesAsync();
                Console.WriteLine("All specified services started successfully.");

                // Wait for Ctrl+C
                Console.WriteLine("Press Ctrl+C to stop the services...");
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    _cancellationTokenSource.Cancel();
                };

                // Keep the application running until cancellation is requested
                await Task.Delay(Timeout.Infinite, _cancellationTokenSource.Token)
                    .ContinueWith(t => { }, TaskContinuationOptions.OnlyOnCanceled);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running services: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                
                _telemetryService?.TrackException(ex, new Dictionary<string, string>
                {
                    ["Location"] = "Program.RunServices"
                });
            }
            finally
            {
                // Clean up resources
                _microserviceManager?.Dispose();
                _brokerManager?.Stop();
                _telemetryService?.Flush();
                
                Console.WriteLine("Services stopped.");
            }
        }
    }
}