using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseReleaseService.IdleDetection.Models
{
    /// <summary>
    /// Represents the result of an idle detection task execution
    /// </summary>
    public class IdleDetectionTaskResult
    {
        /// <summary>
        /// Gets or sets the unique identifier for the task result
        /// </summary>
        public Guid ResultId { get; set; }

        /// <summary>
        /// Gets or sets the task identifier
        /// </summary>
        public Guid TaskId { get; set; }

        /// <summary>
        /// Gets or sets the task name
        /// </summary>
        public string TaskName { get; set; }

        /// <summary>
        /// Gets or sets the detector name that produced this result
        /// </summary>
        public string DetectorName { get; set; }

        /// <summary>
        /// Gets or sets the session identifier
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets or sets the task type
        /// </summary>
        public IdleDetectionTaskType TaskType { get; set; }

        /// <summary>
        /// Gets or sets whether the task was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if the task failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the exception if the task failed
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the task result data
        /// </summary>
        public object Results { get; set; }

        /// <summary>
        /// Gets or sets when the task started execution
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Gets or sets when the task completed execution
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Gets or sets the duration of the task execution
        /// </summary>
        public TimeSpan? ExecutionDuration { get; set; }

        /// <summary>
        /// Gets or sets the duration of the task execution (alias for ExecutionDuration)
        /// </summary>
        public TimeSpan? Duration
        {
            get => ExecutionDuration;
            set => ExecutionDuration = value;
        }

        /// <summary>
        /// Gets or sets the timestamp when the result was created
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets additional metadata about the task result
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// Gets or sets whether the task was cancelled
        /// </summary>
        public bool WasCancelled { get; set; }

        /// <summary>
        /// Gets or sets the cancellation token status
        /// </summary>
        public bool CancellationTokenTriggered { get; set; }

        /// <summary>
        /// Gets or sets the retry count for this task
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets the task priority
        /// </summary>
        public TaskPriority Priority { get; set; }

        /// <summary>
        /// Initializes a new instance of the IdleDetectionTaskResult class
        /// </summary>
        public IdleDetectionTaskResult()
        {
            ResultId = Guid.NewGuid();
            TaskName = string.Empty;
            DetectorName = string.Empty;
            SessionId = string.Empty;
            Timestamp = DateTime.UtcNow;
            Metadata = new Dictionary<string, object>();
            TaskType = IdleDetectionTaskType.CustomTask;
            Priority = TaskPriority.Normal;
        }

        /// <summary>
        /// Initializes a new instance of the IdleDetectionTaskResult class with task information
        /// </summary>
        /// <param name="taskId">The task identifier</param>
        /// <param name="taskName">The task name</param>
        /// <param name="taskType">The task type</param>
        /// <param name="detectorName">The detector name</param>
        /// <param name="sessionId">The session identifier</param>
        public IdleDetectionTaskResult(Guid taskId, string taskName, IdleDetectionTaskType taskType, string detectorName = null, string sessionId = null)
        {
            ResultId = Guid.NewGuid();
            TaskId = taskId;
            TaskName = taskName ?? string.Empty;
            TaskType = taskType;
            DetectorName = detectorName ?? string.Empty;
            SessionId = sessionId ?? string.Empty;
            Timestamp = DateTime.UtcNow;
            Metadata = new Dictionary<string, object>();
            Priority = TaskPriority.Normal;
        }

        /// <summary>
        /// Creates a successful task result
        /// </summary>
        /// <param name="taskId">The task identifier</param>
        /// <param name="taskName">The task name</param>
        /// <param name="taskType">The task type</param>
        /// <param name="result">The result data</param>
        /// <param name="startTime">The start time</param>
        /// <param name="endTime">The end time</param>
        /// <param name="detectorName">The detector name</param>
        /// <param name="sessionId">The session identifier</param>
        /// <returns>A successful task result</returns>
        public static IdleDetectionTaskResult CreateSuccess(Guid taskId, string taskName, IdleDetectionTaskType taskType, object result = null, DateTime? startTime = null, DateTime? endTime = null, string detectorName = null, string sessionId = null)
        {
            return new IdleDetectionTaskResult(taskId, taskName, taskType, detectorName, sessionId)
            {
                Success = true,
                Results = result,
                StartTime = startTime,
                EndTime = endTime ?? DateTime.UtcNow,
                ExecutionDuration = endTime.HasValue && startTime.HasValue ? endTime.Value - startTime.Value : null
            };
        }

        /// <summary>
        /// Creates a failed task result
        /// </summary>
        /// <param name="taskId">The task identifier</param>
        /// <param name="taskName">The task name</param>
        /// <param name="taskType">The task type</param>
        /// <param name="errorMessage">The error message</param>
        /// <param name="exception">The exception</param>
        /// <param name="startTime">The start time</param>
        /// <param name="endTime">The end time</param>
        /// <param name="detectorName">The detector name</param>
        /// <param name="sessionId">The session identifier</param>
        /// <returns>A failed task result</returns>
        public static IdleDetectionTaskResult CreateFailure(Guid taskId, string taskName, IdleDetectionTaskType taskType, string errorMessage, Exception exception = null, DateTime? startTime = null, DateTime? endTime = null, string detectorName = null, string sessionId = null)
        {
            return new IdleDetectionTaskResult(taskId, taskName, taskType, detectorName, sessionId)
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                StartTime = startTime,
                EndTime = endTime ?? DateTime.UtcNow,
                ExecutionDuration = endTime.HasValue && startTime.HasValue ? endTime.Value - startTime.Value : null
            };
        }

        /// <summary>
        /// Creates a cancelled task result
        /// </summary>
        /// <param name="taskId">The task identifier</param>
        /// <param name="taskName">The task name</param>
        /// <param name="taskType">The task type</param>
        /// <param name="startTime">The start time</param>
        /// <param name="detectorName">The detector name</param>
        /// <param name="sessionId">The session identifier</param>
        /// <returns>A cancelled task result</returns>
        public static IdleDetectionTaskResult CreateCancelled(Guid taskId, string taskName, IdleDetectionTaskType taskType, DateTime? startTime = null, string detectorName = null, string sessionId = null)
        {
            return new IdleDetectionTaskResult(taskId, taskName, taskType, detectorName, sessionId)
            {
                Success = false,
                WasCancelled = true,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                ExecutionDuration = startTime.HasValue ? DateTime.UtcNow - startTime.Value : null,
                ErrorMessage = "Task was cancelled"
            };
        }

        /// <summary>
        /// Adds metadata to the task result
        /// </summary>
        /// <param name="key">The metadata key</param>
        /// <param name="value">The metadata value</param>
        public void AddMetadata(string key, object value)
        {
            Metadata[key] = value;
        }

        /// <summary>
        /// Returns a string representation of the task result
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var status = Success ? "Success" : (WasCancelled ? "Cancelled" : "Failed");
            return $"IdleDetectionTaskResult: {TaskName} ({TaskType}) - {status}";
        }
    }
}