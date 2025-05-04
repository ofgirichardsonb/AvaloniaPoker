using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MSA.Foundation.ServiceManagement
{
    /// <summary>
    /// Represents a component that can be registered with the ShutdownCoordinator
    /// </summary>
    public interface IShutdownParticipant
    {
        /// <summary>
        /// Gets the unique ID of this shutdown participant
        /// </summary>
        string ParticipantId { get; }
        
        /// <summary>
        /// Gets the priority of this participant in the shutdown sequence (lower values shut down first)
        /// </summary>
        int ShutdownPriority { get; }
        
        /// <summary>
        /// Called when the application is shutting down to allow the participant to clean up resources
        /// </summary>
        /// <param name="token">Cancellation token for the shutdown operation</param>
        /// <returns>A task representing the asynchronous shutdown operation</returns>
        Task ShutdownAsync(CancellationToken token);
    }
    
    /// <summary>
    /// Defines the phase of the shutdown process
    /// </summary>
    public enum ShutdownPhase
    {
        /// <summary>
        /// The application is still running normally
        /// </summary>
        Running,
        
        /// <summary>
        /// The shutdown sequence has been initiated
        /// </summary>
        ShuttingDown,
        
        /// <summary>
        /// All registered components have been shut down
        /// </summary>
        Completed
    }
    
    /// <summary>
    /// Coordinates the orderly shutdown of application components
    /// </summary>
    public class ShutdownCoordinator : IDisposable
    {
        private static readonly Lazy<ShutdownCoordinator> _instance = 
            new Lazy<ShutdownCoordinator>(() => new ShutdownCoordinator());
            
        private readonly object _lock = new object();
        private readonly List<IShutdownParticipant> _participants = new List<IShutdownParticipant>();
        private readonly CancellationTokenSource _shutdownCts = new CancellationTokenSource();
        private ShutdownPhase _currentPhase = ShutdownPhase.Running;
        private Task _shutdownTask = Task.CompletedTask;
        private bool _isDisposed = false;
        
        /// <summary>
        /// Gets the global instance of the ShutdownCoordinator
        /// </summary>
        public static ShutdownCoordinator Instance => _instance.Value;
        
        /// <summary>
        /// Gets the cancellation token that will be triggered when shutdown begins
        /// </summary>
        public CancellationToken ShutdownToken => _shutdownCts.Token;
        
        /// <summary>
        /// Gets the current phase of the shutdown process
        /// </summary>
        public ShutdownPhase CurrentPhase
        {
            get
            {
                lock (_lock)
                {
                    return _currentPhase;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _currentPhase = value;
                }
            }
        }
        
        /// <summary>
        /// Gets whether the shutdown process has been initiated
        /// </summary>
        public bool IsShuttingDown => CurrentPhase != ShutdownPhase.Running;
        
        /// <summary>
        /// Registers a component to participate in the coordinated shutdown process
        /// </summary>
        /// <param name="participant">The component to register</param>
        /// <exception cref="InvalidOperationException">Thrown if shutdown has already begun</exception>
        /// <exception cref="ArgumentException">Thrown if a participant with the same ID is already registered</exception>
        public void RegisterParticipant(IShutdownParticipant participant)
        {
            if (participant == null)
                throw new ArgumentNullException(nameof(participant));
                
            lock (_lock)
            {
                if (IsShuttingDown)
                    throw new InvalidOperationException("Cannot register new participants after shutdown has begun");
                    
                // Check if this participant is already registered
                if (_participants.Exists(p => p.ParticipantId == participant.ParticipantId))
                    throw new ArgumentException($"A participant with ID '{participant.ParticipantId}' is already registered");
                    
                _participants.Add(participant);
                
                // Sort by shutdown priority
                _participants.Sort((a, b) => a.ShutdownPriority.CompareTo(b.ShutdownPriority));
                
                Console.WriteLine($"Registered participant {participant.ParticipantId} with shutdown priority {participant.ShutdownPriority}");
            }
        }
        
        /// <summary>
        /// Unregisters a component from the shutdown process
        /// </summary>
        /// <param name="participantId">The ID of the component to unregister</param>
        /// <returns>True if the component was found and unregistered; otherwise, false</returns>
        /// <exception cref="InvalidOperationException">Thrown if shutdown has already begun</exception>
        public bool UnregisterParticipant(string participantId)
        {
            lock (_lock)
            {
                if (IsShuttingDown)
                    throw new InvalidOperationException("Cannot unregister participants after shutdown has begun");
                    
                int index = _participants.FindIndex(p => p.ParticipantId == participantId);
                if (index >= 0)
                {
                    _participants.RemoveAt(index);
                    Console.WriteLine($"Unregistered participant {participantId}");
                    return true;
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// Initiates the coordinated shutdown process
        /// </summary>
        /// <param name="reason">The reason for initiating shutdown</param>
        /// <param name="timeout">Optional timeout for the entire shutdown process</param>
        /// <returns>A task representing the asynchronous shutdown operation</returns>
        public Task InitiateShutdownAsync(string reason, TimeSpan? timeout = null)
        {
            lock (_lock)
            {
                if (IsShuttingDown)
                    return _shutdownTask;
                    
                Console.WriteLine($"Initiating application shutdown: {reason}");
                CurrentPhase = ShutdownPhase.ShuttingDown;
                
                // Signal components that shutdown has begun
                _shutdownCts.Cancel();
                
                // Create a task to coordinate the shutdown sequence
                _shutdownTask = Task.Run(async () =>
                {
                    try
                    {
                        // Make a copy of the participants list to avoid modification during enumeration
                        List<IShutdownParticipant> participants;
                        lock (_lock)
                        {
                            participants = new List<IShutdownParticipant>(_participants);
                        }
                        
                        // Create a new cancellation token source for the shutdown sequence
                        using (var cts = new CancellationTokenSource())
                        {
                            // Apply timeout if specified
                            if (timeout.HasValue)
                            {
                                cts.CancelAfter(timeout.Value);
                            }
                            
                            // Shut down components in order of priority
                            foreach (var participant in participants)
                            {
                                try
                                {
                                    Console.WriteLine($"Shutting down {participant.ParticipantId} (Priority: {participant.ShutdownPriority})...");
                                    
                                    // Give each component a limited time to shut down
                                    var componentTimeout = TimeSpan.FromSeconds(5);
                                    using (var componentCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token))
                                    {
                                        componentCts.CancelAfter(componentTimeout);
                                        
                                        var shutdownTask = participant.ShutdownAsync(componentCts.Token);
                                        await shutdownTask.ConfigureAwait(false);
                                        
                                        Console.WriteLine($"{participant.ParticipantId} shut down successfully");
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    Console.WriteLine($"Shutdown of {participant.ParticipantId} was canceled or timed out");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error shutting down {participant.ParticipantId}: {ex.Message}");
                                }
                            }
                        }
                    }
                    finally
                    {
                        lock (_lock)
                        {
                            CurrentPhase = ShutdownPhase.Completed;
                            Console.WriteLine("Shutdown sequence completed");
                        }
                    }
                });
                
                return _shutdownTask;
            }
        }
        
        /// <summary>
        /// Waits for the shutdown process to complete
        /// </summary>
        /// <param name="timeout">Optional timeout for waiting</param>
        /// <returns>True if shutdown completed within the timeout; otherwise, false</returns>
        public async Task<bool> WaitForShutdownCompletionAsync(TimeSpan? timeout = null)
        {
            Task shutdownTask;
            
            lock (_lock)
            {
                if (!IsShuttingDown)
                    return true;
                    
                shutdownTask = _shutdownTask;
            }
            
            if (timeout.HasValue)
            {
                var delayTask = Task.Delay(timeout.Value);
                var completedTask = await Task.WhenAny(shutdownTask, delayTask).ConfigureAwait(false);
                return completedTask == shutdownTask;
            }
            else
            {
                await shutdownTask.ConfigureAwait(false);
                return true;
            }
        }
        
        /// <summary>
        /// Creates a cancellation token that will be canceled when shutdown begins
        /// </summary>
        /// <returns>A cancellation token that will be canceled during shutdown</returns>
        public CancellationToken CreateLinkedShutdownToken()
        {
            return _shutdownCts.Token;
        }
        
        /// <summary>
        /// Creates a cancellation token source that will be canceled when shutdown begins
        /// </summary>
        /// <param name="token">Optional token to link with the shutdown token</param>
        /// <returns>A cancellation token source linked to the shutdown process</returns>
        public CancellationTokenSource CreateLinkedCancellationTokenSource(CancellationToken? token = null)
        {
            if (token.HasValue)
            {
                return CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token, token.Value);
            }
            else
            {
                return CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token);
            }
        }
        
        /// <summary>
        /// Static helper to initiate shutdown from anywhere in the application
        /// </summary>
        /// <param name="reason">The reason for initiating shutdown</param>
        /// <param name="timeout">Optional timeout for the entire shutdown process</param>
        /// <returns>A task representing the asynchronous shutdown operation</returns>
        public static Task BeginGlobalShutdownAsync(string reason, TimeSpan? timeout = null)
        {
            return Instance.InitiateShutdownAsync(reason, timeout);
        }
        
        /// <summary>
        /// Disposes resources used by the ShutdownCoordinator
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;
                
            _isDisposed = true;
            
            // If shutdown hasn't been initiated yet, do it now
            if (!IsShuttingDown)
            {
                InitiateShutdownAsync("Disposing ShutdownCoordinator");
            }
            
            // Wait for shutdown to complete with a reasonable timeout
            WaitForShutdownCompletionAsync(TimeSpan.FromSeconds(5)).Wait();
            
            // Clean up resources
            _shutdownCts.Dispose();
        }
    }
}