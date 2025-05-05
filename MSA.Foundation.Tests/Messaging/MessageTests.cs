using System;
using System.Collections.Generic;
using MSA.Foundation.Messaging;
using NUnit.Framework;
using FluentAssertions;

namespace MSA.Foundation.Tests.Messaging
{
    [TestFixture]
    public class MessageTests
    {
        [Test]
        public void DefaultConstructor_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            var message = new Message();
            
            // Assert
            message.Should().NotBeNull();
            message.MessageId.Should().NotBeEmpty("MessageId should be initialized");
            message.MessageType.Should().Be(MessageType.Unknown, "Default MessageType should be Unknown");
            message.SenderId.Should().BeEmpty("Default SenderId should be empty");
            message.ReceiverId.Should().BeEmpty("Default ReceiverId should be empty");
            message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1), "Timestamp should be initialized to current UTC time");
            message.Payload.Should().BeNull("Default Payload should be null");
            message.Headers.Should().NotBeNull("Headers should be initialized");
            message.Headers.Should().BeEmpty("Headers should be empty by default");
            message.RequireAcknowledgment.Should().BeFalse("RequireAcknowledgment should be false by default");
            message.AcknowledgmentId.Should().BeNull("AcknowledgmentId should be null by default");
        }
        
        [Test]
        public void ParameterizedConstructor_ShouldInitializeCorrectly()
        {
            // Arrange
            string senderId = "test-sender";
            string payload = "test-payload";
            
            // Act
            var message = new Message(MessageType.Command, senderId, payload);
            
            // Assert
            message.Should().NotBeNull();
            message.MessageId.Should().NotBeEmpty("MessageId should be initialized");
            message.MessageType.Should().Be(MessageType.Command, "MessageType should match parameter");
            message.SenderId.Should().Be(senderId, "SenderId should match parameter");
            message.Payload.Should().Be(payload, "Payload should match parameter");
            message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1), "Timestamp should be initialized to current UTC time");
        }
        
        [Test]
        public void CreateAcknowledgment_ShouldCreateCorrectMessage()
        {
            // Arrange
            var originalMessage = new Message(MessageType.Command, "sender-id", "payload")
            {
                Headers = new Dictionary<string, string> { { "key", "value" } }
            };
            string receiverId = "receiver-id";
            
            // Act
            var ackMessage = originalMessage.CreateAcknowledgment(receiverId);
            
            // Assert
            ackMessage.Should().NotBeNull();
            ackMessage.MessageId.Should().NotBeEmpty("MessageId should be initialized");
            ackMessage.MessageId.Should().NotBe(originalMessage.MessageId, "MessageId should be new");
            ackMessage.MessageType.Should().Be(MessageType.Acknowledgment, "MessageType should be Acknowledgment");
            ackMessage.SenderId.Should().Be(receiverId, "SenderId should be the receiver of the original message");
            ackMessage.ReceiverId.Should().Be(originalMessage.SenderId, "ReceiverId should be the sender of the original message");
            ackMessage.AcknowledgmentId.Should().Be(originalMessage.MessageId, "AcknowledgmentId should be the ID of the original message");
            ackMessage.Headers.Should().BeEquivalentTo(originalMessage.Headers, "Headers should be copied from the original message");
        }
        
        [Test]
        public void CreateResponse_ShouldCreateCorrectMessage()
        {
            // Arrange
            var originalMessage = new Message(MessageType.Command, "sender-id", "original-payload")
            {
                Headers = new Dictionary<string, string> { { "key", "value" } }
            };
            string receiverId = "receiver-id";
            string responsePayload = "response-payload";
            
            // Act
            var responseMessage = originalMessage.CreateResponse(receiverId, responsePayload);
            
            // Assert
            responseMessage.Should().NotBeNull();
            responseMessage.MessageId.Should().NotBeEmpty("MessageId should be initialized");
            responseMessage.MessageId.Should().NotBe(originalMessage.MessageId, "MessageId should be new");
            responseMessage.MessageType.Should().Be(MessageType.Response, "MessageType should be Response");
            responseMessage.SenderId.Should().Be(receiverId, "SenderId should be the receiver of the original message");
            responseMessage.ReceiverId.Should().Be(originalMessage.SenderId, "ReceiverId should be the sender of the original message");
            responseMessage.Payload.Should().Be(responsePayload, "Payload should match parameter");
            responseMessage.Headers.Should().ContainKey("OriginalMessageId", "Headers should contain OriginalMessageId");
            responseMessage.Headers["OriginalMessageId"].Should().Be(originalMessage.MessageId, "OriginalMessageId header should be the ID of the original message");
            responseMessage.Headers.Should().ContainKey("key", "Headers should be copied from the original message");
            responseMessage.Headers["key"].Should().Be("value", "Header values should be copied from the original message");
        }
        
        [Test]
        public void ToJson_ShouldSerializeCorrectly()
        {
            // Arrange
            var message = new Message(MessageType.Command, "sender-id", "payload")
            {
                ReceiverId = "receiver-id",
                RequireAcknowledgment = true,
                Headers = new Dictionary<string, string> { { "key", "value" } }
            };
            
            // Act
            string json = message.ToJson();
            
            // Assert
            json.Should().NotBeNullOrEmpty("JSON string should not be empty");
            json.Should().Contain("\"messageType\":1", "JSON should contain message type");
            json.Should().Contain("\"senderId\":\"sender-id\"", "JSON should contain sender ID");
            json.Should().Contain("\"receiverId\":\"receiver-id\"", "JSON should contain receiver ID");
            json.Should().Contain("\"payload\":\"payload\"", "JSON should contain payload");
            json.Should().Contain("\"requireAcknowledgment\":true", "JSON should contain requireAcknowledgment");
            json.Should().Contain("\"key\":\"value\"", "JSON should contain headers");
        }
        
        [Test]
        public void FromJson_WithValidJson_ShouldDeserializeCorrectly()
        {
            // Arrange
            var originalMessage = new Message(MessageType.Command, "sender-id", "payload")
            {
                ReceiverId = "receiver-id",
                RequireAcknowledgment = true,
                Headers = new Dictionary<string, string> { { "key", "value" } }
            };
            string json = originalMessage.ToJson();
            
            // Act
            var deserializedMessage = Message.FromJson(json);
            
            // Assert
            deserializedMessage.Should().NotBeNull("Deserialized message should not be null");
            deserializedMessage.MessageId.Should().Be(originalMessage.MessageId, "MessageId should match original");
            deserializedMessage.MessageType.Should().Be(originalMessage.MessageType, "MessageType should match original");
            deserializedMessage.SenderId.Should().Be(originalMessage.SenderId, "SenderId should match original");
            deserializedMessage.ReceiverId.Should().Be(originalMessage.ReceiverId, "ReceiverId should match original");
            deserializedMessage.Payload.Should().Be(originalMessage.Payload, "Payload should match original");
            deserializedMessage.RequireAcknowledgment.Should().Be(originalMessage.RequireAcknowledgment, "RequireAcknowledgment should match original");
            deserializedMessage.Headers.Should().BeEquivalentTo(originalMessage.Headers, "Headers should match original");
        }
        
        [Test]
        public void FromJson_WithInvalidJson_ShouldReturnNull()
        {
            // Arrange
            string invalidJson = "{ invalid json }";
            
            // Act
            var deserializedMessage = Message.FromJson(invalidJson);
            
            // Assert
            deserializedMessage.Should().BeNull("Deserialized message should be null for invalid JSON");
        }
        
        [Test]
        public void SetPayload_WithValidObject_ShouldSerializeCorrectly()
        {
            // Arrange
            var message = new Message();
            var testPayload = new TestPayload
            {
                Id = 1,
                Name = "Test",
                IsActive = true
            };
            
            // Act
            message.SetPayload(testPayload);
            
            // Assert
            message.Payload.Should().NotBeNullOrEmpty("Payload should not be empty");
            message.Payload.Should().Contain("\"id\":1", "Payload should contain id");
            message.Payload.Should().Contain("\"name\":\"Test\"", "Payload should contain name");
            message.Payload.Should().Contain("\"isActive\":true", "Payload should contain isActive");
        }
        
        [Test]
        public void GetPayload_WithValidPayload_ShouldDeserializeCorrectly()
        {
            // Arrange
            var message = new Message();
            var originalPayload = new TestPayload
            {
                Id = 1,
                Name = "Test",
                IsActive = true
            };
            message.SetPayload(originalPayload);
            
            // Act
            var deserializedPayload = message.GetPayload<TestPayload>();
            
            // Assert
            deserializedPayload.Should().NotBeNull("Deserialized payload should not be null");
            deserializedPayload.Id.Should().Be(originalPayload.Id, "ID should match original");
            deserializedPayload.Name.Should().Be(originalPayload.Name, "Name should match original");
            deserializedPayload.IsActive.Should().Be(originalPayload.IsActive, "IsActive should match original");
        }
        
        [Test]
        public void GetPayload_WithInvalidPayload_ShouldReturnDefault()
        {
            // Arrange
            var message = new Message
            {
                Payload = "{ invalid json }"
            };
            
            // Act
            var deserializedPayload = message.GetPayload<TestPayload>();
            
            // Assert
            deserializedPayload.Should().BeNull("Deserialized payload should be null for invalid JSON");
        }
        
        [Test]
        public void GetPayload_WithNullPayload_ShouldReturnDefault()
        {
            // Arrange
            var message = new Message
            {
                Payload = null
            };
            
            // Act
            var deserializedPayload = message.GetPayload<TestPayload>();
            
            // Assert
            deserializedPayload.Should().BeNull("Deserialized payload should be null for null payload");
        }
        
        [Test]
        public void GetPayload_WithEmptyPayload_ShouldReturnDefault()
        {
            // Arrange
            var message = new Message
            {
                Payload = ""
            };
            
            // Act
            var deserializedPayload = message.GetPayload<TestPayload>();
            
            // Assert
            deserializedPayload.Should().BeNull("Deserialized payload should be null for empty payload");
        }
        
        // Test payload class
        private class TestPayload
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public bool IsActive { get; set; }
        }
    }
}