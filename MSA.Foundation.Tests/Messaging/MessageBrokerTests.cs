using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;
using Moq;
using Xunit;
using FluentAssertions;
using System.Threading;

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
            var messageBroker = CreateMessageBrokerWithMockedSocket();
            var message = new Message(MessageType.Command, "testSender", "testPayload");
            
            messageBroker.Start();
            
            // Act
            bool result = messageBroker.PublishMessage(message);
            
            // Assert
            result.Should().BeTrue("PublishMessage should return true when successful");
            
            // Cleanup
            messageBroker.Stop();
        }
        
        [Fact]
        public async Task PublishMessageAsync_ShouldCompleteSuccessfully()
        {
            // Arrange
            var messageBroker = CreateMessageBrokerWithMockedSocket();
            var message = new Message(MessageType.Command, "testSender", "testPayload");
            
            messageBroker.Start();
            
            // Act
            Func<Task<bool>> publishAction = async () => await messageBroker.PublishMessageAsync(message);
            
            // Assert
            await publishAction.Should().NotThrowAsync();
            var result = await messageBroker.PublishMessageAsync(message);
            result.Should().BeTrue("PublishMessageAsync should return true when successful");
            
            // Cleanup
            messageBroker.Stop();
        }
        
        [Fact]
        public void AcknowledgmentMessages_ShouldBeSentAutomatically_WhenRequireAcknowledgmentIsTrue()
        {
            // Arrange
            var messageBroker = CreateMessageBrokerWithMockedSocket();
            bool acknowledgmentSent = false;
            
            // We'll use a special field to track if an acknowledgment was sent
            SetupMessageBrokerToTrackAcknowledgments(messageBroker, ref acknowledgmentSent);
            
            // Create a message that requires acknowledgment
            var message = new Message(MessageType.Command, "testSender", "testPayload")
            {
                RequireAcknowledgment = true
            };
            
            messageBroker.Start();
            
            // Act - Simulate receiving a message that requires acknowledgment
            SimulateMessageReceived(messageBroker, message);
            
            // Allow time for acknowledgment to be sent
            Thread.Sleep(100);
            
            // Assert
            acknowledgmentSent.Should().BeTrue("An acknowledgment should be sent for messages with RequireAcknowledgment=true");
            
            // Cleanup
            messageBroker.Stop();
        }
        
        // Helper methods
        
        private MessageBroker CreateMessageBrokerWithMockedSocket()
        {
            // For testing purposes, we create a real MessageBroker but we'll
            // control the message flow through a simulated socket adapter
            var broker = new MessageBroker("localhost", 5000, true);
            return broker;
        }
        
        private void SimulateMessageReceived(MessageBroker messageBroker, Message message)
        {
            // In a real scenario, messages would come in through the socket
            // Here we'll use reflection to directly call the message handler
            string topic = message.MessageType.ToString();
            string payload = message.ToJson();
            
            // This is where we'd normally need to use reflection to access private methods
            // For simplicity in this example, we're just using a public method or property
            // that lets us inject messages for testing
            
            // This is simplified - in a real test you'd use reflection or 
            // modify the MessageBroker to have a test hook
            typeof(MessageBroker).GetMethod("OnMessageReceived", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance)
                ?.Invoke(messageBroker, new object[] { topic, payload });
        }
        
        private void SetupMessageBrokerToTrackAcknowledgments(MessageBroker messageBroker, ref bool acknowledgmentSent)
        {
            // In a real implementation, you would use reflection or a test interface
            // to hook into the acknowledgment sending mechanism
            
            // This is simplified - in a real test you'd use reflection or 
            // modify the MessageBroker to have a test hook
            messageBroker.Subscribe(MessageType.Acknowledgment, message => {
                acknowledgmentSent = true;
            });
        }
    }
}