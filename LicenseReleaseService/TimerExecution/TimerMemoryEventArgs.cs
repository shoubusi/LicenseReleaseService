using System;

namespace LicenseReleaseService.TimerExecution
{
    /// <summary>
    /// Event arguments for memory pressure events
    /// </summary>
    public class TimerMemoryPressureEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the memory pressure level
        /// </summary>
        public TimerMemoryPressureLevel PressureLevel { get; }

        /// <summary>
        /// Gets the current memory usage in MB
        /// </summary>
        public long UsedMemoryMB { get; }

        /// <summary>
        /// Gets the memory usage percentage
        /// </summary>
        public double MemoryPressurePercent { get; }

        /// <summary>
        /// Gets the timestamp when the pressure was detected
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the TimerMemoryPressureEventArgs class
        /// </summary>
        /// <param name="pressureLevel">The memory pressure level</param>
        /// <param name="usedMemoryMB">The current memory usage in MB</param>
        /// <param name="memoryPressurePercent">The memory usage percentage</param>
        public TimerMemoryPressureEventArgs(TimerMemoryPressureLevel pressureLevel, long usedMemoryMB, double memoryPressurePercent = 0)
        {
            PressureLevel = pressureLevel;
            UsedMemoryMB = usedMemoryMB;
            MemoryPressurePercent = memoryPressurePercent;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for memory optimization events
    /// </summary>
    public class TimerMemoryOptimizationEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the description of the optimization performed
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the priority of the optimization
        /// </summary>
        public TimerOptimizationPriority Priority { get; }

        /// <summary>
        /// Gets the memory freed in bytes
        /// </summary>
        public long MemoryFreedBytes { get; set; }

        /// <summary>
        /// Gets the timestamp when the optimization was performed
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the TimerMemoryOptimizationEventArgs class
        /// </summary>
        /// <param name="description">Description of the optimization</param>
        /// <param name="priority">Priority of the optimization</param>
        public TimerMemoryOptimizationEventArgs(string description, TimerOptimizationPriority priority)
        {
            Description = description;
            Priority = priority;
            MemoryFreedBytes = 0;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for memory leak events
    /// </summary>
    public class TimerMemoryLeakEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the current memory usage in MB
        /// </summary>
        public long CurrentMemoryMB { get; }

        /// <summary>
        /// Gets the average memory usage in MB
        /// </summary>
        public double AverageMemoryMB { get; }

        /// <summary>
        /// Gets the description of the potential leak
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the timestamp when the leak was detected
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the TimerMemoryLeakEventArgs class
        /// </summary>
        /// <param name="currentMemoryMB">Current memory usage in MB</param>
        /// <param name="averageMemoryMB">Average memory usage in MB</param>
        /// <param name="description">Description of the potential leak</param>
        public TimerMemoryLeakEventArgs(long currentMemoryMB, double averageMemoryMB, string description)
        {
            CurrentMemoryMB = currentMemoryMB;
            AverageMemoryMB = averageMemoryMB;
            Description = description;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents a memory pressure event
    /// </summary>
    public class TimerMemoryPressureEvent
    {
        /// <summary>
        /// Gets the memory pressure level
        /// </summary>
        public TimerMemoryPressureLevel PressureLevel { get; }

        /// <summary>
        /// Gets the memory usage in MB
        /// </summary>
        public long MemoryMB { get; }

        /// <summary>
        /// Gets the timestamp of the event
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the TimerMemoryPressureEvent class
        /// </summary>
        /// <param name="pressureLevel">The pressure level</param>
        /// <param name="memoryMB">Memory usage in MB</param>
        /// <param name="timestamp">Timestamp of the event</param>
        public TimerMemoryPressureEvent(TimerMemoryPressureLevel pressureLevel, long memoryMB, DateTime timestamp)
        {
            PressureLevel = pressureLevel;
            MemoryMB = memoryMB;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Memory metrics for the timer memory manager
    /// </summary>
    public class TimerMemoryMetrics
    {
        /// <summary>
        /// Gets the uptime of the memory manager
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Gets the current memory pressure level
        /// </summary>
        public TimerMemoryPressureLevel CurrentPressureLevel { get; set; }

        /// <summary>
        /// Gets the current working set memory in MB
        /// </summary>
        public long WorkingSetMB { get; set; }

        /// <summary>
        /// Gets the current private memory in MB
        /// </summary>
        public long PrivateMemoryMB { get; set; }

        /// <summary>
        /// Gets the current virtual memory in MB
        /// </summary>
        public long VirtualMemoryMB { get; set; }

        /// <summary>
        /// Gets the number of GC generation 0 collections
        /// </summary>
        public long GCGeneration0Collections { get; set; }

        /// <summary>
        /// Gets the number of GC generation 1 collections
        /// </summary>
        public long GCGeneration1Collections { get; set; }

        /// <summary>
        /// Gets the number of GC generation 2 collections
        /// </summary>
        public long GCGeneration2Collections { get; set; }

        /// <summary>
        /// Gets the total memory optimized in bytes
        /// </summary>
        public long TotalMemoryOptimized { get; set; }

        /// <summary>
        /// Gets the number of forced GC collections
        /// </summary>
        public long GcCollectionsForced { get; set; }

        /// <summary>
        /// Gets the number of memory leaks detected
        /// </summary>
        public long MemoryLeaksDetected { get; set; }

        /// <summary>
        /// Gets the number of active memory pools
        /// </summary>
        public int MemoryPoolsActive { get; set; }

        /// <summary>
        /// Gets the total number of memory pools created
        /// </summary>
        public long MemoryPoolsCreated { get; set; }

        /// <summary>
        /// Gets the total number of memory pools released
        /// </summary>
        public long MemoryPoolsReleased { get; set; }

        /// <summary>
        /// Gets the current memory pressure percentage
        /// </summary>
        public double MemoryPressurePercent { get; set; }

        /// <summary>
        /// Gets whether server GC is enabled
        /// </summary>
        public bool IsServerGC { get; set; }

        /// <summary>
        /// Gets the current GC latency mode
        /// </summary>
        public GCLatencyMode LatencyMode { get; set; }

        /// <summary>
        /// Initializes a new instance of the TimerMemoryMetrics class
        /// </summary>
        public TimerMemoryMetrics()
        {
        }

        /// <summary>
        /// Returns a string representation of the metrics
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("Timer Memory Metrics:");
            builder.AppendLine($"  Uptime: {Uptime}");
            builder.AppendLine($"  Pressure Level: {CurrentPressureLevel}");
            builder.AppendLine($"  Memory Pressure: {MemoryPressurePercent:F2}%");
            builder.AppendLine($"  Working Set: {WorkingSetMB}MB");
            builder.AppendLine($"  Private Memory: {PrivateMemoryMB}MB");
            builder.AppendLine($"  Virtual Memory: {VirtualMemoryMB}MB");
            builder.AppendLine($"  GC Collections - Gen0: {GCGeneration0Collections}, Gen1: {GCGeneration1Collections}, Gen2: {GCGeneration2Collections}");
            builder.AppendLine($"  Total Memory Optimized: {TotalMemoryOptimized / (1024 * 1024)}MB");
            builder.AppendLine($"  Forced GC Collections: {GcCollectionsForced}");
            builder.AppendLine($"  Memory Leaks Detected: {MemoryLeaksDetected}");
            builder.AppendLine($"  Memory Pools - Active: {MemoryPoolsActive}, Created: {MemoryPoolsCreated}, Released: {MemoryPoolsReleased}");
            builder.AppendLine($"  Server GC: {IsServerGC}");
            builder.AppendLine($"  Latency Mode: {LatencyMode}");

            return builder.ToString();
        }
    }

    /// <summary>
    /// Represents a memory pool for object reuse
    /// </summary>
    public class TimerMemoryPool : IDisposable
    {
        private readonly string _name;
        private readonly int _objectSize;
        private readonly Queue<byte[]> _pool;
        private readonly object _lock = new object();
        private int _totalObjectsCreated;
        private int _totalObjectsReused;
        private bool _isDisposed;
        private bool _isCritical;

        /// <summary>
        /// Gets the name of the pool
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// Gets the size of objects in the pool
        /// </summary>
        public int ObjectSize => _objectSize;

        /// <summary>
        /// Gets the current number of objects in the pool
        /// </summary>
        public int Count => _pool.Count;

        /// <summary>
        /// Gets the total number of objects created
        /// </summary>
        public int TotalObjectsCreated => _totalObjectsCreated;

        /// <summary>
        /// Gets the total number of objects reused
        /// </summary>
        public int TotalObjectsReused => _totalObjectsReused;

        /// <summary>
        /// Gets whether the pool is critical and should not be cleared during optimization
        /// </summary>
        public bool IsCritical
        {
            get => _isCritical;
            set => _isCritical = value;
        }

        /// <summary>
        /// Initializes a new instance of the TimerMemoryPool class
        /// </summary>
        /// <param name="name">Name of the pool</param>
        /// <param name="objectSize">Size of objects in bytes</param>
        /// <param name="initialCapacity">Initial capacity</param>
        public TimerMemoryPool(string name, int objectSize, int initialCapacity)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _objectSize = objectSize > 0 ? objectSize : throw new ArgumentException("Object size must be positive", nameof(objectSize));
            _pool = new Queue<byte[]>(initialCapacity);

            // Pre-allocate initial objects
            for (int i = 0; i < initialCapacity; i++)
            {
                _pool.Enqueue(new byte[objectSize]);
                _totalObjectsCreated++;
            }
        }

        /// <summary>
        /// Gets an object from the pool or creates a new one
        /// </summary>
        /// <returns>Byte array object</returns>
        public byte[] GetObject()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(TimerMemoryPool));

                if (_pool.Count > 0)
                {
                    var obj = _pool.Dequeue();
                    _totalObjectsReused++;
                    return obj;
                }

                _totalObjectsCreated++;
                return new byte[_objectSize];
            }
        }

        /// <summary>
        /// Returns an object to the pool
        /// </summary>
        /// <param name="obj">The object to return</param>
        public void ReturnObject(byte[] obj)
        {
            if (obj == null || obj.Length != _objectSize)
                return;

            lock (_lock)
            {
                if (_isDisposed)
                    return;

                // Clear the object before returning to pool
                Array.Clear(obj, 0, obj.Length);
                _pool.Enqueue(obj);
            }
        }

        /// <summary>
        /// Clears all objects from the pool
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    return;

                _pool.Clear();
            }
        }

        /// <summary>
        /// Reduces the pool size by half
        /// </summary>
        public void ReduceSize()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    return;

                var newSize = Math.Max(1, _pool.Count / 2);
                while (_pool.Count > newSize)
                {
                    _pool.Dequeue();
                }
            }
        }

        /// <summary>
        /// Gets the pool metrics
        /// </summary>
        /// <returns>Pool metrics</returns>
        public TimerMemoryPoolMetrics GetMetrics()
        {
            lock (_lock)
            {
                return new TimerMemoryPoolMetrics
                {
                    Name = _name,
                    ObjectSize = _objectSize,
                    Count = _pool.Count,
                    TotalObjectsCreated = _totalObjectsCreated,
                    TotalObjectsReused = _totalObjectsReused,
                    IsCritical = _isCritical,
                    ReuseRate = _totalObjectsCreated > 0 ? (_totalObjectsReused * 100.0 / _totalObjectsCreated) : 0
                };
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the memory pool
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Clear();
                }

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~TimerMemoryPool()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Metrics for a memory pool
    /// </summary>
    public class TimerMemoryPoolMetrics
    {
        /// <summary>
        /// Gets the name of the pool
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the size of objects in the pool
        /// </summary>
        public int ObjectSize { get; set; }

        /// <summary>
        /// Gets the current number of objects in the pool
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Gets the total number of objects created
        /// </summary>
        public int TotalObjectsCreated { get; set; }

        /// <summary>
        /// Gets the total number of objects reused
        /// </summary>
        public int TotalObjectsReused { get; set; }

        /// <summary>
        /// Gets whether the pool is critical
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Gets the reuse rate as a percentage
        /// </summary>
        public double ReuseRate { get; set; }
    }
}