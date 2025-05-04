using System;
using System.Threading;
using System.Threading.Tasks;
using MSA.Foundation.ServiceManagement;

namespace PokerGame.Core.ServiceManagement
{
    /// <summary>
    /// Implementation of IShutdownParticipant to work with MSA.Foundation.ServiceManagement.ShutdownCoordinator
    /// </summary>
    public class ShutdownParticipant : IShutdownParticipant
    {
        private readonly string _participantId;
        private readonly int _shutdownPriority;
        private readonly Func<CancellationToken, Task> _shutdownFunc;
        
        /// <summary>
        /// Gets the unique identifier for this shutdown participant
        /// </summary>
        public string ParticipantId => _participantId;
        
        /// <summary>
        /// Gets the priority of this participant in the shutdown sequence
        /// Lower values indicate higher priority (shutdown earlier in the sequence)
        /// </summary>
        public int ShutdownPriority => _shutdownPriority;
        
        /// <summary>
        /// Initializes a new instance of the ShutdownParticipant class
        /// </summary>
        /// <param name="participantId">The unique identifier for this participant</param>
        /// <param name="shutdownPriority">The priority (lower numbers are higher priority)</param>
        /// <param name="shutdownFunc">The function to execute during shutdown</param>
        public ShutdownParticipant(
            string participantId,
            int shutdownPriority,
            Func<CancellationToken, Task> shutdownFunc)
        {
            _participantId = participantId ?? throw new ArgumentNullException(nameof(participantId));
            _shutdownPriority = shutdownPriority;
            _shutdownFunc = shutdownFunc ?? throw new ArgumentNullException(nameof(shutdownFunc));
        }
        
        /// <summary>
        /// Performs the shutdown operation for this participant
        /// </summary>
        /// <param name="token">A token to monitor for cancellation requests</param>
        /// <returns>A task representing the asynchronous shutdown operation</returns>
        public async Task ShutdownAsync(CancellationToken token)
        {
            try
            {
                await _shutdownFunc(token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ShutdownParticipant '{_participantId}': Error during shutdown: {ex.Message}");
            }
        }
    }
}