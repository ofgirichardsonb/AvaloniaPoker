using System;
using System.Threading;
using PokerGame.Core.Microservices;
using NetMQ;

namespace PokerGame.Console
{
    /// <summary>
    /// Entry point for the microservice-based console application
    /// </summary>
    class MicroserviceConsoleProgram
    {
        /// <summary>
        /// Starts the microservice-based application
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <param name="useEnhancedUi">Whether to use the enhanced UI</param>
        /// <param name="serviceType">Type of service to run, if null runs all services</param>
        /// <param name="portOffset">Port offset for services</param>
        /// <param name="useEmergencyDeck">Whether to use emergency deck</param>
        public static void StartMicroservices(string[] args, bool useEnhancedUi = false, string? serviceType = null, 
                                             int portOffset = 0, bool useEmergencyDeck = false)
        {
            System.Console.WriteLine("Starting poker game with microservices architecture...");
            
            // Ensure we explicitly pass the enhanced UI flag
            if (useEnhancedUi)
            {
                System.Console.WriteLine("Enhanced UI explicitly enabled via command line");
                bool hasEnhancedUiFlag = false;
                
                foreach (var arg in args)
                {
                    if (arg.Equals("--curses", StringComparison.OrdinalIgnoreCase) || 
                        arg.Equals("-c", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("--enhanced-ui", StringComparison.OrdinalIgnoreCase))
                    {
                        hasEnhancedUiFlag = true;
                        break;
                    }
                }
                
                if (!hasEnhancedUiFlag)
                {
                    // Add the enhanced UI flag to the args array
                    var newArgs = new string[args.Length + 1];
                    Array.Copy(args, newArgs, args.Length);
                    newArgs[args.Length] = "--enhanced-ui";
                    args = newArgs;
                    
                    System.Console.WriteLine("Added --enhanced-ui flag to arguments");
                }
                
                System.Console.WriteLine("Using enhanced UI...");
            }
            
            // Create the microservice manager
            MicroserviceManager? manager = null;
            var exitEvent = new System.Threading.ManualResetEvent(false);
            
            // Handle Ctrl+C to ensure proper cleanup
            System.Console.CancelKeyPress += (sender, e) => {
                System.Console.WriteLine("Shutting down microservices...");
                e.Cancel = true; // Prevent the process from terminating immediately
                exitEvent.Set(); // Signal the main thread to exit gracefully
            };
            
            try
            {
                manager = new MicroserviceManager(portOffset);
                
                // Check if running a specific service or all services
                if (serviceType != null)
                {
                    // Single service mode - start only the specified service
                    System.Console.WriteLine($"Running in single-service mode: {serviceType}");
                    
                    switch (serviceType.ToLowerInvariant())
                    {
                        case "gameengine":
                            System.Console.WriteLine("Starting Game Engine Service only...");
                            manager.StartGameEngineService(args);
                            break;
                            
                        case "carddeck":
                            System.Console.WriteLine("Starting Card Deck Service only...");
                            if (useEmergencyDeck)
                            {
                                System.Console.WriteLine("Using emergency deck mode");
                                // Add emergency deck flag to args if needed
                                if (!Array.Exists(args, arg => arg.Equals("--emergency-deck", StringComparison.OrdinalIgnoreCase)))
                                {
                                    var newArgs = new string[args.Length + 1];
                                    Array.Copy(args, newArgs, args.Length);
                                    newArgs[args.Length] = "--emergency-deck";
                                    args = newArgs;
                                }
                            }
                            manager.StartCardDeckService(args);
                            break;
                            
                        case "consoleui":
                            System.Console.WriteLine("Starting Console UI Service only...");
                            manager.StartConsoleUIService(args, useEnhancedUi);
                            break;
                            
                        default:
                            System.Console.WriteLine($"Unknown service type: {serviceType}. Valid types are: GameEngine, CardDeck, ConsoleUI");
                            return;
                    }
                }
                else
                {
                    // Start all required microservices
                    System.Console.WriteLine("Starting all microservices...");
                    manager.StartMicroservices(args);
                }
                
                // Keep the main thread alive until user wants to exit
                System.Console.WriteLine("Press Ctrl+C to exit");
                
                // Wait for the exit signal
                exitEvent.WaitOne();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                System.Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                // Ensure all services are stopped
                if (manager != null)
                {
                    System.Console.WriteLine("Stopping all microservices...");
                    manager.StopMicroservices();
                    manager.Dispose();
                    
                    // Force NetMQ cleanup as a final safety measure
                    try
                    {
                        NetMQ.NetMQConfig.Cleanup(false);
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Final cleanup error: {ex.Message}");
                    }
                }
            }
        }
    }
}