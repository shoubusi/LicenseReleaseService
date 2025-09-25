using System;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Exception thrown when output parsing fails
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
            : base(message)
        {
            RawOutput = rawOutput;
            ParsingOperation = parsingOperation;
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
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
            : base(message, innerException)
        {
            RawOutput = rawOutput;
            ParsingOperation = parsingOperation;
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
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
}