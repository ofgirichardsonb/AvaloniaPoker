using System;
using System.Collections.Generic;
using System.Diagnostics;
using MessageBroker;

namespace PokerGame.Services
{
    /// <summary>
    /// Provides telemetry integration for the message broker
    /// </summary>
    public class MessageBrokerTelemetryHandler
    {
        private readonly TelemetryService _telemetryService;
        private readonly Dictionary<string, Stopwatch> _messageTimers = new Dictionary<string, Stopwatch>();
        
        /// <summary>
        /// Creates a new instance of the MessageBrokerTelemetryHandler
        /// </summary>
        /// <param name="telemetryService">The telemetry service to use</param>
        public MessageBrokerTelemetryHandler(TelemetryService telemetryService)
        {
            _telemetryService = telemetryService;
        }
        
        /// <summary>
        /// Handles message sending telemetry
        /// </summary>
        /// <param name="message">The message being sent</param>
        /// <param name="client">The client sending the message</param>
        public void OnMessageSend(BrokerMessage message, BrokerClient client)
        {
            if (message == null) return;
            
            var properties = new Dictionary<string, string>
            {
                [TelemetryConstants.MessageId] = message.MessageId,
                [TelemetryConstants.MessageType] = message.Type.ToString(),
                [TelemetryConstants.ServiceId] = client.ClientId,
                ["ServiceName"] = client.ClientName,
                ["ServiceType"] = client.ClientType,
                ["RequiresAcknowledgment"] = message.RequiresAcknowledgment.ToString(),
                ["HasReceiver"] = (!string.IsNullOrEmpty(message.ReceiverId)).ToString()
            };
            
            if (!string.IsNullOrEmpty(message.ReceiverId))
            {
                properties["ReceiverId"] = message.ReceiverId;
            }
            
            _telemetryService.TrackEvent(TelemetryConstants.MessageSent, properties);
            
            // Start timing for this message if it requires acknowledgment
            if (message.RequiresAcknowledgment)
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                lock (_messageTimers)
                {
                    _messageTimers[message.MessageId] = stopwatch;
                }
            }
        }
        
        /// <summary>
        /// Handles message received telemetry
        /// </summary>
        /// <param name="message">The message being received</param>
        /// <param name="client">The client receiving the message</param>
        public void OnMessageReceived(BrokerMessage message, BrokerClient client)
        {
            if (message == null) return;
            
            var properties = new Dictionary<string, string>
            {
                [TelemetryConstants.MessageId] = message.MessageId,
                [TelemetryConstants.MessageType] = message.Type.ToString(),
                [TelemetryConstants.ServiceId] = client.ClientId,
                ["ServiceName"] = client.ClientName,
                ["ServiceType"] = client.ClientType,
                ["SenderId"] = message.SenderId,
                ["RequiresAcknowledgment"] = message.RequiresAcknowledgment.ToString()
            };
            
            if (!string.IsNullOrEmpty(message.InResponseTo))
            {
                properties["InResponseTo"] = message.InResponseTo;
            }
            
            _telemetryService.TrackEvent(TelemetryConstants.MessageReceived, properties);
            
            // If this is an acknowledgment, stop timing the original message
            if (message.Type == BrokerMessageType.Acknowledgment && !string.IsNullOrEmpty(message.InResponseTo))
            {
                string originalMessageId = message.InResponseTo;
                Stopwatch? stopwatch = null;
                
                lock (_messageTimers)
                {
                    if (_messageTimers.TryGetValue(originalMessageId, out stopwatch))
                    {
                        _messageTimers.Remove(originalMessageId);
                    }
                }
                
                if (stopwatch != null)
                {
                    stopwatch.Stop();
                    
                    // Track the acknowledgment latency
                    var acknowledgmentProperties = new Dictionary<string, string>
                    {
                        [TelemetryConstants.MessageId] = originalMessageId,
                        ["AcknowledgmentId"] = message.MessageId,
                        ["AcknowledgedBy"] = message.SenderId
                    };
                    
                    _telemetryService.TrackMetric(
                        TelemetryConstants.MessageLatency, 
                        stopwatch.ElapsedMilliseconds, 
                        acknowledgmentProperties);
                    
                    _telemetryService.TrackEvent(
                        TelemetryConstants.MessageAcknowledged, 
                        acknowledgmentProperties);
                }
            }
        }
        
        /// <summary>
        /// Handles message timeout telemetry
        /// </summary>
        /// <param name="message">The message that timed out</param>
        /// <param name="retryCount">The current retry count</param>
        /// <param name="maxRetries">The maximum number of retries</param>
        public void OnMessageTimeout(BrokerMessage message, int retryCount, int maxRetries)
        {
            if (message == null) return;
            
            var properties = new Dictionary<string, string>
            {
                [TelemetryConstants.MessageId] = message.MessageId,
                [TelemetryConstants.MessageType] = message.Type.ToString(),
                ["RetryCount"] = retryCount.ToString(),
                ["MaxRetries"] = maxRetries.ToString(),
                ["SenderId"] = message.SenderId,
                ["ReceiverId"] = message.ReceiverId ?? string.Empty,
                ["IsMaxRetries"] = (retryCount >= maxRetries).ToString()
            };
            
            _telemetryService.TrackEvent(TelemetryConstants.MessageTimeout, properties);
            
            // Track a metric for the timeout as well
            Stopwatch? stopwatch = null;
            lock (_messageTimers)
            {
                if (_messageTimers.TryGetValue(message.MessageId, out stopwatch))
                {
                    if (retryCount >= maxRetries)
                    {
                        _messageTimers.Remove(message.MessageId);
                    }
                }
            }
            
            if (stopwatch != null)
            {
                _telemetryService.TrackMetric(
                    "MessageTimeoutDuration", 
                    stopwatch.ElapsedMilliseconds, 
                    properties);
            }
        }
        
        /// <summary>
        /// Handles message retry telemetry
        /// </summary>
        /// <param name="message">The message being retried</param>
        /// <param name="retryCount">The current retry count</param>
        /// <param name="maxRetries">The maximum number of retries</param>
        public void OnMessageRetry(BrokerMessage message, int retryCount, int maxRetries)
        {
            if (message == null) return;
            
            var properties = new Dictionary<string, string>
            {
                [TelemetryConstants.MessageId] = message.MessageId,
                [TelemetryConstants.MessageType] = message.Type.ToString(),
                ["RetryCount"] = retryCount.ToString(),
                ["MaxRetries"] = maxRetries.ToString(),
                ["SenderId"] = message.SenderId,
                ["ReceiverId"] = message.ReceiverId ?? string.Empty
            };
            
            _telemetryService.TrackEvent(TelemetryConstants.MessageRetry, properties);
        }
        
        /// <summary>
        /// Handles service error telemetry
        /// </summary>
        /// <param name="client">The client that encountered the error</param>
        /// <param name="errorMessage">The error message</param>
        /// <param name="exception">The exception that occurred</param>
        public void OnServiceError(BrokerClient client, string errorMessage, Exception? exception = null)
        {
            var properties = new Dictionary<string, string>
            {
                [TelemetryConstants.ServiceId] = client.ClientId,
                ["ServiceName"] = client.ClientName,
                ["ServiceType"] = client.ClientType,
                [TelemetryConstants.ErrorMessage] = errorMessage
            };
            
            if (exception != null)
            {
                _telemetryService.TrackException(exception, properties);
            }
            else
            {
                _telemetryService.TrackEvent(TelemetryConstants.ServiceError, properties);
            }
        }
    }
}