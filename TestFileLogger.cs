using System;
using PokerGame.Core.Logging;

namespace TestFileLogger
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing FileLogger...");
            
            // Initialize with an explicit path to make it easier to find
            string logPath = "/home/runner/workspace/test_message_trace.log";
            FileLogger.Initialize(logPath);
            
            Console.WriteLine($"Log file initialized at: {logPath}");
            
            // Write some test log entries
            FileLogger.Info("TestProgram", "This is an info message");
            FileLogger.Debug("TestProgram", "This is a debug message");
            FileLogger.Warning("TestProgram", "This is a warning message");
            FileLogger.Error("TestProgram", "This is an error message");
            FileLogger.MessageTrace("TestProgram", "This is a message trace entry");
            
            Console.WriteLine("Log entries written. Check the log file.");
        }
    }
}