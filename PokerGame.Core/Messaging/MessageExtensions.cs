using System;
using System.Collections.Generic;
using System.Text.Json;
using PokerGame.Core.Microservices;
using MicroservicesRegistration = PokerGame.Core.Microservices.ServiceRegistrationPayload;

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
            
            // Map the message type explicitly instead of using a straight cast
            switch (message.Type)
            {
                case Microservices.MessageType.StartHand:
                    networkMessage.Type = MessageType.StartHand;
                    break;
                case Microservices.MessageType.DeckShuffled:
                    networkMessage.Type = MessageType.DeckShuffled;
                    break;
                default:
                    // For other message types, a direct cast might work but could be risky
                    // Using a string-based mapping for safer conversion
                    networkMessage.Type = Enum.Parse<MessageType>(message.Type.ToString());
                    break;
            }
            
            // Convert the payload based on message type
            if (message.Type == Microservices.MessageType.ServiceRegistration)
            {
                try
                {
                    // Special handling for ServiceRegistration payload
                    var originalPayload = message.GetPayload<MicroservicesRegistration>();
                    if (originalPayload != null)
                    {
                        // Create a new instance of the Messaging namespace payload
                        var convertedPayload = new ServiceRegistrationPayload
                        {
                            ServiceId = originalPayload.ServiceId,
                            ServiceName = originalPayload.ServiceName,
                            ServiceType = originalPayload.ServiceType,
                            Endpoint = originalPayload.Endpoint,
                            Capabilities = originalPayload.Capabilities,
                            PublisherPort = originalPayload.PublisherPort,
                            SubscriberPort = originalPayload.SubscriberPort
                        };
                        
                        // Set the converted payload directly
                        networkMessage.Payload = JsonSerializer.Serialize(convertedPayload);
                        Console.WriteLine($"Successfully converted ServiceRegistrationPayload from {message.SenderId}");
                    }
                    else
                    {
                        Console.WriteLine("Warning: ServiceRegistration payload was null");
                        networkMessage.Payload = message.Payload;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error converting ServiceRegistrationPayload: {ex.Message}");
                    networkMessage.Payload = message.Payload;
                }
            }
            else
            {
                // Normal payload handling for other message types
                if (!string.IsNullOrEmpty(message.Payload))
                {
                    networkMessage.Payload = message.Payload;
                }
            }
            
            return networkMessage;
        }
        
        /// <summary>
        /// Converts a NetworkMessage to a Message
        /// </summary>
        /// <param name="networkMessage">The NetworkMessage to convert</param>
        /// <returns>A Message with equivalent properties</returns>
        public static PokerGame.Core.Microservices.Message ToMessage(this NetworkMessage networkMessage)
        {
            var message = new PokerGame.Core.Microservices.Message
            {
                MessageId = networkMessage.MessageId,
                SenderId = networkMessage.SenderId,
                ReceiverId = networkMessage.ReceiverId,
                Timestamp = networkMessage.Timestamp,
                InResponseTo = networkMessage.InResponseTo
            };
            
            // Map the message type explicitly instead of using a straight cast
            switch (networkMessage.Type)
            {
                case MessageType.StartHand:
                    message.Type = Microservices.MessageType.StartHand;
                    break;
                case MessageType.DeckShuffled:
                    message.Type = Microservices.MessageType.DeckShuffled;
                    break;
                default:
                    // For other message types, a string-based mapping is safer than direct casting
                    message.Type = Enum.Parse<Microservices.MessageType>(networkMessage.Type.ToString());
                    break;
            }
            
            // Handle payload conversion based on message type
            if (networkMessage.Type == MessageType.ServiceRegistration)
            {
                try
                {
                    // Special handling for ServiceRegistration payload
                    var originalPayload = networkMessage.GetPayload<ServiceRegistrationPayload>();
                    if (originalPayload != null)
                    {
                        // Create a new instance of the Microservices namespace payload
                        var convertedPayload = new MicroservicesRegistration
                        {
                            ServiceId = originalPayload.ServiceId,
                            ServiceName = originalPayload.ServiceName,
                            ServiceType = originalPayload.ServiceType,
                            Endpoint = originalPayload.Endpoint,
                            Capabilities = originalPayload.Capabilities,
                            PublisherPort = originalPayload.PublisherPort,
                            SubscriberPort = originalPayload.SubscriberPort
                        };
                        
                        // Set the converted payload
                        message.SetPayload(convertedPayload);
                        Console.WriteLine($"Successfully converted NetworkMessage ServiceRegistrationPayload from {networkMessage.SenderId}");
                    }
                    else
                    {
                        Console.WriteLine("Warning: NetworkMessage ServiceRegistration payload was null");
                        message.Payload = networkMessage.Payload;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error converting NetworkMessage ServiceRegistrationPayload: {ex.Message}");
                    message.Payload = networkMessage.Payload;
                }
            }
            else
            {
                // Normal payload handling for other message types
                if (!string.IsNullOrEmpty(networkMessage.Payload))
                {
                    message.Payload = networkMessage.Payload;
                }
            }
            
            return message;
        }
    }
}