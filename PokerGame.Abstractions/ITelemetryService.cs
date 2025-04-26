using System;
using System.Collections.Generic;

namespace PokerGame.Abstractions
{
    /// <summary>
    /// Interface for telemetry services
    /// </summary>
    public interface ITelemetryService : IDisposable
    {
        /// <summary>
        /// Tracks a custom event
        /// </summary>
        /// <param name="eventName">The name of the event</param>
        /// <param name="properties">Optional properties to include with the event</param>
        void TrackEvent(string eventName, IDictionary<string, string>? properties = null);

        /// <summary>
        /// Tracks a request message
        /// </summary>
        /// <param name="messageName">The name of the message</param>
        /// <param name="startTime">The time when the message was sent</param>
        /// <param name="duration">The duration of the message processing</param>
        /// <param name="responseCode">The response code (if applicable)</param>
        /// <param name="success">Whether the message was processed successfully</param>
        /// <param name="properties">Optional properties to include with the request</param>
        void TrackRequest(string messageName, DateTimeOffset startTime, TimeSpan duration, string responseCode, bool success, IDictionary<string, string>? properties = null);

        /// <summary>
        /// Tracks an exception
        /// </summary>
        /// <param name="exception">The exception to track</param>
        /// <param name="properties">Optional properties to include with the exception</param>
        void TrackException(Exception exception, IDictionary<string, string>? properties = null);

        /// <summary>
        /// Tracks a metric value
        /// </summary>
        /// <param name="metricName">The name of the metric</param>
        /// <param name="value">The value of the metric</param>
        /// <param name="properties">Optional properties to include with the metric</param>
        void TrackMetric(string metricName, double value, IDictionary<string, string>? properties = null);
        
        /// <summary>
        /// Tracks a dependency call
        /// </summary>
        /// <param name="dependencyName">The name of the dependency</param>
        /// <param name="target">The target of the dependency call</param>
        /// <param name="startTime">The time when the dependency call was made</param>
        /// <param name="duration">The duration of the dependency call</param>
        /// <param name="success">Whether the dependency call was successful</param>
        /// <param name="properties">Optional properties to include with the dependency</param>
        void TrackDependency(string dependencyName, string target, DateTimeOffset startTime, TimeSpan duration, bool success, IDictionary<string, string>? properties = null);
        
        /// <summary>
        /// Flushes the telemetry client to ensure all telemetry is sent
        /// </summary>
        void Flush();
    }
}