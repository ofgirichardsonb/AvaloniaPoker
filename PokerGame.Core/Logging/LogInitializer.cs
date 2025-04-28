using System;
using System.IO;
using System.Reflection;

namespace PokerGame.Core.Logging
{
    /// <summary>
    /// Provides initialization services for the logging subsystem
    /// </summary>
    public static class LogInitializer
    {
        private static bool _initialized = false;
        private static readonly object _lock = new object();
        
        /// <summary>
        /// Initializes all logging systems at application startup
        /// </summary>
        /// <param name="serviceName">The name of the service being initialized for context</param>
        public static void InitializeLogging(string serviceName)
        {
            lock (_lock)
            {
                if (_initialized)
                {
                    Console.WriteLine("Logging already initialized");
                    return;
                }
                
                try
                {
                    // Get the directory where the current assembly is located
                    var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                    var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
                    
                    // Log some environment info - very helpful for debugging
                    Console.WriteLine($"=== POKER GAME LOGGING INITIALIZATION ===");
                    Console.WriteLine($"Service: {serviceName}");
                    Console.WriteLine($"Current Directory: {Environment.CurrentDirectory}");
                    Console.WriteLine($"Assembly Directory: {assemblyDirectory}");
                    Console.WriteLine($"Application Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");
                    
                    // Initialize the message trace log
                    string messageTraceLogPath = Path.Combine(Environment.CurrentDirectory, "poker_message_trace.log");
                    bool success = FileLogger.Initialize(messageTraceLogPath);
                    
                    if (success)
                    {
                        Console.WriteLine($"Message trace log initialized at: {messageTraceLogPath}");
                        FileLogger.Info("LogInitializer", $"Logging started for service: {serviceName}");
                    }
                    else
                    {
                        Console.WriteLine("Failed to initialize message trace log with specified path, using fallback locations");
                        
                        // The FileLogger will try multiple fallback locations internally
                        // We just need to make sure it's initialized
                        success = FileLogger.Initialize();
                        
                        if (success)
                        {
                            Console.WriteLine($"Message trace log initialized with fallback location: {FileLogger.GetLogFilePath()}");
                            FileLogger.Info("LogInitializer", $"Logging started for service: {serviceName} (using fallback location)");
                        }
                        else
                        {
                            Console.WriteLine("CRITICAL ERROR: Could not initialize logging system with any path");
                        }
                    }
                    
                    _initialized = true;
                    Console.WriteLine("=== LOGGING INITIALIZATION COMPLETE ===");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initializing logging: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }
    }
}