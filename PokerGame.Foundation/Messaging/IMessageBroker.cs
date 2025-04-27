using System;
using System.Threading.Tasks;

namespace PokerGame.Foundation.Messaging
{
    /// <summary>
    /// Interface for message brokers
    /// </summary>
    public interface IMessageBroker : IDisposable
    {
        /// <summary>
        /// Starts the message broker
        /// </summary>
        void Start();
        
        /// <summary>
        /// Stops the message broker
        /// </summary>
        void Stop();
        
        /// <summary>
        /// Publishes a message to the broker
        /// </summary>
        /// <param name="message">The message to publish</param>
        /// <returns>True if the message was published successfully, otherwise false</returns>
        bool PublishMessage(Message message);
        
        /// <summary>
        /// Publishes a message to the broker asynchronously
        /// </summary>
        /// <param name="message">The message to publish</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task<bool> PublishMessageAsync(Message message);
        
        /// <summary>
        /// Subscribes to messages with a callback
        /// </summary>
        /// <param name="messageType">The message type to subscribe to</param>
        /// <param name="callback">The callback to invoke when a message is received</param>
        /// <returns>A subscription ID that can be used to unsubscribe</returns>
        string Subscribe(MessageType messageType, Action<Message> callback);
        
        /// <summary>
        /// Subscribes to messages with an asynchronous callback
        /// </summary>
        /// <param name="messageType">The message type to subscribe to</param>
        /// <param name="callback">The callback to invoke when a message is received</param>
        /// <returns>A subscription ID that can be used to unsubscribe</returns>
        string SubscribeAsync(MessageType messageType, Func<Message, Task> callback);
        
        /// <summary>
        /// Subscribes to all messages with a callback
        /// </summary>
        /// <param name="callback">The callback to invoke when a message is received</param>
        /// <returns>A subscription ID that can be used to unsubscribe</returns>
        string SubscribeAll(Action<Message> callback);
        
        /// <summary>
        /// Subscribes to all messages with an asynchronous callback
        /// </summary>
        /// <param name="callback">The callback to invoke when a message is received</param>
        /// <returns>A subscription ID that can be used to unsubscribe</returns>
        string SubscribeAllAsync(Func<Message, Task> callback);
        
        /// <summary>
        /// Unsubscribes from messages
        /// </summary>
        /// <param name="subscriptionId">The subscription ID</param>
        /// <returns>True if the subscription was removed, otherwise false</returns>
        bool Unsubscribe(string subscriptionId);
    }
}