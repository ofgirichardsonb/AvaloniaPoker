using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.DependencyCollector;

namespace PokerGame.Core.Telemetry
{
    /// <summary>
    /// Provides telemetry services for the poker game
    /// </summary>
    public class TelemetryService : IDisposable
    {
        private static readonly Lazy<TelemetryService> _instance = new Lazy<TelemetryService>(() => new TelemetryService());
        
        /// <summary>
        /// Gets the singleton instance of the telemetry service
        /// </summary>
        public static TelemetryService Instance => _instance.Value;
        
        private readonly TelemetryClient _telemetryClient;
        private readonly DependencyTrackingTelemetryModule _dependencyModule;
        private bool _isInitialized = false;
        private string _instrumentationKey;
        
        /// <summary>
        /// Gets a value indicating whether the telemetry service is initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;
        
        /// <summary>
        /// Creates a new instance of the telemetry service
        /// </summary>
        private TelemetryService()
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
            // Check if instrumentationKey is really empty or just whitespace
            if (string.IsNullOrWhiteSpace(instrumentationKey))
            {
                Console.WriteLine("WARNING: Application Insights instrumentation key is missing, empty, or just whitespace");
                Console.WriteLine($"  - Key value: '{instrumentationKey}'");
                Console.WriteLine($"  - Key length: {(instrumentationKey?.Length.ToString() ?? "null")}");
                Console.WriteLine("  - Current directory: " + Directory.GetCurrentDirectory());
                Console.WriteLine("  - Environment variable exists: " + 
                    (Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY") != null ? "Yes" : "No"));
                return false;
            }
            
            try
            {
                // Make sure the key isn't just whitespace
                string trimmedKey = instrumentationKey.Trim();
                if (string.IsNullOrEmpty(trimmedKey))
                {
                    Console.WriteLine("WARNING: After trimming, Application Insights key is empty");
                    return false;
                }
                
                Console.WriteLine($"Initializing Application Insights telemetry with key: '{trimmedKey.Substring(0,4)}...' (Length: {trimmedKey.Length})");
                
                _instrumentationKey = trimmedKey;
                
                // Set both for compatibility
                _telemetryClient.TelemetryConfiguration.InstrumentationKey = trimmedKey;
                _telemetryClient.TelemetryConfiguration.ConnectionString = $"InstrumentationKey={trimmedKey}";
                
                // Set common properties for all telemetry
                _telemetryClient.Context.Component.Version = GetAppVersion();
                _telemetryClient.Context.Session.Id = Guid.NewGuid().ToString();
                _telemetryClient.Context.User.Id = Environment.MachineName;
                _telemetryClient.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
                
                // Log application identity data
                Console.WriteLine($"Setting telemetry context - Version: {_telemetryClient.Context.Component.Version}, OS: {_telemetryClient.Context.Device.OperatingSystem}");
                
                // Track initialization with explicit flush
                _telemetryClient.TrackEvent("TelemetryInitialized");
                _telemetryClient.Flush();
                Console.WriteLine("Successfully initialized Application Insights telemetry and sent test event");
                
                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR initializing Application Insights: {ex.Message}");
                Console.WriteLine($"Error details: {ex.StackTrace}");
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
        /// Tracks an event with the specified name and properties
        /// </summary>
        /// <param name="eventName">The name of the event</param>
        /// <param name="properties">Additional properties for the event</param>
        public void TrackEvent(string eventName, IDictionary<string, string>? properties = null)
        {
            if (!_isInitialized) return;
            
            try
            {
                _telemetryClient.TrackEvent(eventName, properties);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking event: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tracks a metric with the specified name and value
        /// </summary>
        /// <param name="metricName">The name of the metric</param>
        /// <param name="value">The value of the metric</param>
        /// <param name="properties">Additional properties for the metric</param>
        public void TrackMetric(string metricName, double value, IDictionary<string, string>? properties = null)
        {
            if (!_isInitialized) return;
            
            try
            {
                _telemetryClient.TrackMetric(metricName, value, properties);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking metric: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tracks an exception
        /// </summary>
        /// <param name="exception">The exception to track</param>
        /// <param name="properties">Additional properties for the exception</param>
        public void TrackException(Exception exception, IDictionary<string, string>? properties = null)
        {
            if (!_isInitialized) return;
            
            try
            {
                _telemetryClient.TrackException(exception, properties);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tracks a request with the specified name, timestamp, duration, response code, and success status
        /// </summary>
        /// <param name="name">The name of the request</param>
        /// <param name="timestamp">The timestamp of the request</param>
        /// <param name="duration">The duration of the request</param>
        /// <param name="responseCode">The response code of the request</param>
        /// <param name="success">Whether the request was successful</param>
        public void TrackRequest(string name, DateTimeOffset timestamp, TimeSpan duration, string responseCode, bool success)
        {
            if (!_isInitialized) return;
            
            try
            {
                _telemetryClient.TrackRequest(name, timestamp, duration, responseCode, success);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking request: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tracks a dependency with the specified name, command, timestamp, duration, and success status
        /// </summary>
        /// <param name="dependencyTypeName">The type of dependency (e.g., HTTP, SQL)</param>
        /// <param name="target">The target of the dependency (e.g., a service name)</param>
        /// <param name="dependencyName">The name of the dependency</param>
        /// <param name="data">The dependency data or command text</param>
        /// <param name="startTime">The start time of the dependency call</param>
        /// <param name="duration">The duration of the dependency call</param>
        /// <param name="success">Whether the dependency call was successful</param>
        public void TrackDependency(string dependencyTypeName, string target, string dependencyName, string data,
            DateTimeOffset startTime, TimeSpan duration, bool success)
        {
            if (!_isInitialized) return;
            
            try
            {
                _telemetryClient.TrackDependency(dependencyTypeName, target, dependencyName, data, startTime, duration, success ? "200" : "500", success);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking dependency: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Tracks a trace message with the specified severity level and properties
        /// </summary>
        /// <param name="message">The trace message</param>
        /// <param name="severityLevel">The severity level of the trace</param>
        /// <param name="properties">Additional properties for the trace</param>
        public void TrackTrace(string message, SeverityLevel severityLevel = SeverityLevel.Information, IDictionary<string, string>? properties = null)
        {
            if (!_isInitialized) return;
            
            try
            {
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
        /// Flushes telemetry data asynchronously to the server
        /// </summary>
        public async Task FlushAsync()
        {
            if (!_isInitialized) return;
            
            try
            {
                await Task.Run(() => _telemetryClient.Flush());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error flushing telemetry: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Reads telemetry data from Application Insights (if supported by the API key)
        /// </summary>
        /// <param name="query">The Application Insights query</param>
        /// <returns>The query results as a string, or an error message</returns>
        public async Task<string> QueryTelemetryAsync(string query)
        {
            if (!_isInitialized) return "Telemetry service not initialized";
            
            try
            {
                // This is a placeholder for actual implementation
                // Would require using the Application Insights Data API
                return "Query telemetry functionality not implemented yet.";
            }
            catch (Exception ex)
            {
                return $"Error querying telemetry: {ex.Message}";
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