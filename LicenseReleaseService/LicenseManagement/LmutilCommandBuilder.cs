using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Builds lmutil.exe command lines for various license operations
    /// </summary>
    public class LmutilCommandBuilder
    {
        private readonly ILogger<LmutilCommandBuilder> _logger;
        private readonly List<string> _serverHistory = new List<string>();
        private readonly Dictionary<string, string> _serverAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the LmutilCommandBuilder class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public LmutilCommandBuilder(ILogger<LmutilCommandBuilder> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Adds a server alias for easier reference
        /// </summary>
        /// <param name="alias">Server alias</param>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        public void AddServerAlias(string alias, string server, int port)
        {
            if (string.IsNullOrWhiteSpace(alias))
                throw new ArgumentException("Alias cannot be null or whitespace", nameof(alias));

            if (string.IsNullOrWhiteSpace(server))
                throw new ArgumentException("Server address cannot be null or whitespace", nameof(server));

            if (port <= 0 || port > 65535)
                throw new ArgumentException("Port must be between 1 and 65535", nameof(port));

            _serverAliases[alias] = $"{port}@{server}";
            _logger.LogDebug("Added server alias: {alias} -> {server}:{port}", alias, server, port);
        }

        /// <summary>
        /// Removes a server alias
        /// </summary>
        /// <param name="alias">Server alias to remove</param>
        public void RemoveServerAlias(string alias)
        {
            if (_serverAliases.Remove(alias))
            {
                _logger.LogDebug("Removed server alias: {alias}", alias);
            }
        }

        /// <summary>
        /// Gets all server aliases
        /// </summary>
        /// <returns>Dictionary of server aliases</returns>
        public IReadOnlyDictionary<string, string> GetServerAliases()
        {
            return new Dictionary<string, string>(_serverAliases);
        }

        /// <summary>
        /// Builds a command for checking license server status
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="verbose">Whether to include verbose output</param>
        /// <returns>Command arguments string</returns>
        public string BuildStatusCommand(string server, int port, bool verbose = false)
        {
            var arguments = new CommandArguments()
                .SetServer(server, port);

            if (verbose)
            {
                arguments.AddFlag("v");
            }

            return BuildCommand(LmutilCommands.Lmstat, arguments);
        }

        /// <summary>
        /// Builds a command for checking specific feature status
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="feature">Feature name</param>
        /// <param name="verbose">Whether to include verbose output</param>
        /// <returns>Command arguments string</returns>
        public string BuildFeatureStatusCommand(string server, int port, string feature, bool verbose = false)
        {
            if (string.IsNullOrWhiteSpace(feature))
                throw new ArgumentException("Feature name cannot be null or whitespace", nameof(feature));

            var arguments = new CommandArguments()
                .SetServer(server, port)
                .SetFeature(feature);

            if (verbose)
            {
                arguments.AddFlag("v");
            }

            return BuildCommand(LmutilCommands.LmstatFeature, arguments);
        }

        /// <summary>
        /// Builds a command for checking user-specific license status
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="username">Username</param>
        /// <param name="verbose">Whether to include verbose output</param>
        /// <returns>Command arguments string</returns>
        public string BuildUserStatusCommand(string server, int port, string username, bool verbose = false)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or whitespace", nameof(username));

            var arguments = new CommandArguments()
                .SetServer(server, port)
                .SetUser(username);

            if (verbose)
            {
                arguments.AddFlag("v");
            }

            return BuildCommand(LmutilCommands.LmstatUser, arguments);
        }

        /// <summary>
        /// Builds a command for removing a license from a user
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="feature">Feature name</param>
        /// <param name="username">Username</param>
        /// <param name="hostname">Hostname (optional)</param>
        /// <param name="display">Display name (optional)</param>
        /// <param name="force">Whether to force removal</param>
        /// <returns>Command arguments string</returns>
        public string BuildRemoveCommand(string server, int port, string feature, string username,
            string hostname = null, string display = null, bool force = false)
        {
            if (string.IsNullOrWhiteSpace(feature))
                throw new ArgumentException("Feature name cannot be null or whitespace", nameof(feature));

            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be null or whitespace", nameof(username));

            var arguments = new CommandArguments()
                .SetServer(server, port)
                .SetFeature(feature)
                .SetUser(username, hostname, display);

            if (force)
            {
                arguments.EnableForce();
            }

            return BuildCommand(LmutilCommands.Lmremove, arguments);
        }

        /// <summary>
        /// Builds a command for checking license file checksum
        /// </summary>
        /// <param name="licensePath">License file path</param>
        /// <param name="verbose">Whether to include verbose output</param>
        /// <returns>Command arguments string</returns>
        public string BuildChecksumCommand(string licensePath, bool verbose = false)
        {
            if (string.IsNullOrWhiteSpace(licensePath))
                throw new ArgumentException("License path cannot be null or whitespace", nameof(licensePath));

            var arguments = new CommandArguments()
                .SetLicensePath(licensePath);

            if (verbose)
            {
                arguments.AddFlag("v");
            }

            return BuildCommand(LmutilCommands.Lmcksum, arguments);
        }

        /// <summary>
        /// Builds a command for checking server status with long format
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="includeUsage">Whether to include usage information</param>
        /// <returns>Command arguments string</returns>
        public string BuildLongStatusCommand(string server, int port, bool includeUsage = false)
        {
            var arguments = new CommandArguments()
                .SetServer(server, port);

            if (includeUsage)
            {
                arguments.AddFlag("a");
            }

            return BuildCommand(LmutilCommands.LmstatLong, arguments);
        }

        /// <summary>
        /// Builds a command for checking server daemon status
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <returns>Command arguments string</returns>
        public string BuildDaemonStatusCommand(string server, int port)
        {
            var arguments = new CommandArguments()
                .SetServer(server, port);

            return BuildCommand(LmutilCommands.LmstatDaemon, arguments);
        }

        /// <summary>
        /// Builds a command for checking server vendor status
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <returns>Command arguments string</returns>
        public string BuildVendorStatusCommand(string server, int port)
        {
            var arguments = new CommandArguments()
                .SetServer(server, port);

            return BuildCommand(LmutilCommands.LmstatVendor, arguments);
        }

        /// <summary>
        /// Builds a command for checking server version
        /// </summary>
        /// <returns>Command arguments string</returns>
        public string BuildVersionCommand()
        {
            var arguments = new CommandArguments();
            return BuildCommand(LmutilCommands.LmstatVersion, arguments);
        }

        /// <summary>
        /// Builds a command for getting help
        /// </summary>
        /// <returns>Command arguments string</returns>
        public string BuildHelpCommand()
        {
            var arguments = new CommandArguments();
            return BuildCommand(LmutilCommands.LmstatHelp, arguments);
        }

        /// <summary>
        /// Builds a custom command with specific arguments
        /// </summary>
        /// <param name="command">The lmutil command</param>
        /// <param name="arguments">Command arguments</param>
        /// <returns>Command arguments string</returns>
        public string BuildCommand(LmutilCommands command, CommandArguments arguments)
        {
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            // Validate arguments before building
            var validationErrors = arguments.Validate();
            if (validationErrors.Count > 0)
            {
                var errorMessage = $"Command validation failed for {command}: {string.Join(", ", validationErrors)}";
                _logger.LogError(errorMessage);
                throw new ArgumentException(errorMessage, nameof(arguments));
            }

            // Get the base command
            var baseCommand = command.GetCommandLineArgument();

            // Build the full command line
            var fullCommand = $"{baseCommand} {arguments.BuildArguments()}".Trim();

            // Log the command (without sensitive information)
            LogCommand(command, fullCommand);

            // Add to server history for tracking
            AddToServerHistory(arguments);

            return fullCommand;
        }

        /// <summary>
        /// Builds a command using server alias
        /// </summary>
        /// <param name="command">The lmutil command</param>
        /// <param name="serverAlias">Server alias</param>
        /// <param name="additionalArguments">Additional command arguments</param>
        /// <returns>Command arguments string</returns>
        public string BuildCommandWithAlias(LmutilCommands command, string serverAlias, CommandArguments additionalArguments = null)
        {
            if (string.IsNullOrWhiteSpace(serverAlias))
                throw new ArgumentException("Server alias cannot be null or whitespace", nameof(serverAlias));

            if (!_serverAliases.TryGetValue(serverAlias, out var serverSpec))
            {
                throw new KeyNotFoundException($"Server alias '{serverAlias}' not found");
            }

            var arguments = additionalArguments ?? new CommandArguments();

            // Parse server specification (format: port@server)
            var parts = serverSpec.Split('@');
            if (parts.Length != 2)
            {
                throw new FormatException($"Invalid server specification format: {serverSpec}");
            }

            if (!int.TryParse(parts[0], out var port))
            {
                throw new FormatException($"Invalid port number: {parts[0]}");
            }

            var server = parts[1];
            arguments.SetServer(server, port);

            return BuildCommand(command, arguments);
        }

        /// <summary>
        /// Builds multiple commands for different servers
        /// </summary>
        /// <param name="command">The lmutil command</param>
        /// <param name="servers">List of server configurations</param>
        /// <returns>List of command arguments strings</returns>
        public List<string> BuildMultiServerCommands(LmutilCommands command, List<ServerConfiguration> servers)
        {
            if (servers == null)
                throw new ArgumentNullException(nameof(servers));

            var commands = new List<string>();

            foreach (var server in servers)
            {
                try
                {
                    var arguments = new CommandArguments()
                        .SetServer(server.Address, server.Port)
                        .SetTimeout(server.Timeout);

                    if (server.Debug)
                    {
                        arguments.EnableDebug();
                    }

                    var commandString = BuildCommand(command, arguments);
                    commands.Add(commandString);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to build command for server {Server}:{Port}",
                        server.Address, server.Port);
                }
            }

            return commands;
        }

        /// <summary>
        /// Validates server connectivity prerequisites
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <returns>Validation result</returns>
        public bool ValidateServerPrerequisites(string server, int port)
        {
            if (string.IsNullOrWhiteSpace(server))
            {
                _logger.LogError("Server address cannot be empty");
                return false;
            }

            if (port <= 0 || port > 65535)
            {
                _logger.LogError("Port {Port} is out of valid range (1-65535)", port);
                return false;
            }

            // Additional validation can be added here
            return true;
        }

        /// <summary>
        /// Gets command execution hints based on the command type
        /// </summary>
        /// <param name="command">The lmutil command</param>
        /// <returns>Execution hints</returns>
        public CommandExecutionHints GetExecutionHints(LmutilCommands command)
        {
            var hints = new CommandExecutionHints();

            switch (command)
            {
                case LmutilCommands.Lmstat:
                case LmutilCommands.LmstatVerbose:
                case LmutilCommands.LmstatLong:
                    hints.ExpectedExecutionTime = TimeSpan.FromSeconds(5);
                    hints.CanBeCached = true;
                    hints.CacheDuration = TimeSpan.FromMinutes(5);
                    hints.IsIdempotent = true;
                    break;

                case LmutilCommands.Lmremove:
                    hints.ExpectedExecutionTime = TimeSpan.FromSeconds(10);
                    hints.CanBeCached = false;
                    hints.RequiresConfirmation = true;
                    hints.HasSideEffects = true;
                    break;

                case LmutilCommands.Lmcksum:
                    hints.ExpectedExecutionTime = TimeSpan.FromSeconds(2);
                    hints.CanBeCached = true;
                    hints.CacheDuration = TimeSpan.FromHours(1);
                    hints.IsIdempotent = true;
                    break;

                case LmutilCommands.LmstatVersion:
                case LmutilCommands.LmstatHelp:
                    hints.ExpectedExecutionTime = TimeSpan.FromSeconds(1);
                    hints.CanBeCached = true;
                    hints.CacheDuration = TimeSpan.FromDays(1);
                    hints.IsIdempotent = true;
                    break;

                default:
                    hints.ExpectedExecutionTime = TimeSpan.FromSeconds(5);
                    hints.CanBeCached = false;
                    hints.IsIdempotent = true;
                    break;
            }

            return hints;
        }

        /// <summary>
        /// Logs the command being built
        /// </summary>
        /// <param name="command">The lmutil command</param>
        /// <param name="fullCommand">Full command string</param>
        private void LogCommand(LmutilCommands command, string fullCommand)
        {
            // Sanitize command for logging (remove sensitive information)
            var sanitizedCommand = SanitizeCommandForLogging(fullCommand);
            _logger.LogDebug("Built lmutil command: {Command} -> {SanitizedCommand}", command, sanitizedCommand);
        }

        /// <summary>
        /// Sanitizes command for logging by removing sensitive information
        /// </summary>
        /// <param name="command">Command to sanitize</param>
        /// <returns>Sanitized command</returns>
        private string SanitizeCommandForLogging(string command)
        {
            // Remove potentially sensitive information like passwords or tokens
            var sanitized = command;

            // Remove any potential password-like parameters
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized,
                @"-password\s+\S+", "-password [REDACTED]");
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized,
                @"-token\s+\S+", "-token [REDACTED]");

            return sanitized;
        }

        /// <summary>
        /// Adds server information to history for tracking
        /// </summary>
        /// <param name="arguments">Command arguments</param>
        private void AddToServerHistory(CommandArguments arguments)
        {
            if (!string.IsNullOrWhiteSpace(arguments.Server))
            {
                var serverInfo = $"{arguments.Server}:{arguments.Port}";
                if (!_serverHistory.Contains(serverInfo))
                {
                    _serverHistory.Add(serverInfo);
                    _logger.LogDebug("Added server to history: {ServerInfo}", serverInfo);
                }
            }
        }
    }

    /// <summary>
    /// Server configuration for multi-server operations
    /// </summary>
    public class ServerConfiguration
    {
        /// <summary>
        /// Gets or sets the server address
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Gets or sets the server port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the timeout in seconds
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// Gets or sets whether debug mode is enabled
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Initializes a new instance of the ServerConfiguration class
        /// </summary>
        public ServerConfiguration()
        {
            Timeout = 30;
            Debug = false;
        }

        /// <summary>
        /// Initializes a new instance of the ServerConfiguration class
        /// </summary>
        /// <param name="address">Server address</param>
        /// <param name="port">Server port</param>
        /// <param name="timeout">Timeout in seconds</param>
        /// <param name="debug">Debug mode</param>
        public ServerConfiguration(string address, int port, int timeout = 30, bool debug = false)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Port = port;
            Timeout = timeout;
            Debug = debug;
        }
    }

    /// <summary>
    /// Hints for command execution
    /// </summary>
    public class CommandExecutionHints
    {
        /// <summary>
        /// Gets or sets the expected execution time
        /// </summary>
        public TimeSpan ExpectedExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets whether the command can be cached
        /// </summary>
        public bool CanBeCached { get; set; }

        /// <summary>
        /// Gets or sets the cache duration
        /// </summary>
        public TimeSpan CacheDuration { get; set; }

        /// <summary>
        /// Gets or sets whether the command is idempotent
        /// </summary>
        public bool IsIdempotent { get; set; }

        /// <summary>
        /// Gets or sets whether the command requires confirmation
        /// </summary>
        public bool RequiresConfirmation { get; set; }

        /// <summary>
        /// Gets or sets whether the command has side effects
        /// </summary>
        public bool HasSideEffects { get; set; }

        /// <summary>
        /// Initializes a new instance of the CommandExecutionHints class
        /// </summary>
        public CommandExecutionHints()
        {
            ExpectedExecutionTime = TimeSpan.FromSeconds(5);
            CanBeCached = false;
            CacheDuration = TimeSpan.Zero;
            IsIdempotent = true;
            RequiresConfirmation = false;
            HasSideEffects = false;
        }
    }
}