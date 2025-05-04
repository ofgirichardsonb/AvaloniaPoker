using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using MSA.Foundation.ServiceManagement;
using MSA.Foundation.Messaging;
using PokerGame.Core.ServiceManagement;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Helper class for managing channel-based context lifetime and cleanup
    /// Replaces NetMQContextHelper with a pure .NET implementation
    /// </summary>
    public static class ChannelContextHelper
    {
        private static readonly object _lockObject = new object();
        private static bool _cleanupScheduled = false;
        private static bool _cleanupComplete = false;
        private static readonly ManualResetEvent _cleanupEvent = new ManualResetEvent(false);
        
        // Application-wide shared context info
        private static readonly string _channelBrokerAddress = "channel://central-broker";
        private static readonly Dictionary<string, Channel<IMessage>> _channels 
            = new Dictionary<string, Channel<IMessage>>();
        
        /// <summary>
        /// Gets the in-process broker address
        /// </summary>
        public static string ChannelBrokerAddress => _channelBrokerAddress;
        
        /// <summary>
        /// Static constructor to initialize the context helper
        /// </summary>
        static ChannelContextHelper()
        {
            Console.WriteLine("Initializing Channel context helper and shared channel infrastructure");
            
            // Register for process exit to ensure cleanup
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            
            // Register for application domain unload to ensure cleanup before unloading
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
            
            // Register with the shutdown coordinator
            ShutdownCoordinator.Instance.RegisterParticipant(new ShutdownParticipant(
                "ChannelContextHelper",
                100, // High priority for early shutdown
                async (token) => {
                    Console.WriteLine("ChannelContextHelper: Shutting down as part of application shutdown");
                    PerformCleanup();
                }
            ));
        }

        /// <summary>
        /// Handler for process exit events
        /// </summary>
        private static void OnProcessExit(object? sender, EventArgs e)
        {
            Console.WriteLine("Process exit detected - performing Channel context cleanup");
            ScheduleCleanup(0);
        }
        
        /// <summary>
        /// Handler for domain unload events
        /// </summary>
        private static void OnDomainUnload(object? sender, EventArgs e)
        {
            Console.WriteLine("AppDomain unload detected - performing Channel context cleanup");
            ScheduleCleanup(0);
        }
        
        /// <summary>
        /// Schedules cleanup to occur after the specified delay
        /// </summary>
        /// <param name="delayMs">The delay in milliseconds before cleanup</param>
        public static void ScheduleCleanup(int delayMs = 100)
        {
            bool lockAcquired = false;
            try
            {
                Monitor.TryEnter(_lockObject, 100, ref lockAcquired);
                
                if (!lockAcquired)
                {
                    Console.WriteLine("Could not acquire lock for scheduling cleanup");
                    return;
                }
                
                // If cleanup is already scheduled or completed, don't schedule it again
                if (_cleanupScheduled || _cleanupComplete)
                {
                    return;
                }
                
                _cleanupScheduled = true;
                
                // Perform immediate cleanup
                Task.Run(() => {
                    Thread.Sleep(delayMs);
                    PerformCleanup();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scheduling Channel cleanup: {ex.Message}");
            }
            finally
            {
                if (lockAcquired)
                {
                    Monitor.Exit(_lockObject);
                }
            }
        }
        
        /// <summary>
        /// Performs the actual channel context cleanup
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
                
                Console.WriteLine("Performing Channel context cleanup");
                
                // Set the flag immediately to prevent other cleanup attempts
                _cleanupComplete = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Channel cleanup lock acquisition: {ex.Message}");
                return;
            }
            
            try
            {
                // Close all channels
                try
                {
                    foreach (var channel in _channels)
                    {
                        try
                        {
                            channel.Value.Writer.Complete();
                            Console.WriteLine($"Completed writer for channel: {channel.Key}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error completing channel writer for {channel.Key}: {ex.Message}");
                        }
                    }
                    
                    // Clear the channel dictionary
                    _channels.Clear();
                    
                    Console.WriteLine("Closed and completed all channels");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error closing channels: {ex.Message}");
                }
                
                // Signal cleanup completion
                _cleanupEvent.Set();
                
                Console.WriteLine("Channel context cleanup completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Channel cleanup: {ex.Message}");
            }
            finally
            {
                try
                {
                    // Only force exit during shutdown, otherwise let program continue
                    if (Environment.HasShutdownStarted)
                    {
                        Console.WriteLine("Cleanup completed during shutdown process");
                    }
                    else
                    {
                        Console.WriteLine("Channel cleanup completed, program continues running");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during Channel cleanup finalization: {ex.Message}");
                }
                
                if (lockAcquired)
                {
                    Monitor.Exit(_lockObject);
                }
            }
        }
        
        /// <summary>
        /// Gets or creates a channel for the specified service ID
        /// </summary>
        /// <param name="serviceId">The service ID to get a channel for</param>
        /// <returns>A channel for the specified service</returns>
        public static Channel<IMessage> GetOrCreateServiceChannel(string serviceId)
        {
            lock (_lockObject)
            {
                if (_channels.TryGetValue(serviceId, out var channel))
                {
                    return channel;
                }
                
                // Create a new channel for this service
                var newChannel = Channel.CreateUnbounded<IMessage>(new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });
                
                _channels[serviceId] = newChannel;
                
                Console.WriteLine($"Created new channel for service: {serviceId}");
                
                return newChannel;
            }
        }
        
        /// <summary>
        /// Gets the broadcast channel for distributing messages to all services
        /// </summary>
        /// <returns>The broadcast channel</returns>
        public static Channel<IMessage> GetBroadcastChannel()
        {
            return GetOrCreateServiceChannel("broadcast");
        }
        
        /// <summary>
        /// Creates a message transport for the specified service
        /// </summary>
        /// <param name="serviceId">The service ID to create a transport for</param>
        /// <returns>A configured message transport</returns>
        public static IMessageTransport CreateServiceTransport(string serviceId)
        {
            if (string.IsNullOrEmpty(serviceId))
                throw new ArgumentException("Service ID cannot be null or empty", nameof(serviceId));
                
            var config = new MSA.Foundation.Messaging.MessageTransportConfiguration
            {
                ServiceId = serviceId
                // Note: The properties EnableBroadcast and AcknowledgementRequired 
                // are handled internally by ChannelMessageTransport in our implementation
            };
            
            return new ChannelMessageTransport(config);
        }
    }
}