using System;
using System.Collections.Generic;
using PokerGame.Abstractions;
using CoreTelemetry = PokerGame.Core.Telemetry;

namespace PokerGame.Services
{
    /// <summary>
    /// Adapter to bridge the Core.Telemetry.TelemetryService to the ITelemetryService interface
    /// </summary>
    public class TelemetryServiceAdapter : ITelemetryService
    {
        private readonly CoreTelemetry.TelemetryService _telemetryService;
        
        /// <summary>
        /// Creates a new adapter for the specified telemetry service
        /// </summary>
        /// <param name="telemetryService">The telemetry service to adapt</param>
        public TelemetryServiceAdapter(CoreTelemetry.TelemetryService telemetryService)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }
        
        /// <summary>
        /// Gets whether telemetry is enabled
        /// </summary>
        public bool IsEnabled => _telemetryService.IsInitialized;
        
        /// <summary>
        /// Gets the name of the telemetry service
        /// </summary>
        public string Name => "AppInsightsTelemetry";
        
        /// <summary>
        /// Tracks an event
        /// </summary>
        public void TrackEvent(string eventName, IDictionary<string, string>? properties = null)
        {
            _telemetryService.TrackEvent(eventName, properties);
        }
        
        /// <summary>
        /// Tracks an exception
        /// </summary>
        public void TrackException(Exception exception, IDictionary<string, string>? properties = null)
        {
            _telemetryService.TrackException(exception, properties);
        }
        
        /// <summary>
        /// Tracks a metric
        /// </summary>
        public void TrackMetric(string metricName, double value, IDictionary<string, string>? properties = null)
        {
            _telemetryService.TrackMetric(metricName, value, properties);
        }
        
        /// <summary>
        /// Tracks a dependency
        /// </summary>
        public void TrackDependency(string dependencyName, string target, DateTimeOffset startTime, TimeSpan duration, bool success, IDictionary<string, string>? properties = null)
        {
            _telemetryService.TrackDependency(dependencyName, target, dependencyName, string.Empty, startTime, duration, success);
        }
        
        /// <summary>
        /// Tracks a request
        /// </summary>
        public void TrackRequest(string messageName, DateTimeOffset startTime, TimeSpan duration, string responseCode, bool success, IDictionary<string, string>? properties = null)
        {
            _telemetryService.TrackRequest(messageName, startTime, duration, responseCode, success);
        }
        
        /// <summary>
        /// Flushes telemetry
        /// </summary>
        public void Flush()
        {
            _telemetryService.Flush();
        }
        
        /// <summary>
        /// Disposes the adapter
        /// </summary>
        public void Dispose()
        {
            _telemetryService.Dispose();
        }
    }
}