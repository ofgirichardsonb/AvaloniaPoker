using PokerGame.Core.Game;

namespace PokerGame.Console
{
    class Program
    {
        static void Main(string[] args)
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
