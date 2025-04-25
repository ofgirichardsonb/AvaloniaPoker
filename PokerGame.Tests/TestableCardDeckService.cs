using System.Threading.Tasks;
using PokerGame.Core.Microservices;
using PokerGame.Core.Models;

namespace PokerGame.Tests
{
    /// <summary>
    /// A testable version of CardDeckService that exposes protected members for testing purposes
    /// </summary>
    public class TestableCardDeckService : CardDeckService
    {
        public TestableCardDeckService(int publisherPort, int subscriberPort) 
            : base(publisherPort, subscriberPort)
        {
        }
        
        // Expose the protected methods for testing
        public Task HandleMessageAsyncPublic(Message message)
        {
            return base.HandleMessageAsync(message);
        }
        
        public new void Broadcast(Message message)
        {
            base.Broadcast(message);
        }
        
        public new void SendTo(Message message, string receiverId)
        {
            base.SendTo(message, receiverId);
        }
    }
}