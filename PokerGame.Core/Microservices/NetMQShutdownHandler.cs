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
        public int ShutdownPriority => 1000;
        
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
                    
                _trackedResources.Add(resource);
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
                    
                return _trackedResources.Remove(resource);
            }
        }
        
        /// <summary>
        /// Called when the process exits to perform cleanup
        /// </summary>
        private void OnProcessExit(object sender, EventArgs e)
        {
            Console.WriteLine("Process exit detected - performing NetMQ cleanup");
            PerformCleanup();
        }
        
        /// <summary>
        /// Called when Ctrl+C is pressed to perform cleanup
        /// </summary>
        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Cancel key press detected - performing NetMQ cleanup");
            e.Cancel = true; // Prevent the process from terminating immediately
            PerformCleanup();
        }
        
        /// <summary>
        /// Called by the ShutdownCoordinator to perform cleanup
        /// </summary>
        public async Task ShutdownAsync(CancellationToken token)
        {
            Console.WriteLine("NetMQShutdownHandler: Performing coordinated shutdown of NetMQ resources");
            
            // Dispose all tracked resources in reverse order (LIFO)
            List<IDisposable> resources;
            lock (_lock)
            {
                resources = new List<IDisposable>(_trackedResources);
                resources.Reverse();
                _trackedResources.Clear();
            }
            
            foreach (var resource in resources)
            {
                // Check cancellation token
                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("NetMQShutdownHandler: Shutdown canceled");
                    break;
                }
                
                try
                {
                    // If this takes too long, the token will be canceled
                    Console.WriteLine($"NetMQShutdownHandler: Disposing resource of type {resource.GetType().Name}");
                    resource.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"NetMQShutdownHandler: Error disposing resource: {ex.Message}");
                }
            }
            
            // Wait a bit before final NetMQ cleanup
            try
            {
                await Task.Delay(100, token);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            
            // Perform final NetMQ cleanup
            try
            {
                Console.WriteLine("NetMQShutdownHandler: Performing NetMQContext.Cleanup");
                NetMQConfig.Cleanup(false);
                Console.WriteLine("NetMQShutdownHandler: NetMQContext cleanup completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NetMQShutdownHandler: Error during NetMQContext cleanup: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Performs immediate cleanup of all tracked resources
        /// </summary>
        private void PerformCleanup()
        {
            // This is the synchronous version used by event handlers
            if (_isDisposed)
                return;
                
            Console.WriteLine("NetMQShutdownHandler: Performing immediate cleanup of NetMQ resources");
            
            List<IDisposable> resources;
            lock (_lock)
            {
                _isDisposed = true;
                resources = new List<IDisposable>(_trackedResources);
                resources.Reverse();
                _trackedResources.Clear();
            }
            
            foreach (var resource in resources)
            {
                try
                {
                    Console.WriteLine($"NetMQShutdownHandler: Disposing resource of type {resource.GetType().Name}");
                    resource.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"NetMQShutdownHandler: Error disposing resource: {ex.Message}");
                }
            }
            
            // Perform final NetMQ cleanup
            try
            {
                Console.WriteLine("NetMQShutdownHandler: Performing NetMQContext.Cleanup");
                NetMQConfig.Cleanup(false);
                Console.WriteLine("NetMQShutdownHandler: NetMQContext cleanup completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NetMQShutdownHandler: Error during NetMQContext cleanup: {ex.Message}");
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
        
        /// <summary>
        /// Disposes resources used by the NetMQShutdownHandler
        /// </summary>
        public void Dispose()
        {
            PerformCleanup();
        }
    }
}