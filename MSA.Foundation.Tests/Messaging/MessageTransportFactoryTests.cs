using MSA.Foundation.Messaging;
using NUnit.Framework;
using Moq;

namespace MSA.Foundation.Tests.Messaging;

[TestFixture]
public class MessageTransportFactoryTests
{
    private MessageTransportFactory _factory;
    private Mock<ILogger> _loggerMock;
    
    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger>();
        _factory = new MessageTransportFactory(_loggerMock.Object);
    }
    
    [Test]
    public void CreateTransport_WithInProcessType_ReturnsInProcessTransport()
    {
        // Arrange
        var transportType = MessageTransportType.InProcess;
        var clientId = "test-client";
        var config = new MessageTransportConfig { TransportType = transportType };
        
        // Act
        var transport = _factory.CreateTransport(clientId, config);
        
        // Assert
        Assert.IsNotNull(transport);
        Assert.IsInstanceOf<InProcessMessageTransport>(transport);
        Assert.That(transport.ClientId, Is.EqualTo(clientId));
    }
    
    [Test]
    public void CreateTransport_WithUnknownType_ThrowsArgumentException()
    {
        // Arrange
        var transportType = (MessageTransportType)999; // Invalid enum value
        var clientId = "test-client";
        var config = new MessageTransportConfig { TransportType = transportType };
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _factory.CreateTransport(clientId, config));
    }
    
    [Test]
    public void CreateTransport_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var clientId = "test-client";
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _factory.CreateTransport(clientId, null));
    }
}