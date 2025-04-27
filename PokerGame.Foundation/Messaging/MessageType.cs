namespace PokerGame.Foundation.Messaging
{
    /// <summary>
    /// Message types for the messaging infrastructure
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// Unknown message type
        /// </summary>
        Unknown = 0,
        
        /// <summary>
        /// Service registration message
        /// </summary>
        ServiceRegistration = 1,
        
        /// <summary>
        /// Service discovery message
        /// </summary>
        ServiceDiscovery = 2,
        
        /// <summary>
        /// Command message
        /// </summary>
        Command = 3,
        
        /// <summary>
        /// Event message
        /// </summary>
        Event = 4,
        
        /// <summary>
        /// Acknowledgment message
        /// </summary>
        Acknowledgment = 5,
        
        /// <summary>
        /// Heartbeat message
        /// </summary>
        Heartbeat = 6,
        
        /// <summary>
        /// Debug message
        /// </summary>
        Debug = 7,
        
        /// <summary>
        /// Error message
        /// </summary>
        Error = 8,
        
        /// <summary>
        /// Data message
        /// </summary>
        Data = 9,
        
        /// <summary>
        /// Request message
        /// </summary>
        Request = 10,
        
        /// <summary>
        /// Response message
        /// </summary>
        Response = 11
    }
}