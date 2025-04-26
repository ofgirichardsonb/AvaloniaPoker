using System;
using PokerGame.Core.Game;
using PokerGame.Core.Interfaces;

namespace PokerGame.Console
{
    class Program
    {
        static void Main(string[] args)
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

            if (useMicroservices)
            {
                // Run in microservice mode with optional service type for single-service mode
                MicroserviceConsoleProgram.StartMicroservices(args, useEnhancedUi, serviceType, portOffset, useEmergencyDeck);
            }
            else
            {
                // Run in traditional mode with either standard or enhanced UI
                RunTraditionalMode(useCursesUi || useEnhancedUi);
            }
        }
        
        static void RunTraditionalMode(bool useCursesUi)
        {
            // Create the appropriate UI
            IPokerGameUI ui;
            
            if (useCursesUi)
            {
                System.Console.WriteLine("Starting with enhanced UI...");
                ui = new CursesUI();
            }
            else
            {
                ui = new ConsoleUI();
            }
            
            // Create the game engine with the selected UI
            PokerGameEngine gameEngine = new PokerGameEngine(ui);
            
            // Set the UI's reference to the game engine
            if (ui is ConsoleUI consoleUi)
            {
                consoleUi.SetGameEngine(gameEngine);
                consoleUi.StartGame();
            }
            else if (ui is CursesUI cursesUi)
            {
                cursesUi.SetGameEngine(gameEngine);
                cursesUi.StartGame();
            }
            
            // This will run until the user exits
        }
    }
}
