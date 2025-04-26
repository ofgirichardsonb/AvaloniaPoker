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
        
        /// <summary>
        /// Gets the singleton instance of the BrokerManager
        /// </summary>
        public static BrokerManager Instance => _instance.Value;
        
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
            lock (_startLock)
            {
                if (_isStarted)
                    return;
                
                // Log start
                _telemetryService.TrackEvent("BrokerManagerStarting");
                
                // Set started flag
                _isStarted = true;
                
                // Log completion
                _telemetryService.TrackEvent("BrokerManagerStarted");
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
        }
    }
}