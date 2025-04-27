using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using MSA.Foundation.Telemetry;
using Xunit;
using FluentAssertions;
using Moq;

namespace MSA.Foundation.Tests.Telemetry
{
    public class TelemetryServiceTests
    {
        [Fact]
        public void Initialize_WithoutInstrumentationKey_ShouldReturnFalse()
        {
            // Arrange
            var telemetryService = TelemetryService.Instance;
            
            // Act
            var result = telemetryService.Initialize("");
            
            // Assert
            telemetryService.Should().NotBeNull();
            result.Should().BeFalse("Initialize should return false when no instrumentation key is provided");
        }
        
        [Fact]
        public void Initialize_WithInstrumentationKey_ShouldReturnTrue()
        {
            // Arrange
            var telemetryService = TelemetryService.Instance;
            
            // Act - Note: this won't actually connect to Azure, but the method should return true
            var result = telemetryService.Initialize("test-key");
            
            // Assert
            telemetryService.Should().NotBeNull();
            result.Should().BeTrue("Initialize should return true when an instrumentation key is provided");
        }
        
        [Fact]
        public void TrackEvent_WhenTelemetryIsEnabled_ShouldNotThrowException()
        {
            // Arrange
            var telemetryService = TelemetryService.Instance;
            telemetryService.Initialize("test-key");
            
            // Act
            Action action = () => telemetryService.TrackEvent("TestEvent");
            
            // Assert
            action.Should().NotThrow("TrackEvent should not throw when telemetry is enabled");
        }
        
        [Fact]
        public void TrackEvent_WithProperties_ShouldNotThrowException()
        {
            // Arrange
            var telemetryService = TelemetryService.Instance;
            telemetryService.Initialize("test-key");
            var properties = new Dictionary<string, string> { { "PropertyKey", "PropertyValue" } };
            
            // Act
            Action action = () => telemetryService.TrackEvent("TestEvent", properties);
            
            // Assert
            action.Should().NotThrow("TrackEvent with properties should not throw when telemetry is enabled");
        }
        
        [Fact]
        public void TrackException_WhenTelemetryIsEnabled_ShouldNotThrowException()
        {
            // Arrange
            var telemetryService = TelemetryService.Instance;
            telemetryService.Initialize("test-key");
            var exception = new InvalidOperationException("Test exception");
            
            // Act
            Action action = () => telemetryService.TrackException(exception);
            
            // Assert
            action.Should().NotThrow("TrackException should not throw when telemetry is enabled");
        }
        
        [Fact]
        public void TrackException_WithProperties_ShouldNotThrowException()
        {
            // Arrange
            var telemetryService = TelemetryService.Instance;
            telemetryService.Initialize("test-key");
            var exception = new InvalidOperationException("Test exception");
            var properties = new Dictionary<string, string> { { "PropertyKey", "PropertyValue" } };
            
            // Act
            Action action = () => telemetryService.TrackException(exception, properties);
            
            // Assert
            action.Should().NotThrow("TrackException with properties should not throw when telemetry is enabled");
        }
        
        [Fact]
        public void TrackTrace_WhenTelemetryIsEnabled_ShouldNotThrowException()
        {
            // Arrange
            var telemetryService = TelemetryService.Instance;
            telemetryService.Initialize("test-key");
            
            // Act
            Action action = () => telemetryService.TrackTrace("Test trace message");
            
            // Assert
            action.Should().NotThrow("TrackTrace should not throw when telemetry is enabled");
        }
        
        [Fact]
        public void TrackTrace_WithProperties_ShouldNotThrowException()
        {
            // Arrange
            var telemetryService = TelemetryService.Instance;
            telemetryService.Initialize("test-key");
            var properties = new Dictionary<string, string> { { "PropertyKey", "PropertyValue" } };
            
            // Act
            Action action = () => telemetryService.TrackTrace("Test trace message", properties);
            
            // Assert
            action.Should().NotThrow("TrackTrace with properties should not throw when telemetry is enabled");
        }
        
        [Fact]
        public void TrackRequest_WhenTelemetryIsEnabled_ShouldNotThrowException()
        {
            // Arrange
            var telemetryService = TelemetryService.Instance;
            telemetryService.Initialize("test-key");
            var startTime = DateTimeOffset.UtcNow;
            var duration = TimeSpan.FromMilliseconds(100);
            
            // Act
            Action action = () => telemetryService.TrackRequest("TestRequest", startTime, duration, "200", true);
            
            // Assert
            action.Should().NotThrow("TrackRequest should not throw when telemetry is enabled");
        }
        
        [Fact]
        public void TrackDependency_WhenTelemetryIsEnabled_ShouldNotThrowException()
        {
            // Arrange
            var telemetryService = TelemetryService.Instance;
            telemetryService.Initialize("test-key");
            var startTime = DateTimeOffset.UtcNow;
            var duration = TimeSpan.FromMilliseconds(100);
            
            // Act
            Action action = () => telemetryService.TrackDependency("HTTP", "test-target", "TestDependency", "TestCommand", startTime, duration, true);
            
            // Assert
            action.Should().NotThrow("TrackDependency should not throw when telemetry is enabled");
        }
        
        [Fact]
        public void Flush_ShouldNotThrowException()
        {
            // Arrange
            var telemetryService = TelemetryService.Instance;
            telemetryService.Initialize("test-key");
            
            // Act
            Action action = () => telemetryService.Flush();
            
            // Assert
            action.Should().NotThrow("Flush should not throw exception");
        }
        
        // Helper methods
        
        private IConfiguration CreateConfigurationWithKey(string instrumentationKey)
        {
            var configValues = new Dictionary<string, string>
            {
                {"ApplicationInsights:InstrumentationKey", instrumentationKey}
            };
            
            return new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();
        }
        
        private IConfiguration CreateConfigurationWithoutKey()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>())
                .Build();
        }
    }
}