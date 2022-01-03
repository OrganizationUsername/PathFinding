namespace PathFinding.Shared.Models;

public class ConveyorTile
{
    public (int X, int Y) Direction;
    public Tile Tile;
    public List<Item> Items = new();
    public Conveyor Conveyor;
    public ConveyorTile NextConveyorTile;
}

