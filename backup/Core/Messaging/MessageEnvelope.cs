using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Represents a message envelope used for reliable messaging
    /// </summary>
    public class MessageEnvelope
    {
        /// <summary>
        /// Unique identifier for this message
        /// </summary>
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// The message type
        /// </summary>
        public string Type { get; set; } = string.Empty;
        
        /// <summary>
        /// The ID of the service that sent this message
        /// </summary>
        public string SenderServiceId { get; set; } = string.Empty;
        
        /// <summary>
        /// The ID of the service this message is targeted to (if any)
        /// </summary>
        public string? TargetServiceId { get; set; }
        
        /// <summary>
        /// When the message was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Metadata dictionary for additional message information
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// The message payload (can be any serializable object)
        /// </summary>
        public object? Payload { get; set; }
        
        /// <summary>
        /// Gets a metadata value by key
        /// </summary>
        /// <param name="key">The metadata key</param>
        /// <returns>The metadata value or empty string if not found</returns>
        public string GetMetadata(string key)
        {
            if (Metadata.TryGetValue(key, out string? value))
            {
                return value;
            }
            return string.Empty;
        }
        
        /// <summary>
        /// Serializes a message envelope to JSON
        /// </summary>
        /// <param name="envelope">The message envelope to serialize</param>
        /// <returns>JSON string representation</returns>
        public static string Serialize(MessageEnvelope envelope)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));
            
            try
            {
                // Convert payload to JSON string if it's not already a string
                if (envelope.Payload != null && envelope.Payload is not string)
                {
                    envelope.Payload = JsonSerializer.Serialize(envelope.Payload);
                }
                
                // Serialize the entire envelope
                return JsonSerializer.Serialize(envelope);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error serializing message: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Deserializes a JSON string to a message envelope
        /// </summary>
        /// <param name="json">JSON string representation</param>
        /// <returns>Deserialized message envelope</returns>
        public static MessageEnvelope? Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("JSON cannot be null or empty", nameof(json));
            
            try
            {
                // Deserialize the envelope
                return JsonSerializer.Deserialize<MessageEnvelope>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing message: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Gets the payload as a specific type
        /// </summary>
        /// <typeparam name="T">The type to convert the payload to</typeparam>
        /// <returns>Typed payload or default if conversion fails</returns>
        public T? GetPayload<T>()
        {
            if (Payload == null) return default;
            
            try
            {
                // If payload is already the right type, return it
                if (Payload is T typedPayload)
                {
                    return typedPayload;
                }
                
                // If payload is a string, try to deserialize it
                if (Payload is string payloadJson)
                {
                    return JsonSerializer.Deserialize<T>(payloadJson);
                }
                
                // Otherwise, try to convert via JSON round-trip
                var json = JsonSerializer.Serialize(Payload);
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting payload: {ex.Message}");
                return default;
            }
        }
        
        /// <summary>
        /// Creates a new message envelope with the specified type and payload
        /// </summary>
        /// <param name="type">The message type</param>
        /// <param name="payload">Optional payload</param>
        /// <param name="metadata">Optional metadata dictionary</param>
        /// <returns>New message envelope</returns>
        public static MessageEnvelope Create(string type, object? payload = null, Dictionary<string, string>? metadata = null)
        {
            var envelope = new MessageEnvelope
            {
                Type = type,
                Payload = payload
            };
            
            // Add metadata if provided
            if (metadata != null)
            {
                foreach (var pair in metadata)
                {
                    envelope.Metadata[pair.Key] = pair.Value;
                }
            }
            
            return envelope;
        }
    }
}