using System.Diagnostics;
namespace PathFinding.Shared.Models;

public enum TileRole { Nothing = 0, Source = 1, Destination = 2, Conveyor = 3 }
public class Tile
{
    public string Name { get; set; }
    public bool IsPassable;
    public readonly int Id;
    public TileRole TileRole = TileRole.Nothing;
    public int X;
    public int Y;
    public int ChunkId = -1;
    public string Description => Name;
    public ConveyorTile ConveyorTile { get; set; }
    public bool HasNextConveyorTile => ConveyorTile?.NextConveyorTile is not null;
    public List<ConveyorTile> InboundConveyorTiles { get; set; } = new(Constants.Constants.RectangularGridSides);
    public Tile(int x, int y, bool isPassable, int id)
    {
        Id = id;
        X = x;
        Y = y;
        IsPassable = isPassable;
        Name = $"{x},{y}";
    }

    public void RemoveInboundConveyors(ConveyorTile ct)
    {
        if (InboundConveyorTiles.Remove(ct)) { Trace.WriteLine($"Removed ({this.X},{this.Y}) from ({this.X}, {this.Y})'s inbound."); }
    }
}

