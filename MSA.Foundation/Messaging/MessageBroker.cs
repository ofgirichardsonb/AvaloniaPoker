using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MSA.Foundation.Messaging
{
    /// <summary>
    /// Message broker implementation using the socket communication adapter
    /// </summary>
    public class MessageBroker : IMessageBroker
    {
        private readonly SocketCommunicationAdapter _socketAdapter;
        private readonly ConcurrentDictionary<string, Subscription> _subscriptions;
        private readonly string _brokerId;
        private bool _isRunning;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBroker"/> class
        /// </summary>
        /// <param name="address">The address to bind to</param>
        /// <param name="port">The port to bind to</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        public MessageBroker(string address = "127.0.0.1", int port = 25555, bool verbose = false)
        {
            _socketAdapter = new SocketCommunicationAdapter(address, port, verbose);
            _subscriptions = new ConcurrentDictionary<string, Subscription>();
            _brokerId = $"MessageBroker_{Guid.NewGuid()}";
            _isRunning = false;
        }
        
        /// <summary>
        /// Starts the message broker
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                return;
            }
            
            _socketAdapter.Start();
            _socketAdapter.SubscribeAll(OnMessageReceived);
            _isRunning = true;
        }
        
        /// <summary>
        /// Stops the message broker
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }
            
            _socketAdapter.Stop();
            _isRunning = false;
        }
        
        /// <summary>
        /// Publishes a message to the broker
        /// </summary>
        /// <param name="message">The message to publish</param>
        /// <returns>True if the message was published successfully, otherwise false</returns>
        public bool PublishMessage(Message message)
        {
            if (!_isRunning)
            {
                return false;
            }
            
            string topic = message.MessageType.ToString();
            string payload = message.ToJson();
            
            return _socketAdapter.SendMessage(topic, payload);
        }
        
        /// <summary>
        /// Publishes a message to the broker asynchronously
        /// </summary>
        /// <param name="message">The message to publish</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public Task<bool> PublishMessageAsync(Message message)
        {
            return Task.FromResult(PublishMessage(message));
        }
        
        /// <summary>
        /// Subscribes to messages with a callback
        /// </summary>
        /// <param name="messageType">The message type to subscribe to</param>
        /// <param name="callback">The callback to invoke when a message is received</param>
        /// <returns>A subscription ID that can be used to unsubscribe</returns>
        public string Subscribe(MessageType messageType, Action<Message> callback)
        {
            string subscriptionId = Guid.NewGuid().ToString();
            _subscriptions[subscriptionId] = new Subscription(messageType, callback);
            return subscriptionId;
        }
        
        /// <summary>
        /// Subscribes to messages with an asynchronous callback
        /// </summary>
        /// <param name="messageType">The message type to subscribe to</param>
        /// <param name="callback">The callback to invoke when a message is received</param>
        /// <returns>A subscription ID that can be used to unsubscribe</returns>
        public string SubscribeAsync(MessageType messageType, Func<Message, Task> callback)
        {
            string subscriptionId = Guid.NewGuid().ToString();
            _subscriptions[subscriptionId] = new Subscription(messageType, m => 
            {
                // Fire and forget async
                Task.Run(async () => await callback(m));
            });
            return subscriptionId;
        }
        
        /// <summary>
        /// Subscribes to all messages with a callback
        /// </summary>
        /// <param name="callback">The callback to invoke when a message is received</param>
        /// <returns>A subscription ID that can be used to unsubscribe</returns>
        public string SubscribeAll(Action<Message> callback)
        {
            string subscriptionId = Guid.NewGuid().ToString();
            _subscriptions[subscriptionId] = new Subscription(null, callback);
            return subscriptionId;
        }
        
        /// <summary>
        /// Subscribes to all messages with an asynchronous callback
        /// </summary>
        /// <param name="callback">The callback to invoke when a message is received</param>
        /// <returns>A subscription ID that can be used to unsubscribe</returns>
        public string SubscribeAllAsync(Func<Message, Task> callback)
        {
            string subscriptionId = Guid.NewGuid().ToString();
            _subscriptions[subscriptionId] = new Subscription(null, m =>
            {
                // Fire and forget async
                Task.Run(async () => await callback(m));
            });
            return subscriptionId;
        }
        
        /// <summary>
        /// Unsubscribes from messages
        /// </summary>
        /// <param name="subscriptionId">The subscription ID</param>
        /// <returns>True if the subscription was removed, otherwise false</returns>
        public bool Unsubscribe(string subscriptionId)
        {
            return _subscriptions.TryRemove(subscriptionId, out _);
        }
        
        /// <summary>
        /// Handles received messages from the socket adapter
        /// </summary>
        /// <param name="topic">The message topic</param>
        /// <param name="payload">The message payload</param>
        private void OnMessageReceived(string topic, string payload)
        {
            try
            {
                // Parse the message
                Message? message = Message.FromJson(payload);
                
                if (message == null)
                {
                    return;
                }
                
                // Process acknowledgments if needed
                if (message.RequireAcknowledgment && !string.IsNullOrEmpty(message.ReceiverId) && 
                    (message.ReceiverId == _brokerId || message.ReceiverId == "*"))
                {
                    SendAcknowledgment(message);
                }
                
                // Notify subscribers
                foreach (var subscription in _subscriptions.Values.ToList())
                {
                    if (subscription.MessageType == null || subscription.MessageType == message.MessageType)
                    {
                        try
                        {
                            subscription.Callback(message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error in subscription callback: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sends an acknowledgment for a message
        /// </summary>
        /// <param name="message">The message to acknowledge</param>
        private void SendAcknowledgment(Message message)
        {
            Message ack = message.CreateAcknowledgment(_brokerId);
            PublishMessage(ack);
        }
        
        /// <summary>
        /// Disposes resources used by the message broker
        /// </summary>
        public void Dispose()
        {
            Stop();
            _socketAdapter.Dispose();
        }
        
        /// <summary>
        /// Represents a subscription to the message broker
        /// </summary>
        private class Subscription
        {
            /// <summary>
            /// Gets the message type to subscribe to
            /// </summary>
            public MessageType? MessageType { get; }
            
            /// <summary>
            /// Gets the callback to invoke when a message is received
            /// </summary>
            public Action<Message> Callback { get; }
            
            /// <summary>
            /// Initializes a new instance of the <see cref="Subscription"/> class
            /// </summary>
            /// <param name="messageType">The message type to subscribe to</param>
            /// <param name="callback">The callback to invoke when a message is received</param>
            public Subscription(MessageType? messageType, Action<Message> callback)
            {
                MessageType = messageType;
                Callback = callback;
            }
        }
    }
}