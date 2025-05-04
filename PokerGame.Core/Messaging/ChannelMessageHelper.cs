using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.ServiceManagement;

namespace PokerGame.Core.Messaging
{
    /// <summary>
    /// Helper class for managing channel-based messaging components
    /// </summary>
    public static class ChannelMessageHelper
    {
        private static readonly ConcurrentDictionary<string, object> _resources = 
            new ConcurrentDictionary<string, object>();
            
        /// <summary>
        /// Gets the in-process address for channel-based messaging
        /// </summary>
        public static string InProcessAddress => ChannelMessageTransport.InProcessAddress;
        
        /// <summary>
        /// Registers a resource to be tracked
        /// </summary>
        /// <param name="resourceId">A unique identifier for the resource</param>
        /// <param name="resource">The resource object</param>
        public static void RegisterResource(string resourceId, object resource)
        {
            if (string.IsNullOrEmpty(resourceId))
                throw new ArgumentException("Resource ID cannot be null or empty", nameof(resourceId));
                
            if (resource == null)
                throw new ArgumentNullException(nameof(resource));
                
            _resources.TryAdd(resourceId, resource);
            Console.WriteLine($"ChannelMessageHelper: Registered resource {resourceId}");
        }
        
        /// <summary>
        /// Unregisters a tracked resource
        /// </summary>
        /// <param name="resourceId">The ID of the resource to unregister</param>
        /// <returns>True if the resource was found and unregistered; otherwise, false</returns>
        public static bool UnregisterResource(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId))
                return false;
                
            if (_resources.TryRemove(resourceId, out _))
            {
                Console.WriteLine($"ChannelMessageHelper: Unregistered resource {resourceId}");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Creates a new ChannelMessageTransport for a service
        /// </summary>
        /// <param name="serviceId">The ID of the service</param>
        /// <returns>A new message transport</returns>
        public static ChannelMessageTransport CreateTransport(string serviceId)
        {
            if (string.IsNullOrEmpty(serviceId))
                throw new ArgumentException("Service ID cannot be null or empty", nameof(serviceId));
                
            var transport = new ChannelMessageTransport(serviceId);
            RegisterResource($"transport:{serviceId}", transport);
            return transport;
        }
        
        /// <summary>
        /// Schedules cleanup of channel resources
        /// </summary>
        public static Task CleanupAsync()
        {
            Console.WriteLine("ChannelMessageHelper: Cleaning up channel messaging resources");
            
            // Create a list of cleanup tasks
            var cleanupTasks = new List<Task>();
            
            // Clean up each resource that implements IDisposable
            foreach (var resource in _resources.Values)
            {
                if (resource is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ChannelMessageHelper: Error disposing resource: {ex.Message}");
                    }
                }
                else if (resource is IAsyncDisposable asyncDisposable)
                {
                    // Handle async disposable resources
                    cleanupTasks.Add(Task.Run(async () => 
                    {
                        try
                        {
                            await asyncDisposable.DisposeAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"ChannelMessageHelper: Error async disposing resource: {ex.Message}");
                        }
                    }));
                }
            }
            
            // Clear the resources dictionary
            _resources.Clear();
            
            // Wait for all async cleanup tasks to complete
            if (cleanupTasks.Count > 0)
            {
                return Task.WhenAll(cleanupTasks);
            }
            
            return Task.CompletedTask;
        }
    }
}