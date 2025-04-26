using System;
using System.Threading;
using System.Threading.Tasks;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Represents an execution context that can be passed to components to control their execution
    /// </summary>
    public class ExecutionContext
    {
        /// <summary>
        /// Gets the cancellation token source for this execution context
        /// </summary>
        public CancellationTokenSource? CancellationTokenSource { get; }
        
        /// <summary>
        /// Gets the synchronization context for this execution context
        /// </summary>
        public SynchronizationContext? SynchronizationContext { get; }
        
        /// <summary>
        /// Gets the thread ID for this execution context
        /// </summary>
        public int? ThreadId { get; }
        
        /// <summary>
        /// Gets the task scheduler for this execution context
        /// </summary>
        public TaskScheduler? TaskScheduler { get; }
        
        /// <summary>
        /// Gets whether this execution context is being used for testing
        /// </summary>
        public bool IsTestContext { get; }
        
        /// <summary>
        /// Creates a new execution context with default values
        /// </summary>
        public ExecutionContext()
            : this(null, null, null, null, false)
        {
        }
        
        /// <summary>
        /// Creates a new execution context
        /// </summary>
        /// <param name="cancellationTokenSource">The cancellation token source</param>
        /// <param name="synchronizationContext">The synchronization context</param>
        /// <param name="threadId">The thread ID</param>
        /// <param name="taskScheduler">The task scheduler</param>
        /// <param name="isTestContext">Whether this is a test context</param>
        public ExecutionContext(
            CancellationTokenSource? cancellationTokenSource = null,
            SynchronizationContext? synchronizationContext = null,
            int? threadId = null,
            TaskScheduler? taskScheduler = null,
            bool isTestContext = false)
        {
            CancellationTokenSource = cancellationTokenSource;
            SynchronizationContext = synchronizationContext;
            ThreadId = threadId;
            TaskScheduler = taskScheduler;
            IsTestContext = isTestContext;
        }
        
        /// <summary>
        /// Creates a new execution context from the current thread
        /// </summary>
        /// <returns>A new execution context</returns>
        public static ExecutionContext FromCurrentThread()
        {
            return new ExecutionContext(
                new CancellationTokenSource(),
                SynchronizationContext.Current,
                Thread.CurrentThread.ManagedThreadId,
                TaskScheduler.Current);
        }
        
        /// <summary>
        /// Creates a cancellation-only execution context
        /// </summary>
        /// <returns>A new execution context with just a cancellation token source</returns>
        public static ExecutionContext WithCancellation()
        {
            return new ExecutionContext(new CancellationTokenSource());
        }
        
        /// <summary>
        /// Creates a new execution context for testing
        /// </summary>
        /// <returns>A test execution context</returns>
        public static ExecutionContext ForTesting()
        {
            return new ExecutionContext(
                new CancellationTokenSource(),
                null,
                Thread.CurrentThread.ManagedThreadId,
                TaskScheduler.Current,
                true);
        }
    }
}