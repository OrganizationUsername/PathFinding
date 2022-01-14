namespace PathFinding.Shared.Models;

public class Conveyor
{
    //ToDo: I need to make it so I can serialize this so it can be saved in a map string.
    //ToDo: Since this should be part of the map string, it should also be held in State.
    public int Id;
    public List<ConveyorTile> ConveyorTiles { get; set; } = new();
    public List<Item> Items = new();
    public int Tick;

    public string GetTileText()
    {
        return string
            .Join(", ",
            ConveyorTiles
#if DEBUG
            .OrderBy(ctx => ctx.Location.X)
            .ThenBy(ctx => ctx.Location.Y)
#endif
            .Select(ctx => $"({ctx.Tile.X},{ctx.Tile.Y})"));
    }
}