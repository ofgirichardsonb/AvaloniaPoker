using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using MSA.Foundation.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

namespace PokerGame.Avalonia
{
    internal class Program
    {
        // Store reference to service manager for proper cleanup
        private static PokerGame.Core.ServiceManagement.ServiceManager? _serviceManager = null;
        
        // Store reference to telemetry service for diagnostic data
        private static TelemetryService? _telemetryService = null;
        
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
                // Initialize telemetry first for comprehensive diagnostics
                InitializeTelemetry();
                
                // Start the required services before launching the UI
                StartRequiredServices();
                
                // Start with the appropriate lifetime
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting application: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                
                // Track the exception in telemetry
                _telemetryService?.TrackException(ex, new Dictionary<string, string> {
                    { "Context", "Application Startup" },
                    { "Component", "Program.Main" }
                });
                _telemetryService?.Flush();
                
                // On fatal error, ensure we clean up any lingering processes
                PerformCleanup();
            }
        }
        
        /// <summary>
        /// Initializes Application Insights telemetry for comprehensive diagnostics
        /// </summary>
        private static void InitializeTelemetry()
        {
            try
            {
                Console.WriteLine("Initializing Application Insights telemetry...");
                
                // Get the singleton telemetry service instance
                _telemetryService = TelemetryService.Instance;
                
                // Try environment variable first (most reliable)
                string? instrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
                if (!string.IsNullOrWhiteSpace(instrumentationKey))
                {
                    Console.WriteLine("Using instrumentation key from environment variable");
                    if (_telemetryService.Initialize(instrumentationKey))
                    {
                        Console.WriteLine("✓ Successfully initialized Application Insights telemetry from environment variable");
                        
                        // Track initialization event
                        _telemetryService.TrackEvent("ApplicationStarted", new Dictionary<string, string> {
                            { "Application", "PokerGame.Avalonia" },
                            { "Version", typeof(Program).Assembly.GetName().Version?.ToString() ?? "Unknown" },
                            { "OS", RuntimeInformation.OSDescription }
                        });
                        
                        return;
                    }
                }
                
                // Try configuration file if environment variable failed
                try
                {
                    var basePath = AppContext.BaseDirectory;
                    Console.WriteLine($"Checking configuration file in: {basePath}");
                    
                    // Build configuration
                    var configuration = new ConfigurationBuilder()
                        .SetBasePath(basePath)
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .Build();
                    
                    // Get the key from configuration
                    var configKey = configuration["ApplicationInsights:InstrumentationKey"];
                    if (!string.IsNullOrWhiteSpace(configKey))
                    {
                        Console.WriteLine("Using instrumentation key from appsettings.json");
                        if (_telemetryService.Initialize(configKey))
                        {
                            Console.WriteLine("✓ Successfully initialized Application Insights telemetry from configuration");
                            
                            // Track initialization event
                            _telemetryService.TrackEvent("ApplicationStarted", new Dictionary<string, string> {
                                { "Application", "PokerGame.Avalonia" },
                                { "Version", typeof(Program).Assembly.GetName().Version?.ToString() ?? "Unknown" },
                                { "OS", RuntimeInformation.OSDescription }
                            });
                            
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading configuration: {ex.Message}");
                }
                
                // If we get here, initialization failed with both methods
                Console.WriteLine("⚠ Application Insights telemetry initialization failed - diagnostics will be limited");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing telemetry: {ex.Message}");
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
                
                // Send final telemetry event
                if (_telemetryService != null)
                {
                    Console.WriteLine("Sending final telemetry events...");
                    _telemetryService.TrackEvent("ApplicationStopping", new Dictionary<string, string> {
                        { "Application", "PokerGame.Avalonia" },
                        { "Version", typeof(Program).Assembly.GetName().Version?.ToString() ?? "Unknown" },
                        { "CleanupReason", "Normal" }
                    });
                    
                    // Ensure all telemetry is sent before shutdown
                    _telemetryService.Flush();
                    Console.WriteLine("Final telemetry events sent");
                }
                
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
                
                // Final telemetry flush to ensure all data is sent
                _telemetryService?.Flush();
                
                Console.WriteLine("Cleanup completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup: {ex.Message}");
                
                // Try to report this error to telemetry
                try
                {
                    _telemetryService?.TrackException(ex, new Dictionary<string, string> {
                        { "Context", "Application Cleanup" },
                        { "Component", "Program.PerformCleanup" }
                    });
                    _telemetryService?.Flush();
                }
                catch
                {
                    // Ignore errors in telemetry during shutdown
                }
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
