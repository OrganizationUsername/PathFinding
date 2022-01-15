namespace PathFinding.Shared.Models;

public class ConveyorTile
{
    public static int MaxCellNumber;
    public static Tile[,] Tiles;
    public (int X, int Y) Direction;
    /// <summary>
    /// If X == 0 grid is left. If X==1 cellMax is right. If X== -1, Y determines left/right side.
    /// </summary>
    public (int X, int Y) Lane;
    public Tile Tile;
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