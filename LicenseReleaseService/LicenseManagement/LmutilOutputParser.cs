using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.LicenseManagement
{

    /// <summary>
    /// Parses lmutil.exe output with comprehensive error handling and validation
    /// </summary>
    public class LmutilOutputParser
    {
        private readonly ILogger<LmutilOutputParser> _logger;
        private readonly int _maxOutputLines = 10000; // Prevent memory issues with large outputs

        /// <summary>
        /// Initializes a new instance of the LmutilOutputParser class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public LmutilOutputParser(ILogger<LmutilOutputParser> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Parses lmstat command output
        /// </summary>
        /// <param name="output">The lmstat output to parse</param>
        /// <param name="serverAddress">The server address used for the query</param>
        /// <returns>Parsed license server status</returns>
        public LicenseServerStatus ParseLmstatOutput(string output, string serverAddress)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(output))
                {
                    throw new OutputParsingException("Empty lmstat output", output, "lmstat");
                }

                _logger.LogDebug("Parsing lmstat output for server: {ServerAddress}", serverAddress);

                var status = new LicenseServerStatus
                {
                    ServerAddress = serverAddress,
                    RawOutput = output
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

                // Parse server status
                ParseServerStatus(output, status);

                // Parse feature information
                ParseFeatureInformation(output, status);

                _logger.LogInformation("Successfully parsed lmstat output for server: {ServerAddress}. Found {FeatureCount} features.",
                    serverAddress, status.Features.Count);

                return status;
            }
            catch (OutputParsingException)
            {
                throw; // Re-throw our custom exceptions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse lmstat output for server: {ServerAddress}", serverAddress);
                throw new OutputParsingException(
                    $"Failed to parse lmstat output: {ex.Message}",
                    output,
                    "lmstat",
                    ex);
            }
        }

        /// <summary>
        /// Parses lmremove command output
        /// </summary>
        /// <param name="output">The lmremove output to parse</param>
        /// <param name="feature">The feature name that was removed</param>
        /// <param name="user">The user that was removed</param>
        /// <returns>License release result</returns>
        public LicenseReleaseResult ParseLmremoveOutput(string output, string feature, string user)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(output))
                {
                    throw new OutputParsingException("Empty lmremove output", output, "lmremove");
                }

                _logger.LogDebug("Parsing lmremove output for feature: {Feature}, user: {User}", feature, user);

                var result = new LicenseReleaseResult
                {
                    ReleasedFeature = feature,
                    ReleasedUser = user
                };

                // Check for successful removal
                if (ParsingPatterns.RemoveSuccessPattern.IsMatch(output))
                {
                    result.Success = true;
                    _logger.LogInformation("Successfully parsed lmremove output: License released for user {User} on feature {Feature}", user, feature);
                }
                else if (ParsingPatterns.ContainsErrorPatterns(output))
                {
                    var errors = ParsingPatterns.ExtractErrorMessages(output);
                    result.Success = false;
                    result.ErrorMessage = string.Join(", ", errors);

                    _logger.LogWarning("lmremove command encountered errors: {Errors}", result.ErrorMessage);
                }
                else
                {
                    // If no clear success or error pattern, check exit codes or other indicators
                    result.Success = !output.Contains("Failed", StringComparison.OrdinalIgnoreCase) &&
                                   !output.Contains("Error", StringComparison.OrdinalIgnoreCase);

                    if (!result.Success)
                    {
                        result.ErrorMessage = "Unable to determine operation success from output";
                    }
                }

                return result;
            }
            catch (OutputParsingException)
            {
                throw; // Re-throw our custom exceptions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse lmremove output for feature: {Feature}, user: {User}", feature, user);
                throw new OutputParsingException(
                    $"Failed to parse lmremove output: {ex.Message}",
                    output,
                    "lmremove",
                    ex);
            }
        }

        /// <summary>
        /// Parses output incrementally for large outputs
        /// </summary>
        /// <param name="outputLines">Enumerable of output lines</param>
        /// <param name="command">The command that generated the output</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Parsed result</returns>
        public async Task<object> ParseOutputIncrementallyAsync(IEnumerable<string> outputLines, string command, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Starting incremental parsing for command: {Command}", command);

                var lineCount = 0;
                var outputBuilder = new StringBuilder();

                foreach (var line in outputLines)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    lineCount++;
                    outputBuilder.AppendLine(line);

                    // Prevent memory issues with extremely large outputs
                    if (lineCount > _maxOutputLines)
                    {
                        _logger.LogWarning("Output truncated after {MaxLines} lines for command: {Command}", _maxOutputLines, command);
                        break;
                    }
                }

                var fullOutput = outputBuilder.ToString();

                // Route to appropriate parser based on command
                return command.ToLowerInvariant() switch
                {
                    "lmstat" => ParseLmstatOutput(fullOutput, string.Empty),
                    "lmremove" => ParseLmremoveOutput(fullOutput, string.Empty, string.Empty),
                    _ => throw new OutputParsingException(
                        $"Unsupported command for incremental parsing: {command}",
                        fullOutput,
                        command)
                };
            }
            catch (Exception ex) when (!(ex is OutputParsingException))
            {
                _logger.LogError(ex, "Failed during incremental parsing for command: {Command}", command);
                throw new OutputParsingException(
                    $"Failed during incremental parsing: {ex.Message}",
                    null,
                    command,
                    ex);
            }
        }

        /// <summary>
        /// Validates parsed data consistency
        /// </summary>
        /// <param name="status">The license server status to validate</param>
        /// <returns>Validation result</returns>
        public bool ValidateParsedData(LicenseServerStatus status)
        {
            try
            {
                if (status == null)
                {
                    _logger.LogError("Validation failed: Status object is null");
                    return false;
                }

                // Validate server status
                if (string.IsNullOrWhiteSpace(status.ServerAddress))
                {
                    _logger.LogWarning("Validation warning: Server address is empty");
                }

                // Validate feature data
                foreach (var feature in status.Features.Values)
                {
                    if (string.IsNullOrWhiteSpace(feature.FeatureName))
                    {
                        _logger.LogWarning("Validation warning: Feature name is empty");
                        continue;
                    }

                    if (feature.TotalLicenses < 0)
                    {
                        _logger.LogWarning("Validation warning: Feature {Feature} has negative total licenses", feature.FeatureName);
                        return false;
                    }

                    if (feature.LicensesInUse < 0)
                    {
                        _logger.LogWarning("Validation warning: Feature {Feature} has negative licenses in use", feature.FeatureName);
                        return false;
                    }

                    if (feature.LicensesInUse > feature.TotalLicenses)
                    {
                        _logger.LogWarning("Validation warning: Feature {Feature} has more licenses in use ({InUse}) than total ({Total})",
                            feature.FeatureName, feature.LicensesInUse, feature.TotalLicenses);
                        return false;
                    }

                    // Validate user data
                    foreach (var userUsage in feature.Users.Values)
                    {
                        if (string.IsNullOrWhiteSpace(userUsage.UserName))
                        {
                            _logger.LogWarning("Validation warning: Feature {Feature} has user with empty username", feature.FeatureName);
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(userUsage.HostName))
                        {
                            _logger.LogWarning("Validation warning: Feature {Feature} user {User} has empty hostname", feature.FeatureName, userUsage.UserName);
                        }
                    }
                }

                _logger.LogDebug("Validation completed successfully for server: {ServerAddress}", status.ServerAddress);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Validation failed for server: {ServerAddress}", status?.ServerAddress);
                return false;
            }
        }

        private void ParseServerStatus(string output, LicenseServerStatus status)
        {
            // Check server status
            var serverMatch = ParsingPatterns.ServerStatusPattern.Match(output);
            if (serverMatch.Success)
            {
                var serverStatus = serverMatch.Groups[1].Value.ToUpper();
                status.IsServerUp = serverStatus.Contains("UP");
                status.StatusMessage = $"License server is {serverStatus}";
            }

            // Extract server address from output if not provided
            if (string.IsNullOrWhiteSpace(status.ServerAddress))
            {
                var addressMatch = ParsingPatterns.ServerAddressPattern.Match(output);
                if (addressMatch.Success)
                {
                    status.ServerAddress = addressMatch.Groups[1].Value.Trim();
                }
            }
        }

        private void ParseFeatureInformation(string output, LicenseServerStatus status)
        {
            var lines = output.Split('\n');
            LicenseFeatureInfo currentFeature = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Check for feature header
                var featureMatch = ParsingPatterns.FeatureHeaderPattern.Match(line);
                if (featureMatch.Success)
                {
                    currentFeature = new LicenseFeatureInfo
                    {
                        FeatureName = featureMatch.Groups[1].Value.Trim(),
                        Users = new List<LicenseUserInfo>()
                    };

                    // Parse feature status from the same or next few lines
                    ParseFeatureStatus(lines, ref i, currentFeature);

                    status.Features[currentFeature.FeatureName] = new LicenseFeatureStatus
                    {
                        FeatureName = currentFeature.FeatureName,
                        TotalLicenses = currentFeature.TotalLicenses,
                        LicensesInUse = currentFeature.LicensesInUse,
                        AvailableLicenses = currentFeature.AvailableLicenses,
                        Status = currentFeature.Status,
                        Description = currentFeature.Description,
                        Version = currentFeature.Version,
                        ExpirationDate = currentFeature.ExpirationDate
                    };
                    continue;
                }

                // Parse user information if we're in a feature section
                if (currentFeature != null)
                {
                    var userMatch = ParsingPatterns.UserLicensePattern.Match(line);
                    if (userMatch.Success)
                    {
                        var userInfo = ParseUserInfo(userMatch);
                        currentFeature.Users.Add(userInfo);
                    }
                }
            }
        }

        private void ParseFeatureStatus(string[] lines, ref int currentIndex, LicenseFeatureInfo feature)
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
                        feature.LicensesInUse = inUse;
                        currentIndex += lookAhead; // Skip ahead
                        return;
                    }
                }

                var queueMatch = ParsingPatterns.FeatureQueuePattern.Match(line);
                if (queueMatch.Success)
                {
                    if (int.TryParse(queueMatch.Groups[2].Value, out var reserved))
                    {
                        feature.ReservedLicenses = reserved;
                    }
                }
            }
        }

        private LicenseUserInfo ParseUserInfo(Match userMatch)
        {
            var username = userMatch.Groups[1].Value.Trim();
            var hostname = userMatch.Groups[2].Value.Trim();
            var displayName = username;
            var checkoutTime = (DateTime?)null;
            var processId = (int?)null;

            // Extract display name if available
            var displayNameMatch = Regex.Match(userMatch.Value, @"\(([^)]+)\)");
            if (displayNameMatch.Success)
            {
                displayName = displayNameMatch.Groups[1].Value;
            }

            // Extract checkout time if available
            var timeMatch = Regex.Match(userMatch.Value, @"start\s+([^)]+)", RegexOptions.IgnoreCase);
            if (timeMatch.Success)
            {
                if (DateTime.TryParse(timeMatch.Groups[1].Value, out var parsedCheckoutTime))
                {
                    checkoutTime = parsedCheckoutTime;
                }
            }

            // Try to extract process ID if available
            var processMatch = Regex.Match(userMatch.Value, @"pid\s*(\d+)", RegexOptions.IgnoreCase);
            if (processMatch.Success)
            {
                if (int.TryParse(processMatch.Groups[1].Value, out var parsedProcessId))
                {
                    processId = parsedProcessId;
                }
            }

            return new LicenseUserInfo(username, hostname, displayName, checkoutTime: checkoutTime, processId: processId);
        }
    }
}