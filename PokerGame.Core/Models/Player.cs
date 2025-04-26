using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PokerGame.Core.Models
{
    /// <summary>
    /// Represents a player in a poker game
    /// </summary>
    public class Player
    {
        /// <summary>
        /// Gets or sets the unique identifier for the player
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Gets or sets the name of the player
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the player's current chip count
        /// </summary>
        [JsonPropertyName("chips")]
        public int Chips { get; set; }
        
        /// <summary>
        /// Gets or sets the player's hole cards (private cards in Texas Hold'Em)
        /// </summary>
        [JsonPropertyName("holeCards")]
        public List<Card> HoleCards { get; set; } = new List<Card>();
        
        /// <summary>
        /// Gets or sets whether the player has folded their hand
        /// </summary>
        [JsonPropertyName("hasFolded")]
        public bool HasFolded { get; set; }
        
        /// <summary>
        /// Gets or sets whether the player is the dealer for the current hand
        /// </summary>
        [JsonPropertyName("isDealer")]
        public bool IsDealer { get; set; }
        
        /// <summary>
        /// Gets or sets whether the player is currently active in the game
        /// </summary>
        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Gets or sets whether the player is all-in (has bet all their chips)
        /// </summary>
        [JsonPropertyName("isAllIn")]
        public bool IsAllIn { get; set; }
        
        /// <summary>
        /// Gets or sets the player's current bet amount in the current betting round
        /// </summary>
        [JsonPropertyName("currentBet")]
        public int CurrentBet { get; set; }
        
        /// <summary>
        /// Gets or sets the player's total bet amount in the current hand
        /// </summary>
        [JsonPropertyName("totalBet")]
        public int TotalBet { get; set; }
        
        /// <summary>
        /// Creates a new player with the specified name and initial chip count
        /// </summary>
        /// <param name="name">The player's name</param>
        /// <param name="chips">The player's initial chip count</param>
        public Player(string name, int chips = 1000)
        {
            Name = name;
            Chips = chips;
        }
        
        /// <summary>
        /// Creates a default player
        /// </summary>
        public Player()
        {
            Name = "Player";
            Chips = 1000;
        }
        
        /// <summary>
        /// Adds a card to the player's hole cards
        /// </summary>
        /// <param name="card">The card to add</param>
        public void AddCard(Card card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }
            
            HoleCards.Add(card);
        }
        
        /// <summary>
        /// Clears the player's hole cards
        /// </summary>
        public void ClearHand()
        {
            HoleCards.Clear();
            HasFolded = false;
        }
        
        /// <summary>
        /// Places a bet of the specified amount
        /// </summary>
        /// <param name="amount">The amount to bet</param>
        /// <returns>The actual amount bet (may be less than the requested amount if the player has insufficient chips)</returns>
        public int PlaceBet(int amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentException("Bet amount must be positive", nameof(amount));
            }
            
            // Limit bet to available chips
            int actualBet = Math.Min(amount, Chips);
            
            // Check if player is going all-in
            if (actualBet == Chips)
            {
                IsAllIn = true;
            }
            
            // Deduct chips
            Chips -= actualBet;
            
            // Update bet amounts
            CurrentBet += actualBet;
            TotalBet += actualBet;
            
            return actualBet;
        }
        
        /// <summary>
        /// Resets the player's current bet amount (e.g., at the end of a betting round)
        /// </summary>
        public void ResetCurrentBet()
        {
            CurrentBet = 0;
        }
        
        /// <summary>
        /// Resets the player's total bet amount (e.g., at the end of a hand)
        /// </summary>
        public void ResetTotalBet()
        {
            TotalBet = 0;
            CurrentBet = 0;
        }
        
        /// <summary>
        /// Resets the player for a new hand
        /// </summary>
        public void ResetForNewHand()
        {
            ClearHand();
            ResetTotalBet();
            HasFolded = false;
            IsAllIn = false;
        }
        
        /// <summary>
        /// Resets the player's bet for a new betting round
        /// </summary>
        public void ResetBetForNewRound()
        {
            CurrentBet = 0;
        }
        
        /// <summary>
        /// Awards chips to the player (e.g., for winning a pot)
        /// </summary>
        /// <param name="amount">The amount of chips to award</param>
        public void AwardChips(int amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentException("Award amount must be positive", nameof(amount));
            }
            
            Chips += amount;
        }
        
        /// <summary>
        /// Folds the player's hand
        /// </summary>
        public void Fold()
        {
            HasFolded = true;
        }
        
        /// <summary>
        /// Returns a string representation of the player
        /// </summary>
        /// <returns>A string representation of the player</returns>
        public override string ToString()
        {
            return $"{Name}: ${Chips} chips" + (HasFolded ? " (folded)" : "");
        }
    }
}