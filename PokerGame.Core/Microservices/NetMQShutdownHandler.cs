using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.ServiceManagement;
using PokerGame.Core.Messaging;
using NetMQ;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Handles the safe shutdown of NetMQ resources by coordinating with the global ShutdownCoordinator
    /// </summary>
    public class NetMQShutdownHandler : IShutdownParticipant, IDisposable
    {
        private static readonly Lazy<NetMQShutdownHandler> _instance = 
            new Lazy<NetMQShutdownHandler>(() => new NetMQShutdownHandler());
            
        private readonly List<IDisposable> _trackedResources = new List<IDisposable>();
        private readonly object _lock = new object();
        private bool _isDisposed = false;
        private bool _isShuttingDown = false;
        private readonly SemaphoreSlim _shutdownSemaphore = new SemaphoreSlim(1, 1);
        
        /// <summary>
        /// Gets the singleton instance of the NetMQShutdownHandler
        /// </summary>
        public static NetMQShutdownHandler Instance => _instance.Value;
        
        /// <summary>
        /// Gets the ID of this shutdown participant
        /// </summary>
        public string ParticipantId => "NetMQShutdownHandler";
        
        /// <summary>
        /// Gets the priority of this participant in the shutdown sequence
        /// NetMQ resources should be closed as the last step in the shutdown sequence
        /// </summary>
        public int ShutdownPriority => 1000; // Higher numbers execute later in the sequence
        
        /// <summary>
        /// Creates a new NetMQShutdownHandler and registers it with the global ShutdownCoordinator
        /// </summary>
        private NetMQShutdownHandler()
        {
            // Register with the ShutdownCoordinator
            ShutdownCoordinator.Instance.RegisterParticipant(this);
            
            // Register AppDomain exit handlers to ensure cleanup even if the coordinator isn't used
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.CancelKeyPress += OnCancelKeyPress;
            
            Console.WriteLine("NetMQShutdownHandler initialized and registered with ShutdownCoordinator");
        }
        
        /// <summary>
        /// Tracks a NetMQ resource to be cleaned up during shutdown
        /// </summary>
        /// <param name="resource">The resource to track</param>
        public void TrackResource(IDisposable resource)
        {
            if (resource == null)
                throw new ArgumentNullException(nameof(resource));
                
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(NetMQShutdownHandler));
                
                if (_isShuttingDown)
                {
                    Console.WriteLine($"Warning: Attempted to track resource during shutdown: {resource.GetType().Name}");
                    return;
                }
                    
                _trackedResources.Add(resource);
                Console.WriteLine($"NetMQShutdownHandler: Tracking resource of type {resource.GetType().Name}");
            }
        }
        
        /// <summary>
        /// Untracks a previously tracked resource
        /// </summary>
        /// <param name="resource">The resource to untrack</param>
        /// <returns>True if the resource was found and untracked; otherwise, false</returns>
        public bool UntrackResource(IDisposable resource)
        {
            if (resource == null)
                throw new ArgumentNullException(nameof(resource));
                
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(NetMQShutdownHandler));
                
                if (_isShuttingDown)
                {
                    Console.WriteLine($"Warning: Attempted to untrack resource during shutdown: {resource.GetType().Name}");
                    return false;
                }
                    
                var result = _trackedResources.Remove(resource);
                if (result)
                {
                    Console.WriteLine($"NetMQShutdownHandler: Untracked resource of type {resource.GetType().Name}");
                }
                return result;
            }
        }
        
        /// <summary>
        /// Called when the process exits to perform cleanup
        /// </summary>
        private void OnProcessExit(object sender, EventArgs e)
        {
            Console.WriteLine("Process exit detected - performing NetMQ cleanup");
            PerformSafeCleanup().Wait(TimeSpan.FromSeconds(2)); // Wait with timeout
        }
        
        /// <summary>
        /// Called when Ctrl+C is pressed to perform cleanup
        /// </summary>
        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Cancel key press detected - performing NetMQ cleanup");
            e.Cancel = true; // Prevent the process from terminating immediately
            PerformSafeCleanup().Wait(TimeSpan.FromSeconds(2)); // Wait with timeout
        }
        
        /// <summary>
        /// Called by the ShutdownCoordinator to perform cleanup
        /// </summary>
        public async Task ShutdownAsync(CancellationToken token)
        {
            Console.WriteLine("NetMQShutdownHandler: Performing coordinated shutdown of NetMQ resources");
            
            await PerformSafeCleanup();
        }
        
        /// <summary>
        /// Safely coordinates cleanup with other shutdown processes
        /// </summary>
        private async Task PerformSafeCleanup()
        {
            // Use semaphore to ensure only one cleanup process runs at a time
            bool acquired = false;
            try
            {
                acquired = await _shutdownSemaphore.WaitAsync(TimeSpan.FromSeconds(10));
                if (!acquired)
                {
                    Console.WriteLine("NetMQShutdownHandler: Timed out waiting for shutdown semaphore - another shutdown is in progress");
                    return;
                }
                
                if (_isDisposed)
                {
                    Console.WriteLine("NetMQShutdownHandler: Already disposed, skipping redundant cleanup");
                    return;
                }
                
                // Set flags to prevent new resources from being tracked during shutdown
                lock (_lock)
                {
                    if (_isShuttingDown)
                    {
                        Console.WriteLine("NetMQShutdownHandler: Shutdown already in progress");
                        return;
                    }
                    
                    _isShuttingDown = true;
                }
                
                await PerformCleanupAsync();
                
                // Mark as fully disposed
                lock (_lock)
                {
                    _isDisposed = true;
                }
                
                // Unregister event handlers
                try
                {
                    AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
                    Console.CancelKeyPress -= OnCancelKeyPress;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"NetMQShutdownHandler: Error unregistering event handlers: {ex.Message}");
                }
            }
            finally
            {
                if (acquired)
                {
                    _shutdownSemaphore.Release();
                }
            }
        }
        
        /// <summary>
        /// Performs the actual cleanup work
        /// </summary>
        private async Task PerformCleanupAsync()
        {
            Console.WriteLine("NetMQShutdownHandler: Performing cleanup of NetMQ resources");
            
            // Step 1: Get a snapshot of tracked resources
            List<IDisposable> resources;
            lock (_lock)
            {
                resources = new List<IDisposable>(_trackedResources);
                resources.Reverse(); // LIFO order for cleanup
                _trackedResources.Clear();
            }
            
            // Step 2: Dispose each resource with small delays in between
            foreach (var resource in resources)
            {
                try
                {
                    var resourceType = resource.GetType().Name;
                    Console.WriteLine($"Process exit - closing {resourceType}");
                    
                    // Special handling for NetMQ sockets to ensure proper cleanup
                    if (resourceType.Contains("Socket"))
                    {
                        // Allow time for pending messages to process before closing
                        Console.WriteLine($"Process exit - allowing pending messages to complete for {resourceType}");
                        await Task.Delay(50);
                    }
                    
                    resource.Dispose();
                    Console.WriteLine($"Process exit - {resourceType} disposed successfully");
                    
                    // Small delay between resource disposals to allow for proper sequencing
                    await Task.Delay(10);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Process exit - error disposing resource: {ex.Message}");
                }
            }
            
            // Step 3: Wait before final NetMQ cleanup to allow pending operations to complete
            try
            {
                Console.WriteLine("Process exit - proceeding to NetMQ context cleanup");
                await Task.Delay(100);
                
                Console.WriteLine("Process exit - performing graceful NetMQ context cleanup");
                // Use the false parameter to avoid terminating the context (less aggressive)
                NetMQConfig.Cleanup(false);
                Console.WriteLine("Process exit - graceful NetMQ context cleanup successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Process exit - error during NetMQ cleanup: {ex.Message}");
                
                try
                {
                    // If gentle cleanup failed, try a more aggressive approach as a last resort
                    Console.WriteLine("Process exit - attempting aggressive NetMQ cleanup");
                    await Task.Delay(200); // Give additional time
                    NetMQConfig.Cleanup(true);
                    Console.WriteLine("Process exit - aggressive NetMQ cleanup completed");
                }
                catch (Exception innerEx)
                {
                    Console.WriteLine($"Process exit - aggressive cleanup also failed: {innerEx.Message}");
                }
            }
            
            Console.WriteLine("Process exit NetMQ cleanup sequence completed");
        }
        
        /// <summary>
        /// Disposes resources used by the NetMQShutdownHandler
        /// </summary>
        public void Dispose()
        {
            PerformSafeCleanup().Wait(TimeSpan.FromSeconds(3)); // Wait with timeout
            _shutdownSemaphore.Dispose();
        }
    }
}