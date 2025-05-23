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
        /// <param name="useCursesUi">Whether to use the enhanced curses UI</param>
        public static void StartMicroservices(string[] args, bool useCursesUi = false)
        {
            System.Console.WriteLine("Starting poker game with microservices architecture...");
            if (useCursesUi)
            {
                System.Console.WriteLine("Using enhanced curses UI...");
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
                manager = new MicroserviceManager();
                
                // Start all required microservices with UI preference
                manager.StartMicroservices(args);
                
                // Keep the main thread alive until user wants to exit
                System.Console.WriteLine("Press Ctrl+C to exit");
                
                // Wait for the exit signal
                exitEvent.WaitOne();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error: {ex.Message}");
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