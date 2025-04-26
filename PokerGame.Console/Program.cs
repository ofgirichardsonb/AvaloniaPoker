using System;
using System.Collections.Generic;
using PokerGame.Core.Game;
using PokerGame.Core.Interfaces;
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
                // Check if we should run in microservice mode
                bool useMicroservices = Array.Exists(args, arg => 
                    arg.Equals("--microservices", StringComparison.OrdinalIgnoreCase) || 
                    arg.Equals("-m", StringComparison.OrdinalIgnoreCase));
                    
                // Check if we should use the enhanced NCurses UI
                bool useCursesUi = Array.Exists(args, arg => 
                    arg.Equals("--curses", StringComparison.OrdinalIgnoreCase) || 
                    arg.Equals("-c", StringComparison.OrdinalIgnoreCase));
                
                // Check for enhanced UI flag
                bool useEnhancedUi = useCursesUi || Array.Exists(args, arg => 
                    arg.Equals("--enhanced-ui", StringComparison.OrdinalIgnoreCase));
                
                // Check for emergency deck flag
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
                
                // Extract service type if provided
                string? serviceType = null;
                foreach (string arg in args)
                {
                    if (arg.StartsWith("--service-type="))
                    {
                        serviceType = arg.Substring("--service-type=".Length);
                    }
                }
    
                // Track program configuration
                telemetry.TrackEvent("ProgramConfiguration", new Dictionary<string, string>
                {
                    ["UseMicroservices"] = useMicroservices.ToString(),
                    ["UseCursesUI"] = useCursesUi.ToString(),
                    ["UseEnhancedUI"] = useEnhancedUi.ToString(),
                    ["UseEmergencyDeck"] = useEmergencyDeck.ToString(),
                    ["PortOffset"] = portOffset.ToString(),
                    ["ServiceType"] = serviceType ?? "All",
                    ["Verbose"] = verbose.ToString()
                });
    
                if (useMicroservices)
                {
                    // Run in microservice mode with optional service type for single-service mode
                    // Use the enhanced MicroserviceConsoleProgram from the Services project
                    PokerGame.Services.MicroserviceConsoleProgram.StartMicroservices(
                        args, useEnhancedUi, serviceType, portOffset, useEmergencyDeck);
                }
                else
                {
                    // Run in traditional mode with either standard or enhanced UI
                    RunTraditionalMode(useCursesUi || useEnhancedUi);
                }
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
        
        static void RunTraditionalMode(bool useCursesUi)
        {
            var telemetry = TelemetryService.Instance;
            var startTime = DateTime.UtcNow;
            
            try
            {
                telemetry.TrackEvent("TraditionalModeStarted", new Dictionary<string, string>
                {
                    ["UseCursesUI"] = useCursesUi.ToString()
                });
                
                // Create the appropriate UI
                IPokerGameUI ui;
                
                if (useCursesUi)
                {
                    System.Console.WriteLine("Starting with enhanced UI...");
                    ui = new CursesUI();
                    telemetry.TrackEvent("CursesUICreated");
                }
                else
                {
                    ui = new ConsoleUI();
                    telemetry.TrackEvent("ConsoleUICreated");
                }
                
                // Create the game engine with the selected UI
                PokerGameEngine gameEngine = new PokerGameEngine(ui);
                telemetry.TrackEvent("GameEngineCreated");
                
                // Set the UI's reference to the game engine
                if (ui is ConsoleUI consoleUi)
                {
                    consoleUi.SetGameEngine(gameEngine);
                    consoleUi.StartGame();
                    telemetry.TrackEvent("ConsoleUIGameStarted");
                }
                else if (ui is CursesUI cursesUi)
                {
                    cursesUi.SetGameEngine(gameEngine);
                    cursesUi.StartGame();
                    telemetry.TrackEvent("CursesUIGameStarted");
                }
                
                // This will run until the user exits
                telemetry.TrackEvent("TraditionalModeCompleted", new Dictionary<string, string>
                {
                    ["TotalRuntime"] = (DateTime.UtcNow - startTime).TotalMilliseconds.ToString()
                });
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error in traditional mode: {ex.Message}");
                System.Console.WriteLine(ex.StackTrace);
                
                // Track the exception
                telemetry.TrackException(ex, new Dictionary<string, string>
                {
                    ["Location"] = "Program.RunTraditionalMode",
                    ["UseCursesUI"] = useCursesUi.ToString()
                });
            }
            finally
            {
                // Ensure telemetry is flushed
                telemetry.Flush();
            }
        }
    }
}
