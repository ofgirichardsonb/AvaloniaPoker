using System;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Helper class for managing NetMQ context lifetime and cleanup
    /// </summary>
    public static class NetMQContextHelper
    {
        private static readonly object _lockObject = new object();
        private static bool _cleanupScheduled = false;
        private static bool _cleanupComplete = false;
        private static readonly Timer? _cleanupTimer;
        
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
        static NetMQContextHelper()
        {
            Console.WriteLine("Initializing NetMQ context helper and shared sockets");
            
            try
            {
                // Initialize the shared publisher socket
                _sharedPublisher = new PublisherSocket();
                _sharedPublisher.Bind(_inprocBrokerAddress);
                Console.WriteLine($"Bound shared publisher to {_inprocBrokerAddress}");
                
                // Initialize the shared subscriber socket
                _sharedSubscriber = new SubscriberSocket();
                _sharedSubscriber.Connect(_inprocBrokerAddress);
                _sharedSubscriber.SubscribeToAnyTopic();
                Console.WriteLine($"Connected shared subscriber to {_inprocBrokerAddress}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing shared sockets: {ex.Message}");
            }
            
            // Create a timer that will perform cleanup if it hasn't happened by application exit
            _cleanupTimer = new Timer(_ => PerformCleanup(), null, Timeout.Infinite, Timeout.Infinite);
            
            // Register for process exit to ensure cleanup happens
            AppDomain.CurrentDomain.ProcessExit += (s, e) => {
                Console.WriteLine("Process exit detected - performing NetMQ cleanup");
                PerformCleanup();
            };
        }
        
        /// <summary>
        /// Gets the shared publisher socket for communicating with the central broker
        /// </summary>
        /// <returns>A reference to the shared publisher socket</returns>
        public static PublisherSocket GetSharedPublisher()
        {
            if (_sharedPublisher == null)
            {
                lock (_lockObject)
                {
                    if (_sharedPublisher == null && !_cleanupComplete)
                    {
                        _sharedPublisher = new PublisherSocket();
                        _sharedPublisher.Bind(_inprocBrokerAddress);
                        Console.WriteLine($"Created new shared publisher bound to {_inprocBrokerAddress}");
                    }
                }
            }
            
            return _sharedPublisher!;
        }
        
        /// <summary>
        /// Gets the shared subscriber socket for communicating with the central broker
        /// </summary>
        /// <returns>A reference to the shared subscriber socket</returns>
        public static SubscriberSocket GetSharedSubscriber()
        {
            if (_sharedSubscriber == null)
            {
                lock (_lockObject)
                {
                    if (_sharedSubscriber == null && !_cleanupComplete)
                    {
                        _sharedSubscriber = new SubscriberSocket();
                        _sharedSubscriber.Connect(_inprocBrokerAddress);
                        _sharedSubscriber.SubscribeToAnyTopic();
                        Console.WriteLine($"Created new shared subscriber connected to {_inprocBrokerAddress}");
                    }
                }
            }
            
            return _sharedSubscriber!;
        }
        
        /// <summary>
        /// Creates a new subscriber socket connected to the in-process broker
        /// </summary>
        /// <returns>A new subscriber socket</returns>
        public static SubscriberSocket CreateServiceSubscriber()
        {
            var socket = new SubscriberSocket();
            socket.Connect(_inprocBrokerAddress);
            socket.SubscribeToAnyTopic();
            return socket;
        }
        
        /// <summary>
        /// Creates a new publisher socket for services to send messages to the broker
        /// </summary>
        /// <returns>A new publisher socket</returns>
        public static PublisherSocket CreateServicePublisher()
        {
            var socket = new PublisherSocket();
            socket.Connect(_inprocBrokerAddress);
            return socket;
        }
        
        /// <summary>
        /// Schedules the cleanup to occur after giving time for sockets to close
        /// </summary>
        /// <param name="delayMs">The delay in milliseconds before cleanup</param>
        public static void ScheduleCleanup(int delayMs = 100)
        {
            lock (_lockObject)
            {
                if (_cleanupScheduled || _cleanupComplete)
                    return;
                
                _cleanupScheduled = true;
                Console.WriteLine($"NetMQ cleanup scheduled to occur in {delayMs}ms");
                
                // Schedule the cleanup to happen after a delay
                _cleanupTimer?.Change(delayMs, Timeout.Infinite);
            }
        }
        
        /// <summary>
        /// Performs the actual NetMQ context cleanup
        /// </summary>
        private static void PerformCleanup()
        {
            // Use a non-blocking approach to lock, allowing concurrent cleanup attempts to continue
            bool lockAcquired = false;
            try
            {
                Monitor.TryEnter(_lockObject, 100, ref lockAcquired);
                
                // If we couldn't get the lock in 100ms or cleanup is already done, just return
                if (!lockAcquired || _cleanupComplete)
                    return;
                
                Console.WriteLine("Performing NetMQ context cleanup");
                
                // Set the flag immediately to prevent other cleanup attempts
                _cleanupComplete = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in NetMQ cleanup lock acquisition: {ex.Message}");
                return;
            }
            
            try
            {
                // Close shared sockets before general cleanup
                try
                {
                    if (_sharedPublisher != null)
                    {
                        try { _sharedPublisher.Close(); } catch { /* ignore */ }
                        try { _sharedPublisher.Dispose(); } catch { /* ignore */ }
                        _sharedPublisher = null;
                    }
                    
                    if (_sharedSubscriber != null)
                    {
                        try { _sharedSubscriber.Close(); } catch { /* ignore */ }
                        try { _sharedSubscriber.Dispose(); } catch { /* ignore */ }
                        _sharedSubscriber = null;
                    }
                    
                    Console.WriteLine("Closed and disposed shared sockets");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error closing shared sockets: {ex.Message}");
                }
                
                // Terminate the NetMQ context with forced termination for reliability
                try
                {
                    // First try gentle cleanup
                    NetMQConfig.Cleanup(false);
                }
                catch
                {
                    try
                    {
                        // If gentle failed, force it
                        NetMQConfig.Cleanup(true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Even forced NetMQ cleanup failed: {ex.Message}");
                    }
                }
                
                Console.WriteLine("NetMQ context cleanup completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during NetMQ cleanup: {ex.Message}");
            }
            finally
            {
                try
                {
                    // Dispose the timer
                    _cleanupTimer?.Dispose();
                    
                    // Only force exit during shutdown, otherwise let program continue
                    if (Environment.HasShutdownStarted)
                    {
                        Console.WriteLine("Cleanup completed during shutdown process");
                    }
                    else
                    {
                        Console.WriteLine("NetMQ cleanup completed, program continues running");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during cleanup finalization: {ex.Message}");
                }
                
                // Release the lock if we acquired it
                if (lockAcquired)
                {
                    Monitor.Exit(_lockObject);
                }
            }
        }
    }
}