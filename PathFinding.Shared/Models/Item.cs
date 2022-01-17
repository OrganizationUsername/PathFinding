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

    public void GetSorterLocation()
    {
        foreach (var direction in MainWindowViewModel.ListOfDirections)
        {
            var testLocation = ConveyorTile.Location + direction;
            if (testLocation.X > 0 && testLocation.X < MainWindowViewModel.TileWidth && testLocation.Y > 0 && testLocation.Y < MainWindowViewModel.TileHeight)
            {
                var nextTile = MainWindowViewModel.State.TileGrid[testLocation.X, testLocation.Y];
                if (nextTile.ConveyorTile is not null && nextTile.ConveyorTile?.Conveyor != ConveyorTile.Conveyor)
                {
                    //Then it's a possibility.
                    var asdf = this.ConveyorTile.Location - nextTile.ConveyorTile.Location;
                    Inertia = asdf;

                }
            }

        }


    }

    public (int projectedX, int projectedY, ConveyorTile x) GetNextLocation()
    {
        var nextConveyorTile = ConveyorTile;
        Inertia = GetNextDirection(nextConveyorTile);

        var projectedX = X - Inertia.X;
        var projectedY = Y - Inertia.Y;

        var result = GetNextLocationAgnostic(projectedX, projectedY, ConveyorTile);
        if (result.x is not null) return result;

        return (projectedX, projectedY, nextConveyorTile);
    }

    public (int projectedX, int projectedY, ConveyorTile x) GetNextLocationAgnostic(int projectedX, int projectedY, ConveyorTile ct)
    {
        if (projectedX < 0)
        {
            if (ct.NextConveyorTile is null) { return (X, Y, ct); }
            return (MaxCellNumber - 1, projectedY, ct.NextConveyorTile);
        }

        if (projectedX > MaxCellNumber)
        {
            if (ct.NextConveyorTile is null) { return (X, Y, ct); }
            return (0, projectedY, ct.NextConveyorTile);
        }

        if (projectedY < 0)
        {
            if (ct.NextConveyorTile is null) { return (X, Y, ct); }
            return (projectedX, MaxCellNumber - 1, ct.NextConveyorTile);
        }

        if (projectedY > MaxCellNumber)
        {
            if (ct.NextConveyorTile is null) { return (X, Y, ct); }
            return (projectedX, 0, ct.NextConveyorTile);
        }

        return (-1, -1, null);
    }


    public Coordinate GetNextDirection(ConveyorTile nextConveyorTile)
    {
        if (Left && (X == nextConveyorTile.Lane.X || Y == nextConveyorTile.Lane.Y) || !Left && (X == MaxCellNumber - nextConveyorTile.Lane.X - 1 || Y == MaxCellNumber - nextConveyorTile.Lane.Y - 1)) { return nextConveyorTile.Direction; }
        return Inertia;
    }

}

