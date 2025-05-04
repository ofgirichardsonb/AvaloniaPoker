using System;
using System.Collections.Generic;
using System.Linq;
using PokerGame.Core.Models;
using PokerGame.Core.Game;

namespace PokerGame.Core.AI
{
    /// <summary>
    /// Represents an AI poker player that can make automated decisions
    /// </summary>
    public class AIPokerPlayer
    {
        private readonly Random _random = new Random();
        
        /// <summary>
        /// Determines the next action for the AI player
        /// </summary>
        /// <param name="player">The player model for the AI</param>
        /// <param name="gameEngine">The current game engine</param>
        /// <returns>A tuple containing the action and bet amount (for raises)</returns>
        public (string Action, int BetAmount) DetermineAction(Player player, PokerGameEngine gameEngine)
        {
            // Get the current game state
            PokerGame.Core.Game.GameState gameState = gameEngine.State;
            var communityCards = gameEngine.CommunityCards;
            var currentBet = gameEngine.CurrentBet;
            var pot = gameEngine.Pot;
            
            // If player can check (bet is matched), usually check
            if (player.CurrentBet == currentBet)
            {
                // 70% chance to check, 30% chance to raise
                if (_random.NextDouble() < 0.7)
                {
                    return ("check", 0);
                }
                else
                {
                    // Determine raise amount (between min raise and 1/3 of player's chips)
                    int minRaise = currentBet + gameEngine.BigBlind;
                    int maxRaise = Math.Min(currentBet + player.Chips, currentBet + player.Chips / 3);
                    int raiseAmount = CalculateRaiseAmount(minRaise, maxRaise, player.HoleCards, communityCards, gameState);
                    
                    return ("raise", raiseAmount);
                }
            }
            // If player needs to call
            else if (player.CurrentBet < currentBet)
            {
                int callAmount = currentBet - player.CurrentBet;
                
                // Evaluate hand strength to decide whether to call/raise/fold
                double handStrength = EvaluateHandStrength(player.HoleCards, communityCards, gameState);
                
                // Calculate pot odds (the ratio of the pot size to the cost of the call)
                double potOdds = (double)callAmount / (pot + callAmount);
                
                // If hand is strong
                if (handStrength > 0.7)
                {
                    // 60% chance to raise with a strong hand
                    if (_random.NextDouble() < 0.6)
                    {
                        int minRaise = currentBet + gameEngine.BigBlind;
                        int maxRaise = Math.Min(currentBet + player.Chips, currentBet + player.Chips / 2);
                        int raiseAmount = CalculateRaiseAmount(minRaise, maxRaise, player.HoleCards, communityCards, gameState);
                        
                        return ("raise", raiseAmount);
                    }
                    else
                    {
                        return ("call", 0);
                    }
                }
                // If hand is medium strength
                else if (handStrength > 0.4)
                {
                    // Call if the pot odds are favorable
                    if (handStrength > potOdds)
                    {
                        return ("call", 0);
                    }
                    else
                    {
                        // 20% chance to bluff and call anyway
                        if (_random.NextDouble() < 0.2)
                        {
                            return ("call", 0);
                        }
                        else
                        {
                            return ("fold", 0);
                        }
                    }
                }
                // If hand is weak
                else
                {
                    // Fold most of the time
                    if (_random.NextDouble() < 0.8)
                    {
                        return ("fold", 0);
                    }
                    // Sometimes bluff
                    else
                    {
                        // 15% chance for a bluff raise
                        if (_random.NextDouble() < 0.15)
                        {
                            int minRaise = currentBet + gameEngine.BigBlind;
                            int maxRaise = Math.Min(currentBet + player.Chips, currentBet + player.Chips / 3);
                            int raiseAmount = CalculateRaiseAmount(minRaise, maxRaise, player.HoleCards, communityCards, gameState);
                            
                            return ("raise", raiseAmount);
                        }
                        // 85% chance to just call as a bluff
                        else
                        {
                            return ("call", 0);
                        }
                    }
                }
            }
            
            // Default action if something unexpected happens
            return ("check", 0);
        }
        
        /// <summary>
        /// Calculates a raise amount for the AI player
        /// </summary>
        /// <param name="minRaise">The minimum raise amount</param>
        /// <param name="maxRaise">The maximum raise amount</param>
        /// <param name="holeCards">The player's hole cards</param>
        /// <param name="communityCards">The community cards</param>
        /// <param name="gameState">The current game state</param>
        /// <returns>The calculated raise amount</returns>
        private int CalculateRaiseAmount(int minRaise, int maxRaise, IReadOnlyList<Card> holeCards, IReadOnlyList<Card> communityCards, PokerGame.Core.Game.GameState gameState)
        {
            // Evaluate hand strength to determine raise amount
            double handStrength = EvaluateHandStrength(holeCards, communityCards, gameState);
            
            // Scale the raise amount based on hand strength
            int range = maxRaise - minRaise;
            int raiseAmount = minRaise + (int)(range * handStrength);
            
            // Add some randomness (plus or minus 20%)
            double randomFactor = 0.8 + (_random.NextDouble() * 0.4); // Between 0.8 and 1.2
            raiseAmount = (int)(raiseAmount * randomFactor);
            
            // Ensure the amount is within bounds
            raiseAmount = Math.Max(minRaise, Math.Min(raiseAmount, maxRaise));
            
            return raiseAmount;
        }
        
        /// <summary>
        /// Evaluates the strength of a hand (simplified for AI decisions)
        /// </summary>
        /// <param name="holeCards">The player's hole cards</param>
        /// <param name="communityCards">The community cards</param>
        /// <param name="gameState">The current game state</param>
        /// <returns>A value between 0 and 1 representing hand strength</returns>
        private double EvaluateHandStrength(IReadOnlyList<Card> holeCards, IReadOnlyList<Card> communityCards, PokerGame.Core.Game.GameState gameState)
        {
            // Start with basic hand evaluation
            double strength = 0.0;
            
            // Check for high cards and pairs in hole cards
            if (holeCards.Count == 2)
            {
                // Check if we have a pocket pair
                bool isPocketPair = holeCards[0].Rank == holeCards[1].Rank;
                
                if (isPocketPair)
                {
                    // Pocket pairs are strong starting hands
                    int rank = (int)holeCards[0].Rank;
                    
                    // High pocket pairs are very strong
                    if (rank >= 10) // Tens or higher
                    {
                        strength = 0.9;
                    }
                    // Medium pocket pairs
                    else if (rank >= 7)
                    {
                        strength = 0.7;
                    }
                    // Low pocket pairs
                    else
                    {
                        strength = 0.5;
                    }
                }
                else
                {
                    // Check for high cards
                    int highCard = Math.Max((int)holeCards[0].Rank, (int)holeCards[1].Rank);
                    int lowCard = Math.Min((int)holeCards[0].Rank, (int)holeCards[1].Rank);
                    
                    // Check for suited cards
                    bool isSuited = holeCards[0].Suit == holeCards[1].Suit;
                    
                    // Check for connected cards (straight potential)
                    int gap = Math.Abs((int)holeCards[0].Rank - (int)holeCards[1].Rank);
                    bool isConnected = gap <= 2;
                    
                    // Calculate base strength from hole cards
                    if (highCard >= 13) // Ace
                    {
                        strength = 0.7;
                    }
                    else if (highCard >= 11) // King or Queen
                    {
                        strength = 0.6;
                    }
                    else if (highCard >= 9) // Jack or Ten
                    {
                        strength = 0.5;
                    }
                    else
                    {
                        strength = 0.3;
                    }
                    
                    // Adjust for connected and suited cards
                    if (isSuited)
                    {
                        strength += 0.1;
                    }
                    
                    if (isConnected)
                    {
                        strength += 0.1;
                    }
                    
                    // Boost high card combinations
                    if (lowCard >= 10) // Both cards are Ten or higher
                    {
                        strength += 0.1;
                    }
                }
            }
            
            // If we have community cards, adjust strength based on potential combinations
            if (communityCards.Count > 0)
            {
                // Create a combined list of all cards
                var allCards = new List<Card>();
                foreach (var card in holeCards)
                {
                    allCards.Add(card);
                }
                foreach (var card in communityCards)
                {
                    allCards.Add(card);
                }
                
                // Check for pairs with the community cards
                var rankGroups = allCards.GroupBy(c => c.Rank).ToList();
                
                // Count pairs, trips, quads
                foreach (var group in rankGroups)
                {
                    switch (group.Count())
                    {
                        case 2: // Pair
                            strength += 0.2;
                            break;
                        case 3: // Three of a kind
                            strength += 0.5;
                            break;
                        case 4: // Four of a kind
                            strength = 0.95; // Almost certainly the best hand
                            break;
                    }
                }
                
                // Check for flush potential
                var suitGroups = allCards.GroupBy(c => c.Suit).ToList();
                foreach (var group in suitGroups)
                {
                    // If we have 4+ cards of the same suit, we have flush potential
                    if (group.Count() >= 4)
                    {
                        strength += 0.3;
                    }
                    else if (group.Count() == 5)
                    {
                        strength += 0.6; // Flush
                    }
                }
                
                // Check for straight potential (simplified)
                var distinctRanks = allCards.Select(c => (int)c.Rank).Distinct().OrderBy(r => r).ToList();
                int maxConsecutive = 1;
                int currentConsecutive = 1;
                
                for (int i = 1; i < distinctRanks.Count; i++)
                {
                    if (distinctRanks[i] == distinctRanks[i - 1] + 1)
                    {
                        currentConsecutive++;
                        maxConsecutive = Math.Max(maxConsecutive, currentConsecutive);
                    }
                    else
                    {
                        currentConsecutive = 1;
                    }
                }
                
                // Adjust strength based on straight potential
                if (maxConsecutive >= 5)
                {
                    strength += 0.6; // Straight
                }
                else if (maxConsecutive == 4)
                {
                    strength += 0.2; // Open-ended straight draw
                }
            }
            
            // Normalize strength between 0 and 1
            strength = Math.Min(1.0, Math.Max(0.0, strength));
            
            // Add some randomness to make AI less predictable
            strength += ((_random.NextDouble() * 0.2) - 0.1); // +/- 10%
            strength = Math.Min(1.0, Math.Max(0.0, strength));
            
            return strength;
        }
    }
}