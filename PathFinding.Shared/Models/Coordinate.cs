namespace PathFinding.Shared.Models;

public struct Coordinate : IEquatable<Coordinate>
{
    public int X;
    public int Y;

    public Coordinate(int x, int y) { X = x; Y = y; }
    public static Coordinate operator +(Coordinate l, Coordinate r) => new() { X = l.X + r.X, Y = l.Y + r.Y };
    public static Coordinate operator -(Coordinate l, Coordinate r) => new() { X = l.X - r.X, Y = l.Y - r.Y };
    public static implicit operator Coordinate((int X, int Y) tuple) => new(tuple.X, tuple.Y);
    public override string ToString() => $"({X},{Y})";
    public static bool operator ==(Coordinate l, Coordinate r) => l.X == r.X && l.Y == r.Y;
    public static bool operator !=(Coordinate l, Coordinate r) => !(l.X == r.X && l.Y == r.Y);
    public bool Equals(Coordinate other) => X == other.X && Y == other.Y;
    public override bool Equals(object obj) => obj is Coordinate other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
}