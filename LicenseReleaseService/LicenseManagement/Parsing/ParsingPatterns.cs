using System;
using System.Text.RegularExpressions;

namespace LicenseReleaseService.LicenseManagement.Parsing
{
    /// <summary>
    /// Contains comprehensive regex patterns for parsing lmstat output with enhanced support for various formats
    /// </summary>
    public static class ParsingPatterns
    {
        #region Server Status Patterns

        /// <summary>
        /// Pattern for license server status line
        /// </summary>
        public static readonly Regex ServerStatusPattern = new Regex(
            @"license\s+server\s+(UP|DOWN|DOWN\s+\(MASTER\))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Pattern for license server address extraction
        /// </summary>
        public static readonly Regex ServerAddressPattern = new Regex(
            @"^License\s+server\s+status:\s*([^\r\n]+)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Pattern for server vendor information
        /// </summary>
        public static readonly Regex ServerVendorPattern = new Regex(
            @"^([^\s]+)\s+:\s+license\s+server\s+(UP|DOWN)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Pattern for server daemon status
        /// </summary>
        public static readonly Regex DaemonStatusPattern = new Regex(
            @"(?:lmgrd|vendor\s+daemon)\s+([^\s:]+)\s+:\s+(UP|DOWN|NOT\s+RESPONDING)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        /// <summary>
        /// Pattern for server start time
        /// </summary>
        public static readonly Regex ServerStartTimePattern = new Regex(
            @"(?:started|up)\s+(?:at|since)\s+(\d{1,2}/\d{1,2}/\d{4}\s+\d{1,2}:\d{2}:\d{2})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        /// <summary>
        /// Pattern for timestamp extraction
        /// </summary>
        public static readonly Regex TimestampPattern = new Regex(
            @"^(\d{2}/\d{2}/\d{4}\s+\d{2}:\d{2})",
            RegexOptions.Compiled | RegexOptions.Multiline);

        #endregion

        #region Feature Patterns

        /// <summary>
        /// Pattern for feature header line
        /// </summary>
        public static readonly Regex FeatureHeaderPattern = new Regex(
            @"^Users\s+of\s+([^\s:]+):",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Pattern for feature status line
        /// </summary>
        public static readonly Regex FeatureStatusPattern = new Regex(
            @"Total\s+of\s+(\d+)\s+licenses?\s+issued;\s+Total\s+of\s+(\d+)\s+licenses?\s+in\s+use",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Pattern for feature status with reservations
        /// </summary>
        public static readonly Regex FeatureQueuePattern = new Regex(
            @"Total\s+of\s+(\d+)\s+licenses?\s+in\s+use;\s+(\d+)\s+licenses?\s+(?:reserved|queued)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Pattern for feature expiration
        /// </summary>
        public static readonly Regex FeatureExpirationPattern = new Regex(
            @"expires\s+(?:at|on)\s+(\d{1,2}/\d{1,2}/\d{4}(?:\s+\d{1,2}:\d{2})?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        /// <summary>
        /// Pattern for feature version
        /// </summary>
        public static readonly Regex FeatureVersionPattern = new Regex(
            @"(?:version|v)\s+(\d+(?:\.\d+)*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion

        #region User License Patterns

        /// <summary>
        /// Comprehensive pattern for user license information
        /// </summary>
        public static readonly Regex UserLicensePattern = new Regex(
            @"^([^\s]+)\s+([^\s@]+@?[^\s]*)\s+(?:\([^\)]+\)\s+)?(?:v[\d.]+)?\s*(?:\([^)]+\))?\s*(?:\([^)]+\))?\s*(?:start\s+[^)]+)?\s*(?:\([^)]+\))?$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Enhanced user pattern with better host parsing
        /// </summary>
        public static readonly Regex EnhancedUserPattern = new Regex(
            @"^([^\s]+)\s+([^\s@]+@?[^\s]*)\s+\(([^)]+)\)\s+(?:v([\d.]+))?\s*(?:\(([^)]+)\))?\s*(?:\(([^)]+)\))?\s*(?:start\s+([^)]+))?",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Pattern for user display name
        /// </summary>
        public static readonly Regex UserDisplayNamePattern = new Regex(
            @"\(([^)]+)\)",
            RegexOptions.Compiled);

        /// <summary>
        /// Pattern for hostname extraction
        /// </summary>
        public static readonly Regex HostPattern = new Regex(
            @"([^\s]+)\s+\(([^\)]+)\)",
            RegexOptions.Compiled);

        /// <summary>
        /// Pattern for version extraction
        /// </summary>
        public static readonly Regex VersionPattern = new Regex(
            @"v(\d+\.\d+(?:\.\d+)?)",
            RegexOptions.Compiled);

        /// <summary>
        /// Pattern for checkout time
        /// </summary>
        public static readonly Regex CheckoutTimePattern = new Regex(
            @"start\s+(\d{1,2}/\d{1,2}\s+\d{1,2}:\d{2}(?::\d{2})?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Pattern for process information
        /// </summary>
        public static readonly Regex ProcessInfoPattern = new Regex(
            @"\((?:process\s+)?(\d+)(?:\s*([^)]+))?\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Pattern for handle information
        /// </summary>
        public static readonly Regex HandlePattern = new Regex(
            @"handle\s+(\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Pattern for idle time
        /// </summary>
        public static readonly Regex IdleTimePattern = new Regex(
            @"idle\s+(\d+)\s+(seconds?|minutes?|hours?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Pattern for borrowed licenses
        /// </summary>
        public static readonly Regex BorrowedPattern = new Regex(
            @"(borrowed|detached)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion

        #region Error Patterns

        /// <summary>
        /// Pattern for general errors
        /// </summary>
        public static readonly Regex ErrorPattern = new Regex(
            @"^(ERROR|FATAL|WARNING):\s*([^\r\n]+)",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        /// <summary>
        /// Pattern for connection errors
        /// </summary>
        public static readonly Regex ConnectionErrorPattern = new Regex(
            @"(Cannot\s+connect\s+to\s+license\s+server|Connection\s+refused|No\s+route\s+to\s+host|License\s+server\s+not\s+responding|Network\s+is\s+unreachable)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Pattern for invalid feature errors
        /// </summary>
        public static readonly Regex InvalidFeaturePattern = new Regex(
            @"(Invalid\s+feature\s+name|Unknown\s+feature|Feature\s+not\s+found)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Pattern for timeout errors
        /// </summary>
        public static readonly Regex TimeoutPattern = new Regex(
            @"(timeout|timed\s+out|Connection\s+timeout)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Pattern for license file errors
        /// </summary>
        public static readonly Regex LicenseFileErrorPattern = new Regex(
            @"(Invalid\s+license\s+file|Cannot\s+read\s+license\s+file|License\s+file\s+not\s+found)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion

        #region lmstat Specific Patterns

        /// <summary>
        /// Pattern for lmstat version information
        /// </summary>
        public static readonly Regex LmstatVersionPattern = new Regex(
            @"lmstat\s+.*?v(\d+\.\d+(?:\.\d+)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Pattern for lmstat feature usage summary
        /// </summary>
        public static readonly Regex FeatureUsageSummaryPattern = new Regex(
            @"^([^\s]+)\s+:\s+(\d+)\s+licenses?\s+issued\s*;\s*(\d+)\s+licenses?\s+in\s+use",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Pattern for lmstat server status summary
        /// </summary>
        public static readonly Regex ServerStatusSummaryPattern = new Regex(
            @"^License\s+server\s+status:\s*([^\r\n]+)\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Pattern for lmstat verbose output markers
        /// </summary>
        public static readonly Regex VerboseMarkerPattern = new Regex(
            @"^(?:verbose|debug|detailed)\s+(?:mode|output)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        /// <summary>
        /// Pattern for lmstat server uptime
        /// </summary>
        public static readonly Regex ServerUptimePattern = new Regex(
            @"(?:up\s+time|uptime)\s*:\s*(\d+)\s+(?:days?|hours?|minutes?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Pattern for lmstat platform information
        /// </summary>
        public static readonly Regex PlatformInfoPattern = new Regex(
            @"(?:platform|architecture)\s*:\s*([^\r\n]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        #endregion

        #region Utility Patterns

        /// <summary>
        /// Pattern for empty lines
        /// </summary>
        public static readonly Regex EmptyLinePattern = new Regex(
            @"^\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Pattern for comment lines
        /// </summary>
        public static readonly Regex CommentLinePattern = new Regex(
            @"^\s*#",
            RegexOptions.Compiled);

        /// <summary>
        /// Pattern for IPv4 addresses
        /// </summary>
        public static readonly Regex Ipv4AddressPattern = new Regex(
            @"\b(?:\d{1,3}\.){3}\d{1,3}\b",
            RegexOptions.Compiled);

        /// <summary>
        /// Pattern for hostnames
        /// </summary>
        public static readonly Regex HostnamePattern = new Regex(
            @"^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)*$",
            RegexOptions.Compiled);

        /// <summary>
        /// Pattern for usernames
        /// </summary>
        public static readonly Regex UsernamePattern = new Regex(
            @"^[a-zA-Z0-9_\-\.@]+$",
            RegexOptions.Compiled);

        #endregion

        #region Pattern Selection Methods

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
        /// Gets all patterns relevant for lmstat parsing
        /// </summary>
        /// <returns>Array of lmstat-specific patterns</returns>
        public static Regex[] GetLmstatPatterns()
        {
            return new[]
            {
                ServerStatusPattern,
                FeatureHeaderPattern,
                FeatureStatusPattern,
                UserLicensePattern,
                EnhancedUserPattern,
                ServerAddressPattern,
                ErrorPattern
            };
        }

        /// <summary>
        /// Gets patterns for parsing user information
        /// </summary>
        /// <returns>Array of user-specific patterns</returns>
        public static Regex[] GetUserPatterns()
        {
            return new[]
            {
                EnhancedUserPattern,
                UserDisplayNamePattern,
                VersionPattern,
                CheckoutTimePattern,
                ProcessInfoPattern,
                IdleTimePattern,
                BorrowedPattern
            };
        }

        #endregion

        #region Validation Methods

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
                   RemoveErrorPattern.IsMatch(input) ||
                   TimeoutPattern.IsMatch(input) ||
                   LicenseFileErrorPattern.IsMatch(input);
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

        /// <summary>
        /// Validates if a hostname is in correct format
        /// </summary>
        /// <param name="hostname">The hostname to validate</param>
        /// <returns>True if the hostname is valid</returns>
        public static bool IsValidHostname(string hostname)
        {
            if (string.IsNullOrEmpty(hostname))
                return false;

            return HostnamePattern.IsMatch(hostname) || Ipv4AddressPattern.IsMatch(hostname);
        }

        /// <summary>
        /// Validates if a username is in correct format
        /// </summary>
        /// <param name="username">The username to validate</param>
        /// <returns>True if the username is valid</returns>
        public static bool IsValidUsername(string username)
        {
            if (string.IsNullOrEmpty(username))
                return false;

            return UsernamePattern.IsMatch(username);
        }

        #endregion

        #region Legacy Pattern Support (for backward compatibility)

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
            @"^The\s+hostid\s+of\s+this\s+machine\s+is\s+\""([^""]+)\""",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // Performance and validation patterns
        public static readonly Regex ExecutionTimePattern = new Regex(
            @"real\s+(\d+)m([\d.]+)s",
            RegexOptions.Compiled);

        public static readonly Regex MemoryUsagePattern = new Regex(
            @"([0-9.]+)\s+(KB|MB|GB)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion
    }
}