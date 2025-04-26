using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.DependencyCollector;

namespace MessageBroker
{
    /// <summary>
    /// Provides telemetry services for the message broker
    /// </summary>
    public class TelemetryHelper : IDisposable
    {
        private static readonly Lazy<TelemetryHelper> _instance = new Lazy<TelemetryHelper>(() => new TelemetryHelper());
        
        /// <summary>
        /// Gets the singleton instance of the telemetry service
        /// </summary>
        public static TelemetryHelper Instance => _instance.Value;
        
        private readonly TelemetryClient _telemetryClient;
        private readonly DependencyTrackingTelemetryModule _dependencyModule;
        private bool _isInitialized = false;
        private string? _instrumentationKey;
        
        /// <summary>
        /// Gets a value indicating whether the telemetry service is initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;
        
        /// <summary>
        /// Creates a new instance of the telemetry service
        /// </summary>
        private TelemetryHelper()
        {
            var config = TelemetryConfiguration.CreateDefault();
            _dependencyModule = new DependencyTrackingTelemetryModule();
            _dependencyModule.Initialize(config);
            
            _telemetryClient = new TelemetryClient(config);
        }
        
        /// <summary>
        /// Initializes the telemetry service with the specified instrumentation key
        /// </summary>
        /// <param name="instrumentationKey">The Application Insights instrumentation key</param>
        /// <returns>True if initialization was successful, otherwise false</returns>
        public bool Initialize(string instrumentationKey)
        {
            if (string.IsNullOrEmpty(instrumentationKey))
            {
                Console.WriteLine("Warning: Application Insights instrumentation key is missing or empty");
                return false;
            }
            
            try
            {
                _instrumentationKey = instrumentationKey;
                
                // Use ConnectionString instead of InstrumentationKey (which is deprecated)
                string connectionString = $"InstrumentationKey={instrumentationKey}";
                _telemetryClient.TelemetryConfiguration.ConnectionString = connectionString;
                
                // Set common properties for all telemetry
                _telemetryClient.Context.Component.Version = GetAppVersion();
                _telemetryClient.Context.Session.Id = Guid.NewGuid().ToString();
                _telemetryClient.Context.User.Id = Environment.MachineName;
                _telemetryClient.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
                
                // Track initialization
                _telemetryClient.TrackEvent("MessageBroker.TelemetryInitialized");
                
                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Application Insights: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Gets the application version
        /// </summary>
        /// <returns>The application version string</returns>
        private string GetAppVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetEntryAssembly();
                var version = assembly?.GetName().Version;
                return version?.ToString() ?? "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }
        
        /// <summary>
        /// Tracks a broker event
        /// </summary>
        /// <param name="brokerId">The ID of the broker</param>
        /// <param name="eventName">The name of the event</param>
        /// <param name="properties">Additional properties for the event</param>
        public void TrackBrokerEvent(string brokerId, string eventName, IDictionary<string, string>? properties = null)
        {
            if (!_isInitialized) return;
            
            try
            {
                var eventProperties = properties ?? new Dictionary<string, string>();
                eventProperties["BrokerId"] = brokerId;
                
                _telemetryClient.TrackEvent($"MessageBroker.{eventName}", eventProperties);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking broker event: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tracks a message event
        /// </summary>
        /// <param name="messageId">The ID of the message</param>
        /// <param name="messageType">The type of message</param>
        /// <param name="senderId">The ID of the sender</param>
        /// <param name="receiverId">The ID of the receiver</param>
        /// <param name="eventName">The name of the event</param>
        /// <param name="durationMs">The duration of the operation in milliseconds</param>
        /// <param name="success">Whether the operation was successful</param>
        public void TrackMessageEvent(string messageId, string messageType, string senderId, string receiverId,
            string eventName, double durationMs = 0, bool success = true)
        {
            if (!_isInitialized) return;
            
            try
            {
                var properties = new Dictionary<string, string>
                {
                    { "MessageId", messageId },
                    { "MessageType", messageType },
                    { "SenderId", senderId },
                    { "ReceiverId", string.IsNullOrEmpty(receiverId) ? "Broadcast" : receiverId },
                    { "Success", success.ToString() }
                };
                
                if (durationMs > 0)
                {
                    properties["DurationMs"] = durationMs.ToString("F2");
                }
                
                _telemetryClient.TrackEvent($"MessageBroker.Message.{eventName}", properties);
                
                if (durationMs > 0)
                {
                    _telemetryClient.TrackMetric($"MessageBroker.Message.{eventName}.Duration", durationMs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking message event: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tracks a client operation
        /// </summary>
        /// <param name="clientId">The ID of the client</param>
        /// <param name="clientName">The name of the client</param>
        /// <param name="clientType">The type of client</param>
        /// <param name="operationName">The name of the operation</param>
        /// <param name="success">Whether the operation was successful</param>
        /// <param name="durationMs">The duration of the operation in milliseconds</param>
        public void TrackClientOperation(string clientId, string clientName, string clientType,
            string operationName, bool success = true, double durationMs = 0)
        {
            if (!_isInitialized) return;
            
            try
            {
                var properties = new Dictionary<string, string>
                {
                    { "ClientId", clientId },
                    { "ClientName", clientName },
                    { "ClientType", clientType },
                    { "Operation", operationName },
                    { "Success", success.ToString() }
                };
                
                if (durationMs > 0)
                {
                    properties["DurationMs"] = durationMs.ToString("F2");
                }
                
                _telemetryClient.TrackEvent("MessageBroker.Client.Operation", properties);
                
                if (durationMs > 0)
                {
                    _telemetryClient.TrackMetric($"MessageBroker.Client.{operationName}.Duration", durationMs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking client operation: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tracks a client event
        /// </summary>
        /// <param name="clientId">The ID of the client</param>
        /// <param name="eventName">The name of the event</param>
        /// <param name="properties">Additional properties for the event</param>
        public void TrackClientEvent(string clientId, string eventName, IDictionary<string, string>? properties = null)
        {
            if (!_isInitialized) return;
            
            try
            {
                var eventProperties = properties ?? new Dictionary<string, string>();
                eventProperties["ClientId"] = clientId;
                
                _telemetryClient.TrackEvent($"MessageBroker.Client.{eventName}", eventProperties);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking client event: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tracks an exception
        /// </summary>
        /// <param name="exception">The exception to track</param>
        /// <param name="componentName">The name of the component that threw the exception</param>
        /// <param name="properties">Additional properties for the exception</param>
        public void TrackException(Exception exception, string componentName, IDictionary<string, string>? properties = null)
        {
            if (!_isInitialized) return;
            
            try
            {
                var exceptionProperties = properties ?? new Dictionary<string, string>();
                exceptionProperties["Component"] = componentName;
                
                _telemetryClient.TrackException(exception, exceptionProperties);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tracks a trace message
        /// </summary>
        /// <param name="message">The trace message</param>
        /// <param name="severityLevel">The severity level of the trace</param>
        /// <param name="componentName">The name of the component that generated the trace</param>
        public void TrackTrace(string message, SeverityLevel severityLevel, string componentName)
        {
            if (!_isInitialized) return;
            
            try
            {
                var properties = new Dictionary<string, string>
                {
                    { "Component", componentName }
                };
                
                _telemetryClient.TrackTrace(message, severityLevel, properties);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking trace: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Flushes telemetry data to the server
        /// </summary>
        public void Flush()
        {
            if (!_isInitialized) return;
            
            try
            {
                _telemetryClient.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error flushing telemetry: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Disposes resources used by the telemetry service
        /// </summary>
        public void Dispose()
        {
            try
            {
                Flush();
                _dependencyModule?.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
    }
}