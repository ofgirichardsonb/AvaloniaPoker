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
        /// <param name="useEnhancedUi">Whether to use the enhanced UI (invoked with --curses flag)</param>
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
                    // Add the curses flag to the args array (as this is now the only way to activate enhanced UI)
                    var newArgs = new string[args.Length + 1];
                    Array.Copy(args, newArgs, args.Length);
                    newArgs[args.Length] = "--curses";
                    args = newArgs;
                    
                    System.Console.WriteLine("Added --curses flag to arguments");
                }
                
                System.Console.WriteLine("Using enhanced UI...");
            }
            
            // Create a cleanup event
            var exitEvent = new System.Threading.ManualResetEvent(false);
            
            // Handle Ctrl+C to ensure proper cleanup
            System.Console.CancelKeyPress += (sender, e) => {
                System.Console.WriteLine("Shutting down console UI...");
                e.Cancel = true; // Prevent the process from terminating immediately
                exitEvent.Set(); // Signal the main thread to exit gracefully
            };
            
            try
            {
                // Initialize the broker manager for client-side communications only
                // We don't create or manage services anymore - we assume they're already running
                var brokerManager = PokerGame.Core.Messaging.BrokerManager.Instance;
                brokerManager.Start();
                System.Console.WriteLine("Connected to broker for client communications");
                
                // Create a microservice manager just for the UI service
                MicroserviceManager? manager = new MicroserviceManager(portOffset);
                
                // Check if running in console UI mode
                if (serviceType != null && serviceType.ToLowerInvariant() == "consoleui")
                {
                    System.Console.WriteLine("Starting Console UI client only...");
                    manager.StartConsoleUIService(args, useEnhancedUi);
                }
                else
                {
                    System.Console.WriteLine("Starting console client in client-only mode...");
                    System.Console.WriteLine("Assuming services are already running with port offset: " + portOffset);
                    manager.StartConsoleUIService(args, useEnhancedUi);
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
                // No need to force NetMQ cleanup here as it causes hanging
                // The NetMQContextHelper will handle cleanup properly
                
                // We don't need to stop services here anymore, since they are
                // running in separate processes managed by PokerGame.Services
                System.Console.WriteLine("Console UI client stopped. Services are still running.");
            }
        }
    }
}