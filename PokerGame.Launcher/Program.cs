namespace PokerGame.Launcher
{
    /// <summary>
    /// Main entry point for the PokerGame.Launcher application
    /// Provides a command-line interface for managing poker game services
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// The main entry point for the application
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Exit code: 0 for success, non-zero for errors</returns>
        static async Task<int> Main(string[] args)
        {
            try
            {
                // Display banner
                Console.WriteLine("=======================================");
                Console.WriteLine("  PokerGame Service Launcher Utility   ");
                Console.WriteLine("=======================================");
                Console.WriteLine();
                
                // Run the service launcher with the command line arguments
                return await ServiceLauncher.RunAsync(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }
    }
}