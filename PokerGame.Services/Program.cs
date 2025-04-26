using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using PokerGame.Core.Microservices;
using PokerGame.Core.Messaging;
using PokerGame.Core.Telemetry;
using PokerGame.Abstractions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

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

                // Initialize telemetry first
                _telemetryService = TelemetryService.Instance;
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
                // Initialize the broker manager
                _brokerManager = BrokerManager.Instance;
                _brokerManager.Start();

                // Initialize telemetry for the broker
                TelemetryDecoratorFactory.RegisterBrokerTelemetry(_brokerManager);

                // Initialize the microservice manager
                _microserviceManager = new MicroserviceManager(portOffset);

                // Determine which services to run
                bool runGameEngine = gameEngine || allServices;
                bool runCardDeck = cardDeck || allServices;

                // Start the requested services
                if (runGameEngine)
                {
                    try
                    {
                        // Calculate ports based on the Core's MicroserviceManager port scheme
                        int publisherPort = 25556 + portOffset;
                        int subscriberPort = 25557 + portOffset;
                        Console.WriteLine($"Starting Game Engine Service (publisher: {publisherPort}, subscriber: {subscriberPort})...");
                        
                        var gameEngineService = new GameEngineService(publisherPort, subscriberPort);
                        var decoratedService = _microserviceManager.RegisterService(gameEngineService);
                        Console.WriteLine($"Game Engine Service started with ID: {decoratedService.ServiceId}");
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
                        // Calculate ports based on the Core's MicroserviceManager port scheme
                        int publisherPort = 25559 + portOffset;
                        int subscriberPort = 25556 + portOffset; // Connect to Game Engine's publisher port
                        Console.WriteLine($"Starting Card Deck Service (publisher: {publisherPort}, subscriber: {subscriberPort})...");
                        
                        var cardDeckService = new CardDeckService(publisherPort, subscriberPort);
                        var decoratedService = _microserviceManager.RegisterService(cardDeckService);
                        Console.WriteLine($"Card Deck Service started with ID: {decoratedService.ServiceId}");
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