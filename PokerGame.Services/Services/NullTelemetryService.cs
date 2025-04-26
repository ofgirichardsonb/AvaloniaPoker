using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        public void TrackEvent(string eventName, Dictionary<string, string>? properties = null)
        {
            // Do nothing
        }

        /// <summary>
        /// No-op implementation of TrackException
        /// </summary>
        public void TrackException(Exception exception, Dictionary<string, string>? properties = null)
        {
            // Do nothing
        }

        /// <summary>
        /// No-op implementation of TrackMetric
        /// </summary>
        public void TrackMetric(string name, double value, Dictionary<string, string>? properties = null)
        {
            // Do nothing
        }

        /// <summary>
        /// No-op implementation of TrackDependency
        /// </summary>
        public void TrackDependency(string dependencyType, string target, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, string resultCode, bool success)
        {
            // Do nothing
        }

        /// <summary>
        /// No-op implementation of TrackRequest
        /// </summary>
        public void TrackRequest(string name, DateTimeOffset startTime, TimeSpan duration, string responseCode, bool success)
        {
            // Do nothing
        }

        /// <summary>
        /// No-op implementation of TrackClientEvent
        /// </summary>
        public void TrackClientEvent(string name, Dictionary<string, string>? properties = null)
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
        /// No-op implementation of TrackEvent (async variant)
        /// </summary>
        public Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// No-op implementation of TrackException (async variant)
        /// </summary>
        public Task TrackExceptionAsync(Exception exception, Dictionary<string, string>? properties = null)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// No-op implementation of TrackMetric (async variant)
        /// </summary>
        public Task TrackMetricAsync(string name, double value, Dictionary<string, string>? properties = null)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// No-op implementation of TrackDependency (async variant)
        /// </summary>
        public Task TrackDependencyAsync(string dependencyType, string target, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, string resultCode, bool success)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// No-op implementation of TrackRequest (async variant)
        /// </summary>
        public Task TrackRequestAsync(string name, DateTimeOffset startTime, TimeSpan duration, string responseCode, bool success)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// No-op implementation of TrackClientEvent (async variant)
        /// </summary>
        public Task TrackClientEventAsync(string name, Dictionary<string, string>? properties = null)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// No-op implementation of FlushAsync
        /// </summary>
        public Task FlushAsync()
        {
            return Task.CompletedTask;
        }
    }
}