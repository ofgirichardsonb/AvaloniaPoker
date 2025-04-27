using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using PokerGame.Core.Telemetry;

namespace PokerGame.Launcher
{
    /// <summary>
    /// Main entry point for the PokerGame.Launcher application
    /// Provides a command-line interface for managing poker game services
    /// </summary>
    internal class Program
    {
        // Static TelemetryService instance
        private static TelemetryService? _telemetryService;
        
        /// <summary>
        /// The main entry point for the application
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Exit code: 0 for success, non-zero for errors</returns>
        static async Task<int> Main(string[] args)
        {
            try
            {
                // Initialize configuration
                var configuration = InitializeConfiguration();
                
                // Initialize telemetry
                InitializeTelemetry(configuration);
                
                // Display banner
                Console.WriteLine("=======================================");
                Console.WriteLine("  PokerGame Service Launcher Utility   ");
                Console.WriteLine("=======================================");
                Console.WriteLine();
                
                // Track application start event
                _telemetryService?.TrackEvent("ApplicationStarted", 
                    new Dictionary<string, string> { 
                        { "CommandLineArgs", string.Join(" ", args) } 
                    });
                
                // Run the service launcher with the command line arguments
                var result = await ServiceLauncher.RunAsync(args);
                
                // Track application exit event
                _telemetryService?.TrackEvent("ApplicationExited", 
                    new Dictionary<string, string> { 
                        { "ExitCode", result.ToString() } 
                    });
                
                // Flush telemetry before exiting
                _telemetryService?.Flush();
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                
                // Track exception
                _telemetryService?.TrackException(ex);
                _telemetryService?.Flush();
                
                return 1;
            }
        }
        
        /// <summary>
        /// Initialize the application configuration
        /// </summary>
        private static IConfiguration InitializeConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .AddEnvironmentVariables();
                
            return builder.Build();
        }
        
        /// <summary>
        /// Initialize the telemetry service
        /// </summary>
        private static void InitializeTelemetry(IConfiguration configuration)
        {
            try
            {
                var instrumentationKey = configuration["ApplicationInsights:InstrumentationKey"];
                if (!string.IsNullOrEmpty(instrumentationKey))
                {
                    _telemetryService = new TelemetryService(instrumentationKey);
                    Console.WriteLine("Telemetry service initialized successfully");
                }
                else
                {
                    Console.WriteLine("Instrumentation key not found, telemetry will not be enabled");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize telemetry: {ex.Message}");
            }
        }
    }
}