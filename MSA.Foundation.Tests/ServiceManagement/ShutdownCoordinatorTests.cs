using MSA.Foundation.ServiceManagement;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace MSA.Foundation.Tests.ServiceManagement;

[TestFixture]
public class ShutdownCoordinatorTests
{
    private ShutdownCoordinator _coordinator;
    private List<string> _shutdownOrder;
    private ConcurrentDictionary<string, int> _priorityMap;

    [SetUp]
    public void Setup()
    {
        _coordinator = new ShutdownCoordinator();
        _shutdownOrder = new List<string>();
        _priorityMap = new ConcurrentDictionary<string, int>();
    }

    [Test]
    public void RegisterParticipant_WithValidParticipant_AddsToList()
    {
        // Arrange
        string participantName = "TestParticipant";
        int priority = 100;
        Func<Task> shutdownAction = () => { _shutdownOrder.Add(participantName); return Task.CompletedTask; };
        
        // Act
        _coordinator.RegisterParticipant(participantName, priority, shutdownAction);
        
        // Assert
        Assert.IsTrue(_coordinator.IsRegistered(participantName));
    }
    
    [Test]
    public void ShutdownAll_ExecutesParticipantsInPriorityOrder()
    {
        // Arrange
        RegisterParticipants();
        
        // Act
        _coordinator.ShutdownAll("Test shutdown").Wait();
        
        // Assert
        Assert.That(_shutdownOrder, Has.Count.EqualTo(3));
        
        // Highest priority (lowest number) should be first
        Assert.That(_shutdownOrder[0], Is.EqualTo("HighPriority"));
        Assert.That(_shutdownOrder[1], Is.EqualTo("MediumPriority"));
        Assert.That(_shutdownOrder[2], Is.EqualTo("LowPriority"));
    }
    
    [Test]
    public void ShutdownAll_WithFailingParticipant_ContinuesExecution()
    {
        // Arrange
        RegisterParticipants();
        
        // Add a failing participant
        _coordinator.RegisterParticipant("FailingParticipant", 150, () => 
        {
            _shutdownOrder.Add("FailingParticipant");
            throw new Exception("Simulated failure during shutdown");
        });
        
        // Act - should not throw despite the failing participant
        _coordinator.ShutdownAll("Test shutdown with failure").Wait();
        
        // Assert - all participants should have executed, including the failing one
        Assert.That(_shutdownOrder, Has.Count.EqualTo(4));
        Assert.That(_shutdownOrder, Contains.Item("FailingParticipant"));
        Assert.That(_shutdownOrder, Contains.Item("HighPriority"));
        Assert.That(_shutdownOrder, Contains.Item("MediumPriority"));
        Assert.That(_shutdownOrder, Contains.Item("LowPriority"));
    }
    
    [Test]
    public void IsRegistered_WithUnregisteredParticipant_ReturnsFalse()
    {
        // Act & Assert
        Assert.IsFalse(_coordinator.IsRegistered("NonExistentParticipant"));
    }
    
    [Test]
    public async Task ShutdownAll_WithTimeoutOption_RespectsTimeout()
    {
        // Arrange
        _coordinator.RegisterParticipant("SlowParticipant", 100, async () => 
        {
            _shutdownOrder.Add("SlowParticipant-Start");
            await Task.Delay(500); // Simulate slow operation
            _shutdownOrder.Add("SlowParticipant-End");
        });
        
        // Act - with a short timeout that should cancel the operation
        await _coordinator.ShutdownAll("Test shutdown with timeout", timeout: TimeSpan.FromMilliseconds(100));
        
        // Assert
        Assert.That(_shutdownOrder, Has.Count.EqualTo(1));
        Assert.That(_shutdownOrder[0], Is.EqualTo("SlowParticipant-Start"));
        Assert.That(_shutdownOrder, Does.Not.Contain("SlowParticipant-End"));
    }
    
    [Test]
    public void RegisterParticipant_WithDuplicateName_ThrowsException()
    {
        // Arrange
        string participantName = "DuplicateName";
        _coordinator.RegisterParticipant(participantName, 100, () => Task.CompletedTask);
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _coordinator.RegisterParticipant(participantName, 200, () => Task.CompletedTask));
    }
    
    private void RegisterParticipants()
    {
        // Register participants with different priorities
        _coordinator.RegisterParticipant("HighPriority", 100, () => 
        {
            _shutdownOrder.Add("HighPriority");
            return Task.CompletedTask;
        });
        
        _coordinator.RegisterParticipant("MediumPriority", 200, () => 
        {
            _shutdownOrder.Add("MediumPriority");
            return Task.CompletedTask;
        });
        
        _coordinator.RegisterParticipant("LowPriority", 300, () => 
        {
            _shutdownOrder.Add("LowPriority");
            return Task.CompletedTask;
        });
    }
}