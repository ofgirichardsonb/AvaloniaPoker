using System;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using MSA.Foundation.ServiceManagement;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Enhanced helper class for managing NetMQ context lifetime and cleanup using the ShutdownCoordinator
    /// </summary>
    public static class NetMQContextHelperV2
    {
        private static readonly object _lockObject = new object();
        private static bool _initialized = false;
        private static bool _shuttingDown = false;
        
        // Application-wide shared context info
        private static readonly string _inprocBrokerAddress = "inproc://central-broker";
        private static PublisherSocket? _sharedPublisher;
        private static SubscriberSocket? _sharedSubscriber;
        
        /// <summary>
        /// Gets the in-process broker address
        /// </summary>
        public static string InProcessBrokerAddress => _inprocBrokerAddress;
        
        /// <summary>
        /// Static constructor to initialize the context helper
        /// </summary>
        static NetMQContextHelperV2()
        {
            Console.WriteLine("Initializing NetMQContextHelperV2 with ShutdownCoordinator integration");
            
            // We want to initialize sockets on demand rather than in the static constructor
            // This allows better control of the initialization sequence
            
            // Listen for shutdown notification from the coordinator
            ShutdownCoordinator.Instance.ShutdownToken.Register(() => 
            {
                Console.WriteLine("NetMQContextHelperV2: Received shutdown signal from ShutdownCoordinator");
                MarkShuttingDown();
            });
        }
        
        /// <summary>
        /// Marks the helper as shutting down, preventing new socket creation
        /// </summary>
        private static void MarkShuttingDown()
        {
            lock (_lockObject)
            {
                if (_shuttingDown)
                    return;
                    
                _shuttingDown = true;
                Console.WriteLine("NetMQContextHelperV2: Marked as shutting down");
            }
        }
        
        /// <summary>
        /// Initializes the shared sockets
        /// </summary>
        private static void InitializeIfNeeded()
        {
            if (_initialized)
                return;
                
            lock (_lockObject)
            {
                if (_initialized || _shuttingDown)
                    return;
                    
                try
                {
                    Console.WriteLine("NetMQContextHelperV2: Initializing shared sockets");
                    
                    // Initialize the shared publisher socket
                    _sharedPublisher = new PublisherSocket();
                    _sharedPublisher.Bind(_inprocBrokerAddress);
                    
                    // Register with the shutdown handler
                    NetMQShutdownHandler.Instance.TrackResource(_sharedPublisher);
                    Console.WriteLine($"NetMQContextHelperV2: Bound shared publisher to {_inprocBrokerAddress}");
                    
                    // Initialize the shared subscriber socket
                    _sharedSubscriber = new SubscriberSocket();
                    _sharedSubscriber.Connect(_inprocBrokerAddress);
                    _sharedSubscriber.SubscribeToAnyTopic();
                    
                    // Register with the shutdown handler
                    NetMQShutdownHandler.Instance.TrackResource(_sharedSubscriber);
                    Console.WriteLine($"NetMQContextHelperV2: Connected shared subscriber to {_inprocBrokerAddress}");
                    
                    _initialized = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"NetMQContextHelperV2: Error initializing shared sockets: {ex.Message}");
                    
                    // Clean up any partially initialized resources
                    if (_sharedPublisher != null)
                    {
                        try
                        {
                            NetMQShutdownHandler.Instance.UntrackResource(_sharedPublisher);
                            _sharedPublisher.Dispose();
                        }
                        catch { /* ignore */ }
                        _sharedPublisher = null;
                    }
                    
                    if (_sharedSubscriber != null)
                    {
                        try
                        {
                            NetMQShutdownHandler.Instance.UntrackResource(_sharedSubscriber);
                            _sharedSubscriber.Dispose();
                        }
                        catch { /* ignore */ }
                        _sharedSubscriber = null;
                    }
                    
                    // Re-throw to let the caller handle it
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Gets the shared publisher socket for communicating with the central broker
        /// </summary>
        /// <returns>A reference to the shared publisher socket</returns>
        public static PublisherSocket GetSharedPublisher()
        {
            // Check if shutting down first before trying initialization
            if (_shuttingDown)
                throw new InvalidOperationException("Cannot get shared publisher during shutdown");
                
            // Initialize if needed
            if (!_initialized)
            {
                InitializeIfNeeded();
            }
            
            if (_sharedPublisher == null)
                throw new InvalidOperationException("Shared publisher is not available");
                
            return _sharedPublisher;
        }
        
        /// <summary>
        /// Gets the shared subscriber socket for communicating with the central broker
        /// </summary>
        /// <returns>A reference to the shared subscriber socket</returns>
        public static SubscriberSocket GetSharedSubscriber()
        {
            // Check if shutting down first before trying initialization
            if (_shuttingDown)
                throw new InvalidOperationException("Cannot get shared subscriber during shutdown");
                
            // Initialize if needed
            if (!_initialized)
            {
                InitializeIfNeeded();
            }
            
            if (_sharedSubscriber == null)
                throw new InvalidOperationException("Shared subscriber is not available");
                
            return _sharedSubscriber;
        }
        
        /// <summary>
        /// Creates a new subscriber socket connected to the in-process broker
        /// </summary>
        /// <returns>A new subscriber socket</returns>
        public static SubscriberSocket CreateServiceSubscriber()
        {
            if (_shuttingDown)
                throw new InvalidOperationException("Cannot create service subscriber during shutdown");
                
            var socket = new SubscriberSocket();
            socket.Connect(_inprocBrokerAddress);
            socket.SubscribeToAnyTopic();
            
            // Register with the shutdown handler
            NetMQShutdownHandler.Instance.TrackResource(socket);
            
            return socket;
        }
        
        /// <summary>
        /// Creates a new publisher socket for services to send messages to the broker
        /// </summary>
        /// <returns>A new publisher socket</returns>
        public static PublisherSocket CreateServicePublisher()
        {
            if (_shuttingDown)
                throw new InvalidOperationException("Cannot create service publisher during shutdown");
                
            var socket = new PublisherSocket();
            socket.Connect(_inprocBrokerAddress);
            
            // Register with the shutdown handler
            NetMQShutdownHandler.Instance.TrackResource(socket);
            
            return socket;
        }
        
        /// <summary>
        /// Schedules immediate coordinated cleanup of NetMQ resources
        /// </summary>
        public static Task InitiateCleanupAsync()
        {
            Console.WriteLine("NetMQContextHelperV2: Initiating cleanup through ShutdownCoordinator");
            
            // Mark as shutting down to prevent new socket creation
            MarkShuttingDown();
            
            // Use the global shutdown coordinator to initiate an orderly shutdown
            return ShutdownCoordinator.BeginGlobalShutdownAsync("NetMQ cleanup requested");
        }
    }
}