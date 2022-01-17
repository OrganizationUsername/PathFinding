using PathFinding.Shared.ViewModels;

namespace PathFinding.Shared.Models;

public class Item
{
    //ToDo: Maybe this should also have the last velocity so it can continue to go to the left/right side of conveyor
    public static MainWindowViewModel MainWindowViewModel;
    public static int MaxCellNumber;
    public int X;
    public int Y;
    public Coordinate Inertia;
    public ConveyorTile ConveyorTile;
    public bool Left;
    /// <summary>
    /// Used for coloring.
    /// </summary>
    public bool OriginalLeft;

    public void DeleteItem()
    {
        MainWindowViewModel.Items.Remove(this);
        ConveyorTile.Items.Remove(this);
    }

    public (int projectedX, int projectedY, ConveyorTile x) GetSorterLocation()
    {
        foreach (var direction in MainWindowViewModel.ListOfDirections)
        {
            var testLocation = ConveyorTile.Location + direction;
            if (testLocation.X > 0 && testLocation.X < MainWindowViewModel.TileWidth && testLocation.Y > 0 && testLocation.Y < MainWindowViewModel.TileHeight)
            {
                var nextTile = MainWindowViewModel.State.TileGrid[testLocation.X, testLocation.Y];
                if (nextTile.ConveyorTile is not null && nextTile.ConveyorTile?.Conveyor != ConveyorTile.Conveyor && ConveyorTile.Location + direction == nextTile.ConveyorTile.Location)
                {
                    //Then it's a possibility.
                    var asdf = ConveyorTile.Location - nextTile.ConveyorTile.Location;
                    Inertia = asdf;

                    return GetNextLocationAgnostic(X - asdf.X, Y - asdf.Y, ConveyorTile, nextTile.ConveyorTile);

                }
            }

        }
        return (-1, -1, null);
    }

    public (int projectedX, int projectedY, ConveyorTile x) GetNextLocation()
    {
        var nextConveyorTile = ConveyorTile;
        Inertia = GetNextDirection(nextConveyorTile);

        var projectedX = X - Inertia.X;
        var projectedY = Y - Inertia.Y;

        return GetNextLocationAgnostic(projectedX, projectedY, ConveyorTile, null);
    }

    public (int projectedX, int projectedY, ConveyorTile x) GetNextLocationAgnostic(int projectedX, int projectedY, ConveyorTile ct, ConveyorTile alternate)
    {
        if (projectedX < 0)
        {
            if (ct.NextConveyorTile is null) { return (X, Y, ct); }
            return (MaxCellNumber - 1, projectedY, alternate is not null && !alternate.Items.Any() ? alternate : ct.NextConveyorTile);
        }

        if (projectedX >= MaxCellNumber)
        {
            if (ct.NextConveyorTile is null) { return (X, Y, ct); }
            return (0, projectedY, alternate is not null && !alternate.Items.Any() ? alternate : ct.NextConveyorTile);
        }

        if (projectedY < 0)
        {
            if (ct.NextConveyorTile is null) { return (X, Y, ct); }
            return (projectedX, MaxCellNumber - 1, alternate is not null && !alternate.Items.Any() ? alternate : ct.NextConveyorTile);
        }

        if (projectedY >= MaxCellNumber)
        {
            if (ct.NextConveyorTile is null) { return (X, Y, ct); }
            return (projectedX, 0, alternate is not null && !alternate.Items.Any() ? alternate : ct.NextConveyorTile);
        }

        return (projectedX, projectedY, ct);
    }


    public Coordinate GetNextDirection(ConveyorTile nextConveyorTile)
    {
        if (Left && (X == nextConveyorTile.Lane.X || Y == nextConveyorTile.Lane.Y) || !Left && (X == MaxCellNumber - nextConveyorTile.Lane.X - 1 || Y == MaxCellNumber - nextConveyorTile.Lane.Y - 1)) { return nextConveyorTile.Direction; }
        return Inertia;
    }

}

