using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;

// Using alias to avoid ambiguity with System.Threading.ExecutionContext
using MSAEC = MSA.Foundation.ServiceManagement.ExecutionContext;

namespace MSA.Foundation.Tests.ServiceManagement
{
    [TestFixture]
    public class ExecutionContextTests
    {
        [Test]
        public void Constructor_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            var context = new MSAEC();
            
            // Assert
            context.Should().NotBeNull();
            context.ServiceId.Should().NotBeEmpty("ServiceId should be initialized with a GUID");
            context.CancellationTokenSource.Should().NotBeNull("CancellationTokenSource should be initialized");
            context.CancellationToken.Should().NotBe(CancellationToken.None, "CancellationToken should be linked to CancellationTokenSource");
        }
        
        [Test]
        public void Constructor_WithServiceId_ShouldUseProvidedId()
        {
            // Arrange
            string serviceId = "test-service-id";
            
            // Act
            var context = new MSAEC(serviceId);
            
            // Assert
            context.ServiceId.Should().Be(serviceId, "ServiceId should match the provided value");
        }
        
        [Test]
        public void Constructor_WithServiceIdAndToken_ShouldUseProvidedValues()
        {
            // Arrange
            string serviceId = "test-service-id";
            var cts = new CancellationTokenSource();
            
            // Act
            var context = new MSAEC(serviceId, cts.Token);
            
            // Assert
            context.ServiceId.Should().Be(serviceId, "ServiceId should match the provided value");
            context.CancellationToken.Should().Be(cts.Token, "CancellationToken should match the provided token");
        }
        
        [Test]
        public void Cancel_ShouldCancelTheToken()
        {
            // Arrange
            var context = new MSAEC();
            
            // Act
            context.Cancel();
            
            // Assert
            context.CancellationToken.IsCancellationRequested.Should().BeTrue("Token should be canceled after Cancel is called");
        }
        
        [Test]
        public async Task WithTimeout_ShouldCancelAfterSpecifiedTimeout()
        {
            // Arrange
            var context = new MSAEC();
            var timeout = TimeSpan.FromMilliseconds(50);
            
            // Act
            var contextWithTimeout = context.WithTimeout(timeout);
            
            // Wait longer than the timeout
            await Task.Delay(timeout.Add(TimeSpan.FromMilliseconds(50)));
            
            // Assert
            contextWithTimeout.CancellationToken.IsCancellationRequested.Should().BeTrue("Token should be canceled after timeout period");
        }
        
        [Test]
        public void GetMetadata_ShouldReturnEmptyDictionary_WhenNoMetadataExists()
        {
            // Arrange
            var context = new MSAEC();
            
            // Act
            var metadata = context.GetMetadata();
            
            // Assert
            metadata.Should().NotBeNull("Metadata dictionary should not be null");
            metadata.Should().BeEmpty("Metadata dictionary should be empty initially");
        }
        
        [Test]
        public void SetMetadata_ShouldStoreAndRetrieveValues()
        {
            // Arrange
            var context = new MSAEC();
            string key = "test-key";
            string value = "test-value";
            
            // Act
            context.SetMetadata(key, value);
            var retrievedValue = context.GetMetadata<string>(key);
            
            // Assert
            retrievedValue.Should().Be(value, "Retrieved metadata value should match the stored value");
        }
        
        [Test]
        public void SetMetadata_WithMultipleValues_ShouldStoreAllValues()
        {
            // Arrange
            var context = new MSAEC();
            var metadata = new Dictionary<string, object>
            {
                { "key1", "value1" },
                { "key2", 42 },
                { "key3", true }
            };
            
            // Act
            foreach (var kvp in metadata)
            {
                context.SetMetadata(kvp.Key, kvp.Value);
            }
            
            // Assert
            var allMetadata = context.GetMetadata();
            allMetadata.Should().HaveCount(metadata.Count, "All metadata items should be stored");
            context.GetMetadata<string>("key1").Should().Be("value1");
            context.GetMetadata<int>("key2").Should().Be(42);
            context.GetMetadata<bool>("key3").Should().BeTrue();
        }
        
        [Test]
        public void GetMetadata_WithNonExistentKey_ShouldReturnDefaultValue()
        {
            // Arrange
            var context = new MSAEC();
            
            // Act
            var result = context.GetMetadata<string>("non-existent-key");
            
            // Assert
            result.Should().Be(default, "Non-existent key should return default value");
        }
        
        [Test]
        public void GetMetadata_WithIncorrectType_ShouldThrowException()
        {
            // Arrange
            var context = new MSAEC();
            context.SetMetadata("numeric-key", 42);
            
            // Act
            Action action = () => context.GetMetadata<string>("numeric-key");
            
            // Assert
            action.Should().Throw<InvalidCastException>("Attempting to retrieve metadata with incorrect type should throw");
        }
        
        [Test]
        public void RemoveMetadata_ShouldRemoveExistingKey()
        {
            // Arrange
            var context = new MSAEC();
            string key = "test-key";
            string value = "test-value";
            context.SetMetadata(key, value);
            
            // Act
            context.RemoveMetadata(key);
            
            // Assert
            var metadata = context.GetMetadata();
            metadata.Should().NotContainKey(key, "Key should be removed from metadata");
        }
        
        [Test]
        public void ClearMetadata_ShouldRemoveAllMetadata()
        {
            // Arrange
            var context = new MSAEC();
            context.SetMetadata("key1", "value1");
            context.SetMetadata("key2", "value2");
            
            // Act
            context.ClearMetadata();
            
            // Assert
            var metadata = context.GetMetadata();
            metadata.Should().BeEmpty("All metadata should be cleared");
        }
        
        [Test]
        public void Constructor_WithThread_ShouldInitializeWithProvidedThread()
        {
            // Arrange
            var thread = new Thread(() => { });
            
            // Act
            var context = new MSAEC(thread);
            
            // Assert
            context.ThreadId.Should().Be(thread.ManagedThreadId, "ThreadId should match the provided thread");
            context.ServiceId.Should().NotBeEmpty("ServiceId should be initialized with a GUID");
            context.IsRunning.Should().BeTrue("IsRunning should be initialized as true");
        }
        
        [Test]
        public void IsRunning_ShouldReturnCorrectState()
        {
            // Arrange
            var context = new MSAEC();
            
            // Act & Assert
            context.IsRunning.Should().BeTrue("IsRunning should be initialized as true");
            
            // Stop the context
            context.Stop();
            
            // Assert again
            context.IsRunning.Should().BeFalse("IsRunning should be false after Stop is called");
        }
        
        [Test]
        public void Stop_ShouldCancelTokenAndSetIsRunningToFalse()
        {
            // Arrange
            var context = new MSAEC();
            
            // Act
            context.Stop();
            
            // Assert
            context.IsRunning.Should().BeFalse("IsRunning should be false after Stop is called");
            context.CancellationToken.IsCancellationRequested.Should().BeTrue("Token should be canceled after Stop is called");
        }
        
        [Test]
        public void Stop_WhenAlreadyStopped_ShouldDoNothing()
        {
            // Arrange
            var context = new MSAEC();
            context.Stop(); // First stop
            
            // Act - No exception should be thrown
            Action action = () => context.Stop(); // Second stop
            
            // Assert
            action.Should().NotThrow("Stopping an already stopped context should not throw");
        }
        
        [Test]
        public async Task RunAsync_WithAction_ShouldExecuteAction()
        {
            // Arrange
            var context = new MSAEC();
            bool actionExecuted = false;
            
            // Act
            await context.RunAsync(() => actionExecuted = true);
            
            // Assert
            actionExecuted.Should().BeTrue("The action should be executed");
        }
        
        [Test]
        public async Task RunAsync_WithFunc_ShouldReturnResult()
        {
            // Arrange
            var context = new MSAEC();
            int expectedResult = 42;
            
            // Act
            var result = await context.RunAsync(() => expectedResult);
            
            // Assert
            result.Should().Be(expectedResult, "The function result should be returned");
        }
        
        [Test]
        public async Task RunAsync_WhenStopped_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var context = new MSAEC();
            context.Stop();
            
            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await context.RunAsync(() => { }));
            Assert.That(ex, Is.Not.Null);
        }
        
        [Test]
        public async Task RunAsync_WithFunc_WhenStopped_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var context = new MSAEC();
            context.Stop();
            
            // Act & Assert
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await context.RunAsync(() => 42));
            Assert.That(ex, Is.Not.Null);
        }
        
        [Test]
        public void Dispose_ShouldStopAndDispose()
        {
            // Arrange
            var context = new MSAEC();
            
            // Act
            context.Dispose();
            
            // Assert
            context.IsRunning.Should().BeFalse("IsRunning should be false after Dispose is called");
            context.CancellationToken.IsCancellationRequested.Should().BeTrue("Token should be canceled after Dispose is called");
        }
    }
}