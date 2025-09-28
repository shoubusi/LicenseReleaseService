using System;
using Xunit;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for RecoveryQueueStatus class
    /// </summary>
    public class RecoveryQueueStatusTests
    {
        [Fact]
        public void Constructor_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var status = new RecoveryQueueStatus();

            // Assert
            Assert.Equal(0, status.QueueSize);
            Assert.Equal(0, status.ActiveRecoveries);
            Assert.Equal(0, status.MaxQueueSize);
            Assert.Equal(0, status.MaxConcurrentRecoveries);
            Assert.Equal(RecoveryPriority.Normal, status.HighestPriorityPending);
            Assert.Null(status.OldestPendingRequest);
            Assert.Equal(TimeSpan.Zero, status.ProcessingInterval);
            Assert.Null(status.Statistics);
        }

        [Fact]
        public void Constructor_WithValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var status = new RecoveryQueueStatus
            {
                QueueSize = 5,
                ActiveRecoveries = 2,
                MaxQueueSize = 100,
                MaxConcurrentRecoveries = 10,
                HighestPriorityPending = RecoveryPriority.High,
                OldestPendingRequest = DateTime.Now.AddMinutes(-5),
                ProcessingInterval = TimeSpan.FromSeconds(30),
                Statistics = new RecoveryEngineStatistics
                {
                    TotalRequests = 100,
                    SuccessfulRecoveries = 80,
                    FailedRecoveries = 20
                }
            };

            // Assert
            Assert.Equal(5, status.QueueSize);
            Assert.Equal(2, status.ActiveRecoveries);
            Assert.Equal(100, status.MaxQueueSize);
            Assert.Equal(10, status.MaxConcurrentRecoveries);
            Assert.Equal(RecoveryPriority.High, status.HighestPriorityPending);
            Assert.NotNull(status.OldestPendingRequest);
            Assert.Equal(TimeSpan.FromSeconds(30), status.ProcessingInterval);
            Assert.NotNull(status.Statistics);
            Assert.Equal(100, status.Statistics.TotalRequests);
        }

        [Fact]
        public void IsQueueFull_WithQueueSizeLessThanMax_ShouldReturnFalse()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                QueueSize = 5,
                MaxQueueSize = 10
            };

            // Act & Assert
            Assert.False(status.IsQueueFull);
        }

        [Fact]
        public void IsQueueFull_WithQueueSizeEqualToMax_ShouldReturnTrue()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                QueueSize = 10,
                MaxQueueSize = 10
            };

            // Act & Assert
            Assert.True(status.IsQueueFull);
        }

        [Fact]
        public void IsQueueFull_WithQueueSizeGreaterThanMax_ShouldReturnTrue()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                QueueSize = 15,
                MaxQueueSize = 10
            };

            // Act & Assert
            Assert.True(status.IsQueueFull);
        }

        [Fact]
        public void IsAtMaxCapacity_WithActiveRecoveriesLessThanMax_ShouldReturnFalse()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                ActiveRecoveries = 3,
                MaxConcurrentRecoveries = 5
            };

            // Act & Assert
            Assert.False(status.IsAtMaxCapacity);
        }

        [Fact]
        public void IsAtMaxCapacity_WithActiveRecoveriesEqualToMax_ShouldReturnTrue()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                ActiveRecoveries = 5,
                MaxConcurrentRecoveries = 5
            };

            // Act & Assert
            Assert.True(status.IsAtMaxCapacity);
        }

        [Fact]
        public void IsAtMaxCapacity_WithActiveRecoveriesGreaterThanMax_ShouldReturnTrue()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                ActiveRecoveries = 7,
                MaxConcurrentRecoveries = 5
            };

            // Act & Assert
            Assert.True(status.IsAtMaxCapacity);
        }

        [Fact]
        public void HasPendingRequests_WithZeroQueueSize_ShouldReturnFalse()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                QueueSize = 0
            };

            // Act & Assert
            Assert.False(status.HasPendingRequests);
        }

        [Fact]
        public void HasPendingRequests_WithPositiveQueueSize_ShouldReturnTrue()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                QueueSize = 5
            };

            // Act & Assert
            Assert.True(status.HasPendingRequests);
        }

        [Fact]
        public void QueueUtilization_WithMaxQueueSizeZero_ShouldReturnZero()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                QueueSize = 5,
                MaxQueueSize = 0
            };

            // Act & Assert
            Assert.Equal(0.0, status.QueueUtilization);
        }

        [Fact]
        public void QueueUtilization_WithPositiveMaxQueueSize_ShouldCalculateCorrectly()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                QueueSize = 25,
                MaxQueueSize = 100
            };

            // Act & Assert
            Assert.Equal(25.0, status.QueueUtilization);
        }

        [Fact]
        public void QueueUtilization_WithFullQueue_ShouldReturn100()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                QueueSize = 100,
                MaxQueueSize = 100
            };

            // Act & Assert
            Assert.Equal(100.0, status.QueueUtilization);
        }

        [Fact]
        public void QueueUtilization_WithHalfFullQueue_ShouldReturn50()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                QueueSize = 50,
                MaxQueueSize = 100
            };

            // Act & Assert
            Assert.Equal(50.0, status.QueueUtilization);
        }

        [Fact]
        public void CapacityUtilization_WithMaxConcurrentRecoveriesZero_ShouldReturnZero()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                ActiveRecoveries = 3,
                MaxConcurrentRecoveries = 0
            };

            // Act & Assert
            Assert.Equal(0.0, status.CapacityUtilization);
        }

        [Fact]
        public void CapacityUtilization_WithPositiveMaxConcurrentRecoveries_ShouldCalculateCorrectly()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                ActiveRecoveries = 3,
                MaxConcurrentRecoveries = 10
            };

            // Act & Assert
            Assert.Equal(30.0, status.CapacityUtilization);
        }

        [Fact]
        public void CapacityUtilization_WithFullCapacity_ShouldReturn100()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                ActiveRecoveries = 10,
                MaxConcurrentRecoveries = 10
            };

            // Act & Assert
            Assert.Equal(100.0, status.CapacityUtilization);
        }

        [Fact]
        public void CapacityUtilization_WithHalfCapacity_ShouldReturn50()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                ActiveRecoveries = 5,
                MaxConcurrentRecoveries = 10
            };

            // Act & Assert
            Assert.Equal(50.0, status.CapacityUtilization);
        }

        [Fact]
        public void ToString_WithMinimalValues_ShouldReturnBasicString()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                QueueSize = 5,
                ActiveRecoveries = 2,
                MaxQueueSize = 100,
                MaxConcurrentRecoveries = 10,
                ProcessingInterval = TimeSpan.FromSeconds(30),
                Statistics = new RecoveryEngineStatistics
                {
                    TotalRequests = 10,
                    SuccessfulRecoveries = 8,
                    FailedRecoveries = 2
                }
            };

            // Act
            var result = status.ToString();

            // Assert
            Assert.Contains("Queue=5/100", result);
            Assert.Contains("Active=2/10", result);
            Assert.Contains("HighestPriority=Normal", result);
            Assert.Contains("ProcessingInterval=30.0s", result);
            Assert.Contains("SuccessRate=80.00%", result);
        }

        [Fact]
        public void ToString_WithFullValues_ShouldReturnDetailedString()
        {
            // Arrange
            var oldestTime = new DateTime(2023, 12, 25, 10, 30, 45);
            var status = new RecoveryQueueStatus
            {
                QueueSize = 75,
                ActiveRecoveries = 8,
                MaxQueueSize = 100,
                MaxConcurrentRecoveries = 10,
                HighestPriorityPending = RecoveryPriority.Critical,
                OldestPendingRequest = oldestTime,
                ProcessingInterval = TimeSpan.FromMinutes(2),
                Statistics = new RecoveryEngineStatistics
                {
                    TotalRequests = 1000,
                    SuccessfulRecoveries = 950,
                    FailedRecoveries = 50
                }
            };

            // Act
            var result = status.ToString();

            // Assert
            Assert.Contains("Queue=75/100", result);
            Assert.Contains("(75.0%)", result);
            Assert.Contains("Active=8/10", result);
            Assert.Contains("(80.0%)", result);
            Assert.Contains("HighestPriority=Critical", result);
            Assert.Contains("Oldest=2023-12-25 10:30:45", result);
            Assert.Contains("ProcessingInterval=120.0s", result);
            Assert.Contains("SuccessRate=95.00%", result);
        }

        [Fact]
        public void ToString_WithNullOldestPendingRequest_ShouldNotIncludeOldest()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                QueueSize = 5,
                ActiveRecoveries = 2,
                MaxQueueSize = 100,
                MaxConcurrentRecoveries = 10,
                HighestPriorityPending = RecoveryPriority.Normal,
                OldestPendingRequest = null,
                ProcessingInterval = TimeSpan.FromSeconds(30),
                Statistics = new RecoveryEngineStatistics
                {
                    TotalRequests = 10,
                    SuccessfulRecoveries = 8,
                    FailedRecoveries = 2
                }
            };

            // Act
            var result = status.ToString();

            // Assert
            Assert.Contains("Queue=5/100", result);
            Assert.Contains("Active=2/10", result);
            Assert.Contains("HighestPriority=Normal", result);
            Assert.Contains("Oldest=null", result);
            Assert.Contains("ProcessingInterval=30.0s", result);
            Assert.Contains("SuccessRate=80.00%", result);
        }

        [Fact]
        public void ToString_WithNullStatistics_ShouldNotIncludeSuccessRate()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                QueueSize = 5,
                ActiveRecoveries = 2,
                MaxQueueSize = 100,
                MaxConcurrentRecoveries = 10,
                HighestPriorityPending = RecoveryPriority.Normal,
                OldestPendingRequest = null,
                ProcessingInterval = TimeSpan.FromSeconds(30),
                Statistics = null
            };

            // Act
            var result = status.ToString();

            // Assert
            Assert.Contains("Queue=5/100", result);
            Assert.Contains("Active=2/10", result);
            Assert.Contains("HighestPriority=Normal", result);
            Assert.Contains("Oldest=null", result);
            Assert.Contains("ProcessingInterval=30.0s", result);
            Assert.DoesNotContain("SuccessRate", result);
        }

        [Fact]
        public void ToString_WithZeroProcessingInterval_ShouldDisplayZero()
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                QueueSize = 0,
                ActiveRecoveries = 0,
                MaxQueueSize = 100,
                MaxConcurrentRecoveries = 10,
                HighestPriorityPending = RecoveryPriority.Normal,
                ProcessingInterval = TimeSpan.Zero,
                Statistics = new RecoveryEngineStatistics
                {
                    TotalRequests = 0,
                    SuccessfulRecoveries = 0,
                    FailedRecoveries = 0
                }
            };

            // Act
            var result = status.ToString();

            // Assert
            Assert.Contains("Queue=0/100", result);
            Assert.Contains("Active=0/10", result);
            Assert.Contains("ProcessingInterval=0.0s", result);
            Assert.Contains("SuccessRate=0.00%", result);
        }

        [Theory]
        [InlineData(RecoveryPriority.Low)]
        [InlineData(RecoveryPriority.Normal)]
        [InlineData(RecoveryPriority.High)]
        [InlineData(RecoveryPriority.Critical)]
        public void ToString_WithDifferentPriorities_ShouldDisplayCorrectly(RecoveryPriority priority)
        {
            // Arrange
            var status = new RecoveryQueueStatus
            {
                QueueSize = 5,
                ActiveRecoveries = 2,
                MaxQueueSize = 100,
                MaxConcurrentRecoveries = 10,
                HighestPriorityPending = priority,
                ProcessingInterval = TimeSpan.FromSeconds(30),
                Statistics = new RecoveryEngineStatistics
                {
                    TotalRequests = 10,
                    SuccessfulRecoveries = 8,
                    FailedRecoveries = 2
                }
            };

            // Act
            var result = status.ToString();

            // Assert
            Assert.Contains($"HighestPriority={priority}", result);
        }
    }
}