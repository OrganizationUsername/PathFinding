using System.Runtime.CompilerServices;
using PathFinding.Shared.ViewModels;

namespace PathFinding.Shared.Models;

public class ConveyorTile
{
    public static MainWindowViewModel MainWindowViewModel;
    public static int MaxCellNumber;
    public static Tile[,] Tiles;
    public (int X, int Y) Direction;
    /// <summary>
    /// If X == 0 grid is left. If X==1 cellMax is right. If X== -1, Y determines left/right side.
    /// </summary>
    public (int X, int Y) Lane;
    public Tile Tile;
    public bool IsSorter;
    public Tile TargetTile
    {
        get
        {
            var xMax = Tiles.GetLength(0);
            var yMax = Tiles.GetLength(1);
            var tempX = Tile.X - Direction.X;
            var tempY = Tile.Y - Direction.Y;
            if (tempX < 0 || tempX >= xMax) { return null; }
            if (tempY < 0 || tempY >= yMax) { return null; }
            return Tiles[tempX, tempY];
        }
    }

    public Tile GetTileAtCoordinate(Coordinate c)
    {
        var next = Location - c;
        if (next.X >= 0 && next.X < Tiles.GetLength(0) && next.Y >= 0 && next.Y < Tiles.GetLength(1))
        {
            return Tiles[next.X, next.Y];
        }
        return null;
    }

    public bool IsSorterTarget()
    {
        var xMax = Tiles.GetLength(0);
        var yMax = Tiles.GetLength(1);
        foreach (var direction in MainWindowViewModel.ListOfDirections)
        {
            var possibleLocation = Location + direction;
            if (possibleLocation.X < 0 || possibleLocation.X >= xMax) { continue; }
            if (possibleLocation.Y < 0 || possibleLocation.Y >= yMax) { continue; }

            var tempTile = Tiles[possibleLocation.X, possibleLocation.Y];
            if (tempTile.ConveyorTile?.Conveyor == Conveyor) continue;
            if (tempTile.ConveyorTile is { IsSorter: true }) { return true; }
        }
        return false;
    }

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
}