using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Represents command arguments for lmutil.exe operations
    /// </summary>
    public class CommandArguments
    {
        private readonly Dictionary<string, string> _arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _flags = new List<string>();

        /// <summary>
        /// Gets the license server address
        /// </summary>
        public string Server { get; set; }

        /// <summary>
        /// Gets the license server port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets the license feature name
        /// </summary>
        public string Feature { get; set; }

        /// <summary>
        /// Gets the username for license operations
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets the hostname for license operations
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// Gets the display name for license operations
        /// </summary>
        public string Display { get; set; }

        /// <summary>
        /// Gets the license file path
        /// </summary>
        public string LicensePath { get; set; }

        /// <summary>
        /// Gets the timeout in seconds
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// Gets the verbosity level
        /// </summary>
        public int Verbosity { get; set; }

        /// <summary>
        /// Gets whether to include debug information
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Gets whether to force the operation
        /// </summary>
        public bool Force { get; set; }

        /// <summary>
        /// Gets the output format
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// Gets additional options
        /// </summary>
        public Dictionary<string, string> Options => new Dictionary<string, string>(_arguments);

        /// <summary>
        /// Gets the enabled flags
        /// </summary>
        public IReadOnlyList<string> Flags => _flags.AsReadOnly();

        /// <summary>
        /// Initializes a new instance of the CommandArguments class
        /// </summary>
        public CommandArguments()
        {
            Timeout = 30;
            Verbosity = 0;
            Debug = false;
            Force = false;
        }

        /// <summary>
        /// Adds a named argument
        /// </summary>
        /// <param name="key">Argument key</param>
        /// <param name="value">Argument value</param>
        /// <returns>Current instance for method chaining</returns>
        public CommandArguments AddArgument(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Argument key cannot be null or whitespace", nameof(key));

            _arguments[key] = value;
            return this;
        }

        /// <summary>
        /// Adds a named argument with integer value
        /// </summary>
        /// <param name="key">Argument key</param>
        /// <param name="value">Argument value</param>
        /// <returns>Current instance for method chaining</returns>
        public CommandArguments AddArgument(string key, int value)
        {
            return AddArgument(key, value.ToString());
        }

        /// <summary>
        /// Adds a named argument with boolean value
        /// </summary>
        /// <param name="key">Argument key</param>
        /// <param name="value">Argument value</param>
        /// <returns>Current instance for method chaining</returns>
        public CommandArguments AddArgument(string key, bool value)
        {
            return AddArgument(key, value.ToString().ToLowerInvariant());
        }

        /// <summary>
        /// Adds a flag (switch without value)
        /// </summary>
        /// <param name="flag">Flag name</param>
        /// <returns>Current instance for method chaining</returns>
        public CommandArguments AddFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag))
                throw new ArgumentException("Flag cannot be null or whitespace", nameof(flag));

            if (!_flags.Contains(flag))
            {
                _flags.Add(flag);
            }
            return this;
        }

        /// <summary>
        /// Removes an argument
        /// </summary>
        /// <param name="key">Argument key</param>
        /// <returns>Current instance for method chaining</returns>
        public CommandArguments RemoveArgument(string key)
        {
            _arguments.Remove(key);
            return this;
        }

        /// <summary>
        /// Removes a flag
        /// </summary>
        /// <param name="flag">Flag name</param>
        /// <returns>Current instance for method chaining</returns>
        public CommandArguments RemoveFlag(string flag)
        {
            _flags.Remove(flag);
            return this;
        }

        /// <summary>
        /// Checks if an argument exists
        /// </summary>
        /// <param name="key">Argument key</param>
        /// <returns>True if the argument exists</returns>
        public bool HasArgument(string key)
        {
            return _arguments.ContainsKey(key);
        }

        /// <summary>
        /// Checks if a flag exists
        /// </summary>
        /// <param name="flag">Flag name</param>
        /// <returns>True if the flag exists</returns>
        public bool HasFlag(string flag)
        {
            return _flags.Contains(flag);
        }

        /// <summary>
        /// Gets an argument value
        /// </summary>
        /// <param name="key">Argument key</param>
        /// <param name="defaultValue">Default value if not found</param>
        /// <returns>Argument value or default</returns>
        public string GetArgument(string key, string defaultValue = null)
        {
            return _arguments.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Gets an argument value as integer
        /// </summary>
        /// <param name="key">Argument key</param>
        /// <param name="defaultValue">Default value if not found</param>
        /// <returns>Argument value as integer or default</returns>
        public int GetArgument(string key, int defaultValue)
        {
            if (_arguments.TryGetValue(key, out var value) && int.TryParse(value, out var result))
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// Gets an argument value as boolean
        /// </summary>
        /// <param name="key">Argument key</param>
        /// <param name="defaultValue">Default value if not found</param>
        /// <returns>Argument value as boolean or default</returns>
        public bool GetArgument(string key, bool defaultValue)
        {
            if (_arguments.TryGetValue(key, out var value) && bool.TryParse(value, out var result))
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// Sets the server connection parameters
        /// </summary>
        /// <param name="server">Server address</param>
        /// <param name="port">Server port</param>
        /// <returns>Current instance for method chaining</returns>
        public CommandArguments SetServer(string server, int port)
        {
            Server = server;
            Port = port;
            return this;
        }

        /// <summary>
        /// Sets the license feature
        /// </summary>
        /// <param name="feature">Feature name</param>
        /// <returns>Current instance for method chaining</returns>
        public CommandArguments SetFeature(string feature)
        {
            Feature = feature;
            return this;
        }

        /// <summary>
        /// Sets the user information
        /// </summary>
        /// <param name="username">Username</param>
        /// <param name="hostname">Hostname</param>
        /// <param name="display">Display name</param>
        /// <returns>Current instance for method chaining</returns>
        public CommandArguments SetUser(string username, string hostname = null, string display = null)
        {
            Username = username;
            Hostname = hostname;
            Display = display;
            return this;
        }

        /// <summary>
        /// Sets the license file path
        /// </summary>
        /// <param name="licensePath">License file path</param>
        /// <returns>Current instance for method chaining</returns>
        public CommandArguments SetLicensePath(string licensePath)
        {
            LicensePath = licensePath;
            return this;
        }

        /// <summary>
        /// Sets the timeout
        /// </summary>
        /// <param name="timeout">Timeout in seconds</param>
        /// <returns>Current instance for method chaining</returns>
        public CommandArguments SetTimeout(int timeout)
        {
            Timeout = timeout;
            return this;
        }

        /// <summary>
        /// Enables debug mode
        /// </summary>
        /// <returns>Current instance for method chaining</returns>
        public CommandArguments EnableDebug()
        {
            Debug = true;
            return this;
        }

        /// <summary>
        /// Enables force mode
        /// </summary>
        /// <returns>Current instance for method chaining</returns>
        public CommandArguments EnableForce()
        {
            Force = true;
            return this;
        }

        /// <summary>
        /// Sets the verbosity level
        /// </summary>
        /// <param name="level">Verbosity level</param>
        /// <returns>Current instance for method chaining</returns>
        public CommandArguments SetVerbosity(int level)
        {
            Verbosity = level;
            return this;
        }

        /// <summary>
        /// Sets the output format
        /// </summary>
        /// <param name="format">Output format</param>
        /// <returns>Current instance for method chaining</returns>
        public CommandArguments SetFormat(string format)
        {
            Format = format;
            return this;
        }

        /// <summary>
        /// Builds the command line arguments string
        /// </summary>
        /// <returns>Command line arguments string</returns>
        public string BuildArguments()
        {
            var sb = new StringBuilder();

            // Add server specification if provided
            if (!string.IsNullOrWhiteSpace(Server) && Port > 0)
            {
                sb.Append($"-c {Port}@{EscapeArgument(Server)} ");
            }
            else if (!string.IsNullOrWhiteSpace(LicensePath))
            {
                sb.Append($"-c {EscapeArgument(LicensePath)} ");
            }

            // Add flags
            foreach (var flag in _flags)
            {
                sb.Append($"-{flag} ");
            }

            // Add named arguments
            foreach (var kvp in _arguments)
            {
                sb.Append($"-{kvp.Key} {EscapeArgument(kvp.Value)} ");
            }

            // Add positional arguments based on context
            if (!string.IsNullOrWhiteSpace(Feature))
            {
                sb.Append($"{EscapeArgument(Feature)} ");
            }

            if (!string.IsNullOrWhiteSpace(Username))
            {
                sb.Append($"{EscapeArgument(Username)} ");
            }

            if (!string.IsNullOrWhiteSpace(Hostname))
            {
                sb.Append($"{EscapeArgument(Hostname)} ");
            }

            if (!string.IsNullOrWhiteSpace(Display))
            {
                sb.Append($"{EscapeArgument(Display)} ");
            }

            // Add global options
            if (Timeout > 0)
            {
                sb.Append($"-timeout {Timeout} ");
            }

            if (Verbosity > 0)
            {
                sb.Append($"-{'v'.ToString().PadLeft(Verbosity, 'v')} ");
            }

            if (Debug)
            {
                sb.Append("-debug ");
            }

            if (Force)
            {
                sb.Append("-force ");
            }

            if (!string.IsNullOrWhiteSpace(Format))
            {
                sb.Append($"-format {EscapeArgument(Format)} ");
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Escapes command line arguments
        /// </summary>
        /// <param name="argument">Argument to escape</param>
        /// <returns>Escaped argument</returns>
        private string EscapeArgument(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
                return string.Empty;

            // If the argument contains spaces or special characters, wrap in quotes
            if (argument.Contains(" ") || argument.Contains("\"") || argument.Contains("'"))
            {
                return $"\"{argument.Replace("\"", "\"\"")}\"";
            }

            return argument;
        }

        /// <summary>
        /// Validates the command arguments
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            // Validate server configuration
            if (!string.IsNullOrWhiteSpace(Server))
            {
                if (Port <= 0 || Port > 65535)
                {
                    errors.Add("Port must be between 1 and 65535");
                }

                if (string.IsNullOrWhiteSpace(Server))
                {
                    errors.Add("Server address cannot be empty when port is specified");
                }
            }

            // Validate timeout
            if (Timeout < 0)
            {
                errors.Add("Timeout must be non-negative");
            }

            // Validate verbosity level
            if (Verbosity < 0)
            {
                errors.Add("Verbosity level must be non-negative");
            }

            // Validate required arguments for specific operations
            if (_flags.Contains("f") && string.IsNullOrWhiteSpace(Feature))
            {
                errors.Add("Feature name is required when using feature flag");
            }

            if (_flags.Contains("u") && string.IsNullOrWhiteSpace(Username))
            {
                errors.Add("Username is required when using user flag");
            }

            return errors;
        }

        /// <summary>
        /// Creates a copy of the current command arguments
        /// </summary>
        /// <returns>New copy of command arguments</returns>
        public CommandArguments Copy()
        {
            var copy = new CommandArguments
            {
                Server = Server,
                Port = Port,
                Feature = Feature,
                Username = Username,
                Hostname = Hostname,
                Display = Display,
                LicensePath = LicensePath,
                Timeout = Timeout,
                Verbosity = Verbosity,
                Debug = Debug,
                Force = Force,
                Format = Format
            };

            foreach (var kvp in _arguments)
            {
                copy._arguments[kvp.Key] = kvp.Value;
            }

            copy._flags.AddRange(_flags);

            return copy;
        }

        /// <summary>
        /// Returns a string representation of the command arguments
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("CommandArguments:");

            if (!string.IsNullOrWhiteSpace(Server))
            {
                sb.AppendLine($"  Server: {Server}:{Port}");
            }

            if (!string.IsNullOrWhiteSpace(LicensePath))
            {
                sb.AppendLine($"  LicensePath: {LicensePath}");
            }

            if (!string.IsNullOrWhiteSpace(Feature))
            {
                sb.AppendLine($"  Feature: {Feature}");
            }

            if (!string.IsNullOrWhiteSpace(Username))
            {
                sb.AppendLine($"  User: {Username}@{Hostname} ({Display})");
            }

            sb.AppendLine($"  Timeout: {Timeout}s");
            sb.AppendLine($"  Verbosity: {Verbosity}");
            sb.AppendLine($"  Debug: {Debug}");
            sb.AppendLine($"  Force: {Force}");

            if (_flags.Count > 0)
            {
                sb.AppendLine($"  Flags: {string.Join(", ", _flags)}");
            }

            if (_arguments.Count > 0)
            {
                sb.AppendLine($"  Arguments: {string.Join(", ", _arguments.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            }

            return sb.ToString();
        }
    }
}