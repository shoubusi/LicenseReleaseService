using System;
using System.Text.RegularExpressions;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Contains regex patterns for parsing various lmutil.exe output formats
    /// </summary>
    public static class ParsingPatterns
    {
        // General patterns
        public static readonly Regex ServerStatusPattern = new Regex(
            @"license\s+server\s+(UP|DOWN|DOWN\s+\(MASTER\))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static readonly Regex ServerAddressPattern = new Regex(
            @"^License\s+server\s+status:\s*([^\r\n]+)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public static readonly Regex TimestampPattern = new Regex(
            @"^(\d{2}/\d{2}/\d{4}\s+\d{2}:\d{2})",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // Feature patterns
        public static readonly Regex FeatureHeaderPattern = new Regex(
            @"^Users\s+of\s+([^\s:]+):",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public static readonly Regex FeatureStatusPattern = new Regex(
            @"Total\s+of\s+(\d+)\s+licenses\s+issued;\s+Total\s+of\s+(\d+)\s+licenses\s+in\s+use",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static readonly Regex FeatureQueuePattern = new Regex(
            @"Total\s+of\s+(\d+)\s+licenses\s+in\s+use;\s+(\d+)\s+licenses\s+reserved",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // User/license patterns
        public static readonly Regex UserLicensePattern = new Regex(
            @"^([^\s]+)\s+([^\s@]+@?[^\s]*)\s+\([^\)]+\)\s+(v[\d.]+)?\s*(\([^)]+\))?\s*(\([^)]+\))?\s*(start\s+[^)]+)?",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public static readonly Regex UserPattern = new Regex(
            @"^([^\s]+)\s+([^\s@]+@?[^\s]*)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public static readonly Regex HostPattern = new Regex(
            @"([^\s]+)\s+\(([^\)]+)\)",
            RegexOptions.Compiled);

        public static readonly Regex VersionPattern = new Regex(
            @"v(\d+\.\d+)",
            RegexOptions.Compiled);

        // License server patterns
        public static readonly Regex ServerPattern = new Regex(
            @"^License\s+server\s+[\w-]+\s+:\s*([^\r\n]+)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public static readonly Regex ServerVendorPattern = new Regex(
            @"^([^\s]+)\s+:\s+license\s+server\s+(UP|DOWN)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // Error patterns
        public static readonly Regex ErrorPattern = new Regex(
            @"^(ERROR|FATAL|WARNING):\s*([^\r\n]+)",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        public static readonly Regex ConnectionErrorPattern = new Regex(
            @"(Cannot\s+connect\s+to\s+license\s+server|Connection\s+refused|No\s+route\s+to\s+host|License\s+server\s+not\s+responding)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static readonly Regex InvalidFeaturePattern = new Regex(
            @"(Invalid\s+feature\s+name|Unknown\s+feature|Feature\s+not\s+found)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // lmremove specific patterns
        public static readonly Regex RemoveSuccessPattern = new Regex(
            @"(Removed|Released)\s+(license|user)\s+([^\r\n]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static readonly Regex RemoveErrorPattern = new Regex(
            @"(Cannot\s+remove|Failed\s+to\s+remove|User\s+not\s+found|License\s+not\s+found)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // lmcksum patterns
        public static readonly Regex ChecksumPattern = new Regex(
            @"^([^\s]+)\s+:\s+([0-9a-fA-F]+)\s+([^\r\n]+)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // lmdiag patterns
        public static readonly Regex DiagFeaturePattern = new Regex(
            @"^Feature:\s+([^\r\n]+)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public static readonly Regex DiagStatusPattern = new Regex(
            @"^Status:\s+([^\r\n]+)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // lmhostid patterns
        public static readonly Regex HostIdPattern = new Regex(
            @"^The\s+hostid\s+of\s+this\s+machine\s+is\s+\"([^\"]+)\"",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // Utility patterns
        public static readonly Regex EmptyLinePattern = new Regex(
            @"^\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public static readonly Regex CommentLinePattern = new Regex(
            @"^\s*#",
            RegexOptions.Compiled);

        // Performance and validation patterns
        public static readonly Regex ExecutionTimePattern = new Regex(
            @"real\s+(\d+)m([\d.]+)s",
            RegexOptions.Compiled);

        public static readonly Regex MemoryUsagePattern = new Regex(
            @"([0-9.]+)\s+(KB|MB|GB)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Gets a regex pattern for a specific lmutil command
        /// </summary>
        /// <param name="command">The lmutil command (lmstat, lmremove, etc.)</param>
        /// <returns>A regex pattern suitable for the command output</returns>
        public static Regex GetCommandPattern(string command)
        {
            return command.ToLowerInvariant() switch
            {
                "lmstat" => ServerStatusPattern,
                "lmremove" => RemoveSuccessPattern,
                "lmcksum" => ChecksumPattern,
                "lmdiag" => DiagFeaturePattern,
                "lmhostid" => HostIdPattern,
                _ => ErrorPattern
            };
        }

        /// <summary>
        /// Validates if a string matches any error pattern
        /// </summary>
        /// <param name="input">The input string to validate</param>
        /// <returns>True if the input contains error patterns</returns>
        public static bool ContainsErrorPatterns(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return ErrorPattern.IsMatch(input) ||
                   ConnectionErrorPattern.IsMatch(input) ||
                   InvalidFeaturePattern.IsMatch(input) ||
                   RemoveErrorPattern.IsMatch(input);
        }

        /// <summary>
        /// Extracts all error messages from the input
        /// </summary>
        /// <param name="input">The input string to search</param>
        /// <returns>Array of error messages found</returns>
        public static string[] ExtractErrorMessages(string input)
        {
            if (string.IsNullOrEmpty(input))
                return Array.Empty<string>();

            var matches = ErrorPattern.Matches(input);
            var errors = new string[matches.Count];

            for (int i = 0; i < matches.Count; i++)
            {
                errors[i] = matches[i].Groups[2].Value.Trim();
            }

            return errors;
        }

        /// <summary>
        /// Checks if the input indicates a successful operation
        /// </summary>
        /// <param name="input">The input string to check</param>
        /// <returns>True if the input indicates success</returns>
        public static bool IndicatesSuccess(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return ServerStatusPattern.IsMatch(input) ||
                   RemoveSuccessPattern.IsMatch(input) ||
                   !ContainsErrorPatterns(input);
        }
    }
}