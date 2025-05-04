using System;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;
using MSA.Foundation.ServiceManagement;
using PokerGame.Core.ServiceManagement;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Helper class for creating and managing channel-based message transport
    /// </summary>
    public static class ChannelMessageHelper
    {
        private static readonly string _channelBrokerAddress = "channel://central-broker";
        private static bool _initialized = false;
        private static readonly object _initLock = new object();
        private static IMessageTransport? _sharedTransport;
        private static MSA.Foundation.Messaging.MessageTransportConfiguration _defaultConfiguration = new MSA.Foundation.Messaging.MessageTransportConfiguration
        {
            ServiceId = "central-broker",
            AcknowledgementTimeoutMs = 5000
        };
        
        /// <summary>
        /// Gets the in-process broker address
        /// </summary>
        public static string ChannelBrokerAddress => _channelBrokerAddress;
        
        /// <summary>
        /// Creates a message transport for a service using shared channel communication
        /// </summary>
        /// <param name="serviceId">The identifier of the service</param>
        /// <returns>A message transport configured for the service</returns>
        public static IMessageTransport CreateServiceTransport(string serviceId)
        {
            EnsureInitialized();
            
            var configuration = new MSA.Foundation.Messaging.MessageTransportConfiguration
            {
                ServiceId = serviceId,
                AcknowledgementTimeoutMs = 5000
            };
            
            return MessageTransportFactory.Create(TransportType.Channel, _channelBrokerAddress, configuration);
        }
        
        /// <summary>
        /// Creates a transport for the central broker
        /// </summary>
        /// <returns>A message transport for the central broker</returns>
        public static IMessageTransport CreateBrokerTransport()
        {
            EnsureInitialized();
            return _sharedTransport ?? MessageTransportFactory.Create(TransportType.Channel, _channelBrokerAddress, _defaultConfiguration);
        }
        
        /// <summary>
        /// Ensures the helper is initialized
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_initialized)
                return;
                
            lock (_initLock)
            {
                if (_initialized)
                    return;
                    
                Console.WriteLine("Initializing ChannelMessageHelper");
                
                try
                {
                    // Create shared transport for the central broker
                    _sharedTransport = MessageTransportFactory.Create(TransportType.Channel, _channelBrokerAddress, _defaultConfiguration);
                    
                    // Start the transport
                    _sharedTransport.StartAsync().Wait();
                    
                    Console.WriteLine($"ChannelMessageHelper: Created shared transport for {_channelBrokerAddress}");
                    
                    // Register for application shutdown
                    ShutdownCoordinator.Instance.RegisterParticipant(new ShutdownParticipant(
                        "ChannelMessageHelper",
                        300, // Infrastructure priority
                        async (token) => {
                            Console.WriteLine("ChannelMessageHelper: Shutting down as part of application shutdown");
                            await CleanupAsync(token);
                        }
                    ));
                    
                    _initialized = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ChannelMessageHelper: Error during initialization: {ex.Message}");
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Performs cleanup during application shutdown
        /// </summary>
        /// <param name="token">A token to monitor for cancellation requests</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task CleanupAsync(CancellationToken token = default)
        {
            Console.WriteLine("ChannelMessageHelper: Performing cleanup");
            
            if (_sharedTransport != null)
            {
                Console.WriteLine("ChannelMessageHelper: Stopping shared transport");
                
                try
                {
                    await _sharedTransport.StopAsync();
                    (_sharedTransport as IDisposable)?.Dispose();
                    _sharedTransport = null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ChannelMessageHelper: Error during shared transport cleanup: {ex.Message}");
                }
            }
            
            _initialized = false;
            Console.WriteLine("ChannelMessageHelper: Cleanup completed");
        }
    }
}