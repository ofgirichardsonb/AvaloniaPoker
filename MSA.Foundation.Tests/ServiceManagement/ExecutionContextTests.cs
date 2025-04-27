using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

// Using alias to avoid ambiguity with System.Threading.ExecutionContext
using MSAEC = MSA.Foundation.ServiceManagement.ExecutionContext;

namespace MSA.Foundation.Tests.ServiceManagement
{
    public class ExecutionContextTests
    {
        [Fact]
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
        
        [Fact]
        public void Constructor_WithServiceId_ShouldUseProvidedId()
        {
            // Arrange
            string serviceId = "test-service-id";
            
            // Act
            var context = new MSAEC(serviceId);
            
            // Assert
            context.ServiceId.Should().Be(serviceId, "ServiceId should match the provided value");
        }
        
        [Fact]
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
        
        [Fact]
        public void Cancel_ShouldCancelTheToken()
        {
            // Arrange
            var context = new MSAEC();
            
            // Act
            context.Cancel();
            
            // Assert
            context.CancellationToken.IsCancellationRequested.Should().BeTrue("Token should be canceled after Cancel is called");
        }
        
        [Fact]
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
        
        [Fact]
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
        
        [Fact]
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
        
        [Fact]
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
        
        [Fact]
        public void GetMetadata_WithNonExistentKey_ShouldReturnDefaultValue()
        {
            // Arrange
            var context = new MSAEC();
            
            // Act
            var result = context.GetMetadata<string>("non-existent-key");
            
            // Assert
            result.Should().Be(default, "Non-existent key should return default value");
        }
        
        [Fact]
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
        
        [Fact]
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
        
        [Fact]
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
    }
}