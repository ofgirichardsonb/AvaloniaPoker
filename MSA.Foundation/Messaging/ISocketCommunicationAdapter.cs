using System;

namespace MSA.Foundation.Messaging
{
    /// <summary>
    /// Interface for socket communication adapters
    /// </summary>
    public interface ISocketCommunicationAdapter : IDisposable
    {
        /// <summary>
        /// Starts the socket adapter
        /// </summary>
        void Start();
        
        /// <summary>
        /// Stops the socket adapter
        /// </summary>
        void Stop();
        
        /// <summary>
        /// Sends a message over the socket
        /// </summary>
        /// <param name="topic">The message topic</param>
        /// <param name="message">The message content</param>
        /// <returns>True if the message was sent successfully, otherwise false</returns>
        bool SendMessage(string topic, string message);
        
        /// <summary>
        /// Subscribes to messages on the specified topic
        /// </summary>
        /// <param name="topic">The topic to subscribe to</param>
        /// <param name="callback">The callback to invoke when a message is received</param>
        /// <returns>A subscription ID that can be used to unsubscribe</returns>
        string Subscribe(string topic, Action<string, string> callback);
        
        /// <summary>
        /// Subscribes to all messages
        /// </summary>
        /// <param name="callback">The callback to invoke when a message is received</param>
        /// <returns>A subscription ID that can be used to unsubscribe</returns>
        string SubscribeAll(Action<string, string> callback);
        
        /// <summary>
        /// Unsubscribes from messages
        /// </summary>
        /// <param name="subscriptionId">The subscription ID</param>
        /// <returns>True if the subscription was removed, otherwise false</returns>
        bool Unsubscribe(string subscriptionId);
    }
}