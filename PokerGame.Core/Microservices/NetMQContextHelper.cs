using System;
using System.Threading;
using NetMQ;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Helper class for managing NetMQ context lifetime and cleanup
    /// </summary>
    public static class NetMQContextHelper
    {
        private static readonly object _lockObject = new object();
        private static bool _cleanupScheduled = false;
        private static bool _cleanupComplete = false;
        private static readonly Timer? _cleanupTimer;
        
        /// <summary>
        /// Static constructor to initialize the context helper
        /// </summary>
        static NetMQContextHelper()
        {
            // Create a timer that will perform cleanup if it hasn't happened by application exit
            _cleanupTimer = new Timer(_ => PerformCleanup(), null, Timeout.Infinite, Timeout.Infinite);
            
            // Register for process exit to ensure cleanup happens
            AppDomain.CurrentDomain.ProcessExit += (s, e) => {
                Console.WriteLine("Process exit detected - performing NetMQ cleanup");
                PerformCleanup();
            };
        }
        
        /// <summary>
        /// Schedules the cleanup to occur after giving time for sockets to close
        /// </summary>
        /// <param name="delayMs">The delay in milliseconds before cleanup</param>
        public static void ScheduleCleanup(int delayMs = 100)
        {
            lock (_lockObject)
            {
                if (_cleanupScheduled || _cleanupComplete)
                    return;
                
                _cleanupScheduled = true;
                Console.WriteLine($"NetMQ cleanup scheduled to occur in {delayMs}ms");
                
                // Schedule the cleanup to happen after a delay
                _cleanupTimer?.Change(delayMs, Timeout.Infinite);
            }
        }
        
        /// <summary>
        /// Performs the actual NetMQ context cleanup
        /// </summary>
        private static void PerformCleanup()
        {
            lock (_lockObject)
            {
                if (_cleanupComplete)
                    return;
                
                try
                {
                    Console.WriteLine("Performing NetMQ context cleanup");
                    
                    // Set a flag to indicate cleanup is happening
                    _cleanupComplete = true;
                    
                    // Terminate the NetMQ context - this will affect all running sockets
                    NetMQConfig.Cleanup(false);
                    Console.WriteLine("NetMQ context cleanup completed successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during NetMQ cleanup: {ex.Message}");
                }
                finally
                {
                    // Dispose the timer
                    _cleanupTimer?.Dispose();
                }
            }
        }
    }
}