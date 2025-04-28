using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;
using Moq;
using Xunit;
using FluentAssertions;
using System.Threading;
using System.Reflection;

namespace MSA.Foundation.Tests.Messaging
{
    public class MessageBrokerTests
    {
        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var messageBroker = new MessageBroker("localhost", 5000, false);
            
            // Assert - No exception should be thrown
            messageBroker.Should().NotBeNull();
        }
        
        [Fact]
        public async Task Subscribe_ShouldReceiveMessages_WhenMessageIsPublished()
        {
            // Arrange
            var messageBroker = CreateMessageBrokerWithMockedSocket();
            var messageReceived = new ManualResetEventSlim(false);
            Message? receivedMessage = null;
            
            // Act
            messageBroker.Start();
            string subscriptionId = messageBroker.Subscribe(MessageType.Command, msg => {
                receivedMessage = msg;
                messageReceived.Set();
            });
            
            // Simulate receiving a message
            var message = new Message(MessageType.Command, "testSender", "testPayload");
            SimulateMessageReceived(messageBroker, message);
            
            // Wait for message to be processed
            bool wasSignaled = messageReceived.Wait(TimeSpan.FromSeconds(1));
            
            // Assert
            wasSignaled.Should().BeTrue("The message handler should have been called");
            receivedMessage.Should().NotBeNull();
            receivedMessage!.MessageType.Should().Be(MessageType.Command);
            receivedMessage.SenderId.Should().Be("testSender");
            receivedMessage.Payload.Should().Be("testPayload");
            
            // Cleanup
            messageBroker.Stop();
        }
        
        [Fact]
        public async Task SubscribeAll_ShouldReceiveAllMessageTypes()
        {
            // Arrange
            var messageBroker = CreateMessageBrokerWithMockedSocket();
            var receivedMessages = new List<Message>();
            var allMessagesReceived = new ManualResetEventSlim(false);
            int expectedMessageCount = 3;
            
            messageBroker.Start();
            
            // Act
            string subscriptionId = messageBroker.SubscribeAll(msg => {
                lock (receivedMessages)
                {
                    receivedMessages.Add(msg);
                    if (receivedMessages.Count >= expectedMessageCount)
                        allMessagesReceived.Set();
                }
            });
            
            // Simulate receiving messages of different types
            SimulateMessageReceived(messageBroker, new Message(MessageType.Command, "sender1", "payload1"));
            SimulateMessageReceived(messageBroker, new Message(MessageType.Event, "sender2", "payload2"));
            SimulateMessageReceived(messageBroker, new Message(MessageType.Data, "sender3", "payload3"));
            
            // Wait for all messages to be processed
            bool wasSignaled = allMessagesReceived.Wait(TimeSpan.FromSeconds(1));
            
            // Assert
            wasSignaled.Should().BeTrue("All message handlers should have been called");
            receivedMessages.Should().HaveCount(expectedMessageCount);
            receivedMessages.Should().Contain(m => m.MessageType == MessageType.Command);
            receivedMessages.Should().Contain(m => m.MessageType == MessageType.Event);
            receivedMessages.Should().Contain(m => m.MessageType == MessageType.Data);
            
            // Cleanup
            messageBroker.Stop();
        }
        
        [Fact]
        public void Unsubscribe_ShouldStopReceivingMessages()
        {
            // Arrange
            var messageBroker = CreateMessageBrokerWithMockedSocket();
            var messageReceived = false;
            
            messageBroker.Start();
            
            // Subscribe to messages
            string subscriptionId = messageBroker.Subscribe(MessageType.Command, _ => messageReceived = true);
            
            // Act - Unsubscribe
            bool unsubscribeResult = messageBroker.Unsubscribe(subscriptionId);
            
            // Simulate receiving a message after unsubscribing
            SimulateMessageReceived(messageBroker, new Message(MessageType.Command, "sender", "payload"));
            
            // Assert
            unsubscribeResult.Should().BeTrue("Unsubscribe should return true for a valid subscription ID");
            messageReceived.Should().BeFalse("The message handler should not be called after unsubscribing");
            
            // Cleanup
            messageBroker.Stop();
        }
        
        [Fact]
        public async Task PublishMessage_ShouldReturnTrue_WhenMessageIsSent()
        {
            // Arrange
            var mock = new Mock<ISocketCommunicationAdapter>();
            mock.Setup(m => m.SendMessage(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
            var messageBroker = CreateMessageBrokerWithMock(mock.Object);
            var message = new Message(MessageType.Command, "testSender", "testPayload");
            
            messageBroker.Start();
            
            // Act
            bool result = messageBroker.PublishMessage(message);
            
            // Assert
            result.Should().BeTrue("PublishMessage should return true when successful");
            mock.Verify(m => m.SendMessage(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            
            // Cleanup
            messageBroker.Stop();
        }
        
        [Fact]
        public async Task PublishMessageAsync_ShouldCompleteSuccessfully()
        {
            // Arrange
            var mock = new Mock<ISocketCommunicationAdapter>();
            mock.Setup(m => m.SendMessage(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
            var messageBroker = CreateMessageBrokerWithMock(mock.Object);
            var message = new Message(MessageType.Command, "testSender", "testPayload");
            
            messageBroker.Start();
            
            // Act
            Func<Task<bool>> publishAction = async () => await messageBroker.PublishMessageAsync(message);
            
            // Assert
            await publishAction.Should().NotThrowAsync();
            var result = await messageBroker.PublishMessageAsync(message);
            result.Should().BeTrue("PublishMessageAsync should return true when successful");
            mock.Verify(m => m.SendMessage(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            
            // Cleanup
            messageBroker.Stop();
        }
        
        [Fact]
        public void AcknowledgmentMessages_ShouldBeSentAutomatically_WhenRequireAcknowledgmentIsTrue()
        {
            // Arrange
            var mock = new Mock<ISocketCommunicationAdapter>();
            mock.Setup(m => m.SendMessage(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
            var messageBroker = CreateMessageBrokerWithMock(mock.Object);
            var ackReceived = new ManualResetEventSlim(false);
            
            // Create a message that requires acknowledgment
            var message = new Message(MessageType.Command, "testSender", "testPayload")
            {
                RequireAcknowledgment = true,
                ReceiverId = "MessageBroker_" // This will be matched with the broker ID prefix
            };
            
            messageBroker.Start();
            
            // Subscribe to acknowledgments
            messageBroker.Subscribe(MessageType.Acknowledgment, msg => {
                ackReceived.Set();
            });
            
            // Act - Simulate receiving a message that requires acknowledgment
            SimulateMessageReceived(messageBroker, message);
            
            // Wait for acknowledgment with timeout
            bool wasSignaled = ackReceived.Wait(TimeSpan.FromSeconds(1));
            
            // Assert
            wasSignaled.Should().BeTrue("An acknowledgment should be sent for messages with RequireAcknowledgment=true");
            mock.Verify(m => m.SendMessage(MessageType.Acknowledgment.ToString(), It.IsAny<string>()), Times.Once);
            
            // Cleanup
            messageBroker.Stop();
        }
        
        // Helper methods
        
        private MessageBroker CreateMessageBrokerWithMockedSocket()
        {
            // Create a real MessageBroker for tests that don't need to verify socket interactions
            var broker = new MessageBroker("localhost", 5000, true);
            return broker;
        }
        
        private MessageBroker CreateMessageBrokerWithMock(ISocketCommunicationAdapter mockAdapter)
        {
            // Create a MessageBroker with a mocked socket adapter for testing
            var broker = new MessageBroker("localhost", 5000, true);
            
            // Use reflection to replace the socket adapter with our mock
            var field = typeof(MessageBroker).GetField("_socketAdapter", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(broker, mockAdapter);
            
            return broker;
        }
        
        private void SimulateMessageReceived(MessageBroker messageBroker, Message message)
        {
            // In a real scenario, messages would come in through the socket
            // Here we'll use reflection to directly call the message handler
            string topic = message.MessageType.ToString();
            string payload = message.ToJson();
            
            // Use reflection to access the private OnMessageReceived method
            var method = typeof(MessageBroker).GetMethod("OnMessageReceived", 
                BindingFlags.NonPublic | BindingFlags.Instance);
                
            if (method == null)
            {
                throw new InvalidOperationException("Could not find OnMessageReceived method via reflection");
            }
            
            method.Invoke(messageBroker, new object[] { topic, payload });
        }
    }
}