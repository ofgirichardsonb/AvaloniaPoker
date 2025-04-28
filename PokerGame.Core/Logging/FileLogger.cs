using System;
using System.IO;
using System.Text;
using System.Threading;

namespace PokerGame.Core.Logging
{
    /// <summary>
    /// A simple file logger that writes log messages to a file
    /// </summary>
    public static class FileLogger
    {
        private static readonly object _lock = new object();
        private static string _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "message_trace.log");
        private static bool _initialized = false;
        
        /// <summary>
        /// Initializes the file logger with the specified log file path
        /// </summary>
        /// <param name="logFilePath">The path to the log file</param>
        public static void Initialize(string logFilePath = null)
        {
            if (!string.IsNullOrEmpty(logFilePath))
            {
                _logFilePath = logFilePath;
            }
            
            // Create an empty log file or truncate an existing one
            File.WriteAllText(_logFilePath, $"=== Message Trace Log Started at {DateTime.Now} ===\n");
            _initialized = true;
            
            Log($"FileLogger initialized. Writing to {_logFilePath}");
        }
        
        /// <summary>
        /// Logs a message to the file
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Log(string message)
        {
            if (!_initialized)
            {
                Initialize();
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