using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.DataContracts;

namespace PokerGame.Foundation.Telemetry
{
    /// <summary>
    /// Interface for telemetry services
    /// </summary>
    public interface ITelemetryService : IDisposable
    {
        /// <summary>
        /// Initializes the telemetry service with the specified instrumentation key
        /// </summary>
        /// <param name="instrumentationKey">The Application Insights instrumentation key</param>
        /// <returns>True if initialization was successful, otherwise false</returns>
        bool Initialize(string instrumentationKey);
        
        /// <summary>
        /// Tracks an event with the specified name and properties
        /// </summary>
        /// <param name="eventName">The name of the event</param>
        /// <param name="properties">Additional properties for the event</param>
        void TrackEvent(string eventName, IDictionary<string, string>? properties = null);
        
        /// <summary>
        /// Tracks a metric with the specified name and value
        /// </summary>
        /// <param name="metricName">The name of the metric</param>
        /// <param name="value">The value of the metric</param>
        /// <param name="properties">Additional properties for the metric</param>
        void TrackMetric(string metricName, double value, IDictionary<string, string>? properties = null);
        
        /// <summary>
        /// Tracks an exception
        /// </summary>
        /// <param name="exception">The exception to track</param>
        /// <param name="properties">Additional properties for the exception</param>
        void TrackException(Exception exception, IDictionary<string, string>? properties = null);
        
        /// <summary>
        /// Tracks a request with the specified name, timestamp, duration, response code, and success status
        /// </summary>
        /// <param name="name">The name of the request</param>
        /// <param name="timestamp">The timestamp of the request</param>
        /// <param name="duration">The duration of the request</param>
        /// <param name="responseCode">The response code of the request</param>
        /// <param name="success">Whether the request was successful</param>
        void TrackRequest(string name, DateTimeOffset timestamp, TimeSpan duration, string responseCode, bool success);
        
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
        void TrackDependency(string dependencyTypeName, string target, string dependencyName, string data,
            DateTimeOffset startTime, TimeSpan duration, bool success);
        
        /// <summary>
        /// Tracks a trace message with the specified severity level and properties
        /// </summary>
        /// <param name="message">The trace message</param>
        /// <param name="severityLevel">The severity level of the trace</param>
        /// <param name="properties">Additional properties for the trace</param>
        void TrackTrace(string message, SeverityLevel severityLevel = SeverityLevel.Information, IDictionary<string, string>? properties = null);
        
        /// <summary>
        /// Flushes telemetry data to the server
        /// </summary>
        void Flush();
        
        /// <summary>
        /// Flushes telemetry data asynchronously to the server
        /// </summary>
        Task FlushAsync();
    }
}