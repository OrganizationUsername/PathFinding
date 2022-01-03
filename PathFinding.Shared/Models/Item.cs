using PathFinding.Shared.ViewModels;

namespace PathFinding.Shared.Models;

public class Item
{
    //ToDo: Maybe this should also have the last velocity so it can continue to go to the left/right side of conveyor
    public static MainWindowViewModel MainWindowViewModel;
    public static int MaxCellNumber;
    public int X;
    public int Y;
    public ConveyorTile ConveyorTile;

    public void DeleteItem()
    {
        MainWindowViewModel.Items.Remove(this);
        ConveyorTile.Items.Remove(this);
    }

    public (int projectedX, int projectedY, ConveyorTile x) GetNextLocation()
    {

        var projectedX = X - ConveyorTile.Direction.X;
        var projectedY = Y - ConveyorTile.Direction.Y;

        var nextConveyorTile = ConveyorTile;

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

