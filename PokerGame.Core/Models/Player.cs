using System;
using System.Collections.Generic;

namespace PokerGame.Core.Models
{
    /// <summary>
    /// Represents a player in the poker game
    /// </summary>
    public class Player
    {
        /// <summary>
        /// Initializes a new instance of the Player class
        /// </summary>
        public Player() 
        {
            // Default constructor
        }
        
        /// <summary>
        /// Initializes a new instance of the Player class with an ID and name
        /// </summary>
        /// <param name="id">The player's ID</param>
        /// <param name="name">The player's name</param>
        public Player(string id, string name)
        {
            Id = id;
            Name = name;
            Chips = 1000; // Default starting chips
        }
        
        /// <summary>
        /// Initializes a new instance of the Player class with an ID, name, and starting chips
        /// </summary>
        /// <param name="id">The player's ID</param>
        /// <param name="name">The player's name</param>
        /// <param name="chips">The player's starting chip count</param>
        public Player(string id, string name, int chips)
        {
            Id = id;
            Name = name;
            Chips = chips;
        }
        
        /// <summary>
        /// Gets or sets the player ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the player name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the player's chip count
        /// </summary>
        public int Chips { get; set; }
        
        /// <summary>
        /// Gets or sets the player's current bet in this round
        /// </summary>
        public int CurrentBet { get; set; }
        
        /// <summary>
        /// Gets or sets the player's total bet for the entire hand
        /// </summary>
        public int TotalBet { get; set; }
        
        /// <summary>
        /// Gets or sets whether the player has folded in this hand
        /// </summary>
        public bool HasFolded { get; set; }
        
        /// <summary>
        /// Gets or sets whether the player is all in for this hand
        /// </summary>
        public bool IsAllIn { get; set; }
        
        /// <summary>
        /// Gets or sets whether the player is the dealer
        /// </summary>
        public bool IsDealer { get; set; }
        
        /// <summary>
        /// Gets or sets whether the player is currently active
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Gets or sets whether this player is the current user (for UI purposes)
        /// </summary>
        public bool IsCurrentUser { get; set; }
        
        /// <summary>
        /// Gets or sets whether the player has acted in the current betting round
        /// </summary>
        public bool HasActed { get; set; }
        
        /// <summary>
        /// Gets or sets the player's hole cards
        /// </summary>
        public List<Card> HoleCards { get; set; } = new List<Card>();
        
        /// <summary>
        /// Gets the player's current hand for evaluation
        /// </summary>
        /// <remarks>
        /// This is a compatibility property to support older code that uses CurrentHand instead of HoleCards
        /// </remarks>
        public List<Card> CurrentHand
        {
            get { return HoleCards; }
            set { HoleCards = value; }
        }
        
        /// <summary>
        /// Folds the player's hand
        /// </summary>
        public void Fold()
        {
            HasFolded = true;
            IsActive = false;
        }
        
        /// <summary>
        /// Places a bet for the player
        /// </summary>
        /// <param name="amount">The amount to bet</param>
        /// <returns>The actual amount bet (may be less if player doesn't have enough chips)</returns>
        public int PlaceBet(int amount)
        {
            int actualBet = Math.Min(amount, Chips);
            Chips -= actualBet;
            CurrentBet += actualBet;
            TotalBet += actualBet;
            
            // Check if the player is all in
            if (Chips == 0)
            {
                IsAllIn = true;
            }
            
            return actualBet;
        }
        
        /// <summary>
        /// Resets the player's current bet for a new betting round
        /// </summary>
        public void ResetBetForNewRound()
        {
            CurrentBet = 0;
        }
        
        /// <summary>
        /// Resets the player's current bet for a new betting round (alias for backward compatibility)
        /// </summary>
        public void ResetCurrentBet()
        {
            ResetBetForNewRound();
        }
        
        /// <summary>
        /// Awards chips to the player (e.g., for winning a pot)
        /// </summary>
        /// <param name="amount">The amount of chips to award</param>
        public void AwardChips(int amount)
        {
            Chips += amount;
        }
        
        /// <summary>
        /// Clears the player's hand for a new round
        /// </summary>
        public void ClearHand()
        {
            HoleCards.Clear();
            HasFolded = false;
            IsAllIn = false;
            CurrentBet = 0;
            TotalBet = 0;
        }
        
        /// <summary>
        /// Resets the player for a new hand
        /// </summary>
        public void ResetForNewHand()
        {
            ClearHand();
            IsDealer = false;
            IsActive = true;
            HasActed = false;
        }
        
        /// <summary>
        /// Resets the player's total bet counter
        /// </summary>
        public void ResetTotalBet()
        {
            TotalBet = 0;
        }
    }
}