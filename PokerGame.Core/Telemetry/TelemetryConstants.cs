using System;

namespace PokerGame.Core.Telemetry
{
    /// <summary>
    /// Constants for telemetry keys and categories
    /// </summary>
    public static class TelemetryConstants
    {
        // Common property keys
        public const string MessageId = "MessageId";
        public const string MessageType = "MessageType";
        public const string ServiceId = "ServiceId";
        public const string ErrorMessage = "ErrorMessage";
        
        // Event names
        public const string MessageSent = "MessageSent";
        public const string MessageReceived = "MessageReceived";
        public const string MessageAcknowledged = "MessageAcknowledged";
        public const string MessageTimeout = "MessageTimeout";
        public const string MessageRetry = "MessageRetry";
        public const string ServiceError = "ServiceError";
        
        // Metric names
        public const string MessageLatency = "MessageLatency";
    }
}