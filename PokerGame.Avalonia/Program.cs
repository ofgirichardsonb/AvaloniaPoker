using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PokerGame.Avalonia
{
    internal class Program
    {
        // Store reference to service manager for proper cleanup
        private static PokerGame.Core.ServiceManagement.ServiceManager? _serviceManager = null;
        
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => 
            {
                Console.WriteLine("Process exit detected, cleaning up...");
                PerformCleanup();
            };
            
            try 
            {
                // Start the required services before launching the UI
                StartRequiredServices();
                
                // Start with the appropriate lifetime
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting application: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                
                // On fatal error, ensure we clean up any lingering processes
                PerformCleanup();
            }
        }
        
        /// <summary>
        /// Starts all required services for the Avalonia UI application
        /// </summary>
        private static void StartRequiredServices()
        {
            Console.WriteLine("Starting required services for Avalonia UI...");
            try
            {
                // Get the service manager instance
                _serviceManager = PokerGame.Core.ServiceManagement.ServiceManager.Instance;
                
                // Start the services with port offset 0
                int portOffset = 0;
                bool verbose = true;
                
                Console.WriteLine($"Starting services with port offset {portOffset}...");
                _serviceManager.StartServicesHost(portOffset, verbose);
                
                // Give services a moment to initialize
                System.Threading.Thread.Sleep(1000);
                
                Console.WriteLine("Services started successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting services: {ex.Message}");
                throw; // Re-throw to abort startup
            }
        }
        
        /// <summary>
        /// Performs thorough cleanup of all resources
        /// </summary>
        private static void PerformCleanup()
        {
            try
            {
                Console.WriteLine("Performing application cleanup...");
                
                // Stop all services
                if (_serviceManager != null)
                {
                    Console.WriteLine("Stopping all services...");
                    _serviceManager.StopAllServices();
                }
                
                // Allow some time for services to shut down gracefully
                System.Threading.Thread.Sleep(500);
                
                // Clean up execution contexts
                Console.WriteLine("Cleaning up execution contexts...");
                MSA.Foundation.ServiceManagement.ExecutionContext.CleanupAll();
                
                // Clean up NetMQ resources
                Console.WriteLine("Cleaning up NetMQ resources...");
                try 
                {
                    NetMQ.NetMQConfig.Cleanup(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during NetMQ cleanup: {ex.Message}");
                }
                
                Console.WriteLine("Cleanup completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Console.WriteLine("macOS platform detected - using native backend");
                return AppBuilder.Configure<App>()
                    .UsePlatformDetect()
                    .UseReactiveUI()
                    .WithInterFont()
                    .LogToTrace();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine("Windows platform detected");
                return AppBuilder.Configure<App>()
                    .UsePlatformDetect()
                    .UseReactiveUI() 
                    .WithInterFont()
                    .LogToTrace();
            }
            else 
            {
                // Linux is not supported in this application
                Console.WriteLine("ERROR: This application only supports Windows and macOS platforms.");
                Console.WriteLine("Please run on a supported platform. Exiting...");
                
                // Exit the application immediately to prevent SkiaSharp initialization
                Environment.Exit(1);
                
                // This code will never be reached but is required for compilation
                return AppBuilder.Configure<App>()
                    .LogToTrace();
            }
        }
    }
}
