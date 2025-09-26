using System;

namespace LicenseReleaseService.LicenseManagement.Parsing
{
    /// <summary>
    /// Exception thrown when output parsing fails with detailed context information
    /// </summary>
    public class OutputParsingException : Exception
    {
        /// <summary>
        /// Gets the raw output that failed to parse
        /// </summary>
        public string RawOutput { get; }

        /// <summary>
        /// Gets the parsing operation that failed
        /// </summary>
        public string ParsingOperation { get; }

        /// <summary>
        /// Gets the line number where parsing failed (if applicable)
        /// </summary>
        public int? LineNumber { get; }

        /// <summary>
        /// Gets the column number where parsing failed (if applicable)
        /// </summary>
        public int? ColumnNumber { get; }

        /// <summary>
        /// Gets the severity level of the parsing error
        /// </summary>
        public ParsingErrorSeverity Severity { get; }

        /// <summary>
        /// Gets the error category for better error handling
        /// </summary>
        public ParsingErrorCategory ErrorCategory { get; }

        /// <summary>
        /// Initializes a new instance of the OutputParsingException class
        /// </summary>
        public OutputParsingException() : base() { }

        /// <summary>
        /// Initializes a new instance of the OutputParsingException class with a specified error message
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        public OutputParsingException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the OutputParsingException class with a specified error message and a reference to the inner exception that is the cause of this exception
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="innerException">The exception that is the cause of the current exception</param>
        public OutputParsingException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// Initializes a new instance of the OutputParsingException class with detailed parsing context
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="rawOutput">The raw output that failed to parse</param>
        /// <param name="parsingOperation">The parsing operation that failed</param>
        /// <param name="lineNumber">The line number where parsing failed (if applicable)</param>
        /// <param name="columnNumber">The column number where parsing failed (if applicable)</param>
        public OutputParsingException(string message, string rawOutput, string parsingOperation, int? lineNumber = null, int? columnNumber = null)
            : this(message, rawOutput, parsingOperation, ParsingErrorSeverity.Error, ParsingErrorCategory.General, lineNumber, columnNumber)
        {
        }

        /// <summary>
        /// Initializes a new instance of the OutputParsingException class with detailed parsing context and severity
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="rawOutput">The raw output that failed to parse</param>
        /// <param name="parsingOperation">The parsing operation that failed</param>
        /// <param name="severity">The severity level of the parsing error</param>
        /// <param name="errorCategory">The error category for better error handling</param>
        /// <param name="lineNumber">The line number where parsing failed (if applicable)</param>
        /// <param name="columnNumber">The column number where parsing failed (if applicable)</param>
        public OutputParsingException(string message, string rawOutput, string parsingOperation, ParsingErrorSeverity severity, ParsingErrorCategory errorCategory, int? lineNumber = null, int? columnNumber = null)
            : base(message)
        {
            RawOutput = rawOutput;
            ParsingOperation = parsingOperation;
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
            Severity = severity;
            ErrorCategory = errorCategory;
        }

        /// <summary>
        /// Initializes a new instance of the OutputParsingException class with detailed parsing context and inner exception
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="rawOutput">The raw output that failed to parse</param>
        /// <param name="parsingOperation">The parsing operation that failed</param>
        /// <param name="innerException">The exception that is the cause of the current exception</param>
        /// <param name="lineNumber">The line number where parsing failed (if applicable)</param>
        /// <param name="columnNumber">The column number where parsing failed (if applicable)</param>
        public OutputParsingException(string message, string rawOutput, string parsingOperation, Exception innerException, int? lineNumber = null, int? columnNumber = null)
            : this(message, rawOutput, parsingOperation, ParsingErrorSeverity.Error, ParsingErrorCategory.General, innerException, lineNumber, columnNumber)
        {
        }

        /// <summary>
        /// Initializes a new instance of the OutputParsingException class with detailed parsing context, severity, and inner exception
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="rawOutput">The raw output that failed to parse</param>
        /// <param name="parsingOperation">The parsing operation that failed</param>
        /// <param name="severity">The severity level of the parsing error</param>
        /// <param name="errorCategory">The error category for better error handling</param>
        /// <param name="innerException">The exception that is the cause of the current exception</param>
        /// <param name="lineNumber">The line number where parsing failed (if applicable)</param>
        /// <param name="columnNumber">The column number where parsing failed (if applicable)</param>
        public OutputParsingException(string message, string rawOutput, string parsingOperation, ParsingErrorSeverity severity, ParsingErrorCategory errorCategory, Exception innerException, int? lineNumber = null, int? columnNumber = null)
            : base(message, innerException)
        {
            RawOutput = rawOutput;
            ParsingOperation = parsingOperation;
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
            Severity = severity;
            ErrorCategory = errorCategory;
        }

        /// <summary>
        /// Creates a parsing exception for format errors
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="rawOutput">The raw output that failed to parse</param>
        /// <param name="parsingOperation">The parsing operation that failed</param>
        /// <param name="lineNumber">The line number where parsing failed (if applicable)</param>
        /// <returns>A parsing exception configured for format errors</returns>
        public static OutputParsingException FormatError(string message, string rawOutput, string parsingOperation, int? lineNumber = null)
        {
            return new OutputParsingException(
                message,
                rawOutput,
                parsingOperation,
                ParsingErrorSeverity.Error,
                ParsingErrorCategory.Format,
                lineNumber);
        }

        /// <summary>
        /// Creates a parsing exception for validation errors
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="rawOutput">The raw output that failed to parse</param>
        /// <param name="parsingOperation">The parsing operation that failed</param>
        /// <param name="lineNumber">The line number where parsing failed (if applicable)</param>
        /// <returns>A parsing exception configured for validation errors</returns>
        public static OutputParsingException ValidationError(string message, string rawOutput, string parsingOperation, int? lineNumber = null)
        {
            return new OutputParsingException(
                message,
                rawOutput,
                parsingOperation,
                ParsingErrorSeverity.Warning,
                ParsingErrorCategory.Validation,
                lineNumber);
        }

        /// <summary>
        /// Creates a parsing exception for connection errors
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="rawOutput">The raw output that failed to parse</param>
        /// <param name="parsingOperation">The parsing operation that failed</param>
        /// <returns>A parsing exception configured for connection errors</returns>
        public static OutputParsingException ConnectionError(string message, string rawOutput, string parsingOperation)
        {
            return new OutputParsingException(
                message,
                rawOutput,
                parsingOperation,
                ParsingErrorSeverity.Error,
                ParsingErrorCategory.Connection);
        }

        /// <summary>
        /// Returns a formatted string representation of the exception with context information
        /// </summary>
        /// <returns>A formatted string containing all exception details</returns>
        public override string ToString()
        {
            var baseMessage = base.ToString();

            var details = new System.Text.StringBuilder(baseMessage);
            details.AppendLine();
            details.AppendLine("--- Parsing Context ---");

            if (!string.IsNullOrEmpty(ParsingOperation))
            {
                details.AppendLine($"Operation: {ParsingOperation}");
            }

            details.AppendLine($"Severity: {Severity}");
            details.AppendLine($"Category: {ErrorCategory}");

            if (LineNumber.HasValue)
            {
                details.AppendLine($"Line: {LineNumber.Value}");
            }

            if (ColumnNumber.HasValue)
            {
                details.AppendLine($"Column: {ColumnNumber.Value}");
            }

            if (!string.IsNullOrEmpty(RawOutput))
            {
                details.AppendLine("Raw Output:");
                details.AppendLine(RawOutput);
            }

            return details.ToString();
        }
    }

    /// <summary>
    /// Defines the severity levels for parsing errors
    /// </summary>
    public enum ParsingErrorSeverity
    {
        /// <summary>
        /// Informational message that doesn't prevent parsing
        /// </summary>
        Info,

        /// <summary>
        /// Warning that might affect parsing results but doesn't stop processing
        /// </summary>
        Warning,

        /// <summary>
        /// Error that prevents successful parsing
        /// </summary>
        Error,

        /// <summary>
        /// Critical error that indicates a fundamental parsing failure
        /// </summary>
        Critical
    }

    /// <summary>
    /// Defines categories for parsing errors to enable better error handling
    /// </summary>
    public enum ParsingErrorCategory
    {
        /// <summary>
        /// General parsing error
        /// </summary>
        General,

        /// <summary>
        /// Error related to output format or structure
        /// </summary>
        Format,

        /// <summary>
        /// Error related to data validation
        /// </summary>
        Validation,

        /// <summary>
        /// Error related to network or server connection
        /// </summary>
        Connection,

        /// <summary>
        /// Error related to timeout or performance issues
        /// </summary>
        Timeout,

        /// <summary>
        /// Error related to authentication or authorization
        /// </summary>
        Authentication,

        /// <summary>
        /// Error related to configuration or setup
        /// </summary>
        Configuration
    }
}