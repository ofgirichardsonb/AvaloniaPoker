using System;

namespace PokerGame.Core.ServiceManagement
{
    /// <summary>
    /// Constants for service configuration and ports to ensure consistency across the application
    /// </summary>
    public static class ServiceConstants
    {
        /// <summary>
        /// Base port constants for various services
        /// </summary>
        public static class Ports
        {
            /// <summary>
            /// Base port for the central message broker
            /// </summary>
            public const int CentralBrokerBasePort = 25555;
            
            /// <summary>
            /// Base publisher port for the Game Engine service
            /// </summary>
            public const int GameEnginePublisherBasePort = 25556;
            
            /// <summary>
            /// Base subscriber port for the Game Engine service
            /// </summary>
            public const int GameEngineSubscriberBasePort = 25557;
            
            /// <summary>
            /// Base publisher port for the Console UI service
            /// </summary>
            public const int ConsoleUIPublisherBasePort = 25558;
            
            /// <summary>
            /// Base publisher port for the Card Deck service
            /// </summary>
            public const int CardDeckPublisherBasePort = 25559;
            
            /// <summary>
            /// Gets the actual port number with the specified offset
            /// </summary>
            /// <param name="basePort">The base port number</param>
            /// <param name="offset">The port offset to apply</param>
            /// <returns>The calculated port number</returns>
            public static int GetPort(int basePort, int offset)
            {
                return basePort + offset;
            }
            
            /// <summary>
            /// Gets the Game Engine publisher port with the specified offset
            /// </summary>
            /// <param name="offset">Port offset</param>
            /// <returns>The Game Engine publisher port</returns>
            public static int GetGameEnginePublisherPort(int offset) => GetPort(GameEnginePublisherBasePort, offset);
            
            /// <summary>
            /// Gets the Game Engine subscriber port with the specified offset
            /// </summary>
            /// <param name="offset">Port offset</param>
            /// <returns>The Game Engine subscriber port</returns>
            public static int GetGameEngineSubscriberPort(int offset) => GetPort(GameEngineSubscriberBasePort, offset);
            
            /// <summary>
            /// Gets the Console UI publisher port with the specified offset
            /// </summary>
            /// <param name="offset">Port offset</param>
            /// <returns>The Console UI publisher port</returns>
            public static int GetConsoleUIPublisherPort(int offset) => GetPort(ConsoleUIPublisherBasePort, offset);
            
            /// <summary>
            /// Gets the Card Deck publisher port with the specified offset
            /// </summary>
            /// <param name="offset">Port offset</param>
            /// <returns>The Card Deck publisher port</returns>
            public static int GetCardDeckPublisherPort(int offset) => GetPort(CardDeckPublisherBasePort, offset);
            
            /// <summary>
            /// Gets the Console UI subscriber port with the specified offset
            /// Always subscribes to the Game Engine publisher port
            /// </summary>
            /// <param name="offset">Port offset</param>
            /// <returns>The Console UI subscriber port</returns>
            public static int GetConsoleUISubscriberPort(int offset) => GetGameEnginePublisherPort(offset);
            
            /// <summary>
            /// Gets the Card Deck subscriber port with the specified offset
            /// Always subscribes to the Game Engine publisher port
            /// </summary>
            /// <param name="offset">Port offset</param>
            /// <returns>The Card Deck subscriber port</returns>
            public static int GetCardDeckSubscriberPort(int offset) => GetGameEnginePublisherPort(offset);
            
            /// <summary>
            /// Gets the Central Broker port with the specified offset
            /// </summary>
            /// <param name="offset">Port offset</param>
            /// <returns>The Central Broker port</returns>
            public static int GetCentralBrokerPort(int offset) => GetPort(CentralBrokerBasePort, offset);
        }
        
        /// <summary>
        /// Service type constants to ensure consistency across the application
        /// </summary>
        public static class ServiceTypes
        {
            /// <summary>
            /// Game Engine service type identifier
            /// </summary>
            public const string GameEngine = "GameEngine";
            
            /// <summary>
            /// Card Deck service type identifier
            /// </summary>
            public const string CardDeck = "CardDeck";
            
            /// <summary>
            /// Console UI service type identifier
            /// </summary>
            public const string ConsoleUI = "PlayerUI";
        }
        
        /// <summary>
        /// Constants related to service discovery and registration
        /// </summary>
        public static class Discovery
        {
            /// <summary>
            /// Maximum number of attempts to find a service during discovery
            /// </summary>
            public const int MaxServiceDiscoveryAttempts = 30;
            
            /// <summary>
            /// Delay between service discovery attempts in milliseconds
            /// </summary>
            public const int ServiceDiscoveryDelayMs = 1000;
            
            /// <summary>
            /// Interval between service discovery broadcasts in attempts
            /// </summary>
            public const int ServiceDiscoveryBroadcastInterval = 5;
            
            /// <summary>
            /// Standard port offsets used in the system
            /// </summary>
            public static readonly int[] StandardPortOffsets = new[] { 0, 200, 5000 };
            
            /// <summary>
            /// Attempts to normalize a port offset to one of the standard values
            /// </summary>
            /// <param name="offset">The provided port offset</param>
            /// <returns>The normalized port offset</returns>
            public static int NormalizePortOffset(int offset)
            {
                // If the offset matches a standard offset, return it
                foreach (var standardOffset in StandardPortOffsets)
                {
                    if (offset == standardOffset)
                    {
                        return offset;
                    }
                }
                
                // If not a standard offset, use the closest one
                // This helps with compatibility when different offsets are used
                int closestOffset = StandardPortOffsets[0];
                int minDifference = Math.Abs(offset - closestOffset);
                
                foreach (var standardOffset in StandardPortOffsets)
                {
                    int difference = Math.Abs(offset - standardOffset);
                    if (difference < minDifference)
                    {
                        minDifference = difference;
                        closestOffset = standardOffset;
                    }
                }
                
                return closestOffset;
            }
        }
    }
}