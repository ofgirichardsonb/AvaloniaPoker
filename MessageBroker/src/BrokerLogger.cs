using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace MessageBroker
{
    /// <summary>
    /// Log level for the broker logger
    /// </summary>
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }
    
    /// <summary>
    /// A thread-safe logger for the message broker
    /// </summary>
    public class BrokerLogger
    {
        private static readonly Lazy<BrokerLogger> _instance = new Lazy<BrokerLogger>(() => new BrokerLogger());
        
        /// <summary>
        /// Gets the singleton instance of the logger
        /// </summary>
        public static BrokerLogger Instance => _instance.Value;
        
        private readonly ConcurrentQueue<LogEntry> _logQueue = new ConcurrentQueue<LogEntry>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly Thread? _logProcessingThread;
        private readonly StreamWriter? _logFileWriter;
        private readonly bool _logToConsole;
        private readonly LogLevel _minimumLogLevel;
        
        /// <summary>
        /// Initializes a new instance of the logger
        /// </summary>
        private BrokerLogger()
        {
            try
            {
                var logFilePath = "broker.log";
                _logFileWriter = new StreamWriter(logFilePath, true, Encoding.UTF8);
                _logFileWriter.AutoFlush = true;
                _logToConsole = true;
                _minimumLogLevel = LogLevel.Debug;
                
                // Start the log processing thread
                _logProcessingThread = new Thread(ProcessLogQueue);
                _logProcessingThread.IsBackground = true;
                _logProcessingThread.Start();
                
                // Log startup message
                Info("BrokerLogger", "Logger initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize logger: {ex.Message}");
                _logToConsole = true;
            }
        }
        
        /// <summary>
        /// Logs a trace message
        /// </summary>
        /// <param name="category">The category of the message</param>
        /// <param name="message">The message to log</param>
        public void Trace(string category, string message) => Log(LogLevel.Trace, category, message);
        
        /// <summary>
        /// Logs a debug message
        /// </summary>
        /// <param name="category">The category of the message</param>
        /// <param name="message">The message to log</param>
        public void Debug(string category, string message) => Log(LogLevel.Debug, category, message);
        
        /// <summary>
        /// Logs an informational message
        /// </summary>
        /// <param name="category">The category of the message</param>
        /// <param name="message">The message to log</param>
        public void Info(string category, string message) => Log(LogLevel.Info, category, message);
        
        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="category">The category of the message</param>
        /// <param name="message">The message to log</param>
        public void Warning(string category, string message) => Log(LogLevel.Warning, category, message);
        
        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="category">The category of the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="exception">The exception that caused the error</param>
        public void Error(string category, string message, Exception? exception = null)
        {
            var fullMessage = exception != null ? $"{message}\nException: {exception.Message}\nStackTrace: {exception.StackTrace}" : message;
            Log(LogLevel.Error, category, fullMessage);
        }
        
        /// <summary>
        /// Logs a critical message
        /// </summary>
        /// <param name="category">The category of the message</param>
        /// <param name="message">The message to log</param>
        /// <param name="exception">The exception that caused the critical error</param>
        public void Critical(string category, string message, Exception? exception = null)
        {
            var fullMessage = exception != null ? $"{message}\nException: {exception.Message}\nStackTrace: {exception.StackTrace}" : message;
            Log(LogLevel.Critical, category, fullMessage);
        }
        
        /// <summary>
        /// Logs a message with the specified level
        /// </summary>
        /// <param name="level">The level of the message</param>
        /// <param name="category">The category of the message</param>
        /// <param name="message">The message to log</param>
        private void Log(LogLevel level, string category, string message)
        {
            if (level < _minimumLogLevel)
                return;
                
            var entry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Category = category,
                Message = message
            };
            
            _logQueue.Enqueue(entry);
        }
        
        /// <summary>
        /// Processes the log queue in a separate thread
        /// </summary>
        private void ProcessLogQueue()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                while (_logQueue.TryDequeue(out var entry))
                {
                    WriteLogEntry(entry);
                }
                
                Thread.Sleep(50);
            }
            
            // Process any remaining logs before shutting down
            while (_logQueue.TryDequeue(out var entry))
            {
                WriteLogEntry(entry);
            }
            
            _logFileWriter?.Flush();
            _logFileWriter?.Dispose();
        }
        
        /// <summary>
        /// Writes a log entry to the configured outputs
        /// </summary>
        /// <param name="entry">The log entry to write</param>
        private void WriteLogEntry(LogEntry entry)
        {
            var timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logLevelStr = entry.Level.ToString().PadRight(8);
            var formattedMessage = $"[{timestamp}] [{logLevelStr}] [{entry.Category}] {entry.Message}";
            
            if (_logToConsole)
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = GetColorForLogLevel(entry.Level);
                Console.WriteLine(formattedMessage);
                Console.ForegroundColor = originalColor;
            }
            
            _logFileWriter?.WriteLine(formattedMessage);
        }
        
        /// <summary>
        /// Gets the console color for the specified log level
        /// </summary>
        /// <param name="level">The log level</param>
        /// <returns>The console color for the log level</returns>
        private ConsoleColor GetColorForLogLevel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => ConsoleColor.Gray,
                LogLevel.Debug => ConsoleColor.DarkGray,
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.DarkRed,
                _ => ConsoleColor.White
            };
        }
        
        /// <summary>
        /// Shuts down the logger and releases resources
        /// </summary>
        public void Shutdown()
        {
            _cancellationTokenSource.Cancel();
            _logProcessingThread?.Join(1000);
        }
        
        /// <summary>
        /// A log entry in the queue
        /// </summary>
        private class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public LogLevel Level { get; set; }
            public string Category { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }
    }
}