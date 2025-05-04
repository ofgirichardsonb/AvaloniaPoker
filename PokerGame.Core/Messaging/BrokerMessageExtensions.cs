using System;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Extension methods for BrokerMessage
    /// </summary>
    public static class BrokerMessageExtensions
    {
        /// <summary>
        /// Gets whether the message has a payload
        /// </summary>
        /// <param name="message">The message to check</param>
        /// <returns>True if the message has a payload, otherwise false</returns>
        public static bool HasPayload(this BrokerMessage message)
        {
            return !string.IsNullOrEmpty(message.SerializedPayload);
        }
        
        /// <summary>
        /// Gets the payload of the message as a JSON string
        /// </summary>
        /// <param name="message">The message to get the payload from</param>
        /// <returns>The payload as a JSON string, or null if there is no payload</returns>
        public static string GetPayloadJson(this BrokerMessage message)
        {
            return message.SerializedPayload;
        }
        
        /// <summary>
        /// Sets the payload of the message from a JSON string
        /// </summary>
        /// <param name="message">The message to set the payload for</param>
        /// <param name="json">The JSON string to set as the payload</param>
        public static void SetPayloadJson(this BrokerMessage message, string json)
        {
            message.SerializedPayload = json;
        }
    }
}