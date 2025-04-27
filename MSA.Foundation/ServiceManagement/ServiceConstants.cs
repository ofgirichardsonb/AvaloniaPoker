using System;
using System.Collections.Generic;

namespace MSA.Foundation.ServiceManagement
{
    /// <summary>
    /// Service constants for the application
    /// </summary>
    public static class ServiceConstants
    {
        /// <summary>
        /// Default prefix for static service IDs
        /// </summary>
        public const string StaticServiceIdPrefix = "static_";
        
        /// <summary>
        /// Base publisher port for the application
        /// </summary>
        public const int BasePublisherPort = 5555;
        
        /// <summary>
        /// Base subscriber port for the application
        /// </summary>
        public const int BaseSubscriberPort = 5556;
        
        /// <summary>
        /// Static Card Deck Service ID
        /// </summary>
        public const string CardDeckServiceId = "static_card_deck_service";
        
        /// <summary>
        /// Static Game Engine Service ID
        /// </summary>
        public const string GameEngineServiceId = "static_game_engine_service";
        
        /// <summary>
        /// Static Console UI Service ID
        /// </summary>
        public const string ConsoleUIServiceId = "static_console_ui_service";
        
        /// <summary>
        /// Static Broker Service ID
        /// </summary>
        public const string BrokerServiceId = "static_broker_service";
        
        /// <summary>
        /// Gets the publisher port with an offset
        /// </summary>
        public static int GetPublisherPort(int portOffset)
        {
            return BasePublisherPort + portOffset;
        }
        
        /// <summary>
        /// Gets the subscriber port with an offset
        /// </summary>
        public static int GetSubscriberPort(int portOffset)
        {
            return BaseSubscriberPort + portOffset;
        }
        
        /// <summary>
        /// Normalizes a port number to ensure it's within valid range (1024-65535)
        /// </summary>
        public static int NormalizePort(int port)
        {
            if (port < 1024)
            {
                return 1024;
            }
            
            if (port > 65535)
            {
                return 65535;
            }
            
            return port;
        }

        /// <summary>
        /// Port constants for the application
        /// </summary>
        public static class Ports
        {
            /// <summary>
            /// Base port for the central message broker
            /// </summary>
            public const int BaseCentralBrokerPort = 25555;

            /// <summary>
            /// Gets the central broker port with an optional offset
            /// </summary>
            public static int GetCentralBrokerPort(int portOffset = 0)
            {
                return BaseCentralBrokerPort + portOffset;
            }
            
            /// <summary>
            /// Default port range start for dynamic service allocation
            /// </summary>
            public const int DynamicPortRangeStart = 25600;

            /// <summary>
            /// Default port range end for dynamic service allocation
            /// </summary>
            public const int DynamicPortRangeEnd = 25700;
            
            /// <summary>
            /// Normalizes a port number by adding the base port offset if the port is below a threshold
            /// </summary>
            public static int NormalizePortWithOffset(int port, int portOffset = 0)
            {
                if (port < 1000 && port > 0)
                {
                    return DynamicPortRangeStart + port + portOffset;
                }
                
                return port + portOffset;
            }
        }

        /// <summary>
        /// Service types for the application
        /// </summary>
        public static class ServiceTypes
        {
            private static readonly Dictionary<string, string> _staticIdMappings = new Dictionary<string, string>();

            /// <summary>
            /// Registers a static ID for a service type
            /// </summary>
            public static void RegisterStaticId(string serviceType, string staticId)
            {
                _staticIdMappings[serviceType] = staticId;
            }

            /// <summary>
            /// Gets the static ID for a service type
            /// </summary>
            public static string GetStaticId(string serviceType)
            {
                if (_staticIdMappings.TryGetValue(serviceType, out var staticId))
                {
                    return staticId;
                }
                
                return StaticServiceIdPrefix + serviceType.ToLowerInvariant().Replace(" ", "_");
            }
        }
    }
}