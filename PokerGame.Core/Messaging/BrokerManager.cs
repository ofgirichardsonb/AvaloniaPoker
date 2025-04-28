using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PokerGame.Core.Telemetry;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Manages the central message broker for the application
    /// </summary>
    public class BrokerManager
    {
        private static readonly Lazy<BrokerManager> _instance = new Lazy<BrokerManager>(() => new BrokerManager());
        private object? _telemetryHandler;
        private PokerGame.Core.Telemetry.TelemetryService _telemetryService;
        private bool _isStarted = false;
        private object _startLock = new object();
        private ExecutionContext? _executionContext;
        private CentralMessageBroker? _centralBroker;
        
        /// <summary>
        /// Gets the singleton instance of the BrokerManager
        /// </summary>
        public static BrokerManager Instance => _instance.Value;
        
        /// <summary>
        /// Gets the central message broker
        /// </summary>
        public CentralMessageBroker? CentralBroker => _centralBroker;
        
        /// <summary>
        /// Gets the execution context
        /// </summary>
        public ExecutionContext? ExecutionContext => _executionContext;
        
        /// <summary>
        /// Creates a new instance of the BrokerManager
        /// </summary>
        private BrokerManager()
        {
            // Get the telemetry service
            _telemetryService = PokerGame.Core.Telemetry.TelemetryService.Instance;
            
            // Log initialization
            _telemetryService.TrackEvent("BrokerManagerInitialized");
        }
        
        /// <summary>
        /// Starts the broker manager
        /// </summary>
        public void Start()
        {
            Start(null);
        }
        
        /// <summary>
        /// Starts the broker manager with the specified execution context
        /// </summary>
        /// <param name="executionContext">The execution context to use</param>
        public void Start(ExecutionContext? executionContext)
        {
            lock (_startLock)
            {
                if (_isStarted)
                    return;
                
                // Log start
                _telemetryService.TrackEvent("BrokerManagerStarting");
                
                // Store the execution context
                _executionContext = executionContext ?? new ExecutionContext();
                
                // Automatically create a central broker on the default port for better interoperability
                // This ensures that services can find the broker without explicit initialization
                if (_centralBroker == null)
                {
                    Console.WriteLine("BrokerManager: Automatically creating central broker on default port");
                    var defaultPort = 25555; // Default central broker port
                    _centralBroker = new CentralMessageBroker(_executionContext, defaultPort, true);
                    _centralBroker.Start();
                    
                    // Set telemetry handler if available
                    if (_telemetryHandler != null)
                    {
                        _centralBroker.SetTelemetryHandler(_telemetryHandler);
                    }
                    
                    // Log central broker start
                    _telemetryService.TrackEvent("CentralMessageBrokerStarted", new Dictionary<string, string>
                    {
                        ["Port"] = defaultPort.ToString(),
                        ["Verbose"] = "true",
                        ["AutoCreated"] = "true"
                    });
                    
                    Console.WriteLine($"BrokerManager: Central broker automatically created on port {defaultPort}");
                }
                
                // Set started flag
                _isStarted = true;
                
                // Log completion
                _telemetryService.TrackEvent("BrokerManagerStarted");
            }
        }
        
        /// <summary>
        /// Starts the central message broker on the specified port
        /// </summary>
        /// <param name="port">The port for the central broker to listen on</param>
        /// <param name="executionContext">The execution context to use</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        /// <returns>The created and started central message broker</returns>
        public CentralMessageBroker StartCentralBroker(int port, ExecutionContext? executionContext = null, bool verbose = false)
        {
            lock (_startLock)
            {
                // Make sure the broker manager is started
                if (!_isStarted)
                {
                    Start(executionContext);
                }
                
                // Use the specified or stored execution context
                var context = executionContext ?? _executionContext;
                
                if (context == null)
                {
                    context = new ExecutionContext();
                    _executionContext = context;
                }
                
                // Create the central broker if it doesn't exist
                if (_centralBroker == null)
                {
                    _centralBroker = new CentralMessageBroker(context, port, verbose);
                    _centralBroker.Start();
                    
                    // Set telemetry handler if available
                    if (_telemetryHandler != null)
                    {
                        _centralBroker.SetTelemetryHandler(_telemetryHandler);
                    }
                    
                    // Log central broker start
                    _telemetryService.TrackEvent("CentralMessageBrokerStarted", new Dictionary<string, string>
                    {
                        ["Port"] = port.ToString(),
                        ["Verbose"] = verbose.ToString()
                    });
                }
                
                return _centralBroker;
            }
        }
        
        /// <summary>
        /// Stops the broker manager
        /// </summary>
        public void Stop()
        {
            lock (_startLock)
            {
                if (!_isStarted)
                    return;
                
                // Log stop
                _telemetryService.TrackEvent("BrokerManagerStopping");
                
                // Stop the central broker
                _centralBroker?.Stop();
                
                // Clear started flag
                _isStarted = false;
                
                // Log completion
                _telemetryService.TrackEvent("BrokerManagerStopped");
            }
        }
        
        /// <summary>
        /// Initializes telemetry for the broker
        /// </summary>
        public void InitializeTelemetry()
        {
            if (_telemetryHandler != null)
                return;
                
            // Just log telemetry initialization for now
            // The actual telemetry handler will be set by the Services project
            _telemetryService.TrackEvent("BrokerTelemetryInitialized");
        }
        
        /// <summary>
        /// Sets the telemetry handler for the broker
        /// </summary>
        /// <param name="telemetryHandler">The telemetry handler instance</param>
        public void SetTelemetryHandler(object telemetryHandler)
        {
            _telemetryHandler = telemetryHandler;
            
            // If we have a central broker, set its telemetry handler
            if (_centralBroker != null && _telemetryHandler != null)
            {
                _centralBroker.SetTelemetryHandler(_telemetryHandler);
            }
        }
        
        /// <summary>
        /// Creates a new service-specific execution context that shares the central broker's 
        /// cancellation token source but has its own task scheduler
        /// </summary>
        /// <returns>A new execution context for a service</returns>
        public ExecutionContext CreateServiceExecutionContext()
        {
            if (_executionContext == null)
            {
                _executionContext = new ExecutionContext();
            }
            
            return new ExecutionContext(
                _executionContext.CancellationTokenSource,
                null,
                null,
                TaskScheduler.Default,
                false);
        }
    }
}