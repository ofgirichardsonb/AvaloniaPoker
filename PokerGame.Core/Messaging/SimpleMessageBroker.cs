using System;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Compatibility class that redirects to SocketCommunicationAdapter
    /// This class is maintained for backward compatibility and will be removed in a future release
    /// </summary>
    [Obsolete("Use SocketCommunicationAdapter instead. This class will be removed in a future release.")]
    public class SimpleMessageBroker : SocketCommunicationAdapter
    {
        /// <summary>
        /// Creates a new simple message broker
        /// </summary>
        /// <param name="serviceId">The unique ID of the service using this broker</param>
        /// <param name="publisherPort">The port on which this broker will publish messages</param>
        /// <param name="subscriberPort">The port on which this broker will subscribe to messages</param>
        /// <param name="verbose">Whether to log verbose messages</param>
        public SimpleMessageBroker(string serviceId, int publisherPort, int subscriberPort, bool verbose = false)
            : base(serviceId, publisherPort, subscriberPort, verbose)
        {
        }
        
        /// <summary>
        /// Creates a new simple message broker with an execution context
        /// </summary>
        /// <param name="serviceId">The unique ID of the service using this broker</param>
        /// <param name="publisherPort">The port on which this broker will publish messages</param>
        /// <param name="subscriberPort">The port on which this broker will subscribe to messages</param>
        /// <param name="executionContext">The execution context for this broker</param>
        /// <param name="verbose">Whether to log verbose messages</param>
        public SimpleMessageBroker(string serviceId, int publisherPort, int subscriberPort, ExecutionContext executionContext, bool verbose = false)
            : base(serviceId, publisherPort, subscriberPort, executionContext, verbose)
        {
        }
    }
}