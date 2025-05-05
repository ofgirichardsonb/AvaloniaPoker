using NUnit.Framework;
using Moq;
using PokerGame.Core.Messaging;
using MSA.Foundation.Messaging;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PokerGame.Tests.Core.Messaging;

[TestFixture]
public class ChannelMessageTransportTests
{
    private ChannelMessageTransport _transport;
    private Mock<IServiceProvider> _serviceProviderMock;
    private string _clientId;
    private Mock<IMessage> _messageMock;
    
    [SetUp]
    public void Setup()
    {
        _clientId = "test-client";
        _serviceProviderMock = new Mock<IServiceProvider>();
        
        _transport = new ChannelMessageTransport(_clientId);
        
        _messageMock = new Mock<IMessage>();
        _messageMock.Setup(m => m.MessageType).Returns(MessageType.ServiceRequest);
        _messageMock.Setup(m => m.Content).Returns("Test content");
        _messageMock.Setup(m => m.Headers).Returns(new Dictionary<string, string>());
    }
    
    [TearDown]
    public void TearDown()
    {
        _transport.Dispose();
    }
    
    [Test]
    public void Constructor_SetsClientIdCorrectly()
    {
        // Assert
        Assert.That(_transport.ClientId, Is.EqualTo(_clientId));
    }
    
    [Test]
    public void Initialize_WithConnectionString_ParsesChannelName()
    {
        // Arrange
        string connectionString = "channel://broker";
        
        // Act
        _transport.Initialize(connectionString);
        
        // Assert - no exceptions means success
        Assert.Pass("Initialization with channel URL succeeded");
    }
    
    [Test]
    public void Start_SetsIsRunningToTrue()
    {
        // Act
        _transport.Start();
        
        // Assert
        Assert.IsTrue(_transport.IsRunning);
    }
    
    [Test]
    public void Stop_SetsIsRunningToFalse()
    {
        // Arrange
        _transport.Start();
        
        // Act
        _transport.Stop();
        
        // Assert
        Assert.IsFalse(_transport.IsRunning);
    }
    
    [Test]
    public void Initialize_WithNullOrEmptyConnectionString_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _transport.Initialize(null));
        Assert.Throws<ArgumentException>(() => _transport.Initialize(""));
    }
    
    [Test]
    public void Initialize_WithInvalidConnectionString_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _transport.Initialize("invalid-url"));
    }
    
    [Test]
    public void Initialize_WithWrongProtocol_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _transport.Initialize("tcp://broker"));
    }
    
    [Test]
    public async Task SendMessageAsync_WhenNotRunning_ThrowsInvalidOperationException()
    {
        // Arrange - don't start the transport
        
        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _transport.SendMessageAsync(_messageMock.Object, "target"));
    }
    
    [Test]
    public async Task BroadcastMessageAsync_WhenNotRunning_ThrowsInvalidOperationException()
    {
        // Arrange - don't start the transport
        
        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _transport.BroadcastMessageAsync(_messageMock.Object));
    }
    
    [Test]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        _transport.Initialize("channel://broker");
        _transport.Start();
        
        // Act
        _transport.Dispose();
        
        // Assert
        Assert.IsFalse(_transport.IsRunning);
    }
}