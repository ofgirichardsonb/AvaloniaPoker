using System;
using System.Threading;
using System.Collections.Generic;

namespace MessageBroker
{
    /// <summary>
    /// Manages the lifecycle of a CentralMessageBroker instance
    /// </summary>
    public class BrokerManager : IDisposable
    {
        private static BrokerManager? _instance;
        private static readonly object _lockObject = new object();
        private readonly BrokerLogger _logger = BrokerLogger.Instance;
        private readonly TelemetryHelper _telemetry = TelemetryHelper.Instance;
        
        private CentralMessageBroker? _broker;
        private readonly string _brokerId;
        private int _frontendPort;
        private int _backendPort;
        private int _monitorPort;
        private bool _isStarted;
        private bool _telemetryEnabled = false;
        
        /// <summary>
        /// Gets the singleton instance of the BrokerManager
        /// </summary>
        public static BrokerManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                        {
                            _instance = new BrokerManager();
                        }
                    }
                }
                
                return _instance;
            }
        }
        
        /// <summary>
        /// Gets the broker ID
        /// </summary>
        public string BrokerId => _brokerId;
        
        /// <summary>
        /// Gets the frontend port (client connections)
        /// </summary>
        public int FrontendPort => _frontendPort;
        
        /// <summary>
        /// Gets the backend port (service connections)
        /// </summary>
        public int BackendPort => _backendPort;
        
        /// <summary>
        /// Gets the monitor port
        /// </summary>
        public int MonitorPort => _monitorPort;
        
        /// <summary>
        /// Gets whether the broker is started
        /// </summary>
        public bool IsStarted => _isStarted;
        
        /// <summary>
        /// Private constructor to enforce singleton pattern
        /// </summary>
        private BrokerManager()
        {
            _brokerId = $"Broker-{Guid.NewGuid()}";
            _frontendPort = 5570;
            _backendPort = 5571;
            _monitorPort = 5572;
            _isStarted = false;
        }
        
        /// <summary>
        /// Initializes telemetry with the provided instrumentation key
        /// </summary>
        /// <param name="instrumentationKey">The Application Insights instrumentation key</param>
        public bool InitializeTelemetry(string? instrumentationKey = null)
        {
            try
            {
                if (string.IsNullOrEmpty(instrumentationKey))
                {
                    // Try to get from environment variable
                    instrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
                }
                
                if (!string.IsNullOrEmpty(instrumentationKey))
                {
                    _logger.Info("BrokerManager", "Initializing telemetry...");
                    if (_telemetry.Initialize(instrumentationKey))
                    {
                        _telemetryEnabled = true;
                        _logger.Info("BrokerManager", "Telemetry initialized successfully");
                        
                        // Track telemetry initialization
                        _telemetry.TrackBrokerEvent(_brokerId, "TelemetryInitialized");
                        return true;
                    }
                    else
                    {
                        _logger.Error("BrokerManager", "Failed to initialize telemetry");
                    }
                }
                else
                {
                    _logger.Warning("BrokerManager", "Application Insights instrumentation key not provided, telemetry disabled");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("BrokerManager", "Error initializing telemetry", ex);
            }
            
            _telemetryEnabled = false;
            return false;
        }
        
        /// <summary>
        /// Starts the broker in the current process
        /// </summary>
        /// <param name="enableTelemetry">Whether to enable telemetry</param>
        /// <param name="instrumentationKey">The Application Insights instrumentation key (or null to use environment variable)</param>
        public void Start(bool enableTelemetry = false, string? instrumentationKey = null)
        {
            lock (_lockObject)
            {
                if (_isStarted)
                {
                    _logger.Warning("BrokerManager", "Broker already started");
                    return;
                }
                
                try
                {
                    _logger.Info("BrokerManager", $"Starting broker {_brokerId} in current process");
                    _logger.Info("BrokerManager", $"Frontend port: {_frontendPort}, Backend port: {_backendPort}, Monitor port: {_monitorPort}");
                    
                    // Initialize telemetry if enabled
                    if (enableTelemetry)
                    {
                        InitializeTelemetry(instrumentationKey);
                    }
                    
                    // Get the current thread
                    var currentThread = Thread.CurrentThread;
                    
                    // Create the broker with the current thread
                    _broker = new CentralMessageBroker(currentThread, _frontendPort, _backendPort, _monitorPort);
                    
                    _isStarted = true;
                    _logger.Info("BrokerManager", "Broker started successfully");
                    
                    // Track broker start in telemetry
                    if (_telemetryEnabled)
                    {
                        var properties = new Dictionary<string, string>
                        {
                            { "FrontendPort", _frontendPort.ToString() },
                            { "BackendPort", _backendPort.ToString() },
                            { "MonitorPort", _monitorPort.ToString() }
                        };
                        
                        _telemetry.TrackBrokerEvent(_brokerId, "BrokerStarted", properties);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Critical("BrokerManager", "Error starting broker", ex);
                    
                    // Track start failure in telemetry
                    if (_telemetryEnabled)
                    {
                        _telemetry.TrackException(ex, "BrokerManager");
                    }
                    
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Starts the broker in the current process
        /// </summary>
        public void Start()
        {
            Start(false);
        }
        
        /// <summary>
        /// Starts the broker in the current process with custom ports
        /// </summary>
        public void Start(int frontendPort, int backendPort, int monitorPort)
        {
            _frontendPort = frontendPort;
            _backendPort = backendPort;
            _monitorPort = monitorPort;
            
            Start();
        }
        
        /// <summary>
        /// Stops the broker
        /// </summary>
        public void Stop()
        {
            lock (_lockObject)
            {
                if (!_isStarted)
                {
                    _logger.Warning("BrokerManager", "Broker not started");
                    return;
                }
                
                try
                {
                    _logger.Info("BrokerManager", "Stopping broker");
                    
                    // Track broker stop in telemetry
                    if (_telemetryEnabled)
                    {
                        var serviceCount = _broker?.ServiceCount ?? 0;
                        var properties = new Dictionary<string, string>
                        {
                            { "ServiceCount", serviceCount.ToString() }
                        };
                        
                        _telemetry.TrackBrokerEvent(_brokerId, "BrokerStopping", properties);
                    }
                    
                    _broker?.Stop();
                    
                    _isStarted = false;
                    _logger.Info("BrokerManager", "Broker stopped successfully");
                    
                    // Track broker stopped in telemetry
                    if (_telemetryEnabled)
                    {
                        _telemetry.TrackBrokerEvent(_brokerId, "BrokerStopped");
                        _telemetry.Flush();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("BrokerManager", "Error stopping broker", ex);
                    
                    // Track stop failure in telemetry
                    if (_telemetryEnabled)
                    {
                        _telemetry.TrackException(ex, "BrokerManager");
                        _telemetry.Flush();
                    }
                    
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Gets the current message broker
        /// </summary>
        public CentralMessageBroker? GetBroker()
        {
            return _broker;
        }
        
        /// <summary>
        /// Creates a new client connected to the broker
        /// </summary>
        /// <param name="serviceName">The service name</param>
        /// <param name="serviceType">The service type</param>
        /// <param name="capabilities">The service capabilities</param>
        /// <returns>A new BrokerClient instance</returns>
        public BrokerClient CreateClient(string serviceName, string serviceType, List<string>? capabilities = null)
        {
            if (!_isStarted)
            {
                _logger.Warning("BrokerManager", "Creating client for non-started broker");
            }
            
            var client = new BrokerClient(serviceName, serviceType, capabilities, "localhost", _frontendPort);
            
            // Track client creation in telemetry
            if (_telemetryEnabled)
            {
                var props = new Dictionary<string, string>
                {
                    { "ClientName", serviceName },
                    { "ClientType", serviceType },
                    { "ClientId", client.ClientId }
                };
                
                if (capabilities != null && capabilities.Count > 0)
                {
                    props["Capabilities"] = string.Join(",", capabilities);
                }
                
                _telemetry.TrackBrokerEvent(_brokerId, "ClientCreated", props);
            }
            
            return client;
        }
        
        /// <summary>
        /// Creates a new client connected to the broker
        /// </summary>
        /// <param name="serviceName">The service name</param>
        /// <param name="serviceType">The service type</param>
        /// <param name="capabilities">The service capabilities as array</param>
        /// <returns>A new BrokerClient instance</returns>
        public BrokerClient CreateClient(string serviceName, string serviceType, string[]? capabilities = null)
        {
            List<string>? capList = null;
            if (capabilities != null)
            {
                capList = new List<string>(capabilities);
            }
            
            return CreateClient(serviceName, serviceType, capList);
        }
        
        /// <summary>
        /// Disposes the broker manager and releases resources
        /// </summary>
        public void Dispose()
        {
            lock (_lockObject)
            {
                try
                {
                    // Track disposal in telemetry
                    if (_telemetryEnabled)
                    {
                        _telemetry.TrackBrokerEvent(_brokerId, "BrokerManagerDisposing");
                    }
                    
                    if (_isStarted)
                    {
                        Stop();
                    }
                    
                    _broker?.Dispose();
                    _broker = null;
                    
                    // Final telemetry event and flush
                    if (_telemetryEnabled)
                    {
                        _telemetry.TrackBrokerEvent(_brokerId, "BrokerManagerDisposed");
                        _telemetry.Flush();
                        _telemetry.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("BrokerManager", "Error disposing broker manager", ex);
                }
            }
        }
    }
}