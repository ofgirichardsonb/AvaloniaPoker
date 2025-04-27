using System;
using System.Threading;
using System.Threading.Tasks;

namespace PokerGame.Foundation.ServiceManagement
{
    /// <summary>
    /// Execution context for managing microservice execution
    /// </summary>
    public class ExecutionContext : IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private readonly Thread _thread;
        private readonly object _lock = new object();
        private bool _isRunning;
        
        /// <summary>
        /// Gets the cancellation token for this execution context
        /// </summary>
        public CancellationToken CancellationToken => _cts.Token;
        
        /// <summary>
        /// Gets the thread ID for this execution context
        /// </summary>
        public int ThreadId => _thread.ManagedThreadId;
        
        /// <summary>
        /// Gets whether this execution context is running
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    return _isRunning;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _isRunning = value;
                }
            }
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionContext"/> class
        /// </summary>
        public ExecutionContext()
        {
            _cts = new CancellationTokenSource();
            _thread = Thread.CurrentThread;
            IsRunning = true;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionContext"/> class with a specific thread
        /// </summary>
        /// <param name="thread">The thread to use for this execution context</param>
        public ExecutionContext(Thread thread)
        {
            _cts = new CancellationTokenSource();
            _thread = thread;
            IsRunning = true;
        }
        
        /// <summary>
        /// Runs an action in this execution context
        /// </summary>
        /// <param name="action">The action to run</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public Task RunAsync(Action action)
        {
            return Task.Run(() =>
            {
                if (!IsRunning)
                {
                    throw new InvalidOperationException("Execution context is not running");
                }
                
                action();
            }, CancellationToken);
        }
        
        /// <summary>
        /// Runs a function in this execution context
        /// </summary>
        /// <typeparam name="T">The return type of the function</typeparam>
        /// <param name="func">The function to run</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public Task<T> RunAsync<T>(Func<T> func)
        {
            return Task.Run(() =>
            {
                if (!IsRunning)
                {
                    throw new InvalidOperationException("Execution context is not running");
                }
                
                return func();
            }, CancellationToken);
        }
        
        /// <summary>
        /// Stops this execution context
        /// </summary>
        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }
            
            IsRunning = false;
            _cts.Cancel();
        }
        
        /// <summary>
        /// Disposes resources used by the execution context
        /// </summary>
        public void Dispose()
        {
            Stop();
            _cts.Dispose();
        }
    }
}