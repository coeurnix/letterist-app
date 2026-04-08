namespace Letterist.Model;

public readonly struct BalloonLink : IEquatable<BalloonLink>
{
    public Guid FirstId { get; }
    public Guid SecondId { get; }

    public BalloonLink(Guid a, Guid b)
    {
        if (a == b)
        {
            throw new ArgumentException("Balloon link requires two distinct IDs.", nameof(b));
        }

        if (a.CompareTo(b) <= 0)
        {
            FirstId = a;
            SecondId = b;
        }
        else
        {
            FirstId = b;
            SecondId = a;
        }
    }

    public bool Contains(Guid id) => id == FirstId || id == SecondId;

    public bool Equals(BalloonLink other)
    {
        return FirstId == other.FirstId && SecondId == other.SecondId;
    }

    public override bool Equals(object? obj) => obj is BalloonLink other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(FirstId, SecondId);

    public static bool operator ==(BalloonLink left, BalloonLink right) => left.Equals(right);
    public static bool operator !=(BalloonLink left, BalloonLink right) => !left.Equals(right);
}
