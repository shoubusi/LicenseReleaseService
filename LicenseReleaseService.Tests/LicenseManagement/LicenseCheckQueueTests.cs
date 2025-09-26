using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for the LicenseCheckQueue class
    /// </summary>
    public class LicenseCheckQueueTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ILogger<LicenseCheckQueue>> _mockLogger;
        private readonly TimerExecutionOptions _options;

        public LicenseCheckQueueTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger<LicenseCheckQueue>>();
            _options = new TimerExecutionOptions
            {
                MaxConcurrentExecutions = 2,
                SyncTimeout = TimeSpan.FromSeconds(5)
            };
        }

        [Fact]
        public void Constructor_WithValidParameters_InitializesQueue()
        {
            // Arrange
            var logger = _mockLogger.Object;

            // Act
            var queue = new LicenseCheckQueue(logger, _options);

            // Assert
            Assert.Equal(0, queue.QueueSize);
            Assert.Equal(0, queue.ExecutingCount);
            Assert.Equal(_options.MaxConcurrentExecutions, queue.MaxConcurrentOperations);
            Assert.Equal(0, queue.TotalOperationsProcessed);
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new LicenseCheckQueue(null, _options));
        }

        [Fact]
        public void Constructor_WithNullOptions_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new LicenseCheckQueue(_mockLogger.Object, null));
        }

        [Fact]
        public async Task EnqueueAsync_WithValidOperation_EnqueuesOperation()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);

            // Act
            await queue.EnqueueAsync(operation);

            // Assert
            Assert.Equal(1, queue.QueueSize);
        }

        [Fact]
        public async Task EnqueueAsync_WithNullOperation_ThrowsArgumentNullException()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => queue.EnqueueAsync(null));
        }

        [Fact]
        public async Task EnqueueRangeAsync_WithMultipleOperations_EnqueuesAllOperations()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operations = new[]
            {
                LicenseCheckOperation.CreateStatusCheck("localhost", 27000),
                LicenseCheckOperation.CreateFeatureCheck("localhost", 27000, "feature1"),
                LicenseCheckOperation.CreateUserCheck("localhost", 27000, "user1")
            };

            // Act
            await queue.EnqueueRangeAsync(operations);

            // Assert
            Assert.Equal(3, queue.QueueSize);
        }

        [Fact]
        public async Task EnqueueRangeAsync_WithNullOperations_ThrowsArgumentNullException()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => queue.EnqueueRangeAsync(null));
        }

        [Fact]
        public async Task DequeueAsync_WithEmptyQueue_ReturnsNull()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);

            // Act
            var operation = await queue.DequeueAsync();

            // Assert
            Assert.Null(operation);
        }

        [Fact]
        public async Task DequeueAsync_WithReadyOperation_ReturnsOperation()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            await queue.EnqueueAsync(operation);

            // Act
            var dequeuedOperation = await queue.DequeueAsync();

            // Assert
            Assert.NotNull(dequeuedOperation);
            Assert.Equal(operation.OperationId, dequeuedOperation.OperationId);
            Assert.Equal(LicenseCheckOperationStatus.Executing, dequeuedOperation.Status);
            Assert.Equal(0, queue.QueueSize); // Should be removed from queue
            Assert.Equal(1, queue.ExecutingCount); // Should be added to executing operations
        }

        [Fact]
        public async Task DequeueAsync_WithScheduledOperationInFuture_PutsOperationBack()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            operation.ScheduleFor(DateTime.Now.AddMinutes(1));
            await queue.EnqueueAsync(operation);

            // Act
            var dequeuedOperation = await queue.DequeueAsync();

            // Assert
            Assert.Null(dequeuedOperation);
            Assert.Equal(1, queue.QueueSize); // Should still be in queue
            Assert.Equal(0, queue.ExecutingCount);
        }

        [Fact]
        public async Task DequeueAsync_WithScheduledOperationInPast_ReturnsOperation()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            operation.ScheduleFor(DateTime.Now.AddMinutes(-1));
            await queue.EnqueueAsync(operation);

            // Act
            var dequeuedOperation = await queue.DequeueAsync();

            // Assert
            Assert.NotNull(dequeuedOperation);
            Assert.Equal(operation.OperationId, dequeuedOperation.OperationId);
            Assert.Equal(0, queue.QueueSize);
            Assert.Equal(1, queue.ExecutingCount);
        }

        [Fact]
        public async Task DequeueAsync_WhenSemaphoreNotAcquired_ReturnsNull()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var options = new TimerExecutionOptions
            {
                MaxConcurrentExecutions = 0, // No concurrent executions allowed
                SyncTimeout = TimeSpan.FromMilliseconds(10)
            };
            var restrictedQueue = new LicenseCheckQueue(_mockLogger.Object, options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            await restrictedQueue.EnqueueAsync(operation);

            // Act
            var dequeuedOperation = await restrictedQueue.DequeueAsync();

            // Assert
            Assert.Null(dequeuedOperation);
            Assert.Equal(1, restrictedQueue.QueueSize); // Should still be in queue
        }

        [Fact]
        public void GetQueuedOperations_ReturnsAllQueuedOperations()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation1 = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            var operation2 = LicenseCheckOperation.CreateFeatureCheck("localhost", 27000, "feature1");

            // Act
            queue.EnqueueAsync(operation1).Wait();
            queue.EnqueueAsync(operation2).Wait();

            var queuedOperations = queue.GetQueuedOperations().ToList();

            // Assert
            Assert.Equal(2, queuedOperations.Count);
            Assert.Contains(queuedOperations, op => op.OperationId == operation1.OperationId);
            Assert.Contains(queuedOperations, op => op.OperationId == operation2.OperationId);
        }

        [Fact]
        public void GetExecutingOperations_ReturnsAllExecutingOperations()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            queue.EnqueueAsync(operation).Wait();
            queue.DequeueAsync().Wait(); // This will put it in executing state

            // Act
            var executingOperations = queue.GetExecutingOperations().ToList();

            // Assert
            Assert.Single(executingOperations);
            Assert.Equal(operation.OperationId, executingOperations[0].OperationId);
        }

        [Fact]
        public void GetCompletedOperations_ReturnsCompletedOperations()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            queue.EnqueueAsync(operation).Wait();
            queue.DequeueAsync().Wait();

            // Simulate operation completion
            var result = LicenseCheckResult.CreateSuccess(operation);
            queue.MarkOperationCompleted(operation.OperationId, result);

            // Act
            var completedOperations = queue.GetCompletedOperations().ToList();

            // Assert
            Assert.Single(completedOperations);
            Assert.Equal(operation.OperationId, completedOperations[0].OperationId);
        }

        [Fact]
        public void MarkOperationCompleted_MarksOperationAsCompleted()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            queue.EnqueueAsync(operation).Wait();
            queue.DequeueAsync().Wait();

            var result = LicenseCheckResult.CreateSuccess(operation);

            // Act
            queue.MarkOperationCompleted(operation.OperationId, result);

            // Assert
            Assert.Equal(0, queue.ExecutingCount);
            Assert.Equal(1, queue.TotalOperationsProcessed);
        }

        [Fact]
        public void MarkOperationFailed_MarksOperationAsFailed()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            queue.EnqueueAsync(operation).Wait();
            queue.DequeueAsync().Wait();

            var errorMessage = "Test error message";
            var exception = new InvalidOperationException("Test exception");

            // Act
            queue.MarkOperationFailed(operation.OperationId, errorMessage, exception);

            // Assert
            Assert.Equal(0, queue.ExecutingCount);
            Assert.Equal(1, queue.TotalOperationsProcessed);
        }

        [Fact]
        public void MarkOperationCancelled_MarksOperationAsCancelled()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            queue.EnqueueAsync(operation).Wait();
            queue.DequeueAsync().Wait();

            // Act
            queue.MarkOperationCancelled(operation.OperationId);

            // Assert
            Assert.Equal(0, queue.ExecutingCount);
            Assert.Equal(1, queue.TotalOperationsProcessed);
        }

        [Fact]
        public void MarkOperationTimedOut_MarksOperationAsTimedOut()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            queue.EnqueueAsync(operation).Wait();
            queue.DequeueAsync().Wait();

            // Act
            queue.MarkOperationTimedOut(operation.OperationId);

            // Assert
            Assert.Equal(0, queue.ExecutingCount);
            Assert.Equal(1, queue.TotalOperationsProcessed);
        }

        [Fact]
        public void CancelOperation_WithQueuedOperation_CancelsOperation()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            queue.EnqueueAsync(operation).Wait();

            // Act
            var result = queue.CancelOperation(operation.OperationId);

            // Assert
            Assert.True(result);
            Assert.Equal(0, queue.QueueSize);
        }

        [Fact]
        public void CancelOperation_WithExecutingOperation_CancelsOperation()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            queue.EnqueueAsync(operation).Wait();
            queue.DequeueAsync().Wait();

            // Act
            var result = queue.CancelOperation(operation.OperationId);

            // Assert
            Assert.True(result);
            Assert.Equal(0, queue.ExecutingCount);
        }

        [Fact]
        public void CancelOperation_WithNonexistentOperation_ReturnsFalse()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var nonexistentOperationId = Guid.NewGuid();

            // Act
            var result = queue.CancelOperation(nonexistentOperationId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CancelAllOperationsAsync_CancelsAllOperations()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation1 = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            var operation2 = LicenseCheckOperation.CreateFeatureCheck("localhost", 27000, "feature1");
            await queue.EnqueueAsync(operation1);
            await queue.EnqueueAsync(operation2);
            await queue.DequeueAsync(); // Start executing one operation

            // Act
            await queue.CancelAllOperationsAsync();

            // Assert
            Assert.Equal(0, queue.QueueSize);
            Assert.Equal(0, queue.ExecutingCount);
        }

        [Fact]
        public void GetStatus_ReturnsQueueStatus()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            queue.EnqueueAsync(operation).Wait();
            queue.DequeueAsync().Wait();

            var result = LicenseCheckResult.CreateSuccess(operation);
            queue.MarkOperationCompleted(operation.OperationId, result);

            // Act
            var status = queue.GetStatus();

            // Assert
            Assert.NotNull(status);
            Assert.Equal(0, status.QueueSize);
            Assert.Equal(0, status.ExecutingCount);
            Assert.Equal(1, status.CompletedCount);
            Assert.Equal(0, status.FailedCount);
            Assert.Equal(0, status.CancelledCount);
            Assert.Equal(0, status.TimedOutCount);
            Assert.Equal(1.0, status.SuccessRate);
            Assert.True(status.Timestamp <= DateTime.Now);
        }

        [Fact]
        public void OperationEnqueued_EventIsRaised()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            LicenseCheckOperationEventArgs eventArgs = null;

            queue.OperationEnqueued += (sender, args) =>
            {
                eventArgs = args;
            };

            // Act
            queue.EnqueueAsync(operation).Wait();

            // Assert
            Assert.NotNull(eventArgs);
            Assert.Equal(operation.OperationId, eventArgs.OperationId);
            Assert.Equal(LicenseCheckOperationStatus.Queued, eventArgs.OperationStatus);
        }

        [Fact]
        public void OperationDequeued_EventIsRaised()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            LicenseCheckOperationEventArgs eventArgs = null;

            queue.OperationDequeued += (sender, args) =>
            {
                eventArgs = args;
            };

            queue.EnqueueAsync(operation).Wait();

            // Act
            queue.DequeueAsync().Wait();

            // Assert
            Assert.NotNull(eventArgs);
            Assert.Equal(operation.OperationId, eventArgs.OperationId);
            Assert.Equal(LicenseCheckOperationStatus.Executing, eventArgs.OperationStatus);
        }

        [Fact]
        public void OperationStarted_EventIsRaised()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            LicenseCheckOperationEventArgs eventArgs = null;

            queue.OperationStarted += (sender, args) =>
            {
                eventArgs = args;
            };

            queue.EnqueueAsync(operation).Wait();

            // Act
            queue.DequeueAsync().Wait();

            // Assert
            Assert.NotNull(eventArgs);
            Assert.Equal(operation.OperationId, eventArgs.OperationId);
            Assert.Equal(LicenseCheckOperationStatus.Executing, eventArgs.OperationStatus);
        }

        [Fact]
        public void OperationCompleted_EventIsRaised()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            LicenseCheckOperationEventArgs eventArgs = null;

            queue.OperationCompleted += (sender, args) =>
            {
                eventArgs = args;
            };

            queue.EnqueueAsync(operation).Wait();
            queue.DequeueAsync().Wait();

            var result = LicenseCheckResult.CreateSuccess(operation);

            // Act
            queue.MarkOperationCompleted(operation.OperationId, result);

            // Assert
            Assert.NotNull(eventArgs);
            Assert.Equal(operation.OperationId, eventArgs.OperationId);
            Assert.True(eventArgs.Success);
        }

        [Fact]
        public void OperationFailed_EventIsRaised()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            LicenseCheckOperationEventArgs eventArgs = null;

            queue.OperationFailed += (sender, args) =>
            {
                eventArgs = args;
            };

            queue.EnqueueAsync(operation).Wait();
            queue.DequeueAsync().Wait();

            var errorMessage = "Test error message";

            // Act
            queue.MarkOperationFailed(operation.OperationId, errorMessage);

            // Assert
            Assert.NotNull(eventArgs);
            Assert.Equal(operation.OperationId, eventArgs.OperationId);
            Assert.Equal(errorMessage, eventArgs.ErrorMessage);
            Assert.False(eventArgs.Success);
        }

        [Fact]
        public async Task ProcessQueueAsync_ProcessesOperationsConcurrently()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operations = new List<LicenseCheckOperation>();

            for (int i = 0; i < 5; i++)
            {
                var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
                operations.Add(operation);
                await queue.EnqueueAsync(operation);
            }

            // Act
            // Allow time for the queue processor to work
            await Task.Delay(1000);

            // Assert
            // The queue should have processed some operations
            var status = queue.GetStatus();
            Assert.True(status.TotalProcessed > 0, "Queue should have processed some operations");
        }

        [Fact]
        public void Dispose_CleansUpResources()
        {
            // Arrange
            var queue = new LicenseCheckQueue(_mockLogger.Object, _options);
            var operation = LicenseCheckOperation.CreateStatusCheck("localhost", 27000);
            queue.EnqueueAsync(operation).Wait();

            // Act
            queue.Dispose();

            // Assert
            // The queue should be disposed and no longer usable
            // We can verify this by checking that the queue is empty and no operations are executing
            Assert.Equal(0, queue.QueueSize);
            Assert.Equal(0, queue.ExecutingCount);
        }

        public void Dispose()
        {
            // Clean up any resources if needed
        }
    }
}