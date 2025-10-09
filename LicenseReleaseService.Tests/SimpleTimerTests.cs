using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.TimerExecution
{
    [TestClass]
    public class SimpleTimerTests
    {
        [TestMethod]
        public void TimerStatus_ShouldHaveCorrectValues()
        {
            // Arrange & Act & Assert
            Assert.AreEqual(0, (int)TimerStatus.Created);
            Assert.AreEqual(2, (int)TimerStatus.Running);
            Assert.AreEqual(6, (int)TimerStatus.Stopped);
        }

        [TestMethod]
        public void TimerErrorEventArgs_ShouldCreateWithBasicConstructor()
        {
            // Arrange
            var error = new InvalidOperationException("Test error");
            var status = TimerStatus.Running;
            var executionId = Guid.NewGuid();
            var consecutiveErrors = 1;

            // Act
            var eventArgs = new TimerErrorEventArgs(error, executionId, status, consecutiveErrors);

            // Assert
            Assert.IsNotNull(eventArgs);
            Assert.AreSame(error, eventArgs.Error);
            Assert.AreEqual(status, eventArgs.Status);
            Assert.AreEqual(executionId, eventArgs.ExecutionId);
            Assert.AreEqual(consecutiveErrors, eventArgs.ConsecutiveErrors);
        }

        [TestMethod]
        public void TimerErrorSeverity_ShouldHaveCorrectValues()
        {
            // Arrange & Act & Assert
            Assert.AreEqual(0, (int)TimerErrorSeverity.Low);
            Assert.AreEqual(1, (int)TimerErrorSeverity.Medium);
            Assert.AreEqual(2, (int)TimerErrorSeverity.High);
            Assert.AreEqual(3, (int)TimerErrorSeverity.Critical);
        }

        [TestMethod]
        public void TimerErrorCategory_ShouldHaveCorrectValues()
        {
            // Arrange & Act & Assert
            Assert.AreEqual(0, (int)TimerErrorCategory.Configuration);
            Assert.AreEqual(1, (int)TimerErrorCategory.Execution);
            Assert.AreEqual(2, (int)TimerErrorCategory.Network);
        }

        [TestMethod]
        public void TimerRecoveryAction_ShouldHaveCorrectValues()
        {
            // Arrange & Act & Assert
            Assert.AreEqual(0, (int)TimerRecoveryAction.None);
            Assert.AreEqual(1, (int)TimerRecoveryAction.Retry);
            Assert.AreEqual(2, (int)TimerRecoveryAction.ResetTimer);
        }
    }
}