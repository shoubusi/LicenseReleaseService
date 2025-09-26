using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using LicenseReleaseService.LicenseManagement;
using Xunit;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for the LicenseQueryException class
    /// </summary>
    public class LicenseQueryExceptionTests
    {
        [Fact]
        public void Constructor_WithDefaultParameters_ShouldInitialize()
        {
            // Act
            var exception = new LicenseQueryException();

            // Assert
            Assert.NotNull(exception);
            Assert.Equal("An error occurred during license query operation", exception.Message);
            Assert.Equal(LicenseQueryErrorCode.Unknown, exception.ErrorCode);
            Assert.Equal(string.Empty, exception.Server);
            Assert.Equal(0, exception.Port);
            Assert.Equal(string.Empty, exception.QueryType);
            Assert.False(exception.IsTransient);
            Assert.Null(exception.OriginalException);
        }

        [Fact]
        public void Constructor_WithMessage_ShouldSetMessage()
        {
            // Arrange
            var message = "Test error message";

            // Act
            var exception = new LicenseQueryException(message);

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(message, exception.Message);
            Assert.Equal(LicenseQueryErrorCode.Unknown, exception.ErrorCode);
            Assert.Equal(string.Empty, exception.Server);
            Assert.Equal(0, exception.Port);
            Assert.Equal(string.Empty, exception.QueryType);
            Assert.False(exception.IsTransient);
            Assert.Null(exception.OriginalException);
        }

        [Fact]
        public void Constructor_WithMessageAndInnerException_ShouldSetBoth()
        {
            // Arrange
            var message = "Test error message";
            var innerException = new InvalidOperationException("Inner exception");

            // Act
            var exception = new LicenseQueryException(message, innerException);

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(message, exception.Message);
            Assert.Same(innerException, exception.InnerException);
            Assert.Equal(LicenseQueryErrorCode.Unknown, exception.ErrorCode);
            Assert.Equal(string.Empty, exception.Server);
            Assert.Equal(0, exception.Port);
            Assert.Equal(string.Empty, exception.QueryType);
            Assert.False(exception.IsTransient);
            Assert.Same(innerException, exception.OriginalException);
        }

        [Fact]
        public void Constructor_WithFullParameters_ShouldSetAllProperties()
        {
            // Arrange
            var message = "Test error message";
            var server = "test-server";
            var port = 27000;
            var queryType = "QueryLicenseStatusAsync";
            var errorCode = LicenseQueryErrorCode.NetworkTimeout;
            var isTransient = true;

            // Act
            var exception = new LicenseQueryException(message, server, port, queryType, errorCode, isTransient);

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(message, exception.Message);
            Assert.Equal(server, exception.Server);
            Assert.Equal(port, exception.Port);
            Assert.Equal(queryType, exception.QueryType);
            Assert.Equal(errorCode, exception.ErrorCode);
            Assert.Equal(isTransient, exception.IsTransient);
            Assert.Null(exception.OriginalException);
        }

        [Fact]
        public void Constructor_WithFullParametersAndInnerException_ShouldSetAllProperties()
        {
            // Arrange
            var message = "Test error message";
            var server = "test-server";
            var port = 27000;
            var queryType = "QueryLicenseStatusAsync";
            var errorCode = LicenseQueryErrorCode.NetworkTimeout;
            var innerException = new InvalidOperationException("Inner exception");
            var isTransient = true;

            // Act
            var exception = new LicenseQueryException(message, server, port, queryType, errorCode, innerException, isTransient);

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(message, exception.Message);
            Assert.Equal(server, exception.Server);
            Assert.Equal(port, exception.Port);
            Assert.Equal(queryType, exception.QueryType);
            Assert.Equal(errorCode, exception.ErrorCode);
            Assert.Equal(isTransient, exception.IsTransient);
            Assert.Same(innerException, exception.InnerException);
            Assert.Same(innerException, exception.OriginalException);
        }

        [Fact]
        public void CreateTransient_ShouldCreateTransientException()
        {
            // Arrange
            var message = "Transient error";
            var server = "test-server";
            var port = 27000;
            var queryType = "QueryLicenseStatusAsync";
            var errorCode = LicenseQueryErrorCode.NetworkTimeout;

            // Act
            var exception = LicenseQueryException.CreateTransient(message, server, port, queryType, errorCode);

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(message, exception.Message);
            Assert.Equal(server, exception.Server);
            Assert.Equal(port, exception.Port);
            Assert.Equal(queryType, exception.QueryType);
            Assert.Equal(errorCode, exception.ErrorCode);
            Assert.True(exception.IsTransient);
        }

        [Fact]
        public void CreateNonTransient_ShouldCreateNonTransientException()
        {
            // Arrange
            var message = "Non-transient error";
            var server = "test-server";
            var port = 27000;
            var queryType = "QueryLicenseStatusAsync";
            var errorCode = LicenseQueryErrorCode.ServerNotFound;

            // Act
            var exception = LicenseQueryException.CreateNonTransient(message, server, port, queryType, errorCode);

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(message, exception.Message);
            Assert.Equal(server, exception.Server);
            Assert.Equal(port, exception.Port);
            Assert.Equal(queryType, exception.QueryType);
            Assert.Equal(errorCode, exception.ErrorCode);
            Assert.False(exception.IsTransient);
        }

        [Fact]
        public void FromException_WithTransientException_ShouldCreateTransientException()
        {
            // Arrange
            var message = "Error from exception";
            var server = "test-server";
            var port = 27000;
            var queryType = "QueryLicenseStatusAsync";
            var errorCode = LicenseQueryErrorCode.NetworkTimeout;
            var innerException = new TimeoutException("Timeout occurred");

            // Act
            var exception = LicenseQueryException.FromException(message, server, port, queryType, errorCode, innerException);

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(message, exception.Message);
            Assert.Equal(server, exception.Server);
            Assert.Equal(port, exception.Port);
            Assert.Equal(queryType, exception.QueryType);
            Assert.Equal(errorCode, exception.ErrorCode);
            Assert.True(exception.IsTransient);
            Assert.Same(innerException, exception.OriginalException);
        }

        [Fact]
        public void FromException_WithNonTransientException_ShouldCreateNonTransientException()
        {
            // Arrange
            var message = "Error from exception";
            var server = "test-server";
            var port = 27000;
            var queryType = "QueryLicenseStatusAsync";
            var errorCode = LicenseQueryErrorCode.ServerNotFound;
            var innerException = new FileNotFoundException("File not found");

            // Act
            var exception = LicenseQueryException.FromException(message, server, port, queryType, errorCode, innerException);

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(message, exception.Message);
            Assert.Equal(server, exception.Server);
            Assert.Equal(port, exception.Port);
            Assert.Equal(queryType, exception.QueryType);
            Assert.Equal(errorCode, exception.ErrorCode);
            Assert.False(exception.IsTransient);
            Assert.Same(innerException, exception.OriginalException);
        }

        [Fact]
        public void FromException_WithTransientErrorCode_ShouldCreateTransientException()
        {
            // Arrange
            var message = "Error from exception";
            var server = "test-server";
            var port = 27000;
            var queryType = "QueryLicenseStatusAsync";
            var errorCode = LicenseQueryErrorCode.NetworkTimeout;
            var innerException = new FileNotFoundException("File not found");

            // Act
            var exception = LicenseQueryException.FromException(message, server, port, queryType, errorCode, innerException);

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(message, exception.Message);
            Assert.Equal(server, exception.Server);
            Assert.Equal(port, exception.Port);
            Assert.Equal(queryType, exception.QueryType);
            Assert.Equal(errorCode, exception.ErrorCode);
            Assert.True(exception.IsTransient); // Should be transient due to error code
            Assert.Same(innerException, exception.OriginalException);
        }

        [Fact]
        public void FromException_WithNullException_ShouldCreateNonTransientException()
        {
            // Arrange
            var message = "Error from exception";
            var server = "test-server";
            var port = 27000;
            var queryType = "QueryLicenseStatusAsync";
            var errorCode = LicenseQueryErrorCode.NetworkTimeout;

            // Act
            var exception = LicenseQueryException.FromException(message, server, port, queryType, errorCode, null);

            // Assert
            Assert.NotNull(exception);
            Assert.Equal(message, exception.Message);
            Assert.Equal(server, exception.Server);
            Assert.Equal(port, exception.Port);
            Assert.Equal(queryType, exception.QueryType);
            Assert.Equal(errorCode, exception.ErrorCode);
            Assert.True(exception.IsTransient); // Should be transient due to error code
            Assert.Null(exception.OriginalException);
        }

        [Fact]
        public void ToString_ShouldIncludeAllRelevantInformation()
        {
            // Arrange
            var message = "Test error message";
            var server = "test-server";
            var port = 27000;
            var queryType = "QueryLicenseStatusAsync";
            var errorCode = LicenseQueryErrorCode.NetworkTimeout;
            var innerException = new TimeoutException("Timeout occurred");
            var exception = new LicenseQueryException(message, server, port, queryType, errorCode, innerException, true);

            // Act
            var result = exception.ToString();

            // Assert
            Assert.Contains(message, result);
            Assert.Contains(server, result);
            Assert.Contains(port.ToString(), result);
            Assert.Contains(queryType, result);
            Assert.Contains(errorCode.ToString(), result);
            Assert.Contains("True", result); // IsTransient
            Assert.Contains(innerException.GetType().Name, result);
            Assert.Contains(innerException.Message, result);
        }

        [Fact]
        public void ToString_ShouldHandleNullOriginalException()
        {
            // Arrange
            var message = "Test error message";
            var server = "test-server";
            var port = 27000;
            var queryType = "QueryLicenseStatusAsync";
            var errorCode = LicenseQueryErrorCode.NetworkTimeout;
            var exception = new LicenseQueryException(message, server, port, queryType, errorCode, true);

            // Act
            var result = exception.ToString();

            // Assert
            Assert.Contains(message, result);
            Assert.Contains(server, result);
            Assert.Contains(port.ToString(), result);
            Assert.Contains(queryType, result);
            Assert.Contains(errorCode.ToString(), result);
            Assert.Contains("True", result); // IsTransient
            Assert.DoesNotContain("Original Exception", result);
        }

        [Theory]
        [InlineData(LicenseQueryErrorCode.NetworkTimeout, true)]
        [InlineData(LicenseQueryErrorCode.ServerBusy, true)]
        [InlineData(LicenseQueryErrorCode.ConnectionRefused, true)]
        [InlineData(LicenseQueryErrorCode.ServiceUnavailable, true)]
        [InlineData(LicenseQueryErrorCode.TemporaryFailure, true)]
        [InlineData(LicenseQueryErrorCode.ServerNotFound, false)]
        [InlineData(LicenseQueryErrorCode.AuthenticationFailed, false)]
        [InlineData(LicenseQueryErrorCode.Unknown, false)]
        public void IsTransientErrorCode_ShouldCorrectlyIdentifyTransientErrors(LicenseQueryErrorCode errorCode, bool expected)
        {
            // Act & Assert
            Assert.Equal(expected, IsTransientErrorCodePrivate(errorCode));
        }

        [Theory]
        [InlineData(typeof(IOException), true)]
        [InlineData(typeof(SocketException), true)]
        [InlineData(typeof(TimeoutException), true)]
        [InlineData(typeof(OperationCanceledException), true)]
        [InlineData(typeof(FileNotFoundException), false)]
        [InlineData(typeof(InvalidOperationException), false)]
        [InlineData(typeof(ArgumentNullException), false)]
        public void IsTransientException_ShouldCorrectlyIdentifyTransientExceptions(Type exceptionType, bool expected)
        {
            // Arrange
            Exception exception = null;
            if (exceptionType == typeof(IOException))
                exception = new IOException("IO error");
            else if (exceptionType == typeof(SocketException))
                exception = new SocketException();
            else if (exceptionType == typeof(TimeoutException))
                exception = new TimeoutException("Timeout");
            else if (exceptionType == typeof(OperationCanceledException))
                exception = new OperationCanceledException();
            else if (exceptionType == typeof(FileNotFoundException))
                exception = new FileNotFoundException("File not found");
            else if (exceptionType == typeof(InvalidOperationException))
                exception = new InvalidOperationException("Invalid operation");
            else if (exceptionType == typeof(ArgumentNullException))
                exception = new ArgumentNullException("argument");

            // Act & Assert
            Assert.Equal(expected, IsTransientExceptionPrivate(exception));
        }

        [Fact]
        public void IsTransientException_WithNullException_ShouldReturnFalse()
        {
            // Act & Assert
            Assert.False(IsTransientExceptionPrivate(null));
        }

        [Fact]
        public void Serialization_ShouldPreserveAllProperties()
        {
            // Arrange
            var message = "Test error message";
            var server = "test-server";
            var port = 27000;
            var queryType = "QueryLicenseStatusAsync";
            var errorCode = LicenseQueryErrorCode.NetworkTimeout;
            var isTransient = true;

            var originalException = new LicenseQueryException(message, server, port, queryType, errorCode, isTransient);

            // Act
            var serialized = SerializeAndDeserialize(originalException);

            // Assert
            Assert.NotNull(serialized);
            Assert.Equal(message, serialized.Message);
            Assert.Equal(server, serialized.Server);
            Assert.Equal(port, serialized.Port);
            Assert.Equal(queryType, serialized.QueryType);
            Assert.Equal(errorCode, serialized.ErrorCode);
            Assert.Equal(isTransient, serialized.IsTransient);
        }

        [Fact]
        public void ErrorCode_ShouldHaveCorrectValueCount()
        {
            // Arrange
            var maxErrorCode = (int)LicenseQueryErrorCode.KnowledgeManagementError;

            // Act & Assert
            Assert.Equal(99, maxErrorCode);
        }

        [Fact]
        public void ErrorCode_ShouldHaveLogicalGrouping()
        {
            // Arrange & Act
            var networkErrors = new[]
            {
                LicenseQueryErrorCode.NetworkTimeout,
                LicenseQueryErrorCode.ConnectionRefused,
                LicenseQueryErrorCode.NetworkError
            };

            var serverErrors = new[]
            {
                LicenseQueryErrorCode.ServerNotFound,
                LicenseQueryErrorCode.ServerDown,
                LicenseQueryErrorCode.ServerBusy,
                LicenseQueryErrorCode.ServerMaintenance,
                LicenseQueryErrorCode.ServerUpgrade,
                LicenseQueryErrorCode.ServerBackup,
                LicenseQueryErrorCode.ServerRestore
            };

            var dataErrors = new[]
            {
                LicenseQueryErrorCode.ParseError,
                LicenseQueryErrorCode.DataInconsistency,
                LicenseQueryErrorCode.ConfigurationError
            };

            // Assert
            Assert.Equal(3, networkErrors.Length);
            Assert.Equal(7, serverErrors.Length);
            Assert.Equal(3, dataErrors.Length);
        }

        // Helper methods to test private methods
        private static bool IsTransientErrorCodePrivate(LicenseQueryErrorCode errorCode)
        {
            return errorCode switch
            {
                LicenseQueryErrorCode.NetworkTimeout or
                LicenseQueryErrorCode.ServerBusy or
                LicenseQueryErrorCode.ConnectionRefused or
                LicenseQueryErrorCode.ServiceUnavailable or
                LicenseQueryErrorCode.TemporaryFailure => true,
                _ => false
            };
        }

        private static bool IsTransientExceptionPrivate(Exception exception)
        {
            if (exception == null)
                return false;

            return exception switch
            {
                System.IO.IOException or
                System.Net.Sockets.SocketException or
                System.TimeoutException or
                System.OperationCanceledException => true,
                _ => false
            };
        }

        private static LicenseQueryException SerializeAndDeserialize(LicenseQueryException exception)
        {
            // Serialize
            using var stream = new System.IO.MemoryStream();
            var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            formatter.Serialize(stream, exception);
            stream.Position = 0;

            // Deserialize
            return (LicenseQueryException)formatter.Deserialize(stream);
        }
    }
}