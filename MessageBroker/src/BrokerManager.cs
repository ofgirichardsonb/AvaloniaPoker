using System;
using System.Threading;

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
        
        private CentralMessageBroker? _broker;
        private readonly string _brokerId;
        private readonly int _frontendPort;
        private readonly int _backendPort;
        private readonly int _monitorPort;
        private bool _isStarted;
        
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
        /// Starts the broker in the current process
        /// </summary>
        public void Start()
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
                    
                    // Get the current thread
                    var currentThread = Thread.CurrentThread;
                    
                    // Create the broker with the current thread
                    _broker = new CentralMessageBroker(currentThread, _frontendPort, _backendPort, _monitorPort);
                    
                    _isStarted = true;
                    _logger.Info("BrokerManager", "Broker started successfully");
                }
                catch (Exception ex)
                {
                    _logger.Critical("BrokerManager", "Error starting broker", ex);
                    throw;
                }
            }
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
                    
                    _broker?.Stop();
                    
                    _isStarted = false;
                    _logger.Info("BrokerManager", "Broker stopped successfully");
                }
                catch (Exception ex)
                {
                    _logger.Error("BrokerManager", "Error stopping broker", ex);
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
        /// <param name="serviceId">The service ID</param>
        /// <param name="serviceType">The service type</param>
        /// <param name="capabilities">The service capabilities</param>
        /// <returns>A new BrokerClient instance</returns>
        public BrokerClient CreateClient(string serviceId, string serviceType, string[]? capabilities = null)
        {
            if (!_isStarted)
            {
                _logger.Warning("BrokerManager", "Creating client for non-started broker");
            }
            
            return new BrokerClient(serviceId, serviceType, capabilities, _frontendPort);
        }
        
        /// <summary>
        /// Disposes the broker manager and releases resources
        /// </summary>
        public void Dispose()
        {
            lock (_lockObject)
            {
                if (_isStarted)
                {
                    Stop();
                }
                
                _broker?.Dispose();
                _broker = null;
            }
        }
    }
}