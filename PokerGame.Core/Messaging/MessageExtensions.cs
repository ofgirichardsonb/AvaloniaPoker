using System;
using System.Collections.Generic;
using PokerGame.Core.Microservices;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Extension methods for Message class to aid in conversion between message types
    /// </summary>
    public static class MessageExtensions
    {
        /// <summary>
        /// Converts a Message to a NetworkMessage
        /// </summary>
        /// <param name="message">The Message to convert</param>
        /// <returns>A NetworkMessage with equivalent properties</returns>
        public static NetworkMessage ToNetworkMessage(this PokerGame.Core.Microservices.Message message)
        {
            var networkMessage = new NetworkMessage
            {
                MessageId = message.MessageId,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                Timestamp = message.Timestamp,
                InResponseTo = message.InResponseTo
            };
            
            // Convert the message type (need to cast since the enums are in different namespaces)
            networkMessage.Type = (PokerGame.Core.Messaging.MessageType)(int)message.Type;
            
            // Convert the payload if it exists
            if (!string.IsNullOrEmpty(message.Payload))
            {
                networkMessage.Payload = message.Payload;
            }
            
            return networkMessage;
        }
    }
}