using System;
using PokerGame.Core.Microservices;

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
        public static void StartMicroservices(string[] args)
        {
            System.Console.WriteLine("Starting poker game with microservices architecture...");
            
            // Create the microservice manager
            using (var manager = new MicroserviceManager())
            {
                try
                {
                    // Start all required microservices
                    manager.StartMicroservices();
                    
                    // Keep the main thread alive until user wants to exit
                    System.Console.WriteLine("Press Ctrl+C to exit");
                    
                    // This approach allows the console UI service to handle all input
                    // The main thread just stays alive
                    while (true)
                    {
                        System.Threading.Thread.Sleep(1000);
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error: {ex.Message}");
                }
                finally
                {
                    // Ensure all services are stopped
                    manager.StopMicroservices();
                }
            }
        }
    }
}