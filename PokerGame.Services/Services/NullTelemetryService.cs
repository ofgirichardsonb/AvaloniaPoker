using System;
using System.Collections.Generic;
using PokerGame.Abstractions;

namespace PokerGame.Services
{
    /// <summary>
    /// Implementation of ITelemetryService that does nothing (null object pattern)
    /// Used as a fallback when the real telemetry service cannot be initialized
    /// </summary>
    public class NullTelemetryService : ITelemetryService
    {
        /// <summary>
        /// Gets whether telemetry is enabled
        /// </summary>
        public bool IsEnabled => false;

        /// <summary>
        /// Gets the name of the telemetry service (always "NullTelemetry")
        /// </summary>
        public string Name => "NullTelemetry";

        /// <summary>
        /// No-op implementation of TrackEvent
        /// </summary>
        public void TrackEvent(string eventName, IDictionary<string, string>? properties = null)
        {
            // Do nothing
        }

        /// <summary>
        /// No-op implementation of TrackException
        /// </summary>
        public void TrackException(Exception exception, IDictionary<string, string>? properties = null)
        {
            // Do nothing
        }

        /// <summary>
        /// No-op implementation of TrackMetric
        /// </summary>
        public void TrackMetric(string metricName, double value, IDictionary<string, string>? properties = null)
        {
            // Do nothing
        }

        /// <summary>
        /// No-op implementation of TrackDependency
        /// </summary>
        public void TrackDependency(string dependencyName, string target, DateTimeOffset startTime, TimeSpan duration, bool success, IDictionary<string, string>? properties = null)
        {
            // Do nothing
        }

        /// <summary>
        /// No-op implementation of TrackRequest
        /// </summary>
        public void TrackRequest(string messageName, DateTimeOffset startTime, TimeSpan duration, string responseCode, bool success, IDictionary<string, string>? properties = null)
        {
            // Do nothing
        }

        /// <summary>
        /// No-op implementation of Flush
        /// </summary>
        public void Flush()
        {
            // Do nothing
        }

        /// <summary>
        /// No-op implementation of Dispose
        /// </summary>
        public void Dispose()
        {
            // Do nothing
        }
    }
}