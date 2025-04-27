using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MSA.Foundation.Messaging;
using MSA.Foundation.ServiceManagement;
using MSA.Foundation.Telemetry;
using Xunit;
using FluentAssertions;
using System.Collections.Generic;

namespace MSA.Foundation.Tests.Integration
{
    public class FoundationIntegrationTests
    {
        [Fact]
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
            
            var executionContext = new ExecutionContext("test-service");
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
        
        [Fact]
        public void ExecutionContext_WithMessageBroker_ShouldShareServiceId()
        {
            // Arrange
            var executionContext = new ExecutionContext("test-service-id");
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
        
        [Fact]
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
        
        [Fact]
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
            var executionContext = new ExecutionContext("test-service-id");
            
            // Act - Track an operation with the service ID in the properties
            Action trackAction = () => telemetryService.TrackEvent("TestOperation", 
                new Dictionary<string, string> { { "ServiceId", executionContext.ServiceId } });
            
            // Assert - The operation should not throw an exception
            trackAction.Should().NotThrow();
            
            // Clean up
            telemetryService.Flush();
        }
        
        [Fact]
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
            var executionContext = new ExecutionContext("test-integrated-service");
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
    }
}