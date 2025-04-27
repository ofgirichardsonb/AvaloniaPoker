using System;
using System.Collections.Generic;
using PokerGame.Core.Game;
using PokerGame.Core.Interfaces;
using PokerGame.Core.Messaging;
using PokerGame.Services;

namespace PokerGame.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            // Initialize the telemetry service
            var telemetry = TelemetryService.Instance;
            telemetry.TrackEvent("ProgramStarted", new Dictionary<string, string>
            {
                ["CommandLine"] = string.Join(" ", args)
            });
            
            try
            {
                // Check if we should use the enhanced UI (via curses flag)
                // Note: Enhanced UI and Curses UI are the same thing, just different terminology
                bool useEnhancedUi = Array.Exists(args, arg => 
                    arg.Equals("--curses", StringComparison.OrdinalIgnoreCase) || 
                    arg.Equals("-c", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--enhanced-ui", StringComparison.OrdinalIgnoreCase)); // For backward compatibility
                
                // Check for emergency deck flag - kept for compatibility but ignored
                bool useEmergencyDeck = Array.Exists(args, arg => 
                    arg.Equals("--emergency-deck", StringComparison.OrdinalIgnoreCase));
                
                // Check for verbose logging
                bool verbose = Array.Exists(args, arg => 
                    arg.Equals("--verbose", StringComparison.OrdinalIgnoreCase) || 
                    arg.Equals("-v", StringComparison.OrdinalIgnoreCase));
                
                // Extract port offset if provided
                int portOffset = 0;
                foreach (string arg in args)
                {
                    if (arg.StartsWith("--port-offset="))
                    {
                        string offsetStr = arg.Substring("--port-offset=".Length);
                        if (int.TryParse(offsetStr, out int offset))
                        {
                            portOffset = offset;
                        }
                    }
                }
                
                // Extract service type if provided - always assume ConsoleUI
                string serviceType = "ConsoleUI";
                
                // Track program configuration
                telemetry.TrackEvent("ProgramConfiguration", new Dictionary<string, string>
                {
                    ["UseEnhancedUI"] = useEnhancedUi.ToString(),
                    ["PortOffset"] = portOffset.ToString(),
                    ["ServiceType"] = serviceType,
                    ["Verbose"] = verbose.ToString()
                });
    
                // Always run in microservice client mode, connecting to existing services
                System.Console.WriteLine("Starting in client-only mode, connecting to running services...");
                
                // Connect to the central message broker
                var brokerManager = BrokerManager.Instance;
                brokerManager.Start();
                
                // Run the console UI in client-only mode
                MicroserviceConsoleProgram.StartMicroservices(args, useEnhancedUi, serviceType, portOffset);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Critical error in program entry point: {ex.Message}");
                System.Console.WriteLine(ex.StackTrace);
                
                // Track the exception
                telemetry.TrackException(ex, new Dictionary<string, string>
                {
                    ["Location"] = "Program.Main"
                });
                
                // Ensure telemetry is flushed
                telemetry.Flush();
            }
        }
    }
}
