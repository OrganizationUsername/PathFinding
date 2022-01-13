using System.Reflection.Metadata.Ecma335;
using Microsoft.VisualBasic.CompilerServices;

namespace PathFinding.Shared.Models;

public class ConveyorTile
{
    public static int MaxCellNumber;
    public (int X, int Y) Direction;
    /// <summary>
    /// If X == 0 grid is left. If X==1 cellMax is right. If X== -1, Y determines left/right side.
    /// </summary>
    public (int X, int Y) Lane;
    public Tile Tile;
    public List<Item> Items = new();
    public Conveyor Conveyor;
    public ConveyorTile NextConveyorTile;
    public Coordinate Location => new() { X = Tile.X, Y = Tile.Y };

    public void Setup()
    {
        var conveyorDirection = (-1, -1);
        if (Direction.X > 0) conveyorDirection = (-1, MaxCellNumber - 1);
        if (Direction.X < 0) conveyorDirection = (-1, 0);
        if (Direction.Y > 0) conveyorDirection = (0, -1);
        if (Direction.Y < 0) conveyorDirection = (MaxCellNumber - 1, -1);
        Lane = conveyorDirection;
    }

    public struct Coordinate
    {
        public int X;
        public int Y;

        public Coordinate(int x, int y) { X = x; Y = y; }
        public static Coordinate operator +(Coordinate l, Coordinate r) => new() { X = l.X + r.X, Y = l.Y + r.Y };
        public static Coordinate operator -(Coordinate l, Coordinate r) => new() { X = l.X - r.X, Y = l.Y - r.Y };
    }

}

