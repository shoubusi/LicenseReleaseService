using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LicenseReleaseService.LicenseManagement.Models;
using LicenseReleaseService.LicenseManagement.Parsing;

namespace LicenseReleaseService.LicenseManagement.Parsing
{
    /// <summary>
    /// Specialized parser for lmstat output that uses the new license information models
    /// </summary>
    public class LmstatOutputParser
    {
        private readonly int _maxOutputLines = 10000;

        /// <summary>
        /// Initializes a new instance of the LmstatOutputParser class
        /// </summary>
        public LmstatOutputParser()
        {
        }

        /// <summary>
        /// Parses lmstat output and returns comprehensive license server status using new models
        /// </summary>
        /// <param name="output">The lmstat output to parse</param>
        /// <param name="serverAddress">The server address used for the query</param>
        /// <returns>Comprehensive license server status with detailed feature information</returns>
        public LicenseServerStatus ParseLmstatOutput(string output, string serverAddress)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(output))
                {
                    throw new OutputParsingException("Empty lmstat output", output, "lmstat");
                }

                var status = new LicenseServerStatus
                {
                    Server = serverAddress,
                    LastChecked = DateTime.Now
                };

                // Check for errors in output
                if (ParsingPatterns.ContainsErrorPatterns(output))
                {
                    var errors = ParsingPatterns.ExtractErrorMessages(output);
                    throw new OutputParsingException(
                        $"lmstat command failed: {string.Join(", ", errors)}",
                        output,
                        "lmstat");
                }

                // Parse server status and information
                ParseServerInformation(output, status);

                // Parse feature information using new models
                ParseFeatureInformation(output, status);

                // Calculate aggregate statistics
                UpdateAggregateStatistics(status);

                return status;
            }
            catch (OutputParsingException)
            {
                throw; // Re-throw our custom exceptions
            }
            catch (Exception ex)
            {
                throw new OutputParsingException(
                    $"Failed to parse lmstat output: {ex.Message}",
                    output,
                    "lmstat",
                    ex);
            }
        }

        /// <summary>
        /// Parses lmstat verbose output with additional details
        /// </summary>
        /// <param name="output">The lmstat verbose output to parse</param>
        /// <param name="serverAddress">The server address used for the query</param>
        /// <returns>Comprehensive license server status with verbose information</returns>
        public LicenseServerStatus ParseLmstatVerboseOutput(string output, string serverAddress)
        {
            try
            {
                var status = ParseLmstatOutput(output, serverAddress);

                // Parse additional verbose information
                ParseVerboseInformation(output, status);

                return status;
            }
            catch (Exception ex)
            {
                throw new OutputParsingException(
                    $"Failed to parse verbose lmstat output: {ex.Message}",
                    output,
                    "lmstat-verbose",
                    ex);
            }
        }

        /// <summary>
        /// Parses lmstat output for a specific feature only
        /// </summary>
        /// <param name="output">The lmstat output to parse</param>
        /// <param name="serverAddress">The server address used for the query</param>
        /// <param name="featureName">The specific feature name to parse</param>
        /// <returns>License feature information for the specified feature</returns>
        public LicenseFeature ParseFeatureOutput(string output, string serverAddress, string featureName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(output))
                {
                    throw new OutputParsingException("Empty lmstat feature output", output, $"lmstat-feature-{featureName}");
                }

                if (string.IsNullOrWhiteSpace(featureName))
                {
                    throw new ArgumentException("Feature name cannot be null or whitespace", nameof(featureName));
                }

                // Check for errors in output
                if (ParsingPatterns.ContainsErrorPatterns(output))
                {
                    var errors = ParsingPatterns.ExtractErrorMessages(output);
                    throw new OutputParsingException(
                        $"lmstat feature command failed: {string.Join(", ", errors)}",
                        output,
                        $"lmstat-feature-{featureName}");
                }

                var feature = ParseSingleFeature(output, featureName);

                if (feature == null)
                {
                    throw new OutputParsingException(
                        $"Feature '{featureName}' not found in lmstat output",
                        output,
                        $"lmstat-feature-{featureName}");
                }

                return feature;
            }
            catch (OutputParsingException)
            {
                throw; // Re-throw our custom exceptions
            }
            catch (Exception ex)
            {
                throw new OutputParsingException(
                    $"Failed to parse lmstat feature output: {ex.Message}",
                    output,
                    $"lmstat-feature-{featureName}",
                    ex);
            }
        }

        /// <summary>
        /// Parses lmstat output incrementally for large outputs
        /// </summary>
        /// <param name="outputLines">Enumerable of output lines</param>
        /// <param name="serverAddress">The server address used for the query</param>
        /// <param name="isVerbose">Whether this is verbose output</param>
        /// <returns>Comprehensive license server status</returns>
        public LicenseServerStatus ParseOutputIncrementally(IEnumerable<string> outputLines, string serverAddress, bool isVerbose = false)
        {
            try
            {
                var lineCount = 0;
                var outputBuilder = new StringBuilder();

                foreach (var line in outputLines)
                {
                    lineCount++;
                    outputBuilder.AppendLine(line);

                    // Prevent memory issues with extremely large outputs
                    if (lineCount > _maxOutputLines)
                    {
                        break;
                    }
                }

                var fullOutput = outputBuilder.ToString();

                if (isVerbose)
                {
                    return ParseLmstatVerboseOutput(fullOutput, serverAddress);
                }
                else
                {
                    return ParseLmstatOutput(fullOutput, serverAddress);
                }
            }
            catch (Exception ex) when (!(ex is OutputParsingException))
            {
                throw new OutputParsingException(
                    $"Failed during incremental parsing: {ex.Message}",
                    null,
                    "lmstat-incremental",
                    ex);
            }
        }

        /// <summary>
        /// Validates parsed license server status for consistency
        /// </summary>
        /// <param name="status">The license server status to validate</param>
        /// <returns>True if the data is valid, false otherwise</returns>
        public bool ValidateParsedData(LicenseServerStatus status)
        {
            try
            {
                if (status == null)
                {
                    return false;
                }

                // Validate server information
                if (string.IsNullOrWhiteSpace(status.Server))
                {
                    // Warning: Server address is empty
                }

                // Validate feature details using new models
                foreach (var feature in status.FeatureDetails.Values)
                {
                    var validationErrors = feature.Validate();
                    if (validationErrors.Count > 0)
                    {
                        // Warning: Validation warnings for feature
                    }

                    // Validate individual user licenses
                    foreach (var user in feature.GetAllUsers())
                    {
                        var userValidationErrors = user.Validate();
                        if (userValidationErrors.Count > 0)
                        {
                            // Warning: Validation warnings for user
                        }
                    }
                }

                // Validate aggregate counts consistency
                if (status.FeatureDetails.Values.Sum(f => f.TotalLicenses) != status.TotalLicenses)
                {
                    // Warning: Aggregate total licenses mismatch
                }

                if (status.FeatureDetails.Values.Sum(f => f.UsedLicenses) != status.LicensesInUse)
                {
                    // Warning: Aggregate used licenses mismatch
                }

                return true;
            }
            catch (Exception ex)
            {
                // Validation failed
                return false;
            }
        }

        /// <summary>
        /// Extracts server information from lmstat output
        /// </summary>
        /// <param name="output">The lmstat output</param>
        /// <returns>Dictionary of server information</returns>
        public Dictionary<string, string> ExtractServerInformation(string output)
        {
            var serverInfo = new Dictionary<string, string>();

            try
            {
                if (string.IsNullOrWhiteSpace(output))
                    return serverInfo;

                // Extract server address
                var addressMatch = ParsingPatterns.ServerAddressPattern.Match(output);
                if (addressMatch.Success)
                {
                    serverInfo["Address"] = addressMatch.Groups[1].Value.Trim();
                }

                // Extract vendor information
                var vendorMatch = ParsingPatterns.ServerVendorPattern.Match(output);
                if (vendorMatch.Success)
                {
                    serverInfo["Vendor"] = vendorMatch.Groups[1].Value.Trim();
                }

                // Extract server status
                var statusMatch = ParsingPatterns.ServerStatusPattern.Match(output);
                if (statusMatch.Success)
                {
                    serverInfo["Status"] = statusMatch.Groups[1].Value.ToUpper();
                }

                // Extract start time
                var startTimeMatch = ParsingPatterns.ServerStartTimePattern.Match(output);
                if (startTimeMatch.Success)
                {
                    serverInfo["StartTime"] = startTimeMatch.Groups[1].Value;
                }

                // Extract platform information
                var platformMatch = ParsingPatterns.PlatformInfoPattern.Match(output);
                if (platformMatch.Success)
                {
                    serverInfo["Platform"] = platformMatch.Groups[1].Value.Trim();
                }

                // Extract uptime information
                var uptimeMatch = ParsingPatterns.ServerUptimePattern.Match(output);
                if (uptimeMatch.Success)
                {
                    serverInfo["Uptime"] = uptimeMatch.Groups[1].Value + " " + uptimeMatch.Groups[2].Value;
                }

                return serverInfo;
            }
            catch (Exception ex)
            {
                // Failed to extract server information
                return serverInfo;
            }
        }

        private void ParseServerInformation(string output, LicenseServerStatus status)
        {
            // Parse server status
            var serverMatch = ParsingPatterns.ServerStatusPattern.Match(output);
            if (serverMatch.Success)
            {
                var serverStatus = serverMatch.Groups[1].Value.ToUpper();
                status.IsServerUp = serverStatus.Contains("UP");
                status.IsHealthy = status.IsServerUp;
                status.StatusMessage = $"License server is {serverStatus}";
            }

            // Extract server address if not provided
            if (string.IsNullOrWhiteSpace(status.Server))
            {
                var addressMatch = ParsingPatterns.ServerAddressPattern.Match(output);
                if (addressMatch.Success)
                {
                    status.Server = addressMatch.Groups[1].Value.Trim();
                }
            }

            // Parse port from server address
            if (status.Server.Contains("@"))
            {
                var parts = status.Server.Split('@');
                if (parts.Length == 2)
                {
                    status.ServerName = parts[0];
                    if (int.TryParse(parts[1], out var port))
                    {
                        status.Port = port;
                    }
                }
            }

            // Extract additional server information
            var vendorMatch = ParsingPatterns.ServerVendorPattern.Match(output);
            if (vendorMatch.Success)
            {
                status.Vendor = vendorMatch.Groups[1].Value.Trim();
            }

            // Parse start time
            var startTimeMatch = ParsingPatterns.ServerStartTimePattern.Match(output);
            if (startTimeMatch.Success && DateTime.TryParse(startTimeMatch.Groups[1].Value, out var startTime))
            {
                status.ServerStartTime = startTime;
                status.UptimeSeconds = (long)(DateTime.Now - startTime).TotalSeconds;
            }

            // Parse platform information
            var platformMatch = ParsingPatterns.PlatformInfoPattern.Match(output);
            if (platformMatch.Success)
            {
                var platformInfo = platformMatch.Groups[1].Value.Trim();
                if (platformInfo.Contains("x64") || platformInfo.Contains("64-bit"))
                {
                    status.Architecture = "64-bit";
                }
                else if (platformInfo.Contains("x86") || platformInfo.Contains("32-bit"))
                {
                    status.Architecture = "32-bit";
                }
                status.Platform = platformInfo;
            }
        }

        private void ParseFeatureInformation(string output, LicenseServerStatus status)
        {
            var lines = output.Split('\n');
            LicenseFeature currentFeature = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Check for feature header
                var featureMatch = ParsingPatterns.FeatureHeaderPattern.Match(line);
                if (featureMatch.Success)
                {
                    currentFeature = new LicenseFeature
                    {
                        Name = featureMatch.Groups[1].Value.Trim()
                    };

                    // Parse feature status from the same or next few lines
                    ParseFeatureStatus(lines, ref i, currentFeature);

                    // Parse feature users
                    ParseFeatureUsers(lines, ref i, currentFeature);

                    status.FeatureDetails[currentFeature.Name] = currentFeature;
                    continue;
                }

                // Also check for alternative feature format (some lmstat versions)
                var altFeatureMatch = ParsingPatterns.FeatureUsageSummaryPattern.Match(line);
                if (altFeatureMatch.Success && currentFeature == null)
                {
                    var featureName = altFeatureMatch.Groups[1].Value.Trim();
                    if (!status.FeatureDetails.ContainsKey(featureName))
                    {
                        currentFeature = new LicenseFeature { Name = featureName };

                        if (int.TryParse(altFeatureMatch.Groups[2].Value, out var total) &&
                            int.TryParse(altFeatureMatch.Groups[3].Value, out var used))
                        {
                            currentFeature.TotalLicenses = total;
                            currentFeature.UsedLicenses = used;
                            currentFeature.AvailableLicenses = total - used;
                        }

                        status.FeatureDetails[featureName] = currentFeature;
                    }
                }
            }
        }

        private void ParseFeatureStatus(string[] lines, ref int currentIndex, LicenseFeature feature)
        {
            // Look ahead a few lines for feature status information
            for (int lookAhead = 0; lookAhead < 5 && currentIndex + lookAhead < lines.Length; lookAhead++)
            {
                var line = lines[currentIndex + lookAhead].Trim();

                var statusMatch = ParsingPatterns.FeatureStatusPattern.Match(line);
                if (statusMatch.Success)
                {
                    if (int.TryParse(statusMatch.Groups[1].Value, out var total) &&
                        int.TryParse(statusMatch.Groups[2].Value, out var inUse))
                    {
                        feature.TotalLicenses = total;
                        feature.UsedLicenses = inUse;
                        feature.AvailableLicenses = total - inUse;
                        currentIndex += lookAhead; // Skip ahead
                        return;
                    }
                }

                // Check for expiration information
                var expirationMatch = ParsingPatterns.FeatureExpirationPattern.Match(line);
                if (expirationMatch.Success)
                {
                    if (DateTime.TryParse(expirationMatch.Groups[1].Value, out var expirationDate))
                    {
                        feature.ExpirationDate = expirationDate;
                    }
                }

                // Check for version information
                var versionMatch = ParsingPatterns.FeatureVersionPattern.Match(line);
                if (versionMatch.Success)
                {
                    feature.Version = versionMatch.Groups[1].Value;
                }
            }
        }

        private void ParseFeatureUsers(string[] lines, ref int currentIndex, LicenseFeature feature)
        {
            // Continue parsing from current position to find all users for this feature
            for (int i = currentIndex + 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                // Stop if we hit another feature header or empty line followed by non-user content
                if (ParsingPatterns.FeatureHeaderPattern.IsMatch(line) ||
                    (string.IsNullOrWhiteSpace(line) && i + 1 < lines.Length &&
                     !ParsingPatterns.EnhancedUserPattern.IsMatch(lines[i + 1].Trim())))
                {
                    break;
                }

                // Parse user information using enhanced pattern
                var userMatch = ParsingPatterns.EnhancedUserPattern.Match(line);
                if (userMatch.Success)
                {
                    var licenseInfo = ParseUserLicenseInfo(userMatch, feature.Name);

                    // Add user to appropriate collection based on status
                    if (licenseInfo.Status == LicenseStatus.Borrowed ||
                        line.Contains("borrowed", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("detached", StringComparison.OrdinalIgnoreCase))
                    {
                        feature.AddBorrowedUser(licenseInfo);
                    }
                    else if (licenseInfo.IsIdle)
                    {
                        feature.AddIdleUser(licenseInfo);
                    }
                    else
                    {
                        feature.AddActiveUser(licenseInfo);
                    }
                }
                else
                {
                    // Try the basic user pattern as fallback
                    var basicUserMatch = ParsingPatterns.UserLicensePattern.Match(line);
                    if (basicUserMatch.Success)
                    {
                        var licenseInfo = ParseBasicUserInfo(basicUserMatch, feature.Name);
                        feature.AddActiveUser(licenseInfo);
                    }
                }
            }
        }

        private LicenseInfo ParseUserLicenseInfo(Match userMatch, string featureName)
        {
            var licenseInfo = new LicenseInfo
            {
                Feature = featureName,
                BorrowTime = DateTime.Now,
                UsageDuration = TimeSpan.Zero,
                Status = LicenseStatus.Active
            };

            // Username (group 1)
            licenseInfo.UserHost = userMatch.Groups[1].Value.Trim();

            // Host information (group 2)
            var hostInfo = userMatch.Groups[2].Value.Trim();
            if (hostInfo.Contains("@"))
            {
                var parts = hostInfo.Split('@');
                if (parts.Length == 2)
                {
                    licenseInfo.ClientHost = parts[0].Trim();
                    // Additional host info could be in parts[1]
                }
                else
                {
                    licenseInfo.ClientHost = hostInfo;
                }
            }
            else
            {
                licenseInfo.ClientHost = hostInfo;
            }

            // Display name (group 3)
            if (userMatch.Groups.Count > 3 && !string.IsNullOrWhiteSpace(userMatch.Groups[3].Value))
            {
                var displayName = userMatch.Groups[3].Value.Trim();
                // Use display name to enrich user host information
                if (!string.IsNullOrEmpty(displayName) && displayName != licenseInfo.UserHost)
                {
                    // Could store additional display name info if needed
                }
            }

            // License version (group 4)
            if (userMatch.Groups.Count > 4 && !string.IsNullOrWhiteSpace(userMatch.Groups[4].Value))
            {
                licenseInfo.LicenseVersion = userMatch.Groups[4].Value.Trim();
            }

            // Additional status information (groups 5 and 6)
            if (userMatch.Groups.Count > 5 && !string.IsNullOrWhiteSpace(userMatch.Groups[5].Value))
            {
                var statusInfo = userMatch.Groups[5].Value.Trim().ToLower();
                if (statusInfo.Contains("idle"))
                {
                    licenseInfo.IsIdle = true;
                    licenseInfo.IdleReason = "Idle license";
                    licenseInfo.Status = LicenseStatus.Idle;
                }
            }

            // Checkout time (group 7)
            if (userMatch.Groups.Count > 7 && !string.IsNullOrWhiteSpace(userMatch.Groups[7].Value))
            {
                var timeStr = userMatch.Groups[7].Value.Trim();
                if (DateTime.TryParse(timeStr, out var checkoutTime))
                {
                    licenseInfo.BorrowTime = checkoutTime;
                    licenseInfo.UsageDuration = DateTime.Now - checkoutTime;
                }
            }

            // Check for borrowed status in the full match
            if (userMatch.Value.Contains("borrowed", StringComparison.OrdinalIgnoreCase) ||
                userMatch.Value.Contains("detached", StringComparison.OrdinalIgnoreCase))
            {
                licenseInfo.Status = LicenseStatus.Borrowed;
                licenseInfo.IsIdle = false;
            }

            return licenseInfo;
        }

        private LicenseInfo ParseBasicUserInfo(Match userMatch, string featureName)
        {
            var licenseInfo = new LicenseInfo
            {
                Feature = featureName,
                BorrowTime = DateTime.Now,
                UsageDuration = TimeSpan.Zero,
                Status = LicenseStatus.Active
            };

            // Username (group 1)
            licenseInfo.UserHost = userMatch.Groups[1].Value.Trim();

            // Host information (group 2)
            licenseInfo.ClientHost = userMatch.Groups[2].Value.Trim();

            // Extract additional information from the line
            var line = userMatch.Value;

            // Check for version information
            var versionMatch = ParsingPatterns.VersionPattern.Match(line);
            if (versionMatch.Success)
            {
                licenseInfo.LicenseVersion = versionMatch.Value;
            }

            // Check for checkout time
            var timeMatch = ParsingPatterns.CheckoutTimePattern.Match(line);
            if (timeMatch.Success)
            {
                var timeStr = timeMatch.Groups[1].Value.Trim();
                if (DateTime.TryParse(timeStr, out var checkoutTime))
                {
                    licenseInfo.BorrowTime = checkoutTime;
                    licenseInfo.UsageDuration = DateTime.Now - checkoutTime;
                }
            }

            // Check for idle information
            var idleMatch = ParsingPatterns.IdleTimePattern.Match(line);
            if (idleMatch.Success)
            {
                licenseInfo.IsIdle = true;
                licenseInfo.IdleReason = $"Idle for {idleMatch.Groups[1].Value} {idleMatch.Groups[2].Value}";
                licenseInfo.Status = LicenseStatus.Idle;
            }

            return licenseInfo;
        }

        private LicenseFeature ParseSingleFeature(string output, string featureName)
        {
            var lines = output.Split('\n');
            LicenseFeature feature = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Check for feature header
                var featureMatch = ParsingPatterns.FeatureHeaderPattern.Match(line);
                if (featureMatch.Success)
                {
                    var currentFeatureName = featureMatch.Groups[1].Value.Trim();
                    if (currentFeatureName.Equals(featureName, StringComparison.OrdinalIgnoreCase))
                    {
                        feature = new LicenseFeature { Name = currentFeatureName };
                        ParseFeatureStatus(lines, ref i, feature);
                        ParseFeatureUsers(lines, ref i, feature);
                        return feature;
                    }
                }
            }

            return null;
        }

        private void ParseVerboseInformation(string output, LicenseServerStatus status)
        {
            // Parse additional verbose information
            var lines = output.Split('\n');

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Parse daemon status
                var daemonMatch = ParsingPatterns.DaemonStatusPattern.Match(trimmedLine);
                if (daemonMatch.Success)
                {
                    status.DaemonStatus = $"{daemonMatch.Groups[1].Value}: {daemonMatch.Groups[2].Value}";
                    status.AddServerMessage(trimmedLine);
                }

                // Parse server messages
                if (trimmedLine.Contains("server") || trimmedLine.Contains("daemon") ||
                    trimmedLine.Contains("license") || trimmedLine.Contains("feature"))
                {
                    status.AddServerMessage(trimmedLine);
                }
            }
        }

        private void UpdateAggregateStatistics(LicenseServerStatus status)
        {
            status.TotalLicenses = status.FeatureDetails.Values.Sum(f => f.TotalLicenses);
            status.LicensesInUse = status.FeatureDetails.Values.Sum(f => f.UsedLicenses);
            status.AvailableLicenses = status.FeatureDetails.Values.Sum(f => f.AvailableLicenses);
            status.ConnectedUsers = status.TotalUsers;
        }
    }
}