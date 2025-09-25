using System;
using System.ComponentModel;

namespace LicenseReleaseService.Process
{
    /// <summary>
    /// Configuration options for process execution behavior
    /// </summary>
    public class ProcessExecutionOptions
    {
        private TimeSpan _timeout = TimeSpan.FromSeconds(30);
        private TimeSpan _retryDelay = TimeSpan.FromSeconds(5);
        private int _maxRetries = 3;
        private bool _redirectStandardInput = false;
        private bool _createNoWindow = true;
        private bool _useShellExecute = false;

        /// <summary>
        /// Gets or sets the default timeout for process execution
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:00:30")]
        public TimeSpan Timeout
        {
            get => _timeout;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentException("Timeout must be greater than zero", nameof(value));
                }
                _timeout = value;
            }
        }

        /// <summary>
        /// Gets or sets the delay between retry attempts
        /// </summary>
        [DefaultValue(typeof(TimeSpan), "00:00:05")]
        public TimeSpan RetryDelay
        {
            get => _retryDelay;
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw new ArgumentException("Retry delay cannot be negative", nameof(value));
                }
                _retryDelay = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of retry attempts
        /// </summary>
        [DefaultValue(3)]
        public int MaxRetries
        {
            get => _maxRetries;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentException("Max retries cannot be negative", nameof(value));
                }
                _maxRetries = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to redirect standard input
        /// </summary>
        [DefaultValue(false)]
        public bool RedirectStandardInput
        {
            get => _redirectStandardInput;
            set => _redirectStandardInput = value;
        }

        /// <summary>
        /// Gets or sets whether to create a new window for the process
        /// </summary>
        [DefaultValue(true)]
        public bool CreateNoWindow
        {
            get => _createNoWindow;
            set => _createNoWindow = value;
        }

        /// <summary>
        /// Gets or sets whether to use the shell to execute the process
        /// </summary>
        [DefaultValue(false)]
        public bool UseShellExecute
        {
            get => _useShellExecute;
            set => _useShellExecute = value;
        }

        /// <summary>
        /// Gets or sets the working directory for the process
        /// </summary>
        public string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the environment variables for the process
        /// </summary>
        public System.Collections.Generic.IDictionary<string, string> EnvironmentVariables { get; set; }
            = new System.Collections.Generic.Dictionary<string, string>();

        /// <summary>
        /// Gets or sets whether to kill the process tree on timeout
        /// </summary>
        [DefaultValue(true)]
        public bool KillProcessTreeOnTimeout { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable detailed logging
        /// </summary>
        [DefaultValue(true)]
        public bool EnableDetailedLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets the buffer size for standard output and error
        /// </summary>
        [DefaultValue(4096)]
        public int BufferSize { get; set; } = 4096;

        /// <summary>
        /// Gets or sets the encoding for standard output and error
        /// </summary>
        public System.Text.Encoding Encoding { get; set; } = System.Text.Encoding.UTF8;

        /// <summary>
        /// Gets or sets whether to throw exceptions on non-zero exit codes
        /// </summary>
        [DefaultValue(false)]
        public bool ThrowOnNonZeroExitCode { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum output size in bytes (0 = unlimited)
        /// </summary>
        [DefaultValue(0)]
        public long MaxOutputSize { get; set; } = 0;

        /// <summary>
        /// Initializes a new instance of the ProcessExecutionOptions class
        /// </summary>
        public ProcessExecutionOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the ProcessExecutionOptions class with specified timeout
        /// </summary>
        /// <param name="timeout">Execution timeout</param>
        public ProcessExecutionOptions(TimeSpan timeout) : this()
        {
            Timeout = timeout;
        }

        /// <summary>
        /// Initializes a new instance of the ProcessExecutionOptions class with specified timeout and retry settings
        /// </summary>
        /// <param name="timeout">Execution timeout</param>
        /// <param name="maxRetries">Maximum retry attempts</param>
        /// <param name="retryDelay">Delay between retries</param>
        public ProcessExecutionOptions(TimeSpan timeout, int maxRetries, TimeSpan retryDelay) : this(timeout)
        {
            MaxRetries = maxRetries;
            RetryDelay = retryDelay;
        }

        /// <summary>
        /// Creates a copy of the current options
        /// </summary>
        /// <returns>Copy of the options</returns>
        public ProcessExecutionOptions Clone()
        {
            var clone = new ProcessExecutionOptions
            {
                Timeout = Timeout,
                RetryDelay = RetryDelay,
                MaxRetries = MaxRetries,
                RedirectStandardInput = RedirectStandardInput,
                CreateNoWindow = CreateNoWindow,
                UseShellExecute = UseShellExecute,
                WorkingDirectory = WorkingDirectory,
                KillProcessTreeOnTimeout = KillProcessTreeOnTimeout,
                EnableDetailedLogging = EnableDetailedLogging,
                BufferSize = BufferSize,
                Encoding = Encoding,
                ThrowOnNonZeroExitCode = ThrowOnNonZeroExitCode,
                MaxOutputSize = MaxOutputSize
            };

            // Copy environment variables
            foreach (var kvp in EnvironmentVariables)
            {
                clone.EnvironmentVariables[kvp.Key] = kvp.Value;
            }

            return clone;
        }

        /// <summary>
        /// Validates the current configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public System.Collections.Generic.List<string> Validate()
        {
            var errors = new System.Collections.Generic.List<string>();

            if (Timeout <= TimeSpan.Zero)
            {
                errors.Add("Timeout must be greater than zero");
            }

            if (RetryDelay < TimeSpan.Zero)
            {
                errors.Add("Retry delay cannot be negative");
            }

            if (MaxRetries < 0)
            {
                errors.Add("Max retries cannot be negative");
            }

            if (BufferSize <= 0)
            {
                errors.Add("Buffer size must be greater than zero");
            }

            if (MaxOutputSize < 0)
            {
                errors.Add("Max output size cannot be negative");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the process execution options
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Process Execution Options:");
            builder.AppendLine($"  Timeout: {Timeout.TotalSeconds:F1}s");
            builder.AppendLine($"  Max Retries: {MaxRetries}");
            builder.AppendLine($"  Retry Delay: {RetryDelay.TotalSeconds:F1}s");
            builder.AppendLine($"  Create No Window: {CreateNoWindow}");
            builder.AppendLine($"  Use Shell Execute: {UseShellExecute}");
            builder.AppendLine($"  Redirect Standard Input: {RedirectStandardInput}");
            builder.AppendLine($"  Kill Process Tree On Timeout: {KillProcessTreeOnTimeout}");
            builder.AppendLine($"  Enable Detailed Logging: {EnableDetailedLogging}");
            builder.AppendLine($"  Buffer Size: {BufferSize}");
            builder.AppendLine($"  Encoding: {Encoding.EncodingName}");
            builder.AppendLine($"  Throw On Non-Zero Exit Code: {ThrowOnNonZeroExitCode}");
            builder.AppendLine($"  Max Output Size: {MaxOutputSize}");

            if (!string.IsNullOrWhiteSpace(WorkingDirectory))
            {
                builder.AppendLine($"  Working Directory: {WorkingDirectory}");
            }

            if (EnvironmentVariables.Count > 0)
            {
                builder.AppendLine($"  Environment Variables: {EnvironmentVariables.Count} variables");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Gets default options for license manager operations
        /// </summary>
        /// <returns>Default options</returns>
        public static ProcessExecutionOptions DefaultLicenseManagerOptions()
        {
            return new ProcessExecutionOptions
            {
                Timeout = TimeSpan.FromSeconds(60),
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(2),
                CreateNoWindow = true,
                UseShellExecute = false,
                EnableDetailedLogging = true,
                ThrowOnNonZeroExitCode = false
            };
        }

        /// <summary>
        /// Gets options for quick operations with short timeout
        /// </summary>
        /// <returns>Quick operation options</returns>
        public static ProcessExecutionOptions QuickOperationOptions()
        {
            return new ProcessExecutionOptions
            {
                Timeout = TimeSpan.FromSeconds(10),
                MaxRetries = 1,
                RetryDelay = TimeSpan.FromSeconds(1),
                CreateNoWindow = true,
                UseShellExecute = false,
                EnableDetailedLogging = false
            };
        }

        /// <summary>
        /// Gets options for long-running operations with extended timeout
        /// </summary>
        /// <returns>Long operation options</returns>
        public static ProcessExecutionOptions LongOperationOptions()
        {
            return new ProcessExecutionOptions
            {
                Timeout = TimeSpan.FromMinutes(5),
                MaxRetries = 5,
                RetryDelay = TimeSpan.FromSeconds(10),
                CreateNoWindow = true,
                UseShellExecute = false,
                EnableDetailedLogging = true,
                MaxOutputSize = 10 * 1024 * 1024 // 10MB
            };
        }
    }
}