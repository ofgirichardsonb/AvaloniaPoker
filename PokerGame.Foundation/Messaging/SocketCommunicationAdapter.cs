using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace PokerGame.Foundation.Messaging
{
    /// <summary>
    /// Socket communication adapter using NetMQ for message transport
    /// </summary>
    public class SocketCommunicationAdapter : IDisposable
    {
        private readonly string _address;
        private readonly int _port;
        private readonly bool _verbose;
        
        private PublisherSocket? _publisherSocket;
        private SubscriberSocket? _subscriberSocket;
        private Task? _receiveTask;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly object _socketLock = new object();
        
        private readonly ConcurrentDictionary<string, Action<string, string>> _subscribers = 
            new ConcurrentDictionary<string, Action<string, string>>();
        
        /// <summary>
        /// Initializes a new instance of the <see cref="SocketCommunicationAdapter"/> class
        /// </summary>
        /// <param name="address">The address to bind to</param>
        /// <param name="port">The port to bind to</param>
        /// <param name="verbose">Whether to enable verbose logging</param>
        public SocketCommunicationAdapter(string address, int port, bool verbose = false)
        {
            _address = address;
            _port = port;
            _verbose = verbose;
        }
        
        /// <summary>
        /// Starts the socket adapter
        /// </summary>
        public void Start()
        {
            lock (_socketLock)
            {
                if (_publisherSocket != null || _subscriberSocket != null)
                {
                    LogVerbose("Socket adapter already started");
                    return;
                }
                
                try
                {
                    // Create publisher socket
                    _publisherSocket = new PublisherSocket();
                    _publisherSocket.Options.SendHighWatermark = 1000;
                    _publisherSocket.Bind($"tcp://{_address}:{_port}");
                    LogVerbose($"Publisher socket bound to tcp://{_address}:{_port}");
                    
                    // Create subscriber socket
                    _subscriberSocket = new SubscriberSocket();
                    _subscriberSocket.Options.ReceiveHighWatermark = 1000;
                    _subscriberSocket.Connect($"tcp://{_address}:{_port}");
                    _subscriberSocket.SubscribeToAnyTopic();
                    LogVerbose($"Subscriber socket connected to tcp://{_address}:{_port}");
                    
                    // Start receive task
                    _cancellationTokenSource = new CancellationTokenSource();
                    _receiveTask = Task.Run(() => ReceiveMessages(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                    
                    LogVerbose("Socket adapter started successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error starting socket adapter: {ex.Message}");
                    Dispose();
                }
            }
        }
        
        /// <summary>
        /// Stops the socket adapter
        /// </summary>
        public void Stop()
        {
            lock (_socketLock)
            {
                try
                {
                    // Cancel receive task
                    _cancellationTokenSource?.Cancel();
                    _receiveTask?.Wait(1000);
                    
                    // Dispose sockets
                    _publisherSocket?.Dispose();
                    _subscriberSocket?.Dispose();
                    
                    // Clear references
                    _publisherSocket = null;
                    _subscriberSocket = null;
                    _receiveTask = null;
                    _cancellationTokenSource = null;
                    
                    LogVerbose("Socket adapter stopped successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error stopping socket adapter: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Sends a message
        /// </summary>
        /// <param name="topic">The message topic</param>
        /// <param name="message">The message content</param>
        /// <returns>True if the message was sent successfully, otherwise false</returns>
        public bool SendMessage(string topic, string message)
        {
            lock (_socketLock)
            {
                if (_publisherSocket == null)
                {
                    LogVerbose("Cannot send message: publisher socket is null");
                    return false;
                }
                
                try
                {
                    _publisherSocket.SendMoreFrame(topic).SendFrame(message);
                    LogVerbose($"Sent message with topic '{topic}'");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending message: {ex.Message}");
                    return false;
                }
            }
        }
        
        /// <summary>
        /// Subscribes to messages with a callback
        /// </summary>
        /// <param name="topic">The topic to subscribe to</param>
        /// <param name="callback">The callback to invoke when a message is received</param>
        /// <returns>A subscription ID that can be used to unsubscribe</returns>
        public string Subscribe(string topic, Action<string, string> callback)
        {
            string subscriptionId = Guid.NewGuid().ToString();
            _subscribers[subscriptionId] = (t, m) =>
            {
                if (t == topic)
                {
                    callback(t, m);
                }
            };
            
            LogVerbose($"Added subscription {subscriptionId} for topic '{topic}'");
            return subscriptionId;
        }
        
        /// <summary>
        /// Subscribes to all messages with a callback
        /// </summary>
        /// <param name="callback">The callback to invoke when a message is received</param>
        /// <returns>A subscription ID that can be used to unsubscribe</returns>
        public string SubscribeAll(Action<string, string> callback)
        {
            string subscriptionId = Guid.NewGuid().ToString();
            _subscribers[subscriptionId] = callback;
            
            LogVerbose($"Added subscription {subscriptionId} for all topics");
            return subscriptionId;
        }
        
        /// <summary>
        /// Unsubscribes from messages
        /// </summary>
        /// <param name="subscriptionId">The subscription ID</param>
        /// <returns>True if the subscription was removed, otherwise false</returns>
        public bool Unsubscribe(string subscriptionId)
        {
            bool result = _subscribers.TryRemove(subscriptionId, out _);
            LogVerbose($"Removed subscription {subscriptionId}: {result}");
            return result;
        }
        
        /// <summary>
        /// Receives messages in a background task
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        private void ReceiveMessages(CancellationToken cancellationToken)
        {
            LogVerbose("Message receiving task started");
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_subscriberSocket == null)
                    {
                        Thread.Sleep(100);
                        continue;
                    }
                    
                    // Check if we have a message available
                    if (!_subscriberSocket.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out string? topic))
                    {
                        continue;
                    }
                    
                    // Receive message content
                    string message = _subscriberSocket.ReceiveFrameString();
                    
                    // Notify subscribers
                    foreach (var subscriber in _subscribers.Values.ToList())
                    {
                        try
                        {
                            subscriber(topic, message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error in subscriber: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"Error receiving message: {ex.Message}");
                        Thread.Sleep(100);
                    }
                }
            }
            
            LogVerbose("Message receiving task stopped");
        }
        
        /// <summary>
        /// Logs a verbose message
        /// </summary>
        /// <param name="message">The message to log</param>
        private void LogVerbose(string message)
        {
            if (_verbose)
            {
                Console.WriteLine($"[SocketAdapter] {message}");
            }
        }
        
        /// <summary>
        /// Disposes resources used by the socket adapter
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }
}