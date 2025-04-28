using System.Threading.Tasks;
using PokerGame.Core.Microservices;
using PokerGame.Core.Models;

namespace PokerGame.Services 
{
    /// <summary>
    /// Interface wrapper for the game engine service, which is used by the telemetry decorator
    /// </summary>
    public interface IGameEngineService : PokerGame.Abstractions.IGameEngineService
    {
        // This interface inherits all the members from the core interface
        // and is used to avoid circular references between projects
    }
}