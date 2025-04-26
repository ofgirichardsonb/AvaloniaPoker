using System;
using System.Threading;
using System.Threading.Tasks;
using PokerGame.Core.Microservices;
using PokerGame.Core.Messaging;

namespace PokerGame.Services
{
    /// <summary>
    /// Base class for console programs that host microservices
    /// </summary>
    public class MicroserviceConsoleProgram : IDisposable
    {
        private readonly MicroserviceManager _serviceManager;
        private readonly TelemetryService _telemetryService;
        private readonly BrokerManager _brokerManager;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private bool _isDisposed = false;
        
        /// <summary>
        /// Creates a new instance of the MicroserviceConsoleProgram
        /// </summary>
        /// <param name="brokerHost">The host address for the message broker</param>
        /// <param name="brokerPort">The port for the message broker</param>
        public MicroserviceConsoleProgram(string brokerHost = "localhost", int brokerPort = 5555)
        {
            // Initialize telemetry
            _telemetryService = TelemetryService.Instance;
            
            // Get the broker manager instance
            _brokerManager = BrokerManager.Instance;
            // Start the broker manager
            _brokerManager.Start();
            
            // Create the service manager
            _serviceManager = new MicroserviceManager(brokerPort);
            
            // Track program initialization
            _telemetryService.TrackEvent("ConsoleApplicationStarted", new System.Collections.Generic.Dictionary<string, string>
            {
                ["BrokerHost"] = brokerHost,
                ["BrokerPort"] = brokerPort.ToString(),
                ["OSVersion"] = Environment.OSVersion.ToString(),
                ["RuntimeVersion"] = Environment.Version.ToString()
            });
        }
        
        /// <summary>
        /// Adds a microservice to the program
        /// </summary>
        /// <param name="service">The service to add</param>
        /// <returns>The registered service (which may be decorated with telemetry)</returns>
        public MicroserviceBase AddService(MicroserviceBase service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));
                
            return _serviceManager.RegisterService(service);
        }
        
        /// <summary>
        /// Gets a registered game engine service by ID
        /// </summary>
        /// <param name="serviceId">The ID of the service to get</param>
        /// <returns>The game engine service, or null if not found</returns>
        public IGameEngineService? GetGameEngineService(string serviceId)
        {
            return _serviceManager.GetGameEngineService(serviceId);
        }
        
        /// <summary>
        /// Starts the program and all registered services
        /// </summary>
        public async Task RunAsync()
        {
            Console.WriteLine("Starting microservice console program...");
            
            try
            {
                // Start all registered services
                await _serviceManager.StartAllServicesAsync();
                
                // Track program started
                _telemetryService.TrackEvent("ProgramRunning");
                
                // Wait for cancellation
                Console.WriteLine("All microservices started successfully");
                Console.WriteLine("Press Ctrl+C to exit");
                
                // Set up console cancellation
                Console.CancelKeyPress += (sender, e) => {
                    e.Cancel = true;
                    _cancellationTokenSource.Cancel();
                };
                
                // Wait for cancellation
                try
                {
                    await Task.Delay(-1, _cancellationTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancellation is requested
                }
            }
            catch (Exception ex)
            {
                _telemetryService.TrackException(ex, new System.Collections.Generic.Dictionary<string, string>
                {
                    ["Operation"] = "RunAsync"
                });
                
                Console.WriteLine($"Error running program: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        /// <summary>
        /// Stops the program and all registered services
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                // Track program stopping
                _telemetryService.TrackEvent("ProgramStopping");
                
                // Stop all services
                await _serviceManager.StopAllServicesAsync();
                
                // Track program stopped
                _telemetryService.TrackEvent("ProgramStopped");
            }
            catch (Exception ex)
            {
                _telemetryService.TrackException(ex, new System.Collections.Generic.Dictionary<string, string>
                {
                    ["Operation"] = "StopAsync"
                });
                
                Console.WriteLine($"Error stopping program: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Disposes the program and all registered services
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;
                
            _isDisposed = true;
            
            // Stop everything
            StopAsync().GetAwaiter().GetResult();
            
            // Dispose the service manager
            _serviceManager.Dispose();
            
            // Dispose the broker manager
            _brokerManager.Stop();
            
            // Clean up the cancellation token source
            _cancellationTokenSource.Dispose();
            
            // Flush telemetry
            _telemetryService.Flush();
        }
        
        /// <summary>
        /// Main entry point for console programs
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <param name="setupAction">Action to set up the program</param>
        /// <returns>The exit code</returns>
        public static async Task<int> MainAsync(string[] args, Func<MicroserviceConsoleProgram, Task> setupAction)
        {
            try
            {
                using (var program = new MicroserviceConsoleProgram())
                {
                    // Set up the program
                    await setupAction(program);
                    
                    // Run the program
                    await program.RunAsync();
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }
    }
}