using System;
using System.IO;
using System.Threading.Tasks;

namespace PokerGame.Test
{
    public class FileLogger
    {
        private static string _logPath = Path.Combine(Directory.GetCurrentDirectory(), "starthand_test.log");
        private static object _lockObj = new object();

        public static void Initialize()
        {
            // Clear previous log
            File.WriteAllText(_logPath, "=== StartHand Test Log ===\n");
        }

        public static void Log(string message)
        {
            lock (_lockObj)
            {
                File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
                Console.WriteLine(message);
            }
        }
    }
}
