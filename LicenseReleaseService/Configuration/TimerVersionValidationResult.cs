using System;
using System.Collections.Generic;
using System.Linq;

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Result of version-specific timer configuration validation
    /// </summary>
    public class TimerVersionValidationResult
    {
        /// <summary>
        /// Gets the list of validation errors
        /// </summary>
        public List<string> Errors { get; private set; }

        /// <summary>
        /// Gets the list of validation warnings
        /// </summary>
        public List<string> Warnings { get; private set; }

        /// <summary>
        /// Gets the list of informational messages
        /// </summary>
        public List<string> Information { get; private set; }

        /// <summary>
        /// Gets the validation timestamp
        /// </summary>
        public DateTime ValidationTime { get; }

        /// <summary>
        /// Gets whether the configuration is valid (no errors)
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// Gets whether the configuration has warnings
        /// </summary>
        public bool HasWarnings => Warnings.Count > 0;

        /// <summary>
        /// Gets the total number of validation messages
        /// </summary>
        public int TotalMessageCount => Errors.Count + Warnings.Count + Information.Count;

        /// <summary>
        /// Initializes a new instance of the TimerVersionValidationResult class
        /// </summary>
        public TimerVersionValidationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
            Information = new List<string>();
            ValidationTime = DateTime.Now;
        }

        /// <summary>
        /// Adds an error message
        /// </summary>
        /// <param name="error">Error message</param>
        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                Errors.Add(error);
            }
        }

        /// <summary>
        /// Adds a warning message
        /// </summary>
        /// <param name="warning">Warning message</param>
        public void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning))
            {
                Warnings.Add(warning);
            }
        }

        /// <summary>
        /// Adds an informational message
        /// </summary>
        /// <param name="info">Informational message</param>
        public void AddInformation(string info)
        {
            if (!string.IsNullOrWhiteSpace(info))
            {
                Information.Add(info);
            }
        }

        /// <summary>
        /// Gets a formatted summary of validation results
        /// </summary>
        /// <returns>Formatted validation summary</returns>
        public string GetSummary()
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"Version Configuration Validation Summary - {ValidationTime:yyyy-MM-dd HH:mm:ss}");
            summary.AppendLine($"Total Messages: {TotalMessageCount}");
            summary.AppendLine($"Status: {(IsValid ? "Valid" : "Invalid")}");
            summary.AppendLine();

            if (Errors.Count > 0)
            {
                summary.AppendLine($"Errors ({Errors.Count}):");
                foreach (var error in Errors)
                {
                    summary.AppendLine($"  • {error}");
                }
                summary.AppendLine();
            }

            if (Warnings.Count > 0)
            {
                summary.AppendLine($"Warnings ({Warnings.Count}):");
                foreach (var warning in Warnings)
                {
                    summary.AppendLine($"  • {warning}");
                }
                summary.AppendLine();
            }

            if (Information.Count > 0)
            {
                summary.AppendLine($"Information ({Information.Count}):");
                foreach (var info in Information)
                {
                    summary.AppendLine($"  • {info}");
                }
            }

            return summary.ToString();
        }

        /// <summary>
        /// Gets a short status message
        /// </summary>
        /// <returns>Short status message</returns>
        public string GetStatusMessage()
        {
            if (!IsValid)
            {
                return $"Invalid configuration with {Errors.Count} error(s)";
            }

            if (HasWarnings)
            {
                return $"Valid configuration with {Warnings.Count} warning(s)";
            }

            return "Valid configuration";
        }

        /// <summary>
        /// Merges this validation result with another result
        /// </summary>
        /// <param name="other">Other validation result to merge</param>
        public void Merge(TimerVersionValidationResult other)
        {
            if (other == null)
                return;

            Errors.AddRange(other.Errors);
            Warnings.AddRange(other.Warnings);
            Information.AddRange(other.Information);

            // Remove duplicates
            Errors = Errors.Distinct().ToList();
            Warnings = Warnings.Distinct().ToList();
            Information = Information.Distinct().ToList();
        }

        /// <summary>
        /// Creates a validation result from a list of messages
        /// </summary>
        /// <param name="errors">List of error messages</param>
        /// <param name="warnings">List of warning messages</param>
        /// <param name="information">List of informational messages</param>
        /// <returns>New validation result</returns>
        public static TimerVersionValidationResult FromMessages(
            IEnumerable<string> errors = null,
            IEnumerable<string> warnings = null,
            IEnumerable<string> information = null)
        {
            var result = new TimerVersionValidationResult();

            if (errors != null)
            {
                result.Errors.AddRange(errors.Where(e => !string.IsNullOrWhiteSpace(e)));
            }

            if (warnings != null)
            {
                result.Warnings.AddRange(warnings.Where(w => !string.IsNullOrWhiteSpace(w)));
            }

            if (information != null)
            {
                result.Information.AddRange(information.Where(i => !string.IsNullOrWhiteSpace(i)));
            }

            return result;
        }

        /// <summary>
        /// Creates a successful validation result
        /// </summary>
        /// <returns>Successful validation result</returns>
        public static TimerVersionValidationResult Success()
        {
            return new TimerVersionValidationResult();
        }

        /// <summary>
        /// Creates a failed validation result with errors
        /// </summary>
        /// <param name="errors">List of error messages</param>
        /// <returns>Failed validation result</returns>
        public static TimerVersionValidationResult Failure(params string[] errors)
        {
            var result = new TimerVersionValidationResult();
            if (errors != null)
            {
                result.Errors.AddRange(errors.Where(e => !string.IsNullOrWhiteSpace(e)));
            }
            return result;
        }
    }
}