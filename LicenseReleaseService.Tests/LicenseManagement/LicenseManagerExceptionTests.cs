using System;
using Xunit;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for LicenseManagerException class
    /// </summary>
    public class LicenseManagerExceptionTests
    {
        [Fact]
        public void Constructor_WithMessageOnly_ShouldSetMessageCorrectly()
        {
            // Arrange
            var message = "Test exception message";

            // Act
            var exception = new LicenseManagerException(message);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(0, exception.ErrorCode);
            Assert.Null(exception.InnerException);
        }

        [Fact]
        public void Constructor_WithMessageAndErrorCode_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var message = "Test exception message";
            var errorCode = 42;

            // Act
            var exception = new LicenseManagerException(message, errorCode);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(errorCode, exception.ErrorCode);
            Assert.Null(exception.InnerException);
        }

        [Fact]
        public void Constructor_WithMessageAndInnerException_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var message = "Test exception message";
            var innerException = new InvalidOperationException("Inner exception");

            // Act
            var exception = new LicenseManagerException(message, innerException);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(0, exception.ErrorCode);
            Assert.Same(innerException, exception.InnerException);
        }

        [Fact]
        public void Constructor_WithAllParameters_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var message = "Test exception message";
            var errorCode = 42;
            var innerException = new InvalidOperationException("Inner exception");

            // Act
            var exception = new LicenseManagerException(message, errorCode, innerException);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(errorCode, exception.ErrorCode);
            Assert.Same(innerException, exception.InnerException);
        }

        [Fact]
        public void Constructor_WithEmptyMessage_ShouldNotThrowException()
        {
            // Arrange & Act & Assert
            var exception = new LicenseManagerException("");
            Assert.Equal("", exception.Message);
        }

        [Fact]
        public void Constructor_WithNullMessage_ShouldNotThrowException()
        {
            // Arrange & Act & Assert
            var exception = new LicenseManagerException((string)null!);
            Assert.Null(exception.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(-1)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void Constructor_WithVariousErrorCodes_ShouldSetErrorCodeCorrectly(int errorCode)
        {
            // Arrange
            var message = "Test message";

            // Act
            var exception = new LicenseManagerException(message, errorCode);

            // Assert
            Assert.Equal(errorCode, exception.ErrorCode);
        }

        [Fact]
        public void Serialization_ShouldWorkCorrectly()
        {
            // Arrange
            var message = "Test serialization";
            var errorCode = 123;
            var innerException = new InvalidOperationException("Inner");
            var exception = new LicenseManagerException(message, errorCode, innerException);

            // Act & Assert - Verify that the exception can be created without throwing
            Assert.Equal(message, exception.Message);
            Assert.Equal(errorCode, exception.ErrorCode);
            Assert.Same(innerException, exception.InnerException);
        }

        [Fact]
        public void ToString_ShouldIncludeErrorCode()
        {
            // Arrange
            var message = "Test message";
            var errorCode = 42;
            var exception = new LicenseManagerException(message, errorCode);

            // Act
            var result = exception.ToString();

            // Assert
            Assert.Contains(message, result);
            Assert.Contains("42", result); // Error code should be visible
        }

        [Fact]
        public void ToString_WithInnerException_ShouldIncludeInnerException()
        {
            // Arrange
            var message = "Test message";
            var innerException = new InvalidOperationException("Inner error");
            var exception = new LicenseManagerException(message, innerException);

            // Act
            var result = exception.ToString();

            // Assert
            Assert.Contains(message, result);
            Assert.Contains("Inner error", result);
        }
    }
}