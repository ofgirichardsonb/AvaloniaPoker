using System;
using System.Text.Json.Serialization;

namespace PokerGame.Core.Models
{
    /// <summary>
    /// Suit of a playing card
    /// </summary>
    public enum Suit
    {
        Clubs,
        Diamonds,
        Hearts,
        Spades
    }
    
    /// <summary>
    /// Rank of a playing card
    /// </summary>
    public enum Rank
    {
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
        Ace = 14
    }
    
    /// <summary>
    /// A playing card with a rank and suit
    /// </summary>
    public class Card : IEquatable<Card>, IComparable<Card>
    {
        /// <summary>
        /// Gets or sets the rank of the card
        /// </summary>
        [JsonPropertyName("rank")]
        public Rank Rank { get; set; }
        
        /// <summary>
        /// Gets the numeric value of the card rank
        /// </summary>
        [JsonIgnore]
        public int RankValue => (int)Rank;
        
        /// <summary>
        /// Gets or sets the suit of the card
        /// </summary>
        [JsonPropertyName("suit")]
        public Suit Suit { get; set; }
        
        /// <summary>
        /// Gets or sets a flag indicating whether the card is face up
        /// </summary>
        [JsonPropertyName("isFaceUp")]
        public bool IsFaceUp { get; set; }
        
        /// <summary>
        /// Creates a new Card
        /// </summary>
        public Card()
        {
            Rank = Rank.Ace;
            Suit = Suit.Spades;
            IsFaceUp = false;
        }
        
        /// <summary>
        /// Creates a new Card with the specified rank and suit
        /// </summary>
        /// <param name="rank">The rank of the card</param>
        /// <param name="suit">The suit of the card</param>
        /// <param name="isFaceUp">Whether the card is face up</param>
        public Card(Rank rank, Suit suit, bool isFaceUp = false)
        {
            Rank = rank;
            Suit = suit;
            IsFaceUp = isFaceUp;
        }
        
        /// <summary>
        /// Flips the card over
        /// </summary>
        public void Flip()
        {
            IsFaceUp = !IsFaceUp;
        }
        
        /// <summary>
        /// Returns a string representation of the card
        /// </summary>
        /// <returns>A string representation of the card</returns>
        public override string ToString()
        {
            if (!IsFaceUp)
            {
                return "[Card face down]";
            }
            
            return $"{Rank} of {Suit}";
        }
        
        /// <summary>
        /// Determines whether this card is equal to another card
        /// </summary>
        /// <param name="other">The other card</param>
        /// <returns>True if the cards are equal, false otherwise</returns>
        public bool Equals(Card other)
        {
            if (other == null)
            {
                return false;
            }
            
            return Rank == other.Rank && Suit == other.Suit;
        }
        
        /// <summary>
        /// Determines whether this card is equal to another object
        /// </summary>
        /// <param name="obj">The other object</param>
        /// <returns>True if the objects are equal, false otherwise</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as Card);
        }
        
        /// <summary>
        /// Returns a hash code for this card
        /// </summary>
        /// <returns>A hash code for this card</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Rank, Suit);
        }
        
        /// <summary>
        /// Compares this card to another card
        /// </summary>
        /// <param name="other">The other card</param>
        /// <returns>A negative value if this card is less than the other card, 0 if equal, and a positive value if greater</returns>
        public int CompareTo(Card other)
        {
            if (other == null)
            {
                return 1;
            }
            
            // First compare by rank
            int rankComparison = ((int)Rank).CompareTo((int)other.Rank);
            if (rankComparison != 0)
            {
                return rankComparison;
            }
            
            // If ranks are equal, compare by suit
            return ((int)Suit).CompareTo((int)other.Suit);
        }
        
        /// <summary>
        /// Creates a deep copy of this card
        /// </summary>
        /// <returns>A deep copy of this card</returns>
        public Card Clone()
        {
            return new Card(Rank, Suit, IsFaceUp);
        }
    }
}