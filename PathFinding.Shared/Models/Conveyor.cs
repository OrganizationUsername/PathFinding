namespace PathFinding.Shared.Models;

public class Conveyor
{
    //ToDo: I need to make it so I can serialize this so it can be saved in a map string.
    //ToDo: Since this should be part of the map string, it should also be held in State.
    public int Id;
    public List<ConveyorTile> ConveyorTile { get; set; } = new();
    public List<Item> Items = new();
}

