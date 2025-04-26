using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights.DataContracts;
using PokerGame.Core.Models;
using PokerGame.Core.Game;

namespace PokerGame.Core.Telemetry
{
    /// <summary>
    /// Provides telemetry extensions specific to poker game events
    /// </summary>
    public static class GameTelemetry
    {
        /// <summary>
        /// Tracks a game start event
        /// </summary>
        /// <param name="telemetryService">The telemetry service</param>
        /// <param name="gameId">The ID of the game</param>
        /// <param name="playerCount">The number of players</param>
        /// <param name="startingChips">The starting chips for each player</param>
        public static void TrackGameStart(this TelemetryService telemetryService, string gameId, int playerCount, int startingChips)
        {
            var properties = new Dictionary<string, string>
            {
                { "GameId", gameId },
                { "PlayerCount", playerCount.ToString() },
                { "StartingChips", startingChips.ToString() }
            };
            
            telemetryService.TrackEvent("GameStart", properties);
        }
        
        /// <summary>
        /// Tracks a game end event
        /// </summary>
        /// <param name="telemetryService">The telemetry service</param>
        /// <param name="gameId">The ID of the game</param>
        /// <param name="durationMinutes">The duration of the game in minutes</param>
        /// <param name="winnerId">The ID of the winning player</param>
        public static void TrackGameEnd(this TelemetryService telemetryService, string gameId, double durationMinutes, string winnerId)
        {
            var properties = new Dictionary<string, string>
            {
                { "GameId", gameId },
                { "DurationMinutes", durationMinutes.ToString("F2") },
                { "WinnerId", winnerId }
            };
            
            telemetryService.TrackEvent("GameEnd", properties);
            telemetryService.TrackMetric("GameDuration", durationMinutes);
        }
        
        /// <summary>
        /// Tracks a hand start event
        /// </summary>
        /// <param name="telemetryService">The telemetry service</param>
        /// <param name="gameId">The ID of the game</param>
        /// <param name="handId">The ID of the hand</param>
        /// <param name="dealerId">The ID of the dealer</param>
        /// <param name="smallBlindAmount">The small blind amount</param>
        /// <param name="bigBlindAmount">The big blind amount</param>
        public static void TrackHandStart(this TelemetryService telemetryService, string gameId, string handId, string dealerId, int smallBlindAmount, int bigBlindAmount)
        {
            var properties = new Dictionary<string, string>
            {
                { "GameId", gameId },
                { "HandId", handId },
                { "DealerId", dealerId },
                { "SmallBlind", smallBlindAmount.ToString() },
                { "BigBlind", bigBlindAmount.ToString() }
            };
            
            telemetryService.TrackEvent("HandStart", properties);
        }
        
        /// <summary>
        /// Tracks a hand end event
        /// </summary>
        /// <param name="telemetryService">The telemetry service</param>
        /// <param name="gameId">The ID of the game</param>
        /// <param name="handId">The ID of the hand</param>
        /// <param name="winnerId">The ID of the winning player</param>
        /// <param name="potAmount">The final pot amount</param>
        /// <param name="showdown">Whether the hand went to showdown</param>
        /// <param name="winningHand">The winning hand type</param>
        public static void TrackHandEnd(this TelemetryService telemetryService, string gameId, string handId, string winnerId, int potAmount, bool showdown, string winningHand = "")
        {
            var properties = new Dictionary<string, string>
            {
                { "GameId", gameId },
                { "HandId", handId },
                { "WinnerId", winnerId },
                { "PotAmount", potAmount.ToString() },
                { "Showdown", showdown.ToString() }
            };
            
            if (!string.IsNullOrEmpty(winningHand))
            {
                properties.Add("WinningHand", winningHand);
            }
            
            telemetryService.TrackEvent("HandEnd", properties);
            telemetryService.TrackMetric("PotSize", potAmount);
        }
        
        /// <summary>
        /// Tracks a player action
        /// </summary>
        /// <param name="telemetryService">The telemetry service</param>
        /// <param name="gameId">The ID of the game</param>
        /// <param name="handId">The ID of the hand</param>
        /// <param name="playerId">The ID of the player</param>
        /// <param name="action">The action taken</param>
        /// <param name="amount">The amount of the action (if applicable)</param>
        public static void TrackPlayerAction(this TelemetryService telemetryService, string gameId, string handId, string playerId, string action, int amount = 0)
        {
            var properties = new Dictionary<string, string>
            {
                { "GameId", gameId },
                { "HandId", handId },
                { "PlayerId", playerId },
                { "Action", action }
            };
            
            if (amount > 0)
            {
                properties.Add("Amount", amount.ToString());
            }
            
            telemetryService.TrackEvent("PlayerAction", properties);
        }
        
        /// <summary>
        /// Tracks a player joining a game
        /// </summary>
        /// <param name="telemetryService">The telemetry service</param>
        /// <param name="gameId">The ID of the game</param>
        /// <param name="playerId">The ID of the player</param>
        /// <param name="playerName">The name of the player</param>
        /// <param name="startingChips">The starting chips for the player</param>
        public static void TrackPlayerJoin(this TelemetryService telemetryService, string gameId, string playerId, string playerName, int startingChips)
        {
            var properties = new Dictionary<string, string>
            {
                { "GameId", gameId },
                { "PlayerId", playerId },
                { "PlayerName", playerName },
                { "StartingChips", startingChips.ToString() }
            };
            
            telemetryService.TrackEvent("PlayerJoin", properties);
        }
        
        /// <summary>
        /// Tracks a player leaving a game
        /// </summary>
        /// <param name="telemetryService">The telemetry service</param>
        /// <param name="gameId">The ID of the game</param>
        /// <param name="playerId">The ID of the player</param>
        /// <param name="playerName">The name of the player</param>
        /// <param name="endingChips">The ending chips for the player</param>
        /// <param name="reason">The reason for leaving</param>
        public static void TrackPlayerLeave(this TelemetryService telemetryService, string gameId, string playerId, string playerName, int endingChips, string reason = "")
        {
            var properties = new Dictionary<string, string>
            {
                { "GameId", gameId },
                { "PlayerId", playerId },
                { "PlayerName", playerName },
                { "EndingChips", endingChips.ToString() }
            };
            
            if (!string.IsNullOrEmpty(reason))
            {
                properties.Add("Reason", reason);
            }
            
            telemetryService.TrackEvent("PlayerLeave", properties);
        }
        
        /// <summary>
        /// Tracks a service operation
        /// </summary>
        /// <param name="telemetryService">The telemetry service</param>
        /// <param name="serviceName">The name of the service</param>
        /// <param name="serviceId">The ID of the service</param>
        /// <param name="operationName">The name of the operation</param>
        /// <param name="duration">The duration of the operation</param>
        /// <param name="success">Whether the operation was successful</param>
        /// <param name="additionalProperties">Additional properties for the operation</param>
        public static void TrackServiceOperation(this TelemetryService telemetryService, string serviceName, string serviceId, string operationName, TimeSpan duration, bool success, Dictionary<string, string>? additionalProperties = null)
        {
            var properties = new Dictionary<string, string>
            {
                { "ServiceName", serviceName },
                { "ServiceId", serviceId },
                { "OperationName", operationName },
                { "DurationMs", duration.TotalMilliseconds.ToString("F2") },
                { "Success", success.ToString() }
            };
            
            if (additionalProperties != null)
            {
                foreach (var kvp in additionalProperties)
                {
                    properties[kvp.Key] = kvp.Value;
                }
            }
            
            telemetryService.TrackEvent("ServiceOperation", properties);
            telemetryService.TrackMetric($"Operation.{operationName}.Duration", duration.TotalMilliseconds);
        }
        
        /// <summary>
        /// Tracks a messaging operation
        /// </summary>
        /// <param name="telemetryService">The telemetry service</param>
        /// <param name="messageId">The ID of the message</param>
        /// <param name="messageType">The type of message</param>
        /// <param name="senderId">The ID of the sender</param>
        /// <param name="receiverId">The ID of the receiver</param>
        /// <param name="status">The status of the message</param>
        /// <param name="durationMs">The duration of the operation in milliseconds</param>
        public static void TrackMessaging(this TelemetryService telemetryService, string messageId, string messageType, string senderId, string receiverId, string status, double durationMs = 0)
        {
            var properties = new Dictionary<string, string>
            {
                { "MessageId", messageId },
                { "MessageType", messageType },
                { "SenderId", senderId },
                { "ReceiverId", receiverId },
                { "Status", status }
            };
            
            if (durationMs > 0)
            {
                properties.Add("DurationMs", durationMs.ToString("F2"));
            }
            
            telemetryService.TrackEvent("Messaging", properties);
            
            if (durationMs > 0)
            {
                telemetryService.TrackMetric("MessageLatency", durationMs);
            }
        }
        
        /// <summary>
        /// Tracks a broker operation
        /// </summary>
        /// <param name="telemetryService">The telemetry service</param>
        /// <param name="brokerId">The ID of the broker</param>
        /// <param name="operationName">The name of the operation</param>
        /// <param name="success">Whether the operation was successful</param>
        /// <param name="serviceCount">The number of services connected to the broker</param>
        /// <param name="additionalProperties">Additional properties for the operation</param>
        public static void TrackBrokerOperation(this TelemetryService telemetryService, string brokerId, string operationName, bool success, int serviceCount = 0, Dictionary<string, string>? additionalProperties = null)
        {
            var properties = new Dictionary<string, string>
            {
                { "BrokerId", brokerId },
                { "OperationName", operationName },
                { "Success", success.ToString() }
            };
            
            if (serviceCount > 0)
            {
                properties.Add("ServiceCount", serviceCount.ToString());
            }
            
            if (additionalProperties != null)
            {
                foreach (var kvp in additionalProperties)
                {
                    properties[kvp.Key] = kvp.Value;
                }
            }
            
            telemetryService.TrackEvent("BrokerOperation", properties);
            
            if (serviceCount > 0)
            {
                telemetryService.TrackMetric("ConnectedServices", serviceCount);
            }
        }
    }
}