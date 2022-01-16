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
    public bool OriginalLeft;

    public void DeleteItem()
    {
        MainWindowViewModel.Items.Remove(this);
        ConveyorTile.Items.Remove(this);
    }

    public (int projectedX, int projectedY, ConveyorTile x) GetNextLocation()
    {
        var nextConveyorTile = ConveyorTile;
        Inertia = GetNextDirection(nextConveyorTile);

        var projectedX = X - Inertia.X;
        var projectedY = Y - Inertia.Y;

        if (projectedX < 0)
        {
            if (ConveyorTile.NextConveyorTile is null) { return (X, Y, ConveyorTile); }
            return (MaxCellNumber - 1, projectedY, ConveyorTile.NextConveyorTile);
        }

        if (projectedX > MaxCellNumber)
        {
            if (ConveyorTile.NextConveyorTile is null) { return (X, Y, ConveyorTile); }
            return (0, projectedY, ConveyorTile.NextConveyorTile);
        }

        if (projectedY < 0)
        {
            if (ConveyorTile.NextConveyorTile is null) { return (X, Y, ConveyorTile); }
            return (projectedX, MaxCellNumber - 1, ConveyorTile.NextConveyorTile);
        }

        if (projectedY > MaxCellNumber)
        {
            if (ConveyorTile.NextConveyorTile is null) { return (X, Y, ConveyorTile); }
            return (projectedX, 0, ConveyorTile.NextConveyorTile);
        }

        return (projectedX, projectedY, nextConveyorTile);
    }

    public (int X, int Y) GetNextDirection(ConveyorTile nextConveyorTile)
    {
        if (Left && (X == nextConveyorTile.Lane.X || Y == nextConveyorTile.Lane.Y) || !Left && (X == MaxCellNumber - nextConveyorTile.Lane.X - 1 || Y == MaxCellNumber - nextConveyorTile.Lane.Y - 1)) { return nextConveyorTile.Direction; }
        return Inertia;
    }

}

