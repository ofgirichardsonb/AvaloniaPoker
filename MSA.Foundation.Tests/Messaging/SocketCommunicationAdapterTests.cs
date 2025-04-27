using System;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;
using Xunit;
using FluentAssertions;

namespace MSA.Foundation.Tests.Messaging
{
    public class SocketCommunicationAdapterTests
    {
        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            using var adapter = new SocketCommunicationAdapter("localhost", 5000, false);
            
            // Assert
            adapter.Should().NotBeNull();
        }
        
        [Fact]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            var adapter = new SocketCommunicationAdapter("localhost", 5000, false);
            
            // Act
            Action disposeAction = () => adapter.Dispose();
            
            // Assert
            disposeAction.Should().NotThrow();
        }
        
        [Fact]
        public void Start_ShouldInitializeSocketsCorrectly()
        {
            // Arrange
            using var adapter = new SocketCommunicationAdapter("localhost", 5000, false);
            
            // Act
            Action startAction = () => adapter.Start();
            
            // Assert
            startAction.Should().NotThrow();
        }
        
        [Fact]
        public void SendMessage_ShouldReturnFalse_WhenNotStarted()
        {
            // Arrange
            using var adapter = new SocketCommunicationAdapter("localhost", 5000, false);
            
            // Act
            bool result = adapter.SendMessage("topic", "message");
            
            // Assert
            result.Should().BeFalse("SendMessage should return false when not started");
        }
        
        [Fact]
        public void SendMessage_AfterStart_ShouldSendMessage()
        {
            // Arrange
            using var adapter = new SocketCommunicationAdapter("localhost", 5000, false);
            adapter.Start();
            
            // Act
            bool result = adapter.SendMessage("topic", "message");
            
            // Assert
            result.Should().BeTrue("SendMessage should return true after adapter is started");
        }
        
        [Fact]
        public void Subscribe_ShouldReturnSubscriptionId()
        {
            // Arrange
            using var adapter = new SocketCommunicationAdapter("localhost", 5000, false);
            
            // Act
            string subscriptionId = adapter.Subscribe("topic", (_, _) => { });
            
            // Assert
            subscriptionId.Should().NotBeNullOrEmpty("Subscribe should return a non-empty subscription ID");
        }
        
        [Fact]
        public void SubscribeAll_ShouldReturnSubscriptionId()
        {
            // Arrange
            using var adapter = new SocketCommunicationAdapter("localhost", 5000, false);
            
            // Act
            string subscriptionId = adapter.SubscribeAll((_, _) => { });
            
            // Assert
            subscriptionId.Should().NotBeNullOrEmpty("SubscribeAll should return a non-empty subscription ID");
        }
        
        [Fact]
        public void Unsubscribe_WithValidId_ShouldReturnTrue()
        {
            // Arrange
            using var adapter = new SocketCommunicationAdapter("localhost", 5000, false);
            string subscriptionId = adapter.Subscribe("topic", (_, _) => { });
            
            // Act
            bool result = adapter.Unsubscribe(subscriptionId);
            
            // Assert
            result.Should().BeTrue("Unsubscribe should return true for a valid subscription ID");
        }
        
        [Fact]
        public void Unsubscribe_WithInvalidId_ShouldReturnFalse()
        {
            // Arrange
            using var adapter = new SocketCommunicationAdapter("localhost", 5000, false);
            
            // Act
            bool result = adapter.Unsubscribe("invalid-id");
            
            // Assert
            result.Should().BeFalse("Unsubscribe should return false for an invalid subscription ID");
        }
        
        [Fact]
        public void Stop_ShouldClearAllResources()
        {
            // Arrange
            using var adapter = new SocketCommunicationAdapter("localhost", 5000, false);
            adapter.Start();
            
            // Act
            adapter.Stop();
            
            // Assert
            bool sendResult = adapter.SendMessage("topic", "message");
            sendResult.Should().BeFalse("After stopping, SendMessage should return false");
        }
    }
}