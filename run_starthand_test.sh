#!/bin/bash
set -e

echo "Compiling StartHand test..."
dotnet new console -n TestStartHandResponse --force
cp test_starthand.cs TestStartHandResponse/Program.cs

# Update references
cd TestStartHandResponse
dotnet add reference ../PokerGame.Core/PokerGame.Core.csproj
dotnet add reference ../MSA.Foundation/MSA.Foundation.csproj

# Create a file logger
cat > TestFileLogger.cs << EOF
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
EOF

# Update the program to use the file logger
sed -i 's/Console.WriteLine(/FileLogger.Log(/g' Program.cs
sed -i '1iusing PokerGame.Test;' Program.cs
sed -i '/static async Task Main/i\        static Program()\n        {\n            FileLogger.Initialize();\n        }' Program.cs

# Build and run
echo "Building test..."
dotnet build
echo "Running test..."
dotnet run

# Show the log file
echo "Test completed. Log file contents:"
cat ../starthand_test.log