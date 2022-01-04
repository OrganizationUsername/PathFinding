namespace PathFinding.Shared.Models;

public class ConveyorTile
{
    public (int X, int Y) Direction;
    /// <summary>
    /// If X == 0 grid is left. If X==1 cellMax is right. If X== -1, Y determines left/right side.
    /// </summary>
    public (int X, int Y) Lane;
    public Tile Tile;
    public List<Item> Items = new();
    public Conveyor Conveyor;
    public ConveyorTile NextConveyorTile;
}

