using System.Diagnostics;
using PathFinding.Shared.ViewModels;

namespace PathFinding.Shared.Models;

public class Item
{
    //ToDo: Maybe this should also have the last velocity so it can continue to go to the left/right side of conveyor
    public static MainWindowViewModel MainWindowViewModel;
    public static int MaxCellNumber;
    public int X;
    public int Y;
    public (int X, int Y) Inertia;
    public ConveyorTile ConveyorTile;
    public bool Left;

    public void DeleteItem()
    {
        MainWindowViewModel.Items.Remove(this);
        ConveyorTile.Items.Remove(this);
    }

    public (int projectedX, int projectedY, ConveyorTile x) GetNextLocation()
    {
        //so on the left when going from +x to +y, it's too far to the right by 1.
        var nextConveyorTile = ConveyorTile;
        Trace.WriteLine($"Left is {Left}. Inertia is {Inertia}");
        Trace.WriteLine($"Item is: ({X},{Y}).Next lane is: {ConveyorTile.Lane}. ConveyorTileLane is {ConveyorTile.Lane}.");
        if ((Left && (X == nextConveyorTile.Lane.X || Y == nextConveyorTile.Lane.Y)) || (!Left && (X == MaxCellNumber - nextConveyorTile.Lane.X - 1 || Y == MaxCellNumber - nextConveyorTile.Lane.Y - 1)))
        {
            if (Inertia != nextConveyorTile.Direction)
            {

                Trace.WriteLine($"Changed direction from {Inertia} to {nextConveyorTile.Direction}.");
                Inertia = nextConveyorTile.Direction;
            }
        }

        var projectedX = X - Inertia.X;
        var projectedY = Y - Inertia.Y;

        if (projectedX < 0)
        {
            projectedX = MaxCellNumber - 1;
            nextConveyorTile = ConveyorTile.NextConveyorTile;
        }

        if (projectedX > MaxCellNumber)
        {
            projectedX = 0;
            nextConveyorTile = ConveyorTile.NextConveyorTile;
        }

        if (projectedY < 0)
        {
            projectedY = MaxCellNumber - 1;
            nextConveyorTile = ConveyorTile.NextConveyorTile;
        }

        if (projectedY > MaxCellNumber)
        {
            projectedY = 0;
            nextConveyorTile = ConveyorTile.NextConveyorTile;
        }

        return (projectedX, projectedY, nextConveyorTile);
    }
}

