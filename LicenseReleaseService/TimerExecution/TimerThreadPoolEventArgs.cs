using System;
using System.Collections.Generic;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Event arguments for thread pool events
    /// </summary>
    public class TimerThreadPoolEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the old thread pool size
        /// </summary>
        public int OldSize { get; }

        /// <summary>
        /// Gets the new thread pool size
        /// </summary>
        public int NewSize { get; }

        /// <summary>
        /// Gets the reason for the adjustment
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Gets the timestamp when the adjustment occurred
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the TimerThreadPoolEventArgs class
        /// </summary>
        /// <param name="oldSize">The old thread pool size</param>
        /// <param name="newSize">The new thread pool size</param>
        /// <param name="reason">The reason for the adjustment</param>
        public TimerThreadPoolEventArgs(int oldSize, int newSize, string reason)
        {
            OldSize = oldSize;
            NewSize = newSize;
            Reason = reason;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for work item events
    /// </summary>
    public class TimerWorkItemEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the work item ID
        /// </summary>
        public string WorkItemId { get; }

        /// <summary>
        /// Gets the priority of the work item
        /// </summary>
        public TimerWorkItemPriority Priority { get; }

        /// <summary>
        /// Gets the status message
        /// </summary>
        public string Status { get; }

        /// <summary>
        /// Gets the queue time (time spent waiting in queue)
        /// </summary>
        public TimeSpan QueueTime { get; set; }

        /// <summary>
        /// Gets the processing time (time spent executing)
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }

        /// <summary>
        /// Gets the timestamp when the event occurred
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the TimerWorkItemEventArgs class
        /// </summary>
        /// <param name="workItemId">The work item ID</param>
        /// <param name="priority">The priority of the work item</param>
        /// <param name="status">The status message</param>
        public TimerWorkItemEventArgs(string workItemId, TimerWorkItemPriority priority, string status)
        {
            WorkItemId = workItemId;
            Priority = priority;
            Status = status;
            QueueTime = TimeSpan.Zero;
            ProcessingTime = TimeSpan.Zero;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents a thread pool adjustment event
    /// </summary>
    public class TimerThreadPoolEvent
    {
        /// <summary>
        /// Gets the old thread pool size
        /// </summary>
        public int OldSize { get; }

        /// <summary>
        /// Gets the new thread pool size
        /// </summary>
        public int NewSize { get; }

        /// <summary>
        /// Gets the reason for the adjustment
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Gets whether the adjustment was successful
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the timestamp when the adjustment occurred
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the TimerThreadPoolEvent class
        /// </summary>
        /// <param name="oldSize">The old thread pool size</param>
        /// <param name="newSize">The new thread pool size</param>
        /// <param name="reason">The reason for the adjustment</param>
        /// <param name="success">Whether the adjustment was successful</param>
        /// <param name="timestamp">The timestamp when the adjustment occurred</param>
        public TimerThreadPoolEvent(int oldSize, int newSize, string reason, bool success, DateTime timestamp)
        {
            OldSize = oldSize;
            NewSize = newSize;
            Reason = reason;
            Success = success;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Thread pool metrics
    /// </summary>
    public class TimerThreadPoolMetrics
    {
        /// <summary>
        /// Gets the uptime of the thread pool manager
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets the current thread pool size
        /// </summary>
        public int CurrentThreadPoolSize { get; set; }

        /// <summary>
        /// Gets the optimal thread pool size
        /// </summary>
        public int OptimalThreadPoolSize { get; set; }

        /// <summary>
        /// Gets the minimum thread pool size
        /// </summary>
        public int MinThreadPoolSize { get; set; }

        /// <summary>
        /// Gets the maximum thread pool size
        /// </summary>
        public int MaxThreadPoolSize { get; set; }

        /// <summary>
        /// Gets the total number of adjustments
        /// </summary>
        public long TotalAdjustments { get; set; }

        /// <summary>
        /// Gets the number of successful adjustments
        /// </summary>
        public long SuccessfulAdjustments { get; set; }

        /// <summary>
        /// Gets the number of failed adjustments
        /// </summary>
        public long FailedAdjustments { get; set; }

        /// <summary>
        /// Gets the total number of work items processed
        /// </summary>
        public long WorkItemsProcessed { get; set; }

        /// <summary>
        /// Gets the total number of work items queued
        /// </summary>
        public long WorkItemsQueued { get; set; }

        /// <summary>
        /// Gets the total number of work items rejected
        /// </summary>
        public long WorkItemsRejected { get; set; }

        /// <summary>
        /// Gets the average queue time in milliseconds
        /// </summary>
        public double AverageQueueTime { get; set; }

        /// <summary>
        /// Gets the average processing time in milliseconds
        /// </summary>
        public double AverageProcessingTime { get; set; }

        /// <summary>
        /// Gets the number of available worker threads
        /// </summary>
        public int ActiveWorkerThreads { get; set; }

        /// <summary>
        /// Gets the number of available completion port threads
        /// </summary>
        public int ActiveCompletionPortThreads { get; set; }

        /// <summary>
        /// Gets the maximum number of worker threads
        /// </summary>
        public int MaxWorkerThreads { get; set; }

        /// <summary>
        /// Gets the maximum number of completion port threads
        /// </summary>
        public int MaxCompletionPortThreads { get; set; }

        /// <summary>
        /// Gets the minimum number of worker threads
        /// </summary>
        public int MinWorkerThreads { get; set; }

        /// <summary>
        /// Gets the minimum number of completion port threads
        /// </summary>
        public int MinCompletionPortThreads { get; set; }

        /// <summary>
        /// Gets the number of process threads
        /// </summary>
        public int ProcessThreads { get; set; }

        /// <summary>
        /// Gets the CPU usage percentage
        /// </summary>
        public double CPUUsagePercent { get; set; }

        /// <summary>
        /// Gets the adjustment success rate as a percentage
        /// </summary>
        public double AdjustmentSuccessRate => TotalAdjustments > 0 ? (SuccessfulAdjustments * 100.0 / TotalAdjustments) : 0;

        /// <summary>
        /// Gets the work item success rate as a percentage
        /// </summary>
        public double WorkItemSuccessRate => WorkItemsQueued > 0 ? ((WorkItemsQueued - WorkItemsRejected) * 100.0 / WorkItemsQueued) : 0;

        /// <summary>
        /// Gets the throughput as work items per minute
        /// </summary>
        public double ThroughputPerMinute => Uptime.TotalMinutes > 0 ? WorkItemsProcessed / Uptime.TotalMinutes : 0;

        /// <summary>
        /// Initializes a new instance of the TimerThreadPoolMetrics class
        /// </summary>
        public TimerThreadPoolMetrics()
        {
        }

        /// <summary>
        /// Returns a string representation of the metrics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Timer Thread Pool Metrics:");
            builder.AppendLine($"  Uptime: {Uptime}");
            builder.AppendLine($"  Thread Pool Size: {CurrentThreadPoolSize} (Optimal: {OptimalThreadPoolSize}, Min: {MinThreadPoolSize}, Max: {MaxThreadPoolSize})");
            builder.AppendLine($"  Adjustments: {TotalAdjustments} (Success: {SuccessfulAdjustments}, Failed: {FailedAdjustments})");
            builder.AppendLine($"  Work Items: {WorkItemsProcessed} processed, {WorkItemsQueued} queued, {WorkItemsRejected} rejected");
            builder.AppendLine($"  Success Rates: Adjustment {AdjustmentSuccessRate:F2}%, WorkItem {WorkItemSuccessRate:F2}%");
            builder.AppendLine($"  Average Times: Queue {AverageQueueTime:F2}ms, Processing {AverageProcessingTime:F2}ms");
            builder.AppendLine($"  Throughput: {ThroughputPerMinute:F2} work items/minute");
            builder.AppendLine($"  Threads: {ActiveWorkerThreads} available workers, {ActiveCompletionPortThreads} available IOCPs");
            builder.AppendLine($"  Process Threads: {ProcessThreads}");
            builder.AppendLine($"  CPU Usage: {CPUUsagePercent:F2}%");

            return builder.ToString();
        }
    }

    /// <summary>
    /// Tracks work item execution statistics
    /// </summary>
    public class TimerWorkItemTracker
    {
        private readonly object _lock = new object();
        private readonly string _workItemId;
        private readonly Queue<TimerWorkItemExecution> _executionHistory;
        private DateTime _lastActivity;
        private long _totalExecutions;
        private long _successfulExecutions;
        private long _failedExecutions;
        private double _averageQueueTime;
        private double _averageProcessingTime;

        /// <summary>
        /// Gets the work item ID
        /// </summary>
        public string WorkItemId => _workItemId;

        /// <summary>
        /// Gets the last activity timestamp
        /// </summary>
        public DateTime LastActivity => _lastActivity;

        /// <summary>
        /// Gets the total number of executions
        /// </summary>
        public long TotalExecutions => Interlocked.Read(ref _totalExecutions);

        /// <summary>
        /// Gets the number of successful executions
        /// </summary>
        public long SuccessfulExecutions => Interlocked.Read(ref _successfulExecutions);

        /// <summary>
        /// Gets the number of failed executions
        /// </summary>
        public long FailedExecutions => Interlocked.Read(ref _failedExecutions);

        /// <summary>
        /// Gets the average queue time
        /// </summary>
        public double AverageQueueTime => _averageQueueTime;

        /// <summary>
        /// Gets the average processing time
        /// </summary>
        public double AverageProcessingTime => _averageProcessingTime;

        /// <summary>
        /// Gets the success rate as a percentage
        /// </summary>
        public double SuccessRate => TotalExecutions > 0 ? (SuccessfulExecutions * 100.0 / TotalExecutions) : 0;

        /// <summary>
        /// Initializes a new instance of the TimerWorkItemTracker class
        /// </summary>
        /// <param name="workItemId">The work item ID</param>
        public TimerWorkItemTracker(string workItemId)
        {
            _workItemId = workItemId ?? throw new ArgumentNullException(nameof(workItemId));
            _executionHistory = new Queue<TimerWorkItemExecution>(10);
            _lastActivity = DateTime.UtcNow;
        }

        /// <summary>
        /// Records when a work item is queued
        /// </summary>
        /// <param name="queuedTime">The time when the work item was queued</param>
        /// <param name="priority">The priority of the work item</param>
        public void Queue(DateTime queuedTime, TimerWorkItemPriority priority)
        {
            lock (_lock)
            {
                var execution = new TimerWorkItemExecution(queuedTime, priority);
                _executionHistory.Enqueue(execution);
                _lastActivity = DateTime.UtcNow;

                // Keep only recent history
                while (_executionHistory.Count > 10)
                {
                    _executionHistory.Dequeue();
                }
            }
        }

        /// <summary>
        /// Records when a work item starts execution
        /// </summary>
        /// <param name="startTime">The start time</param>
        public void Start(DateTime startTime)
        {
            lock (_lock)
            {
                if (_executionHistory.Count > 0)
                {
                    var current = _executionHistory.Peek();
                    if (current.StartTime == null)
                    {
                        current.StartTime = startTime;
                        _lastActivity = DateTime.UtcNow;
                    }
                }
            }
        }

        /// <summary>
        /// Records when a work item completes successfully
        /// </summary>
        /// <param name="endTime">The end time</param>
        public void Complete(DateTime endTime)
        {
            lock (_lock)
            {
                if (_executionHistory.Count > 0)
                {
                    var current = _executionHistory.Peek();
                    if (current.StartTime.HasValue && !current.EndTime.HasValue)
                    {
                        current.EndTime = endTime;
                        current.Success = true;
                        _lastActivity = DateTime.UtcNow;

                        Interlocked.Increment(ref _totalExecutions);
                        Interlocked.Increment(ref _successfulExecutions);

                        UpdateAverageTimes(current);
                    }
                }
            }
        }

        /// <summary>
        /// Records when a work item fails
        /// </summary>
        /// <param name="endTime">The end time</param>
        /// <param name="errorMessage">The error message</param>
        public void Fail(DateTime endTime, string errorMessage)
        {
            lock (_lock)
            {
                if (_executionHistory.Count > 0)
                {
                    var current = _executionHistory.Peek();
                    if (current.StartTime.HasValue && !current.EndTime.HasValue)
                    {
                        current.EndTime = endTime;
                        current.Success = false;
                        current.ErrorMessage = errorMessage;
                        _lastActivity = DateTime.UtcNow;

                        Interlocked.Increment(ref _totalExecutions);
                        Interlocked.Increment(ref _failedExecutions);

                        UpdateAverageTimes(current);
                    }
                }
            }
        }

        /// <summary>
        /// Resets the tracker statistics
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                Interlocked.Exchange(ref _totalExecutions, 0);
                Interlocked.Exchange(ref _successfulExecutions, 0);
                Interlocked.Exchange(ref _failedExecutions, 0);
                _averageQueueTime = 0;
                _averageProcessingTime = 0;
                _executionHistory.Clear();
                _lastActivity = DateTime.UtcNow;
            }
        }

        private void UpdateAverageTimes(TimerWorkItemExecution execution)
        {
            if (execution.StartTime.HasValue && execution.EndTime.HasValue)
            {
                var queueTime = (execution.StartTime.Value - execution.QueuedTime).TotalMilliseconds;
                var processingTime = (execution.EndTime.Value - execution.StartTime.Value).TotalMilliseconds;

                _averageQueueTime = (_averageQueueTime * (TotalExecutions - 1) + queueTime) / TotalExecutions;
                _averageProcessingTime = (_averageProcessingTime * (TotalExecutions - 1) + processingTime) / TotalExecutions;
            }
        }
    }

    /// <summary>
    /// Represents a single work item execution
    /// </summary>
    public class TimerWorkItemExecution
    {
        /// <summary>
        /// Gets the time when the work item was queued
        /// </summary>
        public DateTime QueuedTime { get; }

        /// <summary>
        /// Gets the priority of the work item
        /// </summary>
        public TimerWorkItemPriority Priority { get; }

        /// <summary>
        /// Gets the start time of execution
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Gets the end time of execution
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Gets whether the execution was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets the error message if the execution failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerWorkItemExecution class
        /// </summary>
        /// <param name="queuedTime">The time when the work item was queued</param>
        /// <param name="priority">The priority of the work item</param>
        public TimerWorkItemExecution(DateTime queuedTime, TimerWorkItemPriority priority)
        {
            QueuedTime = queuedTime;
            Priority = priority;
        }
    }
}