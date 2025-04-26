namespace PokerGame.Services
{
    /// <summary>
    /// Contains constants for telemetry events and metrics
    /// </summary>
    public static class TelemetryConstants
    {
        // Game Events
        public const string GameStarted = "GameStarted";
        public const string GameEnded = "GameEnded";
        public const string HandStarted = "HandStarted";
        public const string HandCompleted = "HandCompleted";
        public const string RoundStarted = "RoundStarted";
        public const string RoundCompleted = "RoundCompleted";
        public const string PlayerJoined = "PlayerJoined";
        public const string PlayerLeft = "PlayerLeft";
        public const string DeckCreated = "DeckCreated";
        public const string CardsDealt = "CardsDealt";
        public const string CardsBurned = "CardsBurned";
        
        // Player Actions
        public const string PlayerAction = "PlayerAction";
        public const string PlayerBet = "PlayerBet";
        public const string PlayerCall = "PlayerCall";
        public const string PlayerRaise = "PlayerRaise";
        public const string PlayerFold = "PlayerFold";
        public const string PlayerCheck = "PlayerCheck";
        public const string PlayerAllIn = "PlayerAllIn";
        
        // Service Events
        public const string ServiceStarted = "ServiceStarted";
        public const string ServiceStopped = "ServiceStopped";
        public const string ServiceError = "ServiceError";
        public const string ServiceWarning = "ServiceWarning";
        
        // Messaging Events
        public const string MessageSent = "MessageSent";
        public const string MessageReceived = "MessageReceived";
        public const string MessageTimeout = "MessageTimeout";
        public const string MessageRetry = "MessageRetry";
        public const string MessageAcknowledged = "MessageAcknowledged";
        
        // Metrics
        public const string HandDuration = "HandDuration";
        public const string RoundDuration = "RoundDuration";
        public const string GameDuration = "GameDuration";
        public const string MessageLatency = "MessageLatency";
        public const string PotSize = "PotSize";
        
        // Performance Metrics
        public const string MessageProcessingTime = "MessageProcessingTime";
        public const string ServiceDiscoveryTime = "ServiceDiscoveryTime";
        public const string HandEvaluationTime = "HandEvaluationTime";
        
        // Properties
        public const string GameId = "GameId";
        public const string HandId = "HandId";
        public const string PlayerId = "PlayerId";
        public const string ServiceId = "ServiceId";
        public const string MessageId = "MessageId";
        public const string MessageType = "MessageType";
        public const string PlayerCount = "PlayerCount";
        public const string ActionType = "ActionType";
        public const string Amount = "Amount";
        public const string Success = "Success";
        public const string ErrorMessage = "ErrorMessage";
        public const string DeckId = "DeckId";
    }
}