using System;
using PokerGame.Core.Game;

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

            if (useMicroservices)
            {
                // Run in microservice mode
                MicroserviceConsoleProgram.StartMicroservices(args);
            }
            else
            {
                // Run in traditional mode
                RunTraditionalMode();
            }
        }
        
        static void RunTraditionalMode()
        {
            // Create the console UI
            ConsoleUI ui = new ConsoleUI();
            
            // Create the game engine with the console UI
            PokerGameEngine gameEngine = new PokerGameEngine(ui);
            
            // Set the UI's reference to the game engine
            ui.SetGameEngine(gameEngine);
            
            // Start the game with UI
            ui.StartGame();
            
            // This will run until the user exits
        }
    }
}
