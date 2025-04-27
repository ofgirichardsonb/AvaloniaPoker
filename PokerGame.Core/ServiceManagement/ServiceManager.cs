using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PokerGame.Core.Messaging;

namespace PokerGame.Core.ServiceManagement
{
    /// <summary>
    /// Manages in-process services for the poker game
    /// This replaces the previous process-based management system
    /// </summary>
    public class ServiceManager
    {
        private static readonly object _lockObject = new object();
        private static ServiceManager? _instance;
        private readonly Dictionary<string, ServiceInfo> _services = new Dictionary<string, ServiceInfo>();
        private readonly Random _random = new Random();
        private bool _initialized = false;
        
        /// <summary>
        /// Singleton instance
        /// </summary>
        public static ServiceManager Instance
        {
            get
            {
                lock (_lockObject)
                {
                    _instance ??= new ServiceManager();
                    return _instance;
                }
            }
        }
        
        /// <summary>
        /// Private constructor to enforce singleton pattern
        /// </summary>
        private ServiceManager()
        {
        }
        
        /// <summary>
        /// Initialize the service manager
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                return;
                
            // Initialize message broker 
            // Note: We're not initializing the broker here because it should be initialized
            // by the services that need it. We'll leave this as a placeholder for future use.
            
            _initialized = true;
        }
        
        /// <summary>
        /// Start all services using the specified port offset
        /// </summary>
        /// <param name="portOffset">Port offset for services, or -1 for random</param>
        /// <param name="useCurses">Whether to use the curses UI</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        /// <returns>The port offset used</returns>
        public int StartAllServices(int portOffset = -1, bool useCurses = false, bool verbose = false)
        {
            // Ensure the manager is initialized
            Initialize();
            
            // Generate a port offset if not provided
            if (portOffset < 0)
            {
                portOffset = _random.Next(900) + 100;
            }
            
            // Start services
            StartServicesHost(portOffset, verbose);
            
            // Wait for services to start
            Thread.Sleep(1000);
            
            // Start the client
            StartConsoleClient(portOffset, useCurses, verbose);
            
            return portOffset;
        }
        
        /// <summary>
        /// Start the services host with the specified port offset
        /// </summary>
        /// <param name="portOffset">Port offset for services, or -1 for random</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        /// <returns>The port offset used</returns>
        public int StartServicesHost(int portOffset = -1, bool verbose = false)
        {
            // Ensure the manager is initialized
            Initialize();
            
            // Generate a port offset if not provided
            if (portOffset < 0)
            {
                portOffset = _random.Next(900) + 100;
            }
            
            // TODO: Initialize and start the core services
            // This is a placeholder for the actual implementation
            
            return portOffset;
        }
        
        /// <summary>
        /// Start the console client
        /// </summary>
        /// <param name="portOffset">Port offset for services</param>
        /// <param name="useCurses">Whether to use the curses UI</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        /// <returns>An identifier for the client or -1 if failed</returns>
        public int StartConsoleClient(int portOffset, bool useCurses, bool verbose)
        {
            // Ensure the manager is initialized
            Initialize();
            
            // TODO: Start the console client
            // This is a placeholder for the actual implementation
            
            return 1; // Return a dummy ID
        }
        
        /// <summary>
        /// Stop all services
        /// </summary>
        public void StopAllServices()
        {
            // Stop all registered services
            foreach (var service in _services.Values)
            {
                StopService(service);
            }
            
            _services.Clear();
        }
        
        /// <summary>
        /// Stop a specific service
        /// </summary>
        private void StopService(ServiceInfo service)
        {
            try
            {
                // Cancel the token to signal stop
                service.CancellationTokenSource?.Cancel();
                
                // Wait for the task to complete
                if (service.Task != null)
                {
                    Task.WaitAll(new[] { service.Task }, 5000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping service {service.Name}: {ex.Message}");
            }
            finally
            {
                // Dispose of the cancellation token source
                service.CancellationTokenSource?.Dispose();
                service.CancellationTokenSource = null;
            }
        }
        
        /// <summary>
        /// Check if the services host is running
        /// </summary>
        /// <returns>True if running, false otherwise</returns>
        public bool IsServicesHostRunning()
        {
            return _services.ContainsKey("ServicesHost");
        }
        
        /// <summary>
        /// Check if the console client is running
        /// </summary>
        /// <returns>True if running, false otherwise</returns>
        public bool IsConsoleClientRunning()
        {
            return _services.ContainsKey("ConsoleClient");
        }
        
        /// <summary>
        /// Get the status of all services
        /// </summary>
        /// <returns>A list of running service names</returns>
        public List<string> GetRunningServices()
        {
            List<string> result = new List<string>();
            
            foreach (var service in _services.Values)
            {
                if (service.Task != null && !service.Task.IsCompleted)
                {
                    result.Add(service.Name);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Information about a running service
        /// </summary>
        private class ServiceInfo
        {
            /// <summary>
            /// The service name
            /// </summary>
            public string Name { get; set; } = "";
            
            /// <summary>
            /// The task running the service
            /// </summary>
            public Task? Task { get; set; }
            
            /// <summary>
            /// Cancellation token source for stopping the service
            /// </summary>
            public CancellationTokenSource? CancellationTokenSource { get; set; }
        }
    }
}