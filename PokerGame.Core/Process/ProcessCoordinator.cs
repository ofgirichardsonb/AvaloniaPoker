using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PokerGame.Core.Telemetry;

namespace PokerGame.Core.Process
{
    /// <summary>
    /// Coordinates the launching and management of all poker game services
    /// Provides a simplified API for starting and stopping the required services
    /// </summary>
    public class ProcessCoordinator : IDisposable
    {
        private static ProcessCoordinator? _instance;
        private static readonly object _lockObject = new object();
        private readonly ProcessManager _processManager;
        private bool _isDisposed = false;
        private int _servicesHostPid = -1;
        private int _consoleClientPid = -1;
        private readonly Random _random = new Random();

        /// <summary>
        /// Gets the singleton instance of the ProcessCoordinator
        /// </summary>
        public static ProcessCoordinator Instance
        {
            get
            {
                lock (_lockObject)
                {
                    if (_instance == null || _instance._isDisposed)
                    {
                        _instance = new ProcessCoordinator();
                    }
                    return _instance;
                }
            }
        }

        /// <summary>
        /// Private constructor to enforce singleton pattern
        /// </summary>
        private ProcessCoordinator()
        {
            _processManager = ProcessManager.Instance;
            
            // Register for process exit to ensure cleanup happens
            AppDomain.CurrentDomain.ProcessExit += (s, e) => 
            {
                Console.WriteLine("Process exit detected - cleaning up process coordinator");
                Dispose();
            };
        }

        /// <summary>
        /// Start all required services with a random port offset
        /// </summary>
        /// <param name="useCurses">Whether to use the curses UI</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        /// <returns>The port offset used for the services</returns>
        public int StartAllServices(bool useCurses = false, bool verbose = false)
        {
            try
            {
                // Track this operation
                var telemetry = TelemetryService.Instance;
                telemetry.TrackEvent("StartAllServicesRequested", new Dictionary<string, string>
                {
                    ["UseCurses"] = useCurses.ToString(),
                    ["Verbose"] = verbose.ToString()
                });
                
                // Stop any existing services
                StopAllServices();
                
                // Generate a random port offset to avoid conflicts (between 100-999)
                int portOffset = _random.Next(900) + 100;
                Console.WriteLine($"Starting all services with port offset {portOffset}...");
                
                // Start the services in sequence
                // 1. First start the services host
                _servicesHostPid = _processManager.StartServicesHost(portOffset, verbose);
                if (_servicesHostPid == -1)
                {
                    Console.WriteLine("Failed to start services host");
                    return -1;
                }
                
                // Give the services a moment to start up
                Thread.Sleep(3000);
                
                // 2. Then start the console client with the same port offset
                _consoleClientPid = _processManager.StartConsoleClient(portOffset, useCurses, verbose);
                if (_consoleClientPid == -1)
                {
                    Console.WriteLine("Failed to start console client");
                    // Clean up the services host since client failed
                    _processManager.StopProcess(_servicesHostPid);
                    _servicesHostPid = -1;
                    return -1;
                }
                
                Console.WriteLine("All services started successfully");
                
                telemetry.TrackEvent("AllServicesStarted", new Dictionary<string, string>
                {
                    ["PortOffset"] = portOffset.ToString(),
                    ["ServicesHostPid"] = _servicesHostPid.ToString(),
                    ["ConsoleClientPid"] = _consoleClientPid.ToString()
                });
                
                return portOffset;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting services: {ex.Message}");
                
                // Report telemetry
                var telemetry = TelemetryService.Instance;
                telemetry.TrackException(ex, new Dictionary<string, string>
                {
                    ["Location"] = "ProcessCoordinator.StartAllServices",
                    ["UseCurses"] = useCurses.ToString(),
                    ["Verbose"] = verbose.ToString()
                });
                
                // Clean up any services that might have started
                StopAllServices();
                
                return -1;
            }
        }
        
        /// <summary>
        /// Start only the services host (no client UI)
        /// </summary>
        /// <param name="portOffset">Port offset for the services, or -1 for random</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        /// <returns>The port offset used for the services</returns>
        public int StartServicesHost(int portOffset = -1, bool verbose = false)
        {
            try
            {
                // Generate a random port offset if not specified
                if (portOffset < 0)
                {
                    portOffset = _random.Next(900) + 100;
                }
                
                Console.WriteLine($"Starting services host with port offset {portOffset}...");
                
                // Stop any existing services host
                if (_servicesHostPid >= 0)
                {
                    _processManager.StopProcess(_servicesHostPid);
                    _servicesHostPid = -1;
                }
                
                // Start the services host
                _servicesHostPid = _processManager.StartServicesHost(portOffset, verbose);
                if (_servicesHostPid == -1)
                {
                    Console.WriteLine("Failed to start services host");
                    return -1;
                }
                
                Console.WriteLine($"Services host started with PID {_servicesHostPid} and port offset {portOffset}");
                return portOffset;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting services host: {ex.Message}");
                return -1;
            }
        }
        
        /// <summary>
        /// Start only the console client UI (connects to existing services)
        /// </summary>
        /// <param name="portOffset">Port offset matching the services</param>
        /// <param name="useCurses">Whether to use the curses UI</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        /// <returns>The client process ID, or -1 if failed</returns>
        public int StartConsoleClient(int portOffset, bool useCurses = false, bool verbose = false)
        {
            try
            {
                Console.WriteLine($"Starting console client with port offset {portOffset} (curses: {useCurses})...");
                
                // Stop any existing console client
                if (_consoleClientPid >= 0)
                {
                    _processManager.StopProcess(_consoleClientPid);
                    _consoleClientPid = -1;
                }
                
                // Start the console client with updated parameter order (portOffset, useCurses, verbose)
                _consoleClientPid = _processManager.StartConsoleClient(portOffset, useCurses, verbose);
                if (_consoleClientPid == -1)
                {
                    Console.WriteLine("Failed to start console client");
                    return -1;
                }
                
                Console.WriteLine($"Console client started with PID {_consoleClientPid}");
                return _consoleClientPid;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting console client: {ex.Message}");
                return -1;
            }
        }
        
        /// <summary>
        /// Stops all services (both services host and console client)
        /// </summary>
        public void StopAllServices()
        {
            try
            {
                Console.WriteLine("Stopping all services...");
                
                // Stop console client if running
                if (_consoleClientPid >= 0)
                {
                    _processManager.StopProcess(_consoleClientPid);
                    _consoleClientPid = -1;
                }
                
                // Stop services host if running
                if (_servicesHostPid >= 0)
                {
                    _processManager.StopProcess(_servicesHostPid);
                    _servicesHostPid = -1;
                }
                
                Console.WriteLine("All services stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping services: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Checks if the services host is running
        /// </summary>
        public bool IsServicesHostRunning()
        {
            return _servicesHostPid >= 0 && _processManager.IsServiceRunning("ServicesHost");
        }
        
        /// <summary>
        /// Checks if the console client is running
        /// </summary>
        public bool IsConsoleClientRunning()
        {
            return _consoleClientPid >= 0 && 
                   (_processManager.IsServiceRunning("ConsoleUI") || 
                    _processManager.IsServiceRunning("CursesUI"));
        }
        
        /// <summary>
        /// Disposes the managed resources
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;
            
            // Stop all services
            StopAllServices();
            
            _isDisposed = true;
        }
    }
}