using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace PokerGame.Core.Logging
{
    /// <summary>
    /// A simple file logger that writes log messages to a file
    /// </summary>
    public static class FileLogger
    {
        private static readonly object _lock = new object();
        private static string _logFilePath;
        private static bool _initialized = false;
        private static bool _initializationAttempted = false;
        
        // Predefined locations to try for logging
        private static readonly string[] _possibleLogDirectories = new[]
        {
            "/tmp",                                  // Linux/Unix temp directory
            "/home/runner/workspace",                // Replit workspace root
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), // Desktop
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), // AppData
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), // Documents
            Path.GetTempPath(),                      // System temp directory
            AppDomain.CurrentDomain.BaseDirectory,   // Application directory
            "."                                      // Current directory
        };
        
        /// <summary>
        /// Initializes the file logger with the specified log file path
        /// </summary>
        /// <param name="logFilePath">The path to the log file</param>
        public static bool Initialize(string logFilePath = null)
        {
            if (_initialized)
            {
                // Already initialized with a working path
                return true;
            }
            
            if (_initializationAttempted && string.IsNullOrEmpty(logFilePath))
            {
                // We've already tried and failed with the default paths
                return false;
            }
            
            _initializationAttempted = true;
            
            try
            {
                if (!string.IsNullOrEmpty(logFilePath))
                {
                    // Try the specified path first
                    if (TryInitializeLogFile(logFilePath))
                    {
                        return true;
                    }
                    Console.WriteLine($"Warning: Could not write to specified log path: {logFilePath}");
                }
                
                // Try each of our predefined locations
                foreach (var directory in _possibleLogDirectories)
                {
                    if (string.IsNullOrEmpty(directory)) continue;
                    
                    string path = Path.Combine(directory, "poker_message_trace.log");
                    if (TryInitializeLogFile(path))
                    {
                        return true;
                    }
                }
                
                Console.WriteLine("Warning: Could not initialize FileLogger with any path");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing FileLogger: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Attempts to initialize the log file at the specified path
        /// </summary>
        private static bool TryInitializeLogFile(string path)
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Test if we can write to this file
                File.WriteAllText(path, $"=== Message Trace Log Started at {DateTime.Now} ===\n");
                
                // If we got here, the path works
                _logFilePath = path;
                _initialized = true;
                
                // Log this success
                File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] FileLogger initialized. Writing to {path}\n");
                
                // Also echo to console
                Console.WriteLine($"FileLogger initialized successfully at: {path}");
                
                // Add some system information to help with debugging
                LogSystemInfo();
                
                return true;
            }
            catch
            {
                // This path didn't work, try another
                return false;
            }
        }
        
        /// <summary>
        /// Logs basic system information to help with debugging
        /// </summary>
        private static void LogSystemInfo()
        {
            try
            {
                Log("===== System Information =====");
                Log($"OS: {Environment.OSVersion}");
                Log($"Machine Name: {Environment.MachineName}");
                Log($"Current Directory: {Environment.CurrentDirectory}");
                Log($"Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");
                Log($"Process ID: {Process.GetCurrentProcess().Id}");
                Log($"Process Name: {Process.GetCurrentProcess().ProcessName}");
                Log("============================");
            }
            catch (Exception ex)
            {
                Log($"Error logging system info: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets the current log file path
        /// </summary>
        public static string GetLogFilePath()
        {
            return _logFilePath;
        }
        
        /// <summary>
        /// Logs a message to the file
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Log(string message)
        {
            if (!_initialized && !Initialize())
            {
                // If initialization failed, just output to console
                Console.WriteLine($"Console Fallback: {message}");
                return;
            }
            
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(_logFilePath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
                }
            }
            catch (Exception ex)
            {
                // Fallback to console if file logging fails
                Console.WriteLine($"Error writing to log file: {ex.Message}");
                Console.WriteLine($"Console Fallback: {message}");
            }
        }
        
        /// <summary>
        /// Logs a debug message with a specific tag
        /// </summary>
        /// <param name="tag">A tag to identify the source of the message</param>
        /// <param name="message">The message to log</param>
        public static void Debug(string tag, string message)
        {
            Log($"[DEBUG:{tag}] {message}");
        }
        
        /// <summary>
        /// Logs an info message with a specific tag
        /// </summary>
        /// <param name="tag">A tag to identify the source of the message</param>
        /// <param name="message">The message to log</param>
        public static void Info(string tag, string message)
        {
            Log($"[INFO:{tag}] {message}");
        }
        
        /// <summary>
        /// Logs a warning message with a specific tag
        /// </summary>
        /// <param name="tag">A tag to identify the source of the message</param>
        /// <param name="message">The message to log</param>
        public static void Warning(string tag, string message)
        {
            Log($"[WARN:{tag}] {message}");
        }
        
        /// <summary>
        /// Logs an error message with a specific tag
        /// </summary>
        /// <param name="tag">A tag to identify the source of the message</param>
        /// <param name="message">The message to log</param>
        public static void Error(string tag, string message)
        {
            Log($"[ERROR:{tag}] {message}");
        }
        
        /// <summary>
        /// Logs a message trace with a specific tag
        /// </summary>
        /// <param name="tag">A tag to identify the source of the message</param>
        /// <param name="messageInfo">Information about the message</param>
        public static void MessageTrace(string tag, string messageInfo)
        {
            Log($"[MSG:{tag}] {messageInfo}");
        }
    }
}