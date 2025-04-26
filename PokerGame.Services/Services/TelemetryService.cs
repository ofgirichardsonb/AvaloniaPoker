using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.DataContracts;
using PokerGame.Abstractions;

namespace PokerGame.Services
{
    /// <summary>
    /// Provides telemetry services for the poker game using Application Insights
    /// </summary>
    public class TelemetryService : ITelemetryService
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly TelemetryConfiguration _telemetryConfiguration;
        private readonly DependencyTrackingTelemetryModule _dependencyModule;
        private static readonly Lazy<TelemetryService> _instance = new Lazy<TelemetryService>(() => new TelemetryService());

        /// <summary>
        /// Gets the singleton instance of the TelemetryService
        /// </summary>
        public static TelemetryService Instance => _instance.Value;

        /// <summary>
        /// Gets the TelemetryClient instance
        /// </summary>
        public TelemetryClient Client => _telemetryClient;

        /// <summary>
        /// Private constructor to enforce singleton pattern
        /// </summary>
        private TelemetryService()
        {
            // Create the telemetry configuration
            _telemetryConfiguration = TelemetryConfiguration.CreateDefault();
            
            // Get the instrumentation key from environment variables
            string? instrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
            if (!string.IsNullOrEmpty(instrumentationKey))
            {
                _telemetryConfiguration.ConnectionString = $"InstrumentationKey={instrumentationKey}";
            }
            
            // Initialize the dependency tracking module
            _dependencyModule = new DependencyTrackingTelemetryModule();
            _dependencyModule.Initialize(_telemetryConfiguration);
            
            // Create the telemetry client
            _telemetryClient = new TelemetryClient(_telemetryConfiguration);
            
            // Set common properties
            _telemetryClient.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
            _telemetryClient.Context.Component.Version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";
            _telemetryClient.Context.Session.Id = Guid.NewGuid().ToString();
            
            // Track the telemetry service initialization
            TrackEvent("TelemetryInitialized", new Dictionary<string, string>
            {
                ["HasInstrumentationKey"] = (!string.IsNullOrEmpty(instrumentationKey)).ToString()
            });
        }

        /// <summary>
        /// Tracks a custom event
        /// </summary>
        /// <param name="eventName">The name of the event</param>
        /// <param name="properties">Optional properties to include with the event</param>
        public void TrackEvent(string eventName, IDictionary<string, string>? properties = null)
        {
            _telemetryClient.TrackEvent(eventName, properties);
        }

        /// <summary>
        /// Tracks a request message
        /// </summary>
        /// <param name="messageName">The name of the message</param>
        /// <param name="startTime">The time when the message was sent</param>
        /// <param name="duration">The duration of the message processing</param>
        /// <param name="responseCode">The response code (if applicable)</param>
        /// <param name="success">Whether the message was processed successfully</param>
        /// <param name="properties">Optional properties to include with the request</param>
        public void TrackRequest(string messageName, DateTimeOffset startTime, TimeSpan duration, string responseCode, bool success, IDictionary<string, string>? properties = null)
        {
            var requestTelemetry = new RequestTelemetry(messageName, startTime, duration, responseCode, success);
            
            if (properties != null)
            {
                foreach (var property in properties)
                {
                    requestTelemetry.Properties.Add(property.Key, property.Value);
                }
            }
            
            _telemetryClient.TrackRequest(requestTelemetry);
        }

        /// <summary>
        /// Tracks an exception
        /// </summary>
        /// <param name="exception">The exception to track</param>
        /// <param name="properties">Optional properties to include with the exception</param>
        public void TrackException(Exception exception, IDictionary<string, string>? properties = null)
        {
            _telemetryClient.TrackException(exception, properties);
        }

        /// <summary>
        /// Tracks a metric value
        /// </summary>
        /// <param name="metricName">The name of the metric</param>
        /// <param name="value">The value of the metric</param>
        /// <param name="properties">Optional properties to include with the metric</param>
        public void TrackMetric(string metricName, double value, IDictionary<string, string>? properties = null)
        {
            _telemetryClient.TrackMetric(metricName, value, properties);
        }
        
        /// <summary>
        /// Tracks a dependency call
        /// </summary>
        /// <param name="dependencyName">The name of the dependency</param>
        /// <param name="target">The target of the dependency call</param>
        /// <param name="startTime">The time when the dependency call was made</param>
        /// <param name="duration">The duration of the dependency call</param>
        /// <param name="success">Whether the dependency call was successful</param>
        /// <param name="properties">Optional properties to include with the dependency</param>
        public void TrackDependency(string dependencyName, string target, DateTimeOffset startTime, TimeSpan duration, bool success, IDictionary<string, string>? properties = null)
        {
            var dependencyTelemetry = new DependencyTelemetry
            {
                Name = dependencyName,
                Target = target,
                Timestamp = startTime,
                Duration = duration,
                Success = success
            };
            
            if (properties != null)
            {
                foreach (var property in properties)
                {
                    dependencyTelemetry.Properties.Add(property.Key, property.Value);
                }
            }
            
            _telemetryClient.TrackDependency(dependencyTelemetry);
        }
        
        /// <summary>
        /// Flushes the telemetry client to ensure all telemetry is sent
        /// </summary>
        public void Flush()
        {
            _telemetryClient.Flush();
        }
        
        /// <summary>
        /// Disposes the telemetry service
        /// </summary>
        public void Dispose()
        {
            _dependencyModule.Dispose();
            _telemetryClient.Flush();
            _telemetryConfiguration.Dispose();
        }
    }
}