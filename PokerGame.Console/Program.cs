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

            if (useMicroservices)
            {
                // Run in microservice mode and pass the UI flag
                MicroserviceConsoleProgram.StartMicroservices(args, useCursesUi);
            }
            else
            {
                // Run in traditional mode with either standard or enhanced UI
                RunTraditionalMode(useCursesUi);
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
