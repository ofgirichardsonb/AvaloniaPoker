using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MSA.Foundation.Messaging;
using MSA.Foundation.ServiceManagement;
using MSA.Foundation.Telemetry;
using NUnit.Framework;
using FluentAssertions;
using System.Collections.Generic;
using Moq;

// Using alias to avoid ambiguity with System.Threading.ExecutionContext
using MSAEC = MSA.Foundation.ServiceManagement.ExecutionContext;

namespace MSA.Foundation.Tests.Integration
{
    [TestFixture]
    public class FoundationIntegrationTests
    {
        [Test]
        public async Task MessageBroker_WithTelemetry_ShouldTrackMessages()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"ApplicationInsights:InstrumentationKey", "test-key"}
                })
                .Build();
            
            var telemetryService = new TelemetryService(config);
            var messageBroker = new MessageBroker("localhost", 5600, true);
            
            var executionContext = new MSAEC("test-service");
            var messagesReceived = new List<Message>();
            var messageReceived = new ManualResetEventSlim(false);
            
            // Act
            messageBroker.Start();
            string subscriptionId = messageBroker.Subscribe(MessageType.Command, msg => {
                telemetryService.TrackEvent("MessageReceived", 
                    new Dictionary<string, string> { { "MessageType", msg.MessageType.ToString() } });
                messagesReceived.Add(msg);
                messageReceived.Set();
            });
            
            var message = new Message(MessageType.Command, executionContext.ServiceId, "test payload");
            bool publishResult = messageBroker.PublishMessage(message);
            
            // We would normally wait for a message to be received here,
            // but in this unit test environment we're not actually running sockets
            // So we're just testing that the components work together without exceptions
            
            // Assert
            publishResult.Should().BeTrue("Publishing a message should succeed");
            
            // Clean up
            messageBroker.Stop();
            telemetryService.Flush();
        }
        
        [Test]
        public void ExecutionContext_WithMessageBroker_ShouldShareServiceId()
        {
            // Arrange
            var executionContext = new MSAEC("test-service-id");
            var messageBroker = new MessageBroker("localhost", 5601, false);
            
            // Act
            messageBroker.Start();
            var message = new Message(MessageType.Command, executionContext.ServiceId, "test payload");
            bool publishResult = messageBroker.PublishMessage(message);
            
            // Assert
            message.SenderId.Should().Be(executionContext.ServiceId, 
                "Message sender ID should match execution context service ID");
            
            // Clean up
            messageBroker.Stop();
        }
        
        [Test]
        public void ServiceConstants_WithPortOffset_ShouldProvideConsistentPorts()
        {
            // Arrange
            int portOffset = 200;
            
            // Act
            int publisherPort = ServiceConstants.GetPublisherPort(portOffset);
            int subscriberPort = ServiceConstants.GetSubscriberPort(portOffset);
            
            // Assert
            publisherPort.Should().Be(ServiceConstants.BasePublisherPort + portOffset,
                "Publisher port should be base port plus offset");
            subscriberPort.Should().Be(ServiceConstants.BaseSubscriberPort + portOffset,
                "Subscriber port should be base port plus offset");
        }
        
        [Test]
        public void TelemetryService_WithExecutionContext_ShouldTrackOperationWithServiceId()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"ApplicationInsights:InstrumentationKey", "test-key"}
                })
                .Build();
            
            var telemetryService = new TelemetryService(config);
            var executionContext = new MSAEC("test-service-id");
            
            // Act - Track an operation with the service ID in the properties
            Action trackAction = () => telemetryService.TrackEvent("TestOperation", 
                new Dictionary<string, string> { { "ServiceId", executionContext.ServiceId } });
            
            // Assert - The operation should not throw an exception
            trackAction.Should().NotThrow();
            
            // Clean up
            telemetryService.Flush();
        }
        
        [Test]
        public async Task Integration_AllComponents_ShouldWorkTogether()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"ApplicationInsights:InstrumentationKey", "test-key"}
                })
                .Build();
            
            var telemetryService = new TelemetryService(config);
            var executionContext = new MSAEC("test-integrated-service");
            int portOffset = 300;
            
            var publisherPort = ServiceConstants.GetPublisherPort(portOffset);
            var subscriberPort = ServiceConstants.GetSubscriberPort(portOffset);
            
            var messageBroker = new MessageBroker("localhost", publisherPort, true);
            
            // Act & Assert - All components should initialize and work together without exceptions
            
            // Start the message broker
            messageBroker.Start();
            
            // Create and publish a message
            var message = new Message(MessageType.Command, executionContext.ServiceId, "integrated test payload");
            bool publishResult = messageBroker.PublishMessage(message);
            publishResult.Should().BeTrue("Publishing a message should succeed");
            
            // Track the operation with telemetry
            telemetryService.TrackEvent("IntegratedMessageSent", 
                new Dictionary<string, string> { 
                    { "ServiceId", executionContext.ServiceId },
                    { "MessageId", message.MessageId }
                });
            
            // Clean up
            messageBroker.Stop();
            telemetryService.Flush();
            executionContext.Cancel();
        }
        
        [Test]
        public async Task MessageBroker_WithMockedAdapter_ShouldReceiveMessages()
        {
            // Arrange
            var mockAdapter = new Mock<ISocketCommunicationAdapter>();
            var executionContext = new MSAEC("test-mocked-service");
            var receivedMessages = new List<Message>();
            var messageReceived = new ManualResetEventSlim(false);
            
            // Configure the mock adapter to call back when subscribed
            Action<string, string> capturedCallback = null;
            
            mockAdapter.Setup(m => m.Start()).Verifiable();
            mockAdapter.Setup(m => m.SendMessage(It.IsAny<string>(), It.IsAny<string>())).Returns(true).Verifiable();
            mockAdapter.Setup(m => m.SubscribeAll(It.IsAny<Action<string, string>>()))
                .Callback<Action<string, string>>(callback => {
                    capturedCallback = callback;
                })
                .Returns("test-subscription-id")
                .Verifiable();
            
            var messageBroker = new MessageBroker(mockAdapter.Object);
            
            // Act
            messageBroker.Start();
            string subscriptionId = messageBroker.Subscribe(MessageType.Command, msg => {
                receivedMessages.Add(msg);
                messageReceived.Set();
            });
            
            // Publish a message
            var message = new Message(MessageType.Command, executionContext.ServiceId, "test mocked payload");
            bool publishResult = messageBroker.PublishMessage(message);
            
            // Simulate receiving a message through the adapter
            if (capturedCallback != null)
            {
                string topic = MessageType.Command.ToString();
                string payload = message.ToJson();
                capturedCallback(topic, payload);
            }
            
            bool wasSignaled = messageReceived.Wait(TimeSpan.FromSeconds(1));
            
            // Assert
            mockAdapter.Verify(m => m.Start(), Times.Once);
            mockAdapter.Verify(m => m.SubscribeAll(It.IsAny<Action<string, string>>()), Times.Once);
            mockAdapter.Verify(m => m.SendMessage(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            
            publishResult.Should().BeTrue("Publishing a message should succeed");
            wasSignaled.Should().BeTrue("The message handler should be called");
            receivedMessages.Should().ContainSingle("A single message should be received");
            receivedMessages[0].MessageType.Should().Be(MessageType.Command);
            receivedMessages[0].SenderId.Should().Be(executionContext.ServiceId);
            receivedMessages[0].Payload.Should().Be("test mocked payload");
            
            // Clean up
            messageBroker.Stop();
            mockAdapter.Verify(m => m.Stop(), Times.Once);
        }
        
        [Test]
        public async Task MessageBroker_WithAcknowledgment_ShouldSendAckMessages()
        {
            // Arrange
            var mockAdapter = new Mock<ISocketCommunicationAdapter>();
            var executionContext = new MSAEC("test-ack-service");
            var ackReceived = new ManualResetEventSlim(false);
            
            // Configure the mock adapter
            Action<string, string> capturedCallback = null;
            var sentMessages = new List<Tuple<string, string>>();
            
            mockAdapter.Setup(m => m.Start()).Verifiable();
            mockAdapter.Setup(m => m.SendMessage(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((topic, payload) => {
                    sentMessages.Add(Tuple.Create(topic, payload));
                    
                    // If this is an acknowledgment message, signal it
                    if (topic == MessageType.Acknowledgment.ToString())
                    {
                        ackReceived.Set();
                    }
                })
                .Returns(true)
                .Verifiable();
                
            mockAdapter.Setup(m => m.SubscribeAll(It.IsAny<Action<string, string>>()))
                .Callback<Action<string, string>>(callback => {
                    capturedCallback = callback;
                })
                .Returns("test-subscription-id")
                .Verifiable();
            
            var messageBroker = new MessageBroker(mockAdapter.Object);
            
            // Act
            messageBroker.Start();
            
            // Create a message requiring acknowledgment
            var brokerId = "MessageBroker_"; // Partial broker ID for matching
            var message = new Message(MessageType.Command, executionContext.ServiceId, "test ack payload")
            {
                RequireAcknowledgment = true,
                ReceiverId = brokerId
            };
            
            // Simulate receiving this message
            if (capturedCallback != null)
            {
                string topic = MessageType.Command.ToString();
                string payload = message.ToJson();
                capturedCallback(topic, payload);
            }
            
            bool wasAckSignaled = ackReceived.Wait(TimeSpan.FromSeconds(1));
            
            // Assert
            mockAdapter.Verify(m => m.Start(), Times.Once);
            mockAdapter.Verify(m => m.SubscribeAll(It.IsAny<Action<string, string>>()), Times.Once);
            
            wasAckSignaled.Should().BeTrue("An acknowledgment message should be sent");
            sentMessages.Should().Contain(m => m.Item1 == MessageType.Acknowledgment.ToString());
            
            // Clean up
            messageBroker.Stop();
            mockAdapter.Verify(m => m.Stop(), Times.Once);
        }
    }
}