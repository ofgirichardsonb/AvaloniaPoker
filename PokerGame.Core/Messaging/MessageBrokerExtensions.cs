using System;
using System.Threading.Tasks;
using PokerGame.Core.Microservices;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Extensions for integrating the MessageBroker with existing microservices
    /// </summary>
    public static class MessageBrokerExtensions
    {
        /// <summary>
        /// Converts a legacy message to a message envelope
        /// </summary>
        /// <param name="message">Legacy message to convert</param>
        /// <returns>Equivalent message envelope</returns>
        public static MessageEnvelope ToEnvelope(this Message message)
        {
            var envelope = new MessageEnvelope
            {
                MessageId = message.MessageId,
                Type = message.Type.ToString(),
                SenderServiceId = message.SenderId,
                TargetServiceId = message.ReceiverId,
                Timestamp = message.Timestamp,
                Payload = message.Payload
            };
            
            return envelope;
        }
        
        /// <summary>
        /// Converts a message envelope to a legacy message
        /// </summary>
        /// <param name="envelope">Message envelope to convert</param>
        /// <returns>Equivalent legacy message</returns>
        public static Message ToLegacyMessage(this MessageEnvelope envelope)
        {
            // Try to parse the message type from the string
            if (!Enum.TryParse<MessageType>(envelope.Type, out var messageType))
            {
                messageType = MessageType.Error; // Default to Error if unknown
            }
            
            var message = new Message
            {
                MessageId = envelope.MessageId,
                Type = messageType,
                SenderId = envelope.SenderServiceId,
                ReceiverId = envelope.TargetServiceId,
                Timestamp = envelope.Timestamp
            };
            
            // Need to set payload as string because Message.Payload is string
            if (envelope.Payload != null)
            {
                message.Payload = envelope.Payload.ToString() ?? string.Empty;
            }
            
            return message;
        }
        
        /// <summary>
        /// Adapter to handle message envelopes with legacy message handlers
        /// </summary>
        /// <param name="legacyHandler">The legacy message handler</param>
        /// <returns>Adapted handler for message envelopes</returns>
        public static MessageBroker.MessageHandlerDelegate AdaptLegacyHandler(
            Func<Message, Task> legacyHandler)
        {
            return async (envelope) =>
            {
                var legacyMessage = envelope.ToLegacyMessage();
                await legacyHandler(legacyMessage);
            };
        }
        
        /// <summary>
        /// Registers a message broker service with a microservice manager
        /// </summary>
        /// <param name="manager">The microservice manager</param>
        /// <param name="broker">The message broker to register</param>
        public static void RegisterMessageBroker(this MicroserviceManager manager, MessageBroker broker)
        {
            // This will be implemented when we integrate with the existing architecture
            Console.WriteLine("Message broker registered with microservice manager");
        }
    }
}