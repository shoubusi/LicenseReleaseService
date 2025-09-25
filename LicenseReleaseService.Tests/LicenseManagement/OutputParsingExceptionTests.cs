using System;
using Xunit;
using Xunit.Abstractions;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    public class OutputParsingExceptionTests : IDisposable
    {
        private readonly ITestOutputHelper _output;

        public OutputParsingExceptionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Constructor_WithMessage_SetsMessageCorrectly()
        {
            // Arrange
            var message = "Test parsing error message";

            // Act
            var exception = new OutputParsingException(message);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Null(exception.RawOutput);
            Assert.Null(exception.ParsingOperation);
            Assert.Null(exception.LineNumber);
            Assert.Null(exception.ColumnNumber);
        }

        [Fact]
        public void Constructor_WithMessageAndInnerException_SetsBothCorrectly()
        {
            // Arrange
            var message = "Test parsing error message";
            var innerException = new InvalidOperationException("Inner exception");

            // Act
            var exception = new OutputParsingException(message, innerException);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(innerException, exception.InnerException);
            Assert.Null(exception.RawOutput);
            Assert.Null(exception.ParsingOperation);
            Assert.Null(exception.LineNumber);
            Assert.Null(exception.ColumnNumber);
        }

        [Fact]
        public void Constructor_WithFullContext_SetsAllProperties()
        {
            // Arrange
            var message = "Test parsing error message";
            var rawOutput = "Sample raw output content";
            var parsingOperation = "lmstat";
            var lineNumber = 42;
            var columnNumber = 15;

            // Act
            var exception = new OutputParsingException(message, rawOutput, parsingOperation, lineNumber, columnNumber);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(rawOutput, exception.RawOutput);
            Assert.Equal(parsingOperation, exception.ParsingOperation);
            Assert.Equal(lineNumber, exception.LineNumber);
            Assert.Equal(columnNumber, exception.ColumnNumber);
        }

        [Fact]
        public void Constructor_WithFullContextAndInnerException_SetsAllProperties()
        {
            // Arrange
            var message = "Test parsing error message";
            var rawOutput = "Sample raw output content";
            var parsingOperation = "lmstat";
            var innerException = new InvalidOperationException("Inner exception");
            var lineNumber = 42;
            var columnNumber = 15;

            // Act
            var exception = new OutputParsingException(message, rawOutput, parsingOperation, innerException, lineNumber, columnNumber);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(rawOutput, exception.RawOutput);
            Assert.Equal(parsingOperation, exception.ParsingOperation);
            Assert.Equal(innerException, exception.InnerException);
            Assert.Equal(lineNumber, exception.LineNumber);
            Assert.Equal(columnNumber, exception.ColumnNumber);
        }

        [Fact]
        public void ToString_WithMinimalContext_ReturnsBasicMessage()
        {
            // Arrange
            var exception = new OutputParsingException("Test error");

            // Act
            var result = exception.ToString();

            // Assert
            Assert.Contains("Test error", result);
            Assert.Contains("Parsing Context", result);
        }

        [Fact]
        public void ToString_WithFullContext_IncludesAllDetails()
        {
            // Arrange
            var exception = new OutputParsingException(
                "Test error",
                "Sample raw output",
                "lmstat",
                42,
                15);

            // Act
            var result = exception.ToString();

            // Assert
            Assert.Contains("Test error", result);
            Assert.Contains("Operation: lmstat", result);
            Assert.Contains("Line: 42", result);
            Assert.Contains("Column: 15", result);
            Assert.Contains("Raw Output:", result);
            Assert.Contains("Sample raw output", result);
        }

        [Fact]
        public void ToString_WithNullValues_HandlesGracefully()
        {
            // Arrange
            var exception = new OutputParsingException("Test error", null, null);

            // Act
            var result = exception.ToString();

            // Assert
            Assert.Contains("Test error", result);
            Assert.Contains("Parsing Context", result);
            // Should not throw null reference exception
        }

        [Fact]
        public void DefaultConstructor_CreatesEmptyException()
        {
            // Act
            var exception = new OutputParsingException();

            // Assert
            Assert.NotNull(exception);
            Assert.Empty(exception.Message);
            Assert.Null(exception.RawOutput);
            Assert.Null(exception.ParsingOperation);
            Assert.Null(exception.LineNumber);
            Assert.Null(exception.ColumnNumber);
        }

        [Fact]
        public void Properties_AreSettableAndGettable()
        {
            // Arrange
            var exception = new OutputParsingException();

            // Act
            exception.RawOutput = "Test raw output";
            exception.ParsingOperation = "test operation";
            exception.LineNumber = 100;
            exception.ColumnNumber = 50;

            // Assert
            Assert.Equal("Test raw output", exception.RawOutput);
            Assert.Equal("test operation", exception.ParsingOperation);
            Assert.Equal(100, exception.LineNumber);
            Assert.Equal(50, exception.ColumnNumber);
        }

        [Fact]
        public void ToString_IncludesBaseExceptionInformation()
        {
            // Arrange
            var exception = new OutputParsingException("Test error", null, "lmstat", 42, 15);

            // Act
            var result = exception.ToString();

            // Assert
            Assert.Contains("LicenseReleaseService.LicenseManagement.OutputParsingException", result);
            Assert.Contains("Test error", result);
        }

        [Fact]
        public void ToString_WithLongRawOutput_HandlesLargeContent()
        {
            // Arrange
            var longOutput = new string('A', 1000);
            var exception = new OutputParsingException("Test error", longOutput, "lmstat", 42, 15);

            // Act
            var result = exception.ToString();

            // Assert
            Assert.Contains(longOutput, result);
            Assert.Contains("Test error", result);
            Assert.Contains("Operation: lmstat", result);
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}