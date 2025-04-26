using System;
using System.IO;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// A simple logging utility for the poker game
    /// </summary>
    public class Logger
    {
        private readonly string _prefix;
        private readonly bool _verbose;
        private readonly string _logFile;
        private static readonly object _lockObject = new object();
        
        /// <summary>
        /// Creates a new logger instance
        /// </summary>
        /// <param name="prefix">Prefix to add to each log message (typically the service name)</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        /// <param name="logFile">Optional log file path. If provided, messages will be written to this file as well as the console</param>
        public Logger(string prefix, bool verbose = false, string logFile = null)
        {
            _prefix = prefix;
            _verbose = verbose;
            _logFile = logFile;
        }
        
        /// <summary>
        /// Logs a message
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="isVerbose">Whether this is a verbose message</param>
        public void Log(string message, bool isVerbose = false)
        {
            // Skip verbose messages if not in verbose mode
            if (isVerbose && !_verbose)
            {
                return;
            }
            
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string formattedMessage = $"[{timestamp}] [{_prefix}] {message}";
            
            // Output to console
            Console.WriteLine(formattedMessage);
            
            // Output to log file if specified
            if (!string.IsNullOrEmpty(_logFile))
            {
                try
                {
                    lock (_lockObject)
                    {
                        File.AppendAllText(_logFile, formattedMessage + Environment.NewLine);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing to log file: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="exception">Optional exception that caused the error</param>
        public void LogError(string message, Exception exception = null)
        {
            string errorMessage = message;
            if (exception != null)
            {
                errorMessage += $" Exception: {exception.Message}";
                if (_verbose && exception.StackTrace != null)
                {
                    errorMessage += $"\nStack trace: {exception.StackTrace}";
                }
            }
            
            Log($"ERROR: {errorMessage}");
        }
        
        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message">The warning message</param>
        public void LogWarning(string message)
        {
            Log($"WARNING: {message}");
        }
        
        /// <summary>
        /// Logs a debug message (only if verbose mode is enabled)
        /// </summary>
        /// <param name="message">The debug message</param>
        public void LogDebug(string message)
        {
            Log(message, true);
        }
    }
}