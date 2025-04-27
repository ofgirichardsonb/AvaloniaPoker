using System;
using System.Collections.Generic;
using System.Text.Json;
using MSA.Foundation.Messaging;
using Xunit;
using FluentAssertions;

namespace MSA.Foundation.Tests.Messaging
{
    public class MessageTests
    {
        [Fact]
        public void Constructor_WithBasicParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            string senderId = "sender123";
            string payload = "test payload";
            MessageType messageType = MessageType.Command;
            
            // Act
            var message = new Message(messageType, senderId, payload);
            
            // Assert
            message.MessageId.Should().NotBeNullOrEmpty();
            message.MessageType.Should().Be(messageType);
            message.SenderId.Should().Be(senderId);
            message.Payload.Should().Be(payload);
            message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            message.RequireAcknowledgment.Should().BeFalse();
            message.Headers.Should().NotBeNull();
            message.Headers.Should().BeEmpty();
        }

        [Fact]
        public void CreateAcknowledgment_ShouldCreateValidAcknowledgmentMessage()
        {
            // Arrange
            var originalMessage = new Message(MessageType.Command, "sender123", "test payload")
            {
                RequireAcknowledgment = true
            };
            string receiverId = "receiver456";
            
            // Act
            var ackMessage = originalMessage.CreateAcknowledgment(receiverId);
            
            // Assert
            ackMessage.MessageId.Should().NotBeNullOrEmpty();
            ackMessage.MessageId.Should().NotBe(originalMessage.MessageId);
            ackMessage.MessageType.Should().Be(MessageType.Acknowledgment);
            ackMessage.SenderId.Should().Be(receiverId);
            ackMessage.ReceiverId.Should().Be(originalMessage.SenderId);
            ackMessage.AcknowledgmentId.Should().Be(originalMessage.MessageId);
            ackMessage.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void CreateResponse_ShouldCreateValidResponseMessage()
        {
            // Arrange
            var originalMessage = new Message(MessageType.Request, "sender123", "request payload");
            string receiverId = "receiver456";
            string responsePayload = "response payload";
            
            // Act
            var responseMessage = originalMessage.CreateResponse(receiverId, responsePayload);
            
            // Assert
            responseMessage.MessageId.Should().NotBeNullOrEmpty();
            responseMessage.MessageId.Should().NotBe(originalMessage.MessageId);
            responseMessage.MessageType.Should().Be(MessageType.Response);
            responseMessage.SenderId.Should().Be(receiverId);
            responseMessage.ReceiverId.Should().Be(originalMessage.SenderId);
            responseMessage.Payload.Should().Be(responsePayload);
            responseMessage.Headers.Should().ContainKey("OriginalMessageId");
            responseMessage.Headers["OriginalMessageId"].Should().Be(originalMessage.MessageId);
        }

        [Fact]
        public void ToJson_ShouldSerializeMessageCorrectly()
        {
            // Arrange
            var message = new Message(MessageType.Event, "sender123", "test payload")
            {
                ReceiverId = "receiver456",
                RequireAcknowledgment = true
            };
            message.Headers.Add("TestHeader", "TestValue");
            
            // Act
            string json = message.ToJson();
            
            // Assert
            json.Should().NotBeNullOrEmpty();
            json.Should().Contain(message.MessageId);
            json.Should().Contain(message.SenderId);
            json.Should().Contain(message.ReceiverId);
            json.Should().Contain("TestHeader");
            json.Should().Contain("TestValue");
            json.Should().Contain("test payload");
        }

        [Fact]
        public void FromJson_WithValidJson_ShouldDeserializeCorrectly()
        {
            // Arrange
            var originalMessage = new Message(MessageType.Command, "sender123", "test payload")
            {
                ReceiverId = "receiver456",
                RequireAcknowledgment = true
            };
            originalMessage.Headers.Add("TestHeader", "TestValue");
            string json = originalMessage.ToJson();
            
            // Act
            var deserializedMessage = Message.FromJson(json);
            
            // Assert
            deserializedMessage.Should().NotBeNull();
            deserializedMessage!.MessageId.Should().Be(originalMessage.MessageId);
            deserializedMessage.MessageType.Should().Be(originalMessage.MessageType);
            deserializedMessage.SenderId.Should().Be(originalMessage.SenderId);
            deserializedMessage.ReceiverId.Should().Be(originalMessage.ReceiverId);
            deserializedMessage.Payload.Should().Be(originalMessage.Payload);
            deserializedMessage.RequireAcknowledgment.Should().BeTrue();
            deserializedMessage.Headers.Should().ContainKey("TestHeader");
            deserializedMessage.Headers["TestHeader"].Should().Be("TestValue");
        }

        [Fact]
        public void FromJson_WithInvalidJson_ShouldReturnNull()
        {
            // Arrange
            string invalidJson = "{ invalid json }";
            
            // Act
            var result = Message.FromJson(invalidJson);
            
            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void SetAndGetPayload_WithTypedObject_ShouldWorkCorrectly()
        {
            // Arrange
            var message = new Message(MessageType.Data, "sender123", null);
            var testObject = new TestPayload { Id = 123, Name = "Test" };
            
            // Act
            message.SetPayload(testObject);
            var retrievedObject = message.GetPayload<TestPayload>();
            
            // Assert
            message.Payload.Should().NotBeNullOrEmpty();
            retrievedObject.Should().NotBeNull();
            retrievedObject!.Id.Should().Be(testObject.Id);
            retrievedObject.Name.Should().Be(testObject.Name);
        }

        [Fact]
        public void GetPayload_WithInvalidType_ShouldReturnNull()
        {
            // Arrange
            var message = new Message(MessageType.Data, "sender123", "not a valid json for the specified type");
            
            // Act
            var result = message.GetPayload<TestPayload>();
            
            // Assert
            result.Should().BeNull();
        }
        
        // Simple class for testing payload serialization
        private class TestPayload
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}