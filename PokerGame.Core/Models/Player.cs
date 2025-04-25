using System;
using System.Collections.Generic;

namespace PokerGame.Core.Models
{
    /// <summary>
    /// Represents a poker player
    /// </summary>
    public class Player
    {
        /// <summary>
        /// The player's name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// The player's current chip balance
        /// </summary>
        public int Chips { get; private set; }
        
        /// <summary>
        /// The player's hole (private) cards
        /// </summary>
        public List<Card> HoleCards { get; } = new List<Card>();
        
        /// <summary>
        /// The amount the player has bet in the current betting round
        /// </summary>
        public int CurrentBet { get; private set; } = 0;
        
        /// <summary>
        /// Whether the player has folded in the current hand
        /// </summary>
        public bool HasFolded { get; private set; } = false;
        
        /// <summary>
        /// Whether the player is all-in for the current hand
        /// </summary>
        public bool IsAllIn { get; private set; } = false;
        
        /// <summary>
        /// Whether the player is currently active in the game
        /// </summary>
        public bool IsActive => Chips > 0 && !HasFolded;
        
        /// <summary>
        /// The player's current best hand (if evaluated)
        /// </summary>
        public Hand? CurrentHand { get; set; }

        /// <summary>
        /// Creates a new player with the specified name and starting chips
        /// </summary>
        /// <param name="name">The player's name</param>
        /// <param name="startingChips">The player's starting chip amount</param>
        public Player(string name, int startingChips)
        {
            Name = name;
            Chips = startingChips;
        }

        /// <summary>
        /// Places a bet of the specified amount
        /// </summary>
        /// <param name="amount">The amount to bet</param>
        /// <returns>The actual amount bet (may be less if player doesn't have enough chips)</returns>
        public int PlaceBet(int amount)
        {
            // Can't bet more than the player has
            int actualBet = Math.Min(amount, Chips);
            
            Chips -= actualBet;
            CurrentBet += actualBet;
            
            if (Chips == 0)
                IsAllIn = true;
                
            return actualBet;
        }

        /// <summary>
        /// Folds the player's hand
        /// </summary>
        public void Fold()
        {
            HasFolded = true;
        }

        /// <summary>
        /// Adds chips to the player's stack (e.g., when winning a pot)
        /// </summary>
        /// <param name="amount">The amount to add</param>
        public void AddChips(int amount)
        {
            if (amount > 0)
                Chips += amount;
        }

        /// <summary>
        /// Clears the player's state for a new hand
        /// </summary>
        public void ResetForNewHand()
        {
            HoleCards.Clear();
            CurrentBet = 0;
            HasFolded = false;
            IsAllIn = false;
            CurrentHand = null;
        }

        /// <summary>
        /// Resets the player's current bet for a new betting round
        /// </summary>
        public void ResetBetForNewRound()
        {
            CurrentBet = 0;
        }
    }
}
