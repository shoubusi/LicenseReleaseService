using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for the LicenseCheckOperation class
    /// </summary>
    public class LicenseCheckOperationTests
    {
        private readonly ITestOutputHelper _output;

        public LicenseCheckOperationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Constructor_Default_InitializesWithDefaultValues()
        {
            // Arrange & Act
            var operation = new LicenseCheckOperation();

            // Assert
            Assert.NotEqual(Guid.Empty, operation.OperationId);
            Assert.Equal(LicenseCheckOperationType.ServerStatusCheck, operation.OperationType);
            Assert.Equal(string.Empty, operation.Server);
            Assert.Equal(0, operation.Port);
            Assert.Equal(LicenseCheckOperationPriority.Normal, operation.Priority);
            Assert.Equal(TimeSpan.FromMinutes(2), operation.Timeout);
            Assert.Equal(3, operation.MaxRetries);
            Assert.Equal(TimeSpan.FromSeconds(5), operation.RetryDelay);
            Assert.NotNull(operation.Parameters);
            Assert.Empty(operation.Parameters);
            Assert.NotNull(operation.Tags);
            Assert.Empty(operation.Tags);
            Assert.Equal(string.Empty, operation.Description);
            Assert.True(operation.CreatedAt <= DateTime.Now);
            Assert.False(operation.ScheduledFor.HasValue);
            Assert.False(operation.ExecutedAt.HasValue);
            Assert.False(operation.CompletedAt.HasValue);
            Assert.Equal(LicenseCheckOperationStatus.Pending, operation.Status);
            Assert.False(operation.IsRecurring);
            Assert.False(operation.RecurringInterval.HasValue);
            Assert.False(operation.MaxRecurringExecutions.HasValue);
            Assert.Equal(0, operation.RecurringExecutionCount);
            Assert.NotNull(operation.Dependencies);
            Assert.Empty(operation.Dependencies);
            Assert.True(operation.CacheResult);
            Assert.Equal(TimeSpan.FromMinutes(5), operation.CacheDuration);
            Assert.False(operation.EnableDetailedLogging);
            Assert.Equal(0, operation.RetryAttempt);
            Assert.Equal(string.Empty, operation.LastError);
        }

        [Fact]
        public void Constructor_WithParameters_SetsValuesCorrectly()
        {
            // Arrange
            var operationType = LicenseCheckOperationType.FeatureStatusCheck;
            var server = "test-server";
            var port = 27000;
            var feature = "test-feature";
            var user = "test-user";
            var priority = LicenseCheckOperationPriority.High;
            var timeout = TimeSpan.FromMinutes(5);
            var maxRetries = 5;
            var retryDelay = TimeSpan.FromSeconds(10);

            // Act
            var operation = new LicenseCheckOperation(operationType, server, port, feature, user, priority, timeout, maxRetries, retryDelay);

            // Assert
            Assert.Equal(operationType, operation.OperationType);
            Assert.Equal(server, operation.Server);
            Assert.Equal(port, operation.Port);
            Assert.Equal(feature, operation.Feature);
            Assert.Equal(user, operation.User);
            Assert.Equal(priority, operation.Priority);
            Assert.Equal(timeout, operation.Timeout);
            Assert.Equal(maxRetries, operation.MaxRetries);
            Assert.Equal(retryDelay, operation.RetryDelay);
        }

        [Fact]
        public void Constructor_WithBasicParameters_SetsValuesCorrectly()
        {
            // Arrange
            var operationType = LicenseCheckOperationType.ServerStatusCheck;
            var server = "test-server";
            var port = 27000;

            // Act
            var operation = new LicenseCheckOperation(operationType, server, port);

            // Assert
            Assert.Equal(operationType, operation.OperationType);
            Assert.Equal(server, operation.Server);
            Assert.Equal(port, operation.Port);
            Assert.Equal(string.Empty, operation.Feature);
            Assert.Equal(string.Empty, operation.User);
            Assert.Equal(LicenseCheckOperationPriority.Normal, operation.Priority);
            Assert.Equal(TimeSpan.FromMinutes(2), operation.Timeout);
            Assert.Equal(3, operation.MaxRetries);
            Assert.Equal(TimeSpan.FromSeconds(5), operation.RetryDelay);
        }

        [Fact]
        public void CreateStatusCheck_CreatesCorrectOperation()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;

            // Act
            var operation = LicenseCheckOperation.CreateStatusCheck(server, port);

            // Assert
            Assert.Equal(LicenseCheckOperationType.ServerStatusCheck, operation.OperationType);
            Assert.Equal(server, operation.Server);
            Assert.Equal(port, operation.Port);
            Assert.Equal(TimeSpan.FromMinutes(1), operation.Timeout);
            Assert.Contains("Status check", operation.Description);
            Assert.Contains(server, operation.Description);
        }

        [Fact]
        public void CreateFeatureCheck_CreatesCorrectOperation()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "test-feature";

            // Act
            var operation = LicenseCheckOperation.CreateFeatureCheck(server, port, feature);

            // Assert
            Assert.Equal(LicenseCheckOperationType.FeatureStatusCheck, operation.OperationType);
            Assert.Equal(server, operation.Server);
            Assert.Equal(port, operation.Port);
            Assert.Equal(feature, operation.Feature);
            Assert.Equal(TimeSpan.FromMinutes(2), operation.Timeout);
            Assert.Contains("Feature check", operation.Description);
            Assert.Contains(feature, operation.Description);
        }

        [Fact]
        public void CreateUserCheck_CreatesCorrectOperation()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var user = "test-user";

            // Act
            var operation = LicenseCheckOperation.CreateUserCheck(server, port, user);

            // Assert
            Assert.Equal(LicenseCheckOperationType.UserStatusCheck, operation.OperationType);
            Assert.Equal(server, operation.Server);
            Assert.Equal(port, operation.Port);
            Assert.Equal(user, operation.User);
            Assert.Equal(TimeSpan.FromMinutes(2), operation.Timeout);
            Assert.Contains("User check", operation.Description);
            Assert.Contains(user, operation.Description);
        }

        [Fact]
        public void CreateIdleLicenseCheck_CreatesCorrectOperation()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var idleThresholdMinutes = 45;

            // Act
            var operation = LicenseCheckOperation.CreateIdleLicenseCheck(server, port, idleThresholdMinutes);

            // Assert
            Assert.Equal(LicenseCheckOperationType.IdleLicenseCheck, operation.OperationType);
            Assert.Equal(server, operation.Server);
            Assert.Equal(port, operation.Port);
            Assert.Equal(TimeSpan.FromMinutes(3), operation.Timeout);
            Assert.Contains("Idle license check", operation.Description);
            Assert.Contains(idleThresholdMinutes.ToString(), operation.Description);
            Assert.Equal(idleThresholdMinutes, operation.GetParameter<int>("IdleThresholdMinutes"));
        }

        [Fact]
        public void CreateHealthCheck_CreatesCorrectOperation()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;

            // Act
            var operation = LicenseCheckOperation.CreateHealthCheck(server, port);

            // Assert
            Assert.Equal(LicenseCheckOperationType.ServerHealthCheck, operation.OperationType);
            Assert.Equal(server, operation.Server);
            Assert.Equal(port, operation.Port);
            Assert.Equal(TimeSpan.FromMinutes(1), operation.Timeout);
            Assert.Equal(LicenseCheckOperationPriority.High, operation.Priority);
            Assert.Contains("Health check", operation.Description);
        }

        [Fact]
        public void CreateComprehensiveCheck_CreatesCorrectOperation()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;

            // Act
            var operation = LicenseCheckOperation.CreateComprehensiveCheck(server, port);

            // Assert
            Assert.Equal(LicenseCheckOperationType.ComprehensiveCheck, operation.OperationType);
            Assert.Equal(server, operation.Server);
            Assert.Equal(port, operation.Port);
            Assert.Equal(TimeSpan.FromMinutes(5), operation.Timeout);
            Assert.Equal(LicenseCheckOperationPriority.Normal, operation.Priority);
            Assert.Contains("Comprehensive license check", operation.Description);
        }

        [Fact]
        public void CreateRecurringStatusCheck_CreatesCorrectOperation()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var interval = TimeSpan.FromMinutes(10);
            var maxExecutions = 100;

            // Act
            var operation = LicenseCheckOperation.CreateRecurringStatusCheck(server, port, interval, maxExecutions);

            // Assert
            Assert.Equal(LicenseCheckOperationType.ServerStatusCheck, operation.OperationType);
            Assert.Equal(server, operation.Server);
            Assert.Equal(port, operation.Port);
            Assert.True(operation.IsRecurring);
            Assert.Equal(interval, operation.RecurringInterval);
            Assert.Equal(maxExecutions, operation.MaxRecurringExecutions);
            Assert.Contains("Recurring status check", operation.Description);
        }

        [Fact]
        public void AddTag_AddsTagToOperation()
        {
            // Arrange
            var operation = new LicenseCheckOperation();
            var tag = "test-tag";

            // Act
            operation.AddTag(tag);

            // Assert
            Assert.Contains(tag, operation.Tags);
            Assert.Single(operation.Tags);
        }

        [Fact]
        public void AddTag_DoesNotAddDuplicateTag()
        {
            // Arrange
            var operation = new LicenseCheckOperation();
            var tag = "test-tag";

            // Act
            operation.AddTag(tag);
            operation.AddTag(tag);

            // Assert
            Assert.Contains(tag, operation.Tags);
            Assert.Single(operation.Tags);
        }

        [Fact]
        public void AddTag_DoesNotAddEmptyTag()
        {
            // Arrange
            var operation = new LicenseCheckOperation();

            // Act
            operation.AddTag(string.Empty);
            operation.AddTag("   ");
            operation.AddTag(null);

            // Assert
            Assert.Empty(operation.Tags);
        }

        [Fact]
        public void AddTags_AddsMultipleTags()
        {
            // Arrange
            var operation = new LicenseCheckOperation();
            var tags = new[] { "tag1", "tag2", "tag3" };

            // Act
            operation.AddTags(tags);

            // Assert
            Assert.Equal(3, operation.Tags.Count);
            Assert.Contains("tag1", operation.Tags);
            Assert.Contains("tag2", operation.Tags);
            Assert.Contains("tag3", operation.Tags);
        }

        [Fact]
        public void RemoveTag_RemovesTagFromOperation()
        {
            // Arrange
            var operation = new LicenseCheckOperation();
            operation.AddTag("tag1");
            operation.AddTag("tag2");

            // Act
            operation.RemoveTag("tag1");

            // Assert
            Assert.DoesNotContain("tag1", operation.Tags);
            Assert.Contains("tag2", operation.Tags);
            Assert.Single(operation.Tags);
        }

        [Fact]
        public void HasTag_ReturnsCorrectValue()
        {
            // Arrange
            var operation = new LicenseCheckOperation();
            operation.AddTag("existing-tag");

            // Act & Assert
            Assert.True(operation.HasTag("existing-tag"));
            Assert.False(operation.HasTag("non-existing-tag"));
        }

        [Fact]
        public void AddParameter_AddsParameterToOperation()
        {
            // Arrange
            var operation = new LicenseCheckOperation();
            var key = "test-key";
            var value = "test-value";

            // Act
            operation.AddParameter(key, value);

            // Assert
            Assert.Contains(key, operation.Parameters);
            Assert.Equal(value, operation.Parameters[key]);
        }

        [Fact]
        public void GetParameter_ReturnsCorrectValue()
        {
            // Arrange
            var operation = new LicenseCheckOperation();
            operation.AddParameter("string-key", "string-value");
            operation.AddParameter("int-key", 42);
            operation.AddParameter("bool-key", true);

            // Act & Assert
            Assert.Equal("string-value", operation.GetParameter<string>("string-key"));
            Assert.Equal(42, operation.GetParameter<int>("int-key"));
            Assert.True(operation.GetParameter<bool>("bool-key"));
            Assert.Equal("default", operation.GetParameter<string>("non-existing-key", "default"));
            Assert.Equal(0, operation.GetParameter<int>("non-existing-key"));
        }

        [Fact]
        public void ScheduleFor_SetsScheduledTime()
        {
            // Arrange
            var operation = new LicenseCheckOperation();
            var scheduledTime = DateTime.Now.AddHours(1);

            // Act
            operation.ScheduleFor(scheduledTime);

            // Assert
            Assert.Equal(scheduledTime, operation.ScheduledFor);
        }

        [Fact]
        public void ScheduleAfter_SetsScheduledTime()
        {
            // Arrange
            var operation = new LicenseCheckOperation();
            var delay = TimeSpan.FromMinutes(30);
            var expectedTime = DateTime.Now.Add(delay);

            // Act
            operation.ScheduleAfter(delay);

            // Assert
            Assert.True(operation.ScheduledFor.HasValue);
            var scheduledTime = operation.ScheduledFor.Value;
            var timeDiff = Math.Abs((scheduledTime - expectedTime).TotalSeconds);
            Assert.True(timeDiff < 1, $"Scheduled time should be within 1 second of expected time. Difference: {timeDiff}s");
        }

        [Fact]
        public void MarkAsExecuting_UpdatesStatusAndTimestamps()
        {
            // Arrange
            var operation = new LicenseCheckOperation();
            var beforeMark = DateTime.Now;

            // Act
            operation.MarkAsExecuting();

            // Assert
            Assert.Equal(LicenseCheckOperationStatus.Executing, operation.Status);
            Assert.True(operation.ExecutedAt.HasValue);
            Assert.True(operation.ExecutedAt.Value >= beforeMark);
        }

        [Fact]
        public void MarkAsCompleted_UpdatesStatusAndTimestamps()
        {
            // Arrange
            var operation = new LicenseCheckOperation();
            var beforeMark = DateTime.Now;

            // Act
            operation.MarkAsCompleted();

            // Assert
            Assert.Equal(LicenseCheckOperationStatus.Completed, operation.Status);
            Assert.True(operation.CompletedAt.HasValue);
            Assert.True(operation.CompletedAt.Value >= beforeMark);
        }

        [Fact]
        public void MarkAsFailed_UpdatesStatusAndSetsErrorMessage()
        {
            // Arrange
            var operation = new LicenseCheckOperation();
            var errorMessage = "Test error message";
            var beforeMark = DateTime.Now;

            // Act
            operation.MarkAsFailed(errorMessage);

            // Assert
            Assert.Equal(LicenseCheckOperationStatus.Failed, operation.Status);
            Assert.Equal(errorMessage, operation.LastError);
            Assert.True(operation.CompletedAt.HasValue);
            Assert.True(operation.CompletedAt.Value >= beforeMark);
        }

        [Fact]
        public void MarkAsCancelled_UpdatesStatusAndTimestamps()
        {
            // Arrange
            var operation = new LicenseCheckOperation();
            var beforeMark = DateTime.Now;

            // Act
            operation.MarkAsCancelled();

            // Assert
            Assert.Equal(LicenseCheckOperationStatus.Cancelled, operation.Status);
            Assert.True(operation.CompletedAt.HasValue);
            Assert.True(operation.CompletedAt.Value >= beforeMark);
        }

        [Fact]
        public void IncrementRetryAttempt_IncrementsRetryCount()
        {
            // Arrange
            var operation = new LicenseCheckOperation();
            operation.RetryAttempt = 2;

            // Act
            operation.IncrementRetryAttempt();

            // Assert
            Assert.Equal(3, operation.RetryAttempt);
            Assert.Equal(LicenseCheckOperationStatus.Pending, operation.Status);
            Assert.Equal(string.Empty, operation.LastError);
        }

        [Fact]
        public void ResetForRetry_ResetsOperationState()
        {
            // Arrange
            var operation = new LicenseCheckOperation();
            operation.Status = LicenseCheckOperationStatus.Failed;
            operation.ExecutedAt = DateTime.Now;
            operation.CompletedAt = DateTime.Now;
            operation.LastError = "Test error";

            // Act
            operation.ResetForRetry();

            // Assert
            Assert.Equal(LicenseCheckOperationStatus.Pending, operation.Status);
            Assert.False(operation.ExecutedAt.HasValue);
            Assert.False(operation.CompletedAt.HasValue);
            Assert.Equal(string.Empty, operation.LastError);
        }

        [Fact]
        public void Clone_CreatesCopyWithNewId()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                OperationType = LicenseCheckOperationType.FeatureStatusCheck,
                Server = "test-server",
                Port = 27000,
                Feature = "test-feature",
                User = "test-user",
                Priority = LicenseCheckOperationPriority.High,
                Timeout = TimeSpan.FromMinutes(10),
                MaxRetries = 5,
                RetryDelay = TimeSpan.FromSeconds(15),
                Description = "Test operation",
                Status = LicenseCheckOperationStatus.Executing,
                IsRecurring = true,
                RecurringInterval = TimeSpan.FromMinutes(30)
            };

            operation.AddTag("tag1");
            operation.AddTag("tag2");
            operation.AddParameter("param1", "value1");

            // Act
            var clone = operation.Clone();

            // Assert
            Assert.NotEqual(operation.OperationId, clone.OperationId);
            Assert.Equal(operation.OperationType, clone.OperationType);
            Assert.Equal(operation.Server, clone.Server);
            Assert.Equal(operation.Port, clone.Port);
            Assert.Equal(operation.Feature, clone.Feature);
            Assert.Equal(operation.User, clone.User);
            Assert.Equal(operation.Priority, clone.Priority);
            Assert.Equal(operation.Timeout, clone.Timeout);
            Assert.Equal(operation.MaxRetries, clone.MaxRetries);
            Assert.Equal(operation.RetryDelay, clone.RetryDelay);
            Assert.Equal(operation.Description, clone.Description);
            Assert.Equal(LicenseCheckOperationStatus.Pending, clone.Status); // Status is reset
            Assert.Equal(operation.IsRecurring, clone.IsRecurring);
            Assert.Equal(operation.RecurringInterval, clone.RecurringInterval);
            Assert.Equal(operation.Tags.Count, clone.Tags.Count);
            Assert.Contains("tag1", clone.Tags);
            Assert.Contains("tag2", clone.Tags);
            Assert.Equal(operation.Parameters.Count, clone.Parameters.Count);
            Assert.Equal("value1", clone.Parameters["param1"]);
        }

        [Fact]
        public void ToString_ReturnsCorrectStringRepresentation()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                OperationType = LicenseCheckOperationType.ServerStatusCheck,
                Server = "test-server",
                Port = 27000,
                Status = LicenseCheckOperationStatus.Executing
            };

            // Act
            var result = operation.ToString();

            // Assert
            Assert.Contains(operation.OperationId.ToString("N"), result);
            Assert.Contains(operation.OperationType.ToString(), result);
            Assert.Contains(operation.Server, result);
            Assert.Contains(operation.Port.ToString(), result);
            Assert.Contains(operation.Status.ToString(), result);
        }

        [Fact]
        public void IsReadyToExecute_ReturnsTrueForPendingOperation()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                Status = LicenseCheckOperationStatus.Pending
            };

            // Act & Assert
            Assert.True(operation.IsReadyToExecute);
        }

        [Fact]
        public void IsReadyToExecute_ReturnsFalseForScheduledOperationInFuture()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                Status = LicenseCheckOperationStatus.Pending,
                ScheduledFor = DateTime.Now.AddHours(1)
            };

            // Act & Assert
            Assert.False(operation.IsReadyToExecute);
        }

        [Fact]
        public void IsReadyToExecute_ReturnsTrueForScheduledOperationInPast()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                Status = LicenseCheckOperationStatus.Pending,
                ScheduledFor = DateTime.Now.AddHours(-1)
            };

            // Act & Assert
            Assert.True(operation.IsReadyToExecute);
        }

        [Fact]
        public void IsReadyToExecute_ReturnsFalseForNonPendingOperation()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                Status = LicenseCheckOperationStatus.Executing
            };

            // Act & Assert
            Assert.False(operation.IsReadyToExecute);
        }

        [Fact]
        public void IsExecuting_ReturnsTrueForExecutingOperation()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                Status = LicenseCheckOperationStatus.Executing
            };

            // Act & Assert
            Assert.True(operation.IsExecuting);
        }

        [Fact]
        public void IsExecuting_ReturnsFalseForNonExecutingOperation()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                Status = LicenseCheckOperationStatus.Pending
            };

            // Act & Assert
            Assert.False(operation.IsExecuting);
        }

        [Fact]
        public void IsCompleted_ReturnsTrueForCompletedOperation()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                Status = LicenseCheckOperationStatus.Completed
            };

            // Act & Assert
            Assert.True(operation.IsCompleted);
        }

        [Theory]
        [InlineData(LicenseCheckOperationStatus.Failed)]
        [InlineData(LicenseCheckOperationStatus.Cancelled)]
        public void IsCompleted_ReturnsTrueForTerminalStates(LicenseCheckOperationStatus status)
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                Status = status
            };

            // Act & Assert
            Assert.True(operation.IsCompleted);
        }

        [Fact]
        public void CanRetry_ReturnsTrueForFailedOperationWithRetriesRemaining()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                Status = LicenseCheckOperationStatus.Failed,
                RetryAttempt = 1,
                MaxRetries = 3
            };

            // Act & Assert
            Assert.True(operation.CanRetry);
        }

        [Fact]
        public void CanRetry_ReturnsFalseForFailedOperationWithNoRetriesRemaining()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                Status = LicenseCheckOperationStatus.Failed,
                RetryAttempt = 3,
                MaxRetries = 3
            };

            // Act & Assert
            Assert.False(operation.CanRetry);
        }

        [Fact]
        public void CanRetry_ReturnsFalseForNonFailedOperation()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                Status = LicenseCheckOperationStatus.Pending
            };

            // Act & Assert
            Assert.False(operation.CanRetry);
        }

        [Fact]
        public void CanRetry_ReturnsFalseWhenMaxRetriesIsZero()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                Status = LicenseCheckOperationStatus.Failed,
                RetryAttempt = 0,
                MaxRetries = 0
            };

            // Act & Assert
            Assert.False(operation.CanRetry);
        }

        [Fact]
        public void IsTimedOut_ReturnsTrueWhenOperationExceedsTimeout()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                ExecutedAt = DateTime.Now.AddMinutes(-3),
                Timeout = TimeSpan.FromMinutes(2)
            };

            // Act & Assert
            Assert.True(operation.IsTimedOut);
        }

        [Fact]
        public void IsTimedOut_ReturnsFalseWhenOperationWithinTimeout()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                ExecutedAt = DateTime.Now.AddMinutes(-1),
                Timeout = TimeSpan.FromMinutes(2)
            };

            // Act & Assert
            Assert.False(operation.IsTimedOut);
        }

        [Fact]
        public void IsTimedOut_ReturnsFalseWhenOperationNotExecuted()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                Timeout = TimeSpan.FromMinutes(2)
            };

            // Act & Assert
            Assert.False(operation.IsTimedOut);
        }

        [Fact]
        public void Duration_ReturnsNullWhenOperationNotCompleted()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                ExecutedAt = DateTime.Now.AddMinutes(-1)
            };

            // Act & Assert
            Assert.Null(operation.Duration);
        }

        [Fact]
        public void Duration_ReturnsCorrectTimeSpanWhenOperationCompleted()
        {
            // Arrange
            var executedAt = DateTime.Now.AddMinutes(-2);
            var completedAt = DateTime.Now.AddMinutes(-1);
            var operation = new LicenseCheckOperation
            {
                ExecutedAt = executedAt,
                CompletedAt = completedAt
            };

            // Act
            var duration = operation.Duration;

            // Assert
            Assert.NotNull(duration);
            Assert.Equal(TimeSpan.FromMinutes(1), duration.Value);
        }

        [Fact]
        public void Age_ReturnsCorrectTimeSpan()
        {
            // Arrange
            var operation = new LicenseCheckOperation
            {
                CreatedAt = DateTime.Now.AddMinutes(-5)
            };

            // Act
            var age = operation.Age;

            // Assert
            Assert.True(age.TotalMinutes >= 4.9 && age.TotalMinutes <= 5.1,
                $"Age should be approximately 5 minutes, but was {age.TotalMinutes} minutes");
        }
    }
}