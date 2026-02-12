using Microsoft.Extensions.Logging;
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
        // Lower per-frame budget to smooth out spikes when many spawn/load tasks enqueue at once
        private readonly int _maxTasksPerFrame = 4;
        private readonly int _maxTotalQueuedTasks = 150;
        private readonly TimeSpan _maxProcessingTimePerFrame = TimeSpan.FromMilliseconds(8); // tighter per-frame slice

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
            int queuedAtStart = _taskQueue.Count;
            int maxTasksThisFrame = _maxTasksPerFrame;

            // Increase throughput when queue is backing up, still capped by time budget.
            if (queuedAtStart > 40)
            {
                maxTasksThisFrame = Math.Min(12, _maxTasksPerFrame + (queuedAtStart / 25));
            }

            while (processedThisFrame < maxTasksThisFrame)
            {
                if (_frameStopwatch.Elapsed >= _maxProcessingTimePerFrame)
                {
                    _logger.LogDebug("Frame processing time limit reached ({ElapsedMs}ms). Remaining tasks: {Count}",
                                    _frameStopwatch.Elapsed.TotalMilliseconds, _taskQueue.Count);
                    break;
                }

                if (!_taskQueue.TryDequeue(out var taskItem))
                    break;

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

            int remaining = _taskQueue.Count;
            if (remaining > 50) // Warn about buildup
            {
                _logger.LogWarning("Task queue is backing up. Current count: {Count}", remaining);
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
            private readonly Queue<T> _criticalQueue = new();
            private readonly Queue<T> _highQueue = new();
            private readonly Queue<T> _normalQueue = new();
            private readonly Queue<T> _lowQueue = new();
            private int _count;

            public void Enqueue(T item)
            {
                lock (_lock)
                {
                    switch (item.TaskPriority)
                    {
                        case Priority.Critical:
                            _criticalQueue.Enqueue(item);
                            break;
                        case Priority.High:
                            _highQueue.Enqueue(item);
                            break;
                        case Priority.Normal:
                            _normalQueue.Enqueue(item);
                            break;
                        default:
                            _lowQueue.Enqueue(item);
                            break;
                    }

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
                    if (_criticalQueue.Count > 0)
                    {
                        item = _criticalQueue.Dequeue();
                        _count--;
                        return true;
                    }

                    if (_highQueue.Count > 0)
                    {
                        item = _highQueue.Dequeue();
                        _count--;
                        return true;
                    }

                    if (_normalQueue.Count > 0)
                    {
                        item = _normalQueue.Dequeue();
                        _count--;
                        return true;
                    }

                    if (_lowQueue.Count > 0)
                    {
                        item = _lowQueue.Dequeue();
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
                    _criticalQueue.Clear();
                    _highQueue.Clear();
                    _normalQueue.Clear();
                    _lowQueue.Clear();
                    _count = 0;
                }
            }

            public int Count => Volatile.Read(ref _count);
        }
    }
}
