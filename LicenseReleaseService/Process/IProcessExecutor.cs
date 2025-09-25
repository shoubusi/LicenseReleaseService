using System;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.Process
{
    /// <summary>
    /// Defines the contract for executing external processes with comprehensive error handling and monitoring
    /// </summary>
    public interface IProcessExecutor
    {
        /// <summary>
        /// Executes a process asynchronously with default timeout
        /// </summary>
        /// <param name="filePath">Path to the executable file</param>
        /// <param name="arguments">Command line arguments</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Process execution result</returns>
        Task<ProcessExecutionResult> ExecuteAsync(string filePath, string arguments,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a process asynchronously with custom timeout
        /// </summary>
        /// <param name="filePath">Path to the executable file</param>
        /// <param name="arguments">Command line arguments</param>
        /// <param name="timeout">Execution timeout</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Process execution result</returns>
        Task<ProcessExecutionResult> ExecuteAsync(string filePath, string arguments,
            TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a process synchronously with default timeout
        /// </summary>
        /// <param name="filePath">Path to the executable file</param>
        /// <param name="arguments">Command line arguments</param>
        /// <returns>Process execution result</returns>
        ProcessExecutionResult Execute(string filePath, string arguments);

        /// <summary>
        /// Executes a process synchronously with custom timeout
        /// </summary>
        /// <param name="filePath">Path to the executable file</param>
        /// <param name="arguments">Command line arguments</param>
        /// <param name="timeout">Execution timeout</param>
        /// <returns>Process execution result</returns>
        ProcessExecutionResult Execute(string filePath, string arguments, TimeSpan timeout);
    }
}