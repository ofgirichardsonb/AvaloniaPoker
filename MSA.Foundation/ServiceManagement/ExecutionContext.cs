using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MSA.Foundation.ServiceManagement
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
        private readonly Dictionary<string, object> _metadata = new Dictionary<string, object>();
        
        /// <summary>
        /// Gets the cancellation token for this execution context
        /// </summary>
        public CancellationToken CancellationToken => _cts.Token;
        
        /// <summary>
        /// Gets the cancellation token source for this execution context
        /// </summary>
        public CancellationTokenSource CancellationTokenSource => _cts;
        
        /// <summary>
        /// Gets the thread ID for this execution context
        /// </summary>
        public int ThreadId => _thread.ManagedThreadId;
        
        /// <summary>
        /// Gets the service ID for this execution context
        /// </summary>
        public string ServiceId { get; }
        
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
            ServiceId = Guid.NewGuid().ToString();
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionContext"/> class with a specific service ID
        /// </summary>
        /// <param name="serviceId">The service ID to use for this execution context</param>
        public ExecutionContext(string serviceId)
        {
            _cts = new CancellationTokenSource();
            _thread = Thread.CurrentThread;
            IsRunning = true;
            ServiceId = serviceId;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionContext"/> class with a specific service ID and cancellation token
        /// </summary>
        /// <param name="serviceId">The service ID to use for this execution context</param>
        /// <param name="cancellationToken">The cancellation token to use for this execution context</param>
        public ExecutionContext(string serviceId, CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _thread = Thread.CurrentThread;
            IsRunning = true;
            ServiceId = serviceId;
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
            ServiceId = Guid.NewGuid().ToString();
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
        /// Cancels the execution context
        /// </summary>
        public void Cancel()
        {
            _cts.Cancel();
        }
        
        /// <summary>
        /// Creates a new execution context with a timeout
        /// </summary>
        /// <param name="timeout">The timeout period</param>
        /// <returns>A new execution context with the specified timeout</returns>
        public ExecutionContext WithTimeout(TimeSpan timeout)
        {
            var context = new ExecutionContext(ServiceId);
            context._cts.CancelAfter(timeout);
            return context;
        }
        
        /// <summary>
        /// Gets all metadata for this execution context
        /// </summary>
        /// <returns>A dictionary containing all metadata</returns>
        public Dictionary<string, object> GetMetadata()
        {
            lock (_lock)
            {
                return new Dictionary<string, object>(_metadata);
            }
        }
        
        /// <summary>
        /// Gets metadata for a specific key
        /// </summary>
        /// <typeparam name="T">The type of the metadata value</typeparam>
        /// <param name="key">The key to get metadata for</param>
        /// <returns>The metadata value for the specified key</returns>
        public T GetMetadata<T>(string key)
        {
            lock (_lock)
            {
                if (!_metadata.ContainsKey(key))
                {
                    return default;
                }
                
                return (T)_metadata[key];
            }
        }
        
        /// <summary>
        /// Sets metadata for a specific key
        /// </summary>
        /// <param name="key">The key to set metadata for</param>
        /// <param name="value">The metadata value to set</param>
        public void SetMetadata(string key, object value)
        {
            lock (_lock)
            {
                _metadata[key] = value;
            }
        }
        
        /// <summary>
        /// Removes metadata for a specific key
        /// </summary>
        /// <param name="key">The key to remove metadata for</param>
        public void RemoveMetadata(string key)
        {
            lock (_lock)
            {
                if (_metadata.ContainsKey(key))
                {
                    _metadata.Remove(key);
                }
            }
        }
        
        /// <summary>
        /// Clears all metadata
        /// </summary>
        public void ClearMetadata()
        {
            lock (_lock)
            {
                _metadata.Clear();
            }
        }
        
        /// <summary>
        /// Disposes resources used by the execution context
        /// </summary>
        public void Dispose()
        {
            Stop();
            _cts.Dispose();
        }
        
        /// <summary>
        /// Creates a cancellation-only execution context
        /// </summary>
        /// <returns>A new execution context with just a cancellation token source</returns>
        public static ExecutionContext WithCancellation()
        {
            return new ExecutionContext(Guid.NewGuid().ToString());
        }
    }
}