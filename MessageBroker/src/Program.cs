using System;
using System.Threading;
using System.Threading.Tasks;

namespace MessageBroker
{
    /// <summary>
    /// The main program class for the message broker
    /// </summary>
    public class Program
    {
        private static readonly ManualResetEvent _exitEvent = new ManualResetEvent(false);
        private static CentralMessageBroker? _broker;
        
        /// <summary>
        /// The entry point for the application
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Starting Message Broker...");
                
                // Parse command line arguments for ports
                var frontendPort = GetPortFromArgs(args, "--frontend-port", 5570);
                var backendPort = GetPortFromArgs(args, "--backend-port", 5571);
                var monitorPort = GetPortFromArgs(args, "--monitor-port", 5572);
                
                Console.WriteLine($"Frontend Port: {frontendPort}");
                Console.WriteLine($"Backend Port: {backendPort}");
                Console.WriteLine($"Monitor Port: {monitorPort}");
                
                // Create and start the broker
                _broker = new CentralMessageBroker(frontendPort, backendPort, monitorPort);
                
                // Register Ctrl+C handler
                Console.CancelKeyPress += OnCancelKeyPress;
                
                Console.WriteLine("Message Broker started successfully!");
                Console.WriteLine("Press Ctrl+C to exit");
                
                // Wait for exit signal
                _exitEvent.WaitOne();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting broker: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                // Clean up resources
                _broker?.Dispose();
                
                Console.WriteLine("Message Broker shut down");
            }
        }
        
        /// <summary>
        /// Handles the Ctrl+C key press event
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="e">The event arguments</param>
        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Shutting down...");
            
            // Prevent the process from terminating immediately
            e.Cancel = true;
            
            // Signal the exit event
            _exitEvent.Set();
        }
        
        /// <summary>
        /// Gets a port from the command line arguments
        /// </summary>
        /// <param name="args">The command line arguments</param>
        /// <param name="argName">The argument name to look for</param>
        /// <param name="defaultValue">The default value to use if the argument is not found</param>
        /// <returns>The port value</returns>
        private static int GetPortFromArgs(string[] args, string argName, int defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(argName, StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(args[i + 1], out int port) && port > 0 && port < 65536)
                    {
                        return port;
                    }
                }
            }
            
            return defaultValue;
        }
    }
}