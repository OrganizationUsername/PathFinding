namespace PathFinding.Models;

public enum TileRole { Nothing = 0, Source = 1, Destination = 2, Conveyor = 3 }
public class Tile
{
    public string Name { get; set; }
    public bool IsPassable;
    public readonly int Id;
    public TileRole TileRole = TileRole.Nothing;
    public bool IsPartOfSolution;
    public int X;
    public int Y;
    public int ChunkId = -1;
    public string Description => Name;

    public Tile(int x, int y, bool isPassable, int id)
    {
        Id = id;
        X = x;
        Y = y;
        IsPassable = isPassable;
        Name = $"{x},{y}";
    }
}

