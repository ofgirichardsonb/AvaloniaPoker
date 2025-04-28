using System;

// This adapter is explicitly for transitioning from obsolete types to new types
#pragma warning disable CS0619 // Type or member is obsolete

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Provides conversion methods between SimpleMessage and NetworkMessage
    /// This adapter class helps with the migration from SimpleMessage to NetworkMessage
    /// </summary>
    public static class MessageAdapter
    {
        /// <summary>
        /// Converts a SimpleMessage to a NetworkMessage
        /// </summary>
        /// <param name="simpleMessage">The simple message to convert</param>
        /// <returns>An equivalent network message</returns>
        public static NetworkMessage ToNetworkMessage(this SimpleMessage simpleMessage)
        {
            if (simpleMessage == null)
                throw new ArgumentNullException(nameof(simpleMessage));
                
            return new NetworkMessage
            {
                MessageId = simpleMessage.MessageId,
                Timestamp = simpleMessage.Timestamp,
                SenderId = simpleMessage.SenderId,
                ReceiverId = simpleMessage.ReceiverId,
                InResponseTo = simpleMessage.InResponseTo,
                Payload = simpleMessage.Payload,
                Type = ToMessageType(simpleMessage.Type)
            };
        }
        
        /// <summary>
        /// Converts a NetworkMessage to a SimpleMessage
        /// </summary>
        /// <param name="networkMessage">The network message to convert</param>
        /// <returns>An equivalent simple message</returns>
        public static SimpleMessage ToSimpleMessage(this NetworkMessage networkMessage)
        {
            if (networkMessage == null)
                throw new ArgumentNullException(nameof(networkMessage));
                
            return new SimpleMessage
            {
                MessageId = networkMessage.MessageId,
                Timestamp = networkMessage.Timestamp,
                SenderId = networkMessage.SenderId,
                ReceiverId = networkMessage.ReceiverId,
                InResponseTo = networkMessage.InResponseTo,
                Payload = networkMessage.Payload,
                Type = ToSimpleMessageType(networkMessage.Type)
            };
        }
        
        /// <summary>
        /// Converts a SimpleMessageType to a MessageType
        /// </summary>
        /// <param name="simpleType">The simple message type to convert</param>
        /// <returns>The equivalent message type</returns>
        public static MessageType ToMessageType(SimpleMessageType simpleType)
        {
            return simpleType switch
            {
                SimpleMessageType.Heartbeat => MessageType.Heartbeat,
                SimpleMessageType.ServiceRegistration => MessageType.ServiceRegistration,
                SimpleMessageType.Acknowledgment => MessageType.Acknowledgment,
                SimpleMessageType.Error => MessageType.Error,
                SimpleMessageType.GameState => MessageType.GameState,
                SimpleMessageType.PlayerJoin => MessageType.PlayerJoin,
                SimpleMessageType.PlayerAction => MessageType.PlayerAction,
                SimpleMessageType.CardDeal => MessageType.CardDeal,
                SimpleMessageType.DeckShuffle => MessageType.DeckShuffle,
                SimpleMessageType.DeckCreate => MessageType.DeckCreate,
                SimpleMessageType.StartHand => MessageType.StartHand,
                SimpleMessageType.EndHand => MessageType.EndHand,
                SimpleMessageType.InfoMessage => MessageType.InfoMessage,
                SimpleMessageType.DebugMessage => MessageType.DebugMessage,
                _ => MessageType.Debug // Default case
            };
        }
        
        /// <summary>
        /// Converts a MessageType to a SimpleMessageType
        /// </summary>
        /// <param name="messageType">The message type to convert</param>
        /// <returns>The equivalent simple message type</returns>
        public static SimpleMessageType ToSimpleMessageType(MessageType messageType)
        {
            return messageType switch
            {
                MessageType.Heartbeat => SimpleMessageType.Heartbeat,
                MessageType.ServiceRegistration => SimpleMessageType.ServiceRegistration,
                MessageType.Acknowledgment => SimpleMessageType.Acknowledgment, 
                MessageType.Error => SimpleMessageType.Error,
                MessageType.GameState => SimpleMessageType.GameState,
                MessageType.PlayerJoin => SimpleMessageType.PlayerJoin,
                MessageType.PlayerAction => SimpleMessageType.PlayerAction,
                MessageType.CardDeal => SimpleMessageType.CardDeal,
                MessageType.DeckShuffle => SimpleMessageType.DeckShuffle,
                MessageType.DeckCreate => SimpleMessageType.DeckCreate,
                MessageType.StartHand => SimpleMessageType.StartHand,
                MessageType.EndHand => SimpleMessageType.EndHand,
                MessageType.InfoMessage => SimpleMessageType.InfoMessage,
                MessageType.DebugMessage => SimpleMessageType.DebugMessage,
                _ => SimpleMessageType.DebugMessage // Default case
            };
        }
    }
}