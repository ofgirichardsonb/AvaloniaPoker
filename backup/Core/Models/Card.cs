using System;

namespace PokerGame.Core.Models
{
    /// <summary>
    /// Represents a playing card suit
    /// </summary>
    public enum Suit
    {
        Clubs,
        Diamonds,
        Hearts,
        Spades
    }

    /// <summary>
    /// Represents a playing card rank
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
    /// Represents a playing card with a rank and suit
    /// </summary>
    public class Card : IEquatable<Card>, IComparable<Card>
    {
        public Rank Rank { get; }
        public Suit Suit { get; }

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        /// <summary>
        /// Returns a short string representation of the card (e.g., "AS" for Ace of Spades)
        /// </summary>
        public string ShortName
        {
            get
            {
                string rankChar;
                
                switch (Rank)
                {
                    case Rank.Ten:
                        rankChar = "T";
                        break;
                    case Rank.Jack:
                        rankChar = "J";
                        break;
                    case Rank.Queen:
                        rankChar = "Q";
                        break;
                    case Rank.King:
                        rankChar = "K";
                        break;
                    case Rank.Ace:
                        rankChar = "A";
                        break;
                    default:
                        rankChar = ((int)Rank).ToString();
                        break;
                }
                
                string suitChar = Suit.ToString()[0].ToString();
                return rankChar + suitChar;
            }
        }

        /// <summary>
        /// Returns a string representation of the card (e.g., "Ace of Spades")
        /// </summary>
        public override string ToString()
        {
            return $"{Rank} of {Suit}";
        }

        public bool Equals(Card? other)
        {
            if (other == null)
                return false;
                
            return Rank == other.Rank && Suit == other.Suit;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Card);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Rank, Suit);
        }

        public int CompareTo(Card? other)
        {
            if (other == null)
                return 1;
                
            return ((int)Rank).CompareTo((int)other.Rank);
        }

        public static bool operator ==(Card? left, Card? right)
        {
            if (left is null)
                return right is null;
                
            return left.Equals(right);
        }

        public static bool operator !=(Card? left, Card? right)
        {
            return !(left == right);
        }

        public static bool operator <(Card left, Card right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(Card left, Card right)
        {
            return left.CompareTo(right) > 0;
        }
    }
}
