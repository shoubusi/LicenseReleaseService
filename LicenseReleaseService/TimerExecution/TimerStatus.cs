using System;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Represents the current status of the timer executor
    /// </summary>
    public enum TimerStatus
    {
        /// <summary>
        /// Timer is created but not started
        /// </summary>
        Created = 0,

        /// <summary>
        /// Timer is starting up
        /// </summary>
        Starting = 1,

        /// <summary>
        /// Timer is running and executing periodically
        /// </summary>
        Running = 2,

        /// <summary>
        /// Timer is paused and not executing
        /// </summary>
        Paused = 3,

        /// <summary>
        /// Timer is currently executing a callback
        /// </summary>
        Executing = 4,

        /// <summary>
        /// Timer is stopping and cleaning up resources
        /// </summary>
        Stopping = 5,

        /// <summary>
        /// Timer is stopped and not running
        /// </summary>
        Stopped = 6,

        /// <summary>
        /// Timer is in error state due to failures
        /// </summary>
        Error = 7,

        /// <summary>
        /// Timer is in circuit breaker state due to too many consecutive errors
        /// </summary>
        CircuitBreaker = 8,

        /// <summary>
        /// Timer is disposed and cannot be used
        /// </summary>
        Disposed = 9,

        /// <summary>
        /// Timer is restarting
        /// </summary>
        Restarting = 10,

        /// <summary>
        /// Timer is waiting for execution to complete
        /// </summary>
        Waiting = 11,

        /// <summary>
        /// Timer is cancelled and stopping execution
        /// </summary>
        Cancelled = 12
    }

    /// <summary>
    /// Extension methods for TimerStatus enum
    /// </summary>
    public static class TimerStatusExtensions
    {
        /// <summary>
        /// Determines if the timer status represents a running state
        /// </summary>
        /// <param name="status">The timer status</param>
        /// <returns>True if the timer is in a running state; otherwise, false</returns>
        public static bool IsRunning(this TimerStatus status)
        {
            return status == TimerStatus.Running ||
                   status == TimerStatus.Executing ||
                   status == TimerStatus.Waiting;
        }

        /// <summary>
        /// Determines if the timer status represents a transitional state
        /// </summary>
        /// <param name="status">The timer status</param>
        /// <returns>True if the timer is in a transitional state; otherwise, false</returns>
        public static bool IsTransitional(this TimerStatus status)
        {
            return status == TimerStatus.Starting ||
                   status == TimerStatus.Stopping ||
                   status == TimerStatus.Restarting ||
                   status == TimerStatus.Waiting;
        }

        /// <summary>
        /// Determines if the timer status represents an error state
        /// </summary>
        /// <param name="status">The timer status</param>
        /// <returns>True if the timer is in an error state; otherwise, false</returns>
        public static bool IsError(this TimerStatus status)
        {
            return status == TimerStatus.Error ||
                   status == TimerStatus.CircuitBreaker ||
                   status == TimerStatus.Cancelled;
        }

        /// <summary>
        /// Determines if the timer status represents a terminal state
        /// </summary>
        /// <param name="status">The timer status</param>
        /// <returns>True if the timer is in a terminal state; otherwise, false</returns>
        public static bool IsTerminal(this TimerStatus status)
        {
            return status == TimerStatus.Stopped ||
                   status == TimerStatus.Disposed;
        }

        /// <summary>
        /// Determines if the timer can be started from the current status
        /// </summary>
        /// <param name="status">The timer status</param>
        /// <returns>True if the timer can be started; otherwise, false</returns>
        public static bool CanStart(this TimerStatus status)
        {
            return status == TimerStatus.Created ||
                   status == TimerStatus.Stopped ||
                   status == TimerStatus.Error ||
                   status == TimerStatus.Cancelled;
        }

        /// <summary>
        /// Determines if the timer can be stopped from the current status
        /// </summary>
        /// <param name="status">The timer status</param>
        /// <returns>True if the timer can be stopped; otherwise, false</returns>
        public static bool CanStop(this TimerStatus status)
        {
            return status == TimerStatus.Running ||
                   status == TimerStatus.Executing ||
                   status == TimerStatus.Paused ||
                   status == TimerStatus.Waiting;
        }

        /// <summary>
        /// Determines if the timer can be paused from the current status
        /// </summary>
        /// <param name="status">The timer status</param>
        /// <returns>True if the timer can be paused; otherwise, false</returns>
        public static bool CanPause(this TimerStatus status)
        {
            return status == TimerStatus.Running ||
                   status == TimerStatus.Executing;
        }

        /// <summary>
        /// Determines if the timer can be resumed from the current status
        /// </summary>
        /// <param name="status">The timer status</param>
        /// <returns>True if the timer can be resumed; otherwise, false</returns>
        public static bool CanResume(this TimerStatus status)
        {
            return status == TimerStatus.Paused;
        }

        /// <summary>
        /// Determines if the timer can be restarted from the current status
        /// </summary>
        /// <param name="status">The timer status</param>
        /// <returns>True if the timer can be restarted; otherwise, false</returns>
        public static bool CanRestart(this TimerStatus status)
        {
            return status == TimerStatus.Running ||
                   status == TimerStatus.Executing ||
                   status == TimerStatus.Paused ||
                   status == TimerStatus.Error ||
                   status == TimerStatus.CircuitBreaker;
        }

        /// <summary>
        /// Gets a user-friendly description of the timer status
        /// </summary>
        /// <param name="status">The timer status</param>
        /// <returns>User-friendly description</returns>
        public static string GetDescription(this TimerStatus status)
        {
            return status switch
            {
                TimerStatus.Created => "Timer created but not started",
                TimerStatus.Starting => "Timer is starting up",
                TimerStatus.Running => "Timer is running and executing periodically",
                TimerStatus.Paused => "Timer is paused and not executing",
                TimerStatus.Executing => "Timer is currently executing a callback",
                TimerStatus.Stopping => "Timer is stopping and cleaning up resources",
                TimerStatus.Stopped => "Timer is stopped and not running",
                TimerStatus.Error => "Timer is in error state due to failures",
                TimerStatus.CircuitBreaker => "Timer is in circuit breaker state due to too many consecutive errors",
                TimerStatus.Disposed => "Timer is disposed and cannot be used",
                TimerStatus.Restarting => "Timer is restarting",
                TimerStatus.Waiting => "Timer is waiting for execution to complete",
                TimerStatus.Cancelled => "Timer is cancelled and stopping execution",
                _ => "Unknown timer status"
            };
        }

        /// <summary>
        /// Gets the color code for the timer status (for logging and UI purposes)
        /// </summary>
        /// <param name="status">The timer status</param>
        /// <returns>Color code string</returns>
        public static string GetColorCode(this TimerStatus status)
        {
            return status switch
            {
                TimerStatus.Created => "Gray",
                TimerStatus.Starting => "Blue",
                TimerStatus.Running => "Green",
                TimerStatus.Paused => "Yellow",
                TimerStatus.Executing => "DarkGreen",
                TimerStatus.Stopping => "Orange",
                TimerStatus.Stopped => "Gray",
                TimerStatus.Error => "Red",
                TimerStatus.CircuitBreaker => "DarkRed",
                TimerStatus.Disposed => "DarkGray",
                TimerStatus.Restarting => "Blue",
                TimerStatus.Waiting => "Cyan",
                TimerStatus.Cancelled => "Red",
                _ => "Black"
            };
        }

        /// <summary>
        /// Gets the priority level for the timer status
        /// </summary>
        /// <param name="status">The timer status</param>
        /// <returns>Priority level (higher is more important)</returns>
        public static int GetPriority(this TimerStatus status)
        {
            return status switch
            {
                TimerStatus.Disposed => 0,
                TimerStatus.Created => 1,
                TimerStatus.Stopped => 2,
                TimerStatus.Paused => 3,
                TimerStatus.Running => 4,
                TimerStatus.Starting => 5,
                TimerStatus.Executing => 6,
                TimerStatus.Stopping => 7,
                TimerStatus.Restarting => 8,
                TimerStatus.Waiting => 9,
                TimerStatus.Error => 10,
                TimerStatus.Cancelled => 11,
                TimerStatus.CircuitBreaker => 12,
                _ => 0
            };
        }
    }
}