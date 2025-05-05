using System;
using System.Collections.Generic;
using System.Linq;
using PokerGame.Core.Models;
// Import Game namespace with an alias to avoid ambiguity
using GameNS = PokerGame.Core.Game;

namespace PokerGame.Core.AI
{
    /// <summary>
    /// Represents an AI poker player that can make automated decisions
    /// </summary>
    public class AIPokerPlayer
    {
        private readonly Random _random = new Random();
        private Player _player;
        
        public int BigBlind { get; set; } = 10;
        public int MaxBet { get; set; } = 100;
        
        public AIPokerPlayer(Player player)
        {
            _player = player;
        }
        
        /// <summary>
        /// Makes a decision for the AI player based on current game state
        /// </summary>
        /// <param name="communityCards">The community cards currently on the table</param>
        /// <param name="currentBet">The current bet that needs to be called</param>
        /// <param name="canCheck">Whether the player can check</param>
        /// <returns>A PlayerAction with the AI's decision</returns>
        public PlayerAction MakeDecision(List<Card> communityCards, int currentBet, bool canCheck)
        {
            // Evaluate hand strength to make decision
            double handStrength = EvaluateHandStrength(_player.HoleCards, communityCards, GameNS.GameState.PreFlop);
            
            // Cannot bet more than the player has
            if (currentBet > _player.Chips)
            {
                return new PlayerAction(ActionType.Fold, 0, _player.Id);
            }
            
            // If no current bet and can check
            if (currentBet == 0 && canCheck)
            {
                // With strong hand, 50% chance to bet
                if (handStrength > 0.6 && _random.NextDouble() > 0.5)
                {
                    int betAmount = CalculateRaiseAmount(BigBlind, MaxBet, _player.HoleCards, communityCards, GameNS.GameState.PreFlop);
                    return new PlayerAction(ActionType.Bet, betAmount, _player.Id);
                }
                return new PlayerAction(ActionType.Check, 0, _player.Id);
            }
            
            // If no current bet but cannot check, must bet
            if (currentBet == 0 && !canCheck)
            {
                int betAmount = CalculateRaiseAmount(BigBlind, MaxBet, _player.HoleCards, communityCards, GameNS.GameState.PreFlop);
                return new PlayerAction(ActionType.Bet, betAmount, _player.Id);
            }
            
            // If very strong hand
            if (handStrength > 0.7)
            {
                int raiseAmount = CalculateRaiseAmount(currentBet + BigBlind, MaxBet, _player.HoleCards, communityCards, GameNS.GameState.PreFlop);
                return new PlayerAction(ActionType.Raise, raiseAmount, _player.Id);
            }
            
            // If medium strength hand
            if (handStrength > 0.4)
            {
                return new PlayerAction(ActionType.Call, currentBet, _player.Id);
            }
            
            // With weak hand and high bet, fold
            if (currentBet > BigBlind * 3 && handStrength < 0.4)
            {
                return new PlayerAction(ActionType.Fold, 0, _player.Id);
            }
            
            // With weak hand and low bet, call
            return new PlayerAction(ActionType.Call, currentBet, _player.Id);
        }
        
        /// <summary>
        /// Determines the next action for the AI player
        /// </summary>
        /// <param name="player">The player model for the AI</param>
        /// <param name="gameEngine">The current game engine</param>
        /// <returns>A tuple containing the action and bet amount (for raises)</returns>
        public (string Action, int BetAmount) DetermineAction(Player player, GameNS.PokerGameEngine gameEngine)
        {
            // Critical debug information for AI decision making
            Console.WriteLine($"★★★★★ [AI] DetermineAction for {player.Name} - Current game state: {gameEngine.State} ★★★★★");
            Console.WriteLine($"★★★★★ [AI] Player state: HasActed={player.HasActed}, IsActive={player.IsActive}, HasFolded={player.HasFolded}, Chips={player.Chips} ★★★★★");
            
            // ENHANCED PLAYER STATE MANAGEMENT
            // This ensures proper AI player state in all game phases
            
            // First, perform comprehensive check for current player
            bool isCurrentPlayer = gameEngine.CurrentPlayer?.Name == player.Name;
            Console.WriteLine($"[AI] {player.Name} is{(isCurrentPlayer ? "" : " not")} the current player (Game State: {gameEngine.State})");
            
            // Always reactivate AI at start of new hands
            if (gameEngine.State == GameNS.GameState.PreFlop || 
                gameEngine.State == GameNS.GameState.WaitingToStart)
            {
                if (!player.IsActive && !player.HasFolded)
                {
                    Console.WriteLine($"[AI] Reactivating {player.Name} at start of new hand");
                    player.IsActive = true;
                    player.HasActed = false;
                }
            }
            
            // If AI is current player but not active, reactivate
            if (isCurrentPlayer)
            {
                if (!player.IsActive && !player.HasFolded)
                {
                    Console.WriteLine($"[AI] FORCING {player.Name} to be active since they are the current player");
                    player.IsActive = true;
                }
                
                // Always reset HasActed when it's the AI's turn
                if (player.HasActed)
                {
                    Console.WriteLine($"[AI] Resetting HasActed for {player.Name} since it's their turn");
                    player.HasActed = false;
                }
                
                // If AI needs to respond to a raise, ensure correct state
                if (player.CurrentBet < gameEngine.CurrentBet)
                {
                    Console.WriteLine($"[AI] {player.Name} needs to respond to a raise - ensuring proper state");
                    player.HasActed = false;
                    player.IsActive = true;
                }
            }
            
            // Player can only act if they have chips
            if (player.Chips <= 0)
            {
                Console.WriteLine($"★★★★★ [AI] {player.Name} cannot act: No chips left (Chips={player.Chips}) ★★★★★");
                return ("fold", 0); // Default to fold if can't act
            }
            
            // Get the current game state
            GameNS.GameState gameState = gameEngine.State;
            var communityCards = gameEngine.CommunityCards;
            var currentBet = gameEngine.CurrentBet;
            var pot = gameEngine.Pot;
            
            // If player can check (bet is matched), usually check but sometimes raise
            if (player.CurrentBet == currentBet)
            {
                Console.WriteLine($"★★★★★ [AI] {player.Name} can check (CurrentBet={player.CurrentBet} matches table bet={currentBet}) ★★★★★");
                
                // Evaluate hand strength to determine whether to check or raise
                double handStrength = EvaluateHandStrength(player.HoleCards, communityCards, gameState);
                
                // With very strong hands, raise more often
                double checkProbability = handStrength < 0.7 ? 0.8 : 0.5;
                
                if (_random.NextDouble() < checkProbability)
                {
                    Console.WriteLine($"★★★★★ [AI] {player.Name} decides to check (hand strength = {handStrength:F2}) ★★★★★");
                    return ("check", 0);
                }
                else
                {
                    // Determine raise amount (between min raise and 1/3 of player's chips)
                    int minRaise = currentBet + gameEngine.BigBlind;
                    // Respect the maximum bet limit for the table
                    int maxRaise = Math.Min(
                        gameEngine.MaxBet, // Table max bet
                        Math.Min(currentBet + player.Chips, currentBet + player.Chips / 3) // Player's chip limit
                    );
                    int raiseAmount = CalculateRaiseAmount(minRaise, maxRaise, player.HoleCards, communityCards, gameState);
                    
                    Console.WriteLine($"★★★★★ [AI] {player.Name} decides to raise to {raiseAmount} (hand strength = {handStrength:F2}) ★★★★★");
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
                
                Console.WriteLine($"★★★★★ [AI] {player.Name} needs to call {callAmount} with hand strength {handStrength:F2}, pot odds {potOdds:F2} ★★★★★");
                
                // Detect if this is a counter-raise (someone raised after us)
                // Enhanced detection - we consider it a counter-raise if:
                // 1. We have already acted (HasActed=true) OR
                // 2. We have already bet something (CurrentBet > 0) AND there's a higher bet to call
                bool isCounterRaise = (player.HasActed || player.CurrentBet > 0) && callAmount > 0;
                
                if (isCounterRaise)
                {
                    Console.WriteLine($"★★★★★ [AI] COUNTER-RAISE DETECTED! {player.Name} has bet {player.CurrentBet} but needs to call {callAmount} more ★★★★★");
                    Console.WriteLine($"★★★★★ [AI] {player.Name} state: HasActed={player.HasActed}, IsActive={player.IsActive}, HasFolded={player.HasFolded}, CurrentBet={player.CurrentBet} ★★★★★");
                    Console.WriteLine($"★★★★★ [AI] Game state: {gameState}, Current bet: {currentBet}, Pot: {pot} ★★★★★");
                    
                    // CRITICAL - Reset HasActed to false explicitly when responding to a counter-raise
                    if (player.HasActed)
                    {
                        Console.WriteLine($"★★★★★ [AI] Explicitly resetting HasActed for {player.Name} to respond to counter-raise ★★★★★");
                        player.HasActed = false;
                    }
                }
                
                // NEVER fold pocket pairs on a counter-raise
                bool hasPocketPair = player.HoleCards.Count == 2 && player.HoleCards[0].Rank == player.HoleCards[1].Rank;
                
                // If hand is strong
                if (handStrength > 0.7 || (hasPocketPair && handStrength > 0.5))
                {
                    // With a strong hand, be more aggressive when someone has re-raised us
                    double raiseChance = isCounterRaise ? 0.75 : 0.6;
                    
                    // Higher chance to raise with a strong hand
                    if (_random.NextDouble() < raiseChance)
                    {
                        int minRaise = currentBet + gameEngine.BigBlind;
                        // Respect the maximum bet limit for the table
                        int maxRaise = Math.Min(
                            gameEngine.MaxBet, // Table max bet
                            Math.Min(currentBet + player.Chips, currentBet + player.Chips / 2) // Player's chip limit
                        );
                        int raiseAmount = CalculateRaiseAmount(minRaise, maxRaise, player.HoleCards, communityCards, gameState);
                        
                        Console.WriteLine($"★★★★★ [AI] {player.Name} decides to RAISE to {raiseAmount} with strong hand (strength={handStrength:F2}) ★★★★★");
                        return ("raise", raiseAmount);
                    }
                    else
                    {
                        Console.WriteLine($"★★★★★ [AI] {player.Name} decides to CALL with strong hand (strength={handStrength:F2}) ★★★★★");
                        return ("call", 0);
                    }
                }
                // If hand is medium strength
                else if (handStrength > 0.4 || hasPocketPair)
                {
                    // With pocket pairs, be more likely to call
                    if (hasPocketPair)
                    {
                        Console.WriteLine($"★★★★★ [AI] {player.Name} has pocket pair, more likely to call ★★★★★");
                        // 80% chance to call with any pocket pair
                        if (_random.NextDouble() < 0.8)
                        {
                            Console.WriteLine($"★★★★★ [AI] {player.Name} decides to CALL with pocket pair ★★★★★");
                            return ("call", 0);
                        }
                    }
                    
                    // Call if the pot odds are favorable
                    if (handStrength > potOdds)
                    {
                        Console.WriteLine($"★★★★★ [AI] {player.Name} decides to CALL with medium hand (strength={handStrength:F2} > pot odds={potOdds:F2}) ★★★★★");
                        return ("call", 0);
                    }
                    else
                    {
                        // 30% chance to bluff and call anyway (increased from 20%)
                        if (_random.NextDouble() < 0.3)
                        {
                            Console.WriteLine($"★★★★★ [AI] {player.Name} decides to CALL as a BLUFF with medium hand ★★★★★");
                            return ("call", 0);
                        }
                        else
                        {
                            Console.WriteLine($"★★★★★ [AI] {player.Name} decides to FOLD medium hand (strength={handStrength:F2} < pot odds={potOdds:F2}) ★★★★★");
                            return ("fold", 0);
                        }
                    }
                }
                // If hand is weak
                else
                {
                    // Fold most of the time with weak hands
                    double foldChance = isCounterRaise ? 0.9 : 0.8; // More likely to fold on a counter-raise with weak hand
                    
                    if (_random.NextDouble() < foldChance)
                    {
                        Console.WriteLine($"★★★★★ [AI] {player.Name} decides to FOLD weak hand (strength={handStrength:F2}) ★★★★★");
                        return ("fold", 0);
                    }
                    // Sometimes bluff
                    else
                    {
                        // 15% chance for a bluff raise
                        if (_random.NextDouble() < 0.15)
                        {
                            int minRaise = currentBet + gameEngine.BigBlind;
                            // Respect the maximum bet limit for the table
                            int maxRaise = Math.Min(
                                gameEngine.MaxBet, // Table max bet
                                Math.Min(currentBet + player.Chips, currentBet + player.Chips / 3) // Player's chip limit
                            );
                            int raiseAmount = CalculateRaiseAmount(minRaise, maxRaise, player.HoleCards, communityCards, gameState);
                            
                            Console.WriteLine($"★★★★★ [AI] {player.Name} decides to RAISE to {raiseAmount} as a BLUFF! ★★★★★");
                            return ("raise", raiseAmount);
                        }
                        // 85% chance to just call as a bluff
                        else
                        {
                            Console.WriteLine($"★★★★★ [AI] {player.Name} decides to CALL as a BLUFF with weak hand ★★★★★");
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
        private int CalculateRaiseAmount(int minRaise, int maxRaise, IReadOnlyList<Card> holeCards, IReadOnlyList<Card> communityCards, GameNS.GameState gameState)
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
        private double EvaluateHandStrength(IReadOnlyList<Card> holeCards, IReadOnlyList<Card> communityCards, GameNS.GameState gameState)
        {
            // Start with basic hand evaluation
            double strength = 0.0;
            
            // Debug logging for hand evaluation
            Console.WriteLine($"★★★★★ [AI] Evaluating hand strength for {holeCards.Count} hole cards and {communityCards.Count} community cards ★★★★★");
            if (holeCards.Count > 0)
            {
                foreach (var card in holeCards)
                {
                    Console.WriteLine($"★★★★★ [AI] Hole card: {card.Rank} of {card.Suit} ★★★★★");
                }
            }
            
            // Check for high cards and pairs in hole cards
            if (holeCards.Count == 2)
            {
                // Check if we have a pocket pair
                bool isPocketPair = holeCards[0].Rank == holeCards[1].Rank;
                
                if (isPocketPair)
                {
                    // Pocket pairs are strong starting hands
                    int rank = (int)holeCards[0].Rank;
                    
                    // High pocket pairs are very strong - ALWAYS play these aggressively
                    if (rank >= 10) // Tens or higher
                    {
                        strength = 0.95; // Increased to be more aggressive with high pairs
                        Console.WriteLine($"★★★★★ [AI] Found high pocket pair of {holeCards[0].Rank}s, strength: {strength} ★★★★★");
                    }
                    // Medium pocket pairs - NEVER fold these pre-flop
                    else if (rank >= 7)
                    {
                        strength = 0.80; // Increased to avoid folding medium pairs
                        Console.WriteLine($"★★★★★ [AI] Found medium pocket pair of {holeCards[0].Rank}s, strength: {strength} ★★★★★");
                    }
                    // Low pocket pairs - Still valuable
                    else
                    {
                        strength = 0.65; // Increased to make low pairs more playable
                        Console.WriteLine($"★★★★★ [AI] Found low pocket pair of {holeCards[0].Rank}s, strength: {strength} ★★★★★");
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