using System;
using MSA.Foundation.ServiceManagement;
using NUnit.Framework;
using FluentAssertions;

namespace MSA.Foundation.Tests.ServiceManagement
{
    [TestFixture]
    public class ServiceConstantsTests
    {
        [Test]
        public void BasePublisherPort_ShouldHaveExpectedValue()
        {
            // Assert
            ServiceConstants.BasePublisherPort.Should().Be(5555, "Base publisher port should be 5555");
        }
        
        [Test]
        public void BaseSubscriberPort_ShouldHaveExpectedValue()
        {
            // Assert
            ServiceConstants.BaseSubscriberPort.Should().Be(5556, "Base subscriber port should be 5556");
        }
        
        [Test]
        public void GetPublisherPort_WithZeroOffset_ShouldReturnBasePort()
        {
            // Act
            int port = ServiceConstants.GetPublisherPort(0);
            
            // Assert
            port.Should().Be(ServiceConstants.BasePublisherPort, "Publisher port with zero offset should equal base port");
        }
        
        [Test]
        public void GetSubscriberPort_WithZeroOffset_ShouldReturnBasePort()
        {
            // Act
            int port = ServiceConstants.GetSubscriberPort(0);
            
            // Assert
            port.Should().Be(ServiceConstants.BaseSubscriberPort, "Subscriber port with zero offset should equal base port");
        }
        
        [Test]
        public void GetPublisherPort_WithPositiveOffset_ShouldAddOffsetToBasePort()
        {
            // Arrange
            int offset = 100;
            
            // Act
            int port = ServiceConstants.GetPublisherPort(offset);
            
            // Assert
            port.Should().Be(ServiceConstants.BasePublisherPort + offset, "Publisher port should add offset to base port");
        }
        
        [Test]
        public void GetSubscriberPort_WithPositiveOffset_ShouldAddOffsetToBasePort()
        {
            // Arrange
            int offset = 100;
            
            // Act
            int port = ServiceConstants.GetSubscriberPort(offset);
            
            // Assert
            port.Should().Be(ServiceConstants.BaseSubscriberPort + offset, "Subscriber port should add offset to base port");
        }
        
        [Test]
        public void GetPublisherPort_WithNegativeOffset_ShouldSubtractOffsetFromBasePort()
        {
            // Arrange
            int offset = -100;
            
            // Act
            int port = ServiceConstants.GetPublisherPort(offset);
            
            // Assert
            port.Should().Be(ServiceConstants.BasePublisherPort + offset, "Publisher port should subtract offset from base port");
        }
        
        [Test]
        public void GetSubscriberPort_WithNegativeOffset_ShouldSubtractOffsetFromBasePort()
        {
            // Arrange
            int offset = -100;
            
            // Act
            int port = ServiceConstants.GetSubscriberPort(offset);
            
            // Assert
            port.Should().Be(ServiceConstants.BaseSubscriberPort + offset, "Subscriber port should subtract offset from base port");
        }
        
        [Test]
        public void NormalizePort_WithPortInRange_ShouldReturnSamePort()
        {
            // Arrange
            int port = 5600; // A valid port in the normal range
            
            // Act
            int normalizedPort = ServiceConstants.NormalizePort(port);
            
            // Assert
            normalizedPort.Should().Be(port, "Normalize should return the same port if it's in valid range");
        }
        
        [Test]
        public void NormalizePort_WithPortBelowRange_ShouldAdjustToMinimumPort()
        {
            // Arrange
            int port = 0; // Below valid port range
            
            // Act
            int normalizedPort = ServiceConstants.NormalizePort(port);
            
            // Assert
            normalizedPort.Should().Be(1024, "Normalize should adjust to minimum valid port");
        }
        
        [Test]
        public void NormalizePort_WithPortAboveRange_ShouldAdjustToMaximumPort()
        {
            // Arrange
            int port = 70000; // Above valid port range
            
            // Act
            int normalizedPort = ServiceConstants.NormalizePort(port);
            
            // Assert
            normalizedPort.Should().Be(65535, "Normalize should adjust to maximum valid port");
        }
        
        [Test]
        public void ServiceTypeIds_ShouldAllBeUnique()
        {
            // Arrange & Act
            var serviceIds = new[]
            {
                ServiceConstants.CardDeckServiceId,
                ServiceConstants.GameEngineServiceId,
                ServiceConstants.ConsoleUIServiceId,
                ServiceConstants.BrokerServiceId
            };
            
            // Assert
            serviceIds.Should().OnlyHaveUniqueItems("All service type IDs should be unique");
        }
    }
}