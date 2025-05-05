using MSA.Foundation.Messaging;
using Moq;
using NUnit.Framework;
using System.Threading.Channels;

namespace MSA.Foundation.Tests.Messaging;

[TestFixture]
public class InProcessMessageTransportTests
{
    private InProcessMessageTransport _transport;
    private string _clientId;
    private Mock<IMessage> _messageMock;
    
    [SetUp]
    public void Setup()
    {
        _clientId = "test-client";
        _transport = new InProcessMessageTransport(_clientId);
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
    public async Task SendMessage_WhenTransportIsRunning_MessageCanBeReceived()
    {
        // Arrange
        _transport.Start();
        var tcs = new TaskCompletionSource<IMessage>();
        
        _transport.MessageReceived += (sender, message) => {
            tcs.SetResult(message);
        };
        
        // Act
        await _transport.SendMessageAsync(_messageMock.Object, "target-client");
        
        // Use a timeout to avoid tests hanging
        var receivedMessage = await Task.WhenAny(tcs.Task, Task.Delay(1000)).Unwrap();
        
        // Assert
        Assert.IsNotNull(receivedMessage);
        Assert.That(receivedMessage.MessageType, Is.EqualTo(_messageMock.Object.MessageType));
        Assert.That(receivedMessage.Content, Is.EqualTo(_messageMock.Object.Content));
    }
    
    [Test]
    public async Task BroadcastMessage_WhenTransportIsRunning_MessageCanBeReceived()
    {
        // Arrange
        _transport.Start();
        var tcs = new TaskCompletionSource<IMessage>();
        
        _transport.BroadcastMessageReceived += (sender, message) => {
            tcs.SetResult(message);
        };
        
        // Act
        await _transport.BroadcastMessageAsync(_messageMock.Object);
        
        // Use a timeout to avoid tests hanging
        var receivedMessage = await Task.WhenAny(tcs.Task, Task.Delay(1000)).Unwrap();
        
        // Assert
        Assert.IsNotNull(receivedMessage);
        Assert.That(receivedMessage.MessageType, Is.EqualTo(_messageMock.Object.MessageType));
        Assert.That(receivedMessage.Content, Is.EqualTo(_messageMock.Object.Content));
    }
    
    [Test]
    public async Task SendMessage_WhenTransportIsStopped_ThrowsInvalidOperationException()
    {
        // Arrange
        _transport.Start();
        _transport.Stop();
        
        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _transport.SendMessageAsync(_messageMock.Object, "target-client"));
    }
    
    [Test]
    public async Task BroadcastMessage_WhenTransportIsStopped_ThrowsInvalidOperationException()
    {
        // Arrange
        _transport.Start();
        _transport.Stop();
        
        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _transport.BroadcastMessageAsync(_messageMock.Object));
    }
    
    [Test]
    public void Dispose_CompletesChannelsAndCleansUpResources()
    {
        // Arrange
        _transport.Start();
        
        // Act
        _transport.Dispose();
        
        // Assert
        Assert.IsFalse(_transport.IsRunning);
    }
}