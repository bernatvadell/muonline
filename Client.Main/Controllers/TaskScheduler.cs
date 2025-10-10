using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Client.Main.Controllers
{
    /// <summary>
    /// Intelligent task scheduler for main thread actions with priority management and backpressure control
    /// to prevent micro-freezing during network packet processing and UI updates.
    /// </summary>
    public class TaskScheduler : IDisposable
    {
        private readonly ILogger<TaskScheduler> _logger;
        private readonly ConcurrentPriorityQueue<TaskItem> _taskQueue;
        private readonly CancellationTokenSource _cts = new();

        // Configuration
        private readonly int _maxTasksPerFrame = 10;
        private readonly int _maxTotalQueuedTasks = 100;
        private readonly TimeSpan _maxProcessingTimePerFrame = TimeSpan.FromMilliseconds(16); // ~60 FPS

        // Statistics
        private long _processedTasks;
        private readonly Stopwatch _uptimeStopwatch = new();
        private readonly Stopwatch _frameStopwatch = new();

        // Priority levels
        public enum Priority
        {
            Critical = 0,     // Immediate processing (damage notifications, critical updates)
            High = 1,         // High priority (player movements, NPC spawns in view)
            Normal = 2,       // Standard tasks (UI updates, equipment changes)
            Low = 3,          // Background tasks (model loading, texture caching)
        }

        private class TaskItem : IComparable<TaskItem>
        {
            public Action Action { get; }
            public Priority TaskPriority { get; }
            public DateTime Created { get; } = DateTime.UtcNow;

            public TaskItem(Action action, Priority priority)
            {
                Action = action;
                TaskPriority = priority;
            }

            public int CompareTo(TaskItem other)
            {
                if (TaskPriority != other.TaskPriority)
                {
                    return TaskPriority.CompareTo(other.TaskPriority);
                }
                return Created.CompareTo(other.Created); // FIFO within priority
            }
        }

        public TaskScheduler(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger<TaskScheduler>() ??
                     LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<TaskScheduler>();
            _taskQueue = new ConcurrentPriorityQueue<TaskItem>();
            _uptimeStopwatch.Start();
        }

        /// <summary>
        /// Queues a task for main thread execution with specified priority
        /// </summary>
        public bool QueueTask(Action action, Priority priority = Priority.Normal)
        {
            if (action == null) return false;

            if (_taskQueue.Count >= _maxTotalQueuedTasks)
            {
                _logger.LogWarning("Task queue is full ({Count}). Dropping task with priority {Priority}",
                                  _taskQueue.Count, priority);
                return false;
            }

            var taskItem = new TaskItem(action, priority);
            _taskQueue.Enqueue(taskItem);

            // _logger.LogDebug("Queued task with priority {Priority}. Queue size: {Count}",
            //                 priority, _taskQueue.Count);

            return true;
        }

        /// <summary>
        /// Processes queued tasks on the main thread. Should be called each frame.
        /// </summary>
        public void ProcessFrame()
        {
            if (_cts.IsCancellationRequested) return;

            _frameStopwatch.Restart();
            var processedThisFrame = 0;

            while (_taskQueue.Count > 0 && processedThisFrame < _maxTasksPerFrame)
            {
                if (_frameStopwatch.Elapsed >= _maxProcessingTimePerFrame)
                {
                    _logger.LogDebug("Frame processing time limit reached ({ElapsedMs}ms). Remaining tasks: {Count}",
                                    _frameStopwatch.Elapsed.TotalMilliseconds, _taskQueue.Count);
                    break;
                }

                if (!_taskQueue.TryDequeue(out var taskItem)) continue;

                try
                {
                    var taskStartTime = Stopwatch.GetTimestamp();
                    taskItem.Action();
                    var processingTime = Stopwatch.GetElapsedTime(taskStartTime).TotalMilliseconds;

                    processedThisFrame++;
                    Interlocked.Increment(ref _processedTasks);

                    if (processingTime > 1.0) // Log slow tasks
                    {
                        _logger.LogInformation("Slow task execution ({ProcessingTime:F2}ms) - Priority: {Priority}",
                                              processingTime, taskItem.TaskPriority);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing scheduled task - Priority: {Priority}",
                                    taskItem.TaskPriority);
                }
            }

            if (_taskQueue.Count > 50) // Warn about buildup
            {
                _logger.LogWarning("Task queue is backing up. Current count: {Count}", _taskQueue.Count);
            }
        }

        /// <summary>
        /// Gets the number of currently queued tasks
        /// </summary>
        public int QueuedTaskCount => _taskQueue.Count;

        /// <summary>
        /// Gets statistics about task processing
        /// </summary>
        public (long ProcessedTasks, int QueuedTasks, double QueueProcessingRate) GetStatistics()
        {
            var elapsedSec = Math.Max(_uptimeStopwatch.Elapsed.TotalSeconds, 0.001);
            var rate = _processedTasks / elapsedSec;
            return (_processedTasks, _taskQueue.Count, rate);
        }

        /// <summary>
        /// Clears all queued tasks
        /// </summary>
        public void ClearQueue()
        {
            _taskQueue.Clear();
            _logger.LogInformation("Task queue cleared");
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            _cts.Cancel();
            ClearQueue();
        }

        // Simple concurrent priority queue implementation
        private class ConcurrentPriorityQueue<T> where T : TaskItem
        {
            private readonly object _lock = new();
            private readonly Dictionary<Priority, ConcurrentQueue<T>> _queues = new()
            {
                { Priority.Critical, new ConcurrentQueue<T>() },
                { Priority.High,     new ConcurrentQueue<T>() },
                { Priority.Normal,   new ConcurrentQueue<T>() },
                { Priority.Low,      new ConcurrentQueue<T>() },
            };

            private volatile int _count;

            public void Enqueue(T item)
            {
                lock (_lock)
                {
                    _queues[item.TaskPriority].Enqueue(item);
                    _count++;
                }
            }

            public bool TryDequeue(out T item)
            {
                lock (_lock)
                {
                    // Fast-path: empty
                    if (_count == 0)
                    {
                        item = default;
                        return false;
                    }

                    // Check queues by priority order
                    if (_queues[Priority.Critical].TryDequeue(out item) ||
                        _queues[Priority.High].TryDequeue(out item) ||
                        _queues[Priority.Normal].TryDequeue(out item) ||
                        _queues[Priority.Low].TryDequeue(out item))
                    {
                        _count--;
                        return true;
                    }

                    item = default;
                    return false;
                }
            }

            public void Clear()
            {
                lock (_lock)
                {
                    foreach (var q in _queues.Values)
                    {
                        while (q.TryDequeue(out _)) { }
                    }
                    _count = 0;
                }
            }

            public int Count
            {
                get
                {
                    lock (_lock)
                    {
                        return _count;
                    }
                }
            }
        }
    }
}
