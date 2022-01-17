using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Numerics;
using PathFinding.Core;
using PathFinding.Annotations;
using PathFinding.Shared.Models;
using PathFinding.Shared.ViewModels;
using Point = System.Numerics.Vector2;

namespace PathFinding;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
// ReSharper disable once UnusedMember.Global
public partial class MainWindow
{
    public MainWindowViewModel Vm { get; }
    public WriteableBitmap Wb { get; }
    private Point? _point;
    private bool _clicked;
    private int BubbleSize => Vm.State.TileSize / 3;
    private int TileSize => Vm.TileSize;
    private readonly Random _ran = new();
    private Cell[,] _cellBackup;
    private int LeftX => Vm.Left;
    private int TopY => Vm.Top;
    private DateTime _dt = DateTime.Now;
    private int _ticksPerSecond;
    private double _rightClickX;
    private double _rightClickY;
    private readonly List<Color> _metroColors;
    private readonly Dictionary<Key, int> _numberKeys;
    private readonly Dictionary<Key, (int X, int Y)> _movementKeys;
    public List<Key> LastPressedKeys = new();

    public MainWindow()
    {
        Vm = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<MainWindowViewModel>()!;
        Wb = new(Vm.PixelWidth, Vm.PixelHeight, 96, 96, PixelFormats.Bgra32, null);

        InitializeComponent();
        DataContext = Vm;

        //CompositionTarget.Rendering += RenderTickAsync; /* https://docs.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-render-on-a-per-frame-interval-using-compositiontarget?view=netframeworkdesktop-4.8&viewFallbackFrom=netdesktop-6.0 */
        var dt = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1) };
        dt.Tick += RenderTickAsync;
        dt.Start();

        _metroColors = new() { (Color)ColorConverter.ConvertFromString("#0039A6")!, (Color)ColorConverter.ConvertFromString("#FF6319")!, (Color)ColorConverter.ConvertFromString("#6CBE45")!, (Color)ColorConverter.ConvertFromString("#996633")!, (Color)ColorConverter.ConvertFromString("#FCCC0A")!, (Color)ColorConverter.ConvertFromString("#EE352E")!, (Color)ColorConverter.ConvertFromString("#00933C")!, (Color)ColorConverter.ConvertFromString("#B933AD")!, };
        _numberKeys = new() { { Key.D1, 1 }, { Key.D2, 2 }, { Key.D3, 3 }, { Key.D4, 4 }, { Key.D5, 5 }, { Key.NumPad1, 1 }, { Key.NumPad2, 2 }, { Key.NumPad3, 3 }, { Key.NumPad4, 4 }, { Key.NumPad5, 5 } };
        _movementKeys = new() { { Key.W, (0, -1) }, { Key.S, (0, 1) }, { Key.A, (-1, 0) }, { Key.D, (1, 0) }, { Key.Up, (0, -1) }, { Key.Down, (0, 1) }, { Key.Left, (-1, 0) }, { Key.Right, (1, 0) } };

        //TODO: What if I got "R" to get a Rectangle you could throw in?
    }

    [SuppressMessage("ReSharper.DPA", "DPA0002: Excessive memory allocations in SOH")]
    private async void RenderTickAsync(object sender, EventArgs e)
    {
        HandleKeyPress();

        Vm.Left = Math.Max(0, Vm.Left); Vm.Top = Math.Max(0, Vm.Top);

        var dt = DateTime.Now;
        if (dt - _dt > TimeSpan.FromMilliseconds(1000))
        {
            Vm.Fps = _ticksPerSecond;
            _ticksPerSecond = 0;
            _dt = dt;
        }
        _ticksPerSecond++;
        try
        {
            await Vm.Tick(_point, _clicked);
        }
        catch (Exception ee)
        {
            Console.WriteLine(ee);
        }

        var sw = new Stopwatch();
        sw.Start();
        Wb.Lock();
        Wb.Clear(Colors.CornflowerBlue);

        var minX = Math.Max(0, LeftX / TileSize - 1);
        var minY = Math.Max(0, TopY / TileSize - 1);
        var maxX = Math.Min(Vm.State.X, minX + Vm.PixelWidth / TileSize + 3);
        var maxY = Math.Min(Vm.State.Y, minY + Vm.PixelHeight / TileSize + 3);

        DrawTiles(minX, maxX, minY, maxY);
        DrawSolutionCells(minX, maxX, minY, maxY);

        DrawConveyors();

        var partialTile = TileSize / Math.Max(1, Vm.MaxCellNumber);
        var offset = Vm.MaxCellNumber == 1 ? TileSize / 4 : 0;
        foreach (var item in Vm.Items)
        {
            var tile = item.ConveyorTile.Tile;
            if (tile.X <= minX || tile.X > maxX || tile.Y <= minY || tile.Y > maxY) /*Item outside of render window*/ continue;
            var leftPixel = tile.X * TileSize + partialTile * item.X;
            var topPixel = tile.Y * TileSize + partialTile * item.Y;
            Wb.FillRectangle(leftPixel - LeftX + offset, topPixel - TopY + offset, leftPixel + partialTile - LeftX - offset, topPixel + partialTile - TopY - offset, item.OriginalLeft ? Colors.SaddleBrown : Colors.DeepPink);
        }

        //if (_ran.NextDouble() < 0.05)
        //{
        //    var bm = new BitmapImage(new("Assets/Human.png", UriKind.Relative));
        //    var writeableBitmap = new WriteableBitmap(bm);
        //    Wb.Blit(new(_ran.Next(0, 350), _ran.Next(0, 350), writeableBitmap.PixelWidth, writeableBitmap.PixelHeight), writeableBitmap, new(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
        //}

        if (Vm.AnswerCells is not null)
        {
            DrawCostText(Vm.AnswerCells);
            _cellBackup = Vm.AnswerCells;
            Vm.AnswerCells = null;
        }

        Wb.Unlock();
        //Trace.WriteLine($"Time to draw everything: {sw.ElapsedMilliseconds}"); //~1
    }

    private void DrawConveyors()
    {
        for (var i = 0; i < Vm.Conveyors.Count; i++)
        {
            var conveyor = Vm.Conveyors[i];
            var color = _metroColors[i % _metroColors.Count];
            for (var index = 0; index < conveyor.ConveyorTiles.Count; index++)
            {
                var cell = conveyor.ConveyorTiles[index].Tile;
                //Wb.FillRectangle(cell.X * TileSize + 1 - LeftX, cell.Y * TileSize + 1 - TopY, cell.X * TileSize + TileSize - 1 - LeftX, cell.Y * TileSize + TileSize - 1 - TopY, Colors.Black);
                var ct = conveyor.ConveyorTiles[index];
                var (x, y) = ct.Direction;
                if (x == 0 && y == 0)
                {
                    /*Wb.FillRectangle(cell.X * TileSize + 1 - LeftX, cell.Y * TileSize + 1 - TopY, cell.X * TileSize + TileSize - 1 - LeftX, cell.Y * TileSize + TileSize - 1 - TopY, Colors.Gray);*/
                    continue;
                }

                var leftPosition = cell.X * TileSize - LeftX;
                var length = TileSize * conveyor.Tick / 8;
                int thing = TileSize - length;
                if (x != 0)
                {
                    Wb.FillRectangle(
                        leftPosition + 1, cell.Y * TileSize + 1 - TopY,
                        leftPosition + TileSize - 2, cell.Y * TileSize + TileSize - 2 - TopY,
                        color);
                    Wb.DrawLine(
                        leftPosition + 1, cell.Y * TileSize + 1 - TopY,
                        leftPosition + TileSize - 1, cell.Y * TileSize + 1 - TopY,
                        Colors.Gray);
                    Wb.DrawLine(
                        leftPosition + 1, cell.Y * TileSize + TileSize - 1 - TopY,
                        leftPosition + TileSize - 1, cell.Y * TileSize + TileSize - 1 - TopY,
                        Colors.Gray);

                    Wb.DrawLine(leftPosition + (x > 0 ? thing : length) / 2, cell.Y * TileSize + 2 - TopY,
                        leftPosition + (x > 0 ? thing : length) / 2, cell.Y * TileSize + TileSize - 2 - TopY, Colors.Black);
                    Wb.DrawLine(leftPosition + (x > 0 ? thing : length) / 2 + TileSize / 2, cell.Y * TileSize + 2 - TopY,
                        leftPosition + (x > 0 ? thing : length) / 2 + TileSize / 2, cell.Y * TileSize + TileSize - 2 - TopY,
                        Colors.Black);
                    //ToDo: Add a little filled triangle to each line so it's obvious which way the conveyor is pointing when paused.
                    Wb.DrawLine(
                        cell.X * TileSize + TileSize / 2 - LeftX, cell.Y * TileSize - TopY + 2,
                        -LeftX + ((x > 0) ? cell.X * TileSize + 1 : (cell.X + 1) * TileSize - 2),
                        cell.Y * TileSize + TileSize / 2 - TopY, Colors.Black);
                    Wb.DrawLine(
                        -LeftX + ((x > 0) ? cell.X * TileSize + 1 : (cell.X + 1) * TileSize - 2),
                        cell.Y * TileSize + TileSize / 2 - TopY,
                        cell.X * TileSize + TileSize / 2 - LeftX, (cell.Y + 1) * TileSize - TopY - 1, Colors.Black);
                    if (ct.IsSorter)
                    {
                        Wb.DrawLine(leftPosition, cell.Y * TileSize - TopY + TileSize / 2,
                            leftPosition + TileSize, cell.Y * TileSize - TopY + TileSize / 2, Colors.Black);
                    }
                }
                else
                {
                    Wb.FillRectangle(
                        leftPosition + 1, cell.Y * TileSize + 1 - TopY,
                        leftPosition + TileSize - 2, cell.Y * TileSize + TileSize - 2 - TopY,
                        color);
                    Wb.DrawLine(
                        leftPosition + 1, cell.Y * TileSize + 1 - TopY,
                        leftPosition + TileSize - 1, cell.Y * TileSize - 1 - TopY,
                        Colors.Gray);
                    Wb.DrawLine(
                        leftPosition + 1, cell.Y * TileSize + TileSize - 1 - TopY,
                        leftPosition + TileSize - 1, cell.Y * TileSize + TileSize - 1 - TopY,
                        Colors.Gray);

                    Wb.DrawLine(leftPosition, cell.Y * TileSize - TopY + (y > 0 ? thing : length) / 2,
                        leftPosition + TileSize, cell.Y * TileSize - TopY + (y > 0 ? thing : length) / 2, Colors.Black);
                    Wb.DrawLine(leftPosition, cell.Y * TileSize - TopY + (y > 0 ? thing : length) / 2 + TileSize / 2,
                        leftPosition + TileSize, cell.Y * TileSize - TopY + (y > 0 ? thing : length) / 2 + TileSize / 2,
                        Colors.Black);

                    Wb.DrawLine(
                        (cell.X + 1) * TileSize - LeftX - 2, cell.Y * TileSize + TileSize / 2 - TopY,
                        cell.X * TileSize + TileSize / 2 - LeftX,
                        -TopY + (y > 0 ? cell.Y * TileSize + 1 : (cell.Y + 1) * TileSize) - 1, Colors.Black);

                    Wb.DrawLine(
                        cell.X * TileSize - LeftX + 2, cell.Y * TileSize + TileSize / 2 - TopY,
                        cell.X * TileSize + TileSize / 2 - LeftX,
                        -TopY + (y > 0 ? cell.Y * TileSize + 1 : (cell.Y + 1) * TileSize) - 1, Colors.Black);
                    if (ct.IsSorter)
                    {
                        Wb.DrawLine(leftPosition + TileSize / 2, cell.Y * TileSize - TopY,
                            leftPosition + TileSize / 2, cell.Y * TileSize + TileSize - 1 - TopY, Colors.Black);
                    }
                }
            }
        }
    }

    private void DrawTiles(int minX, int maxX, int minY, int maxY)
    {
        for (var x = minX; x < maxX; x++)
        {
            for (var y = minY; y < maxY; y++)
            {
                var tile = Vm.State.TileGrid[x, y];
                if ((tile.X + 1) * TileSize - LeftX < 0 || tile.X * TileSize - LeftX > Vm.PixelWidth ||
                    (tile.Y + 1) * TileSize - TopY < 0 || tile.Y * TileSize - TopY > Vm.PixelHeight) continue;
                var color = GetTileColor(tile);

                if (tile.TileRole == TileRole.Conveyor) color = Colors.LightBlue;
                if (Vm.EntitiesToHighlight.Contains(tile))
                {
                    Wb.FillRectangle(tile.X * TileSize + 1 - LeftX, tile.Y * TileSize + 1 - TopY,
                        tile.X * TileSize + TileSize - 1 - LeftX, tile.Y * TileSize + TileSize - 1 - TopY, Colors.Peru);
                    Wb.FillRectangle(tile.X * TileSize + 3 - LeftX, tile.Y * TileSize + 3 - TopY,
                        tile.X * TileSize + TileSize - 3 - LeftX, tile.Y * TileSize + TileSize - 3 - TopY, color);
                }
                else
                {
                    Wb.FillRectangle(tile.X * TileSize + 1 - LeftX, tile.Y * TileSize + 1 - TopY,
                        tile.X * TileSize + TileSize - 1 - LeftX, tile.Y * TileSize + TileSize - 1 - TopY, color);
                }
            }
        }
    }

    private void HandleKeyPress()
    {
        if (Keyboard.FocusedElement is TextBox) { return; }

        foreach (var kvp in _numberKeys)
        {
            if (Keyboard.IsKeyDown(kvp.Key))
            {
                Vm.CurrentPlayer = Math.Min(kvp.Value, Vm.PlayerCount);
            }
        }

        foreach (var kvp in _movementKeys)
        {
            if (Keyboard.IsKeyDown(kvp.Key))
            {
                Vm.Left += 10 * kvp.Value.X;
                Vm.Top += 10 * kvp.Value.Y;
                DrawCostText(_cellBackup);
            }
        }

        if (Keyboard.IsKeyDown(Key.C) && !LastPressedKeys.Contains(Key.C))
        {
            //ToDo: The logic should be in Vm
            Vm.ClickMode = (PathFinding.Shared.ViewModels.ClickMode)(((int)Vm.ClickMode + 1) % (Vm.MaxClickMode + 1));
            LastPressedKeys.Add(Key.C);
        }

        if (Keyboard.IsKeyDown(Key.R) && !LastPressedKeys.Contains(Key.R))
        {
            Vm.TryRotateConveyor(_point);
            LastPressedKeys.Add(Key.R);
        }

        if (Keyboard.IsKeyDown(Key.Space) && !LastPressedKeys.Contains(Key.Space))
        {
            Vm.Paused = !Vm.Paused;
            LastPressedKeys.Add(Key.Space);
        }

        if (!Keyboard.IsKeyDown(Key.C) && LastPressedKeys.Contains(Key.C)) LastPressedKeys.Remove(Key.C);
        if (!Keyboard.IsKeyDown(Key.R) && LastPressedKeys.Contains(Key.R)) LastPressedKeys.Remove(Key.R);
        if (!Keyboard.IsKeyDown(Key.Space) && LastPressedKeys.Contains(Key.Space)) LastPressedKeys.Remove(Key.Space);
    }

    private void DrawSolutionCells(int minX, int maxX, int minY, int maxY)
    {
        foreach (var (i, valueTuples) in Vm.SolutionDictionary)
        {
            for (var j = 0; j < valueTuples.Count; j++)
            {
                var (x, y) = valueTuples[j];

                if (x < minX || x >= maxX || y < minY || y >= maxY) continue;

                var tile = Vm.State.TileGrid[x, y];

                if (j == 0) { Wb.FillEllipseCentered(tile.X * TileSize + TileSize / 2 - LeftX, tile.Y * TileSize + TileSize / 2 - TopY, BubbleSize, BubbleSize, Colors.Green); continue; }
                if (j == valueTuples.Count - 1) { Wb.FillEllipseCentered(tile.X * TileSize + TileSize / 2 - LeftX, tile.Y * TileSize + TileSize / 2 - TopY, BubbleSize, BubbleSize, Colors.Black); continue; }

                Wb.FillEllipseCentered(tile.X * TileSize + (TileSize / 2 - LeftX), tile.Y * TileSize + TileSize / 2 - TopY, BubbleSize, BubbleSize, _metroColors[i % _metroColors.Count]);
            }
        }

        foreach (var (i, value) in Vm.PlayerDictionary)
        {
            if (value.Source is not null) Wb.FillEllipseCentered(value.Source.X * TileSize + TileSize / 2 - LeftX, value.Source.Y * TileSize + TileSize / 2 - TopY, BubbleSize, BubbleSize, Colors.Green);
            if (value.Destination is not null) { Wb.FillEllipseCentered(value.Destination.X * TileSize + TileSize / 2 - LeftX, value.Destination.Y * TileSize + TileSize / 2 - TopY, BubbleSize, BubbleSize, Colors.Black); }
        }

    }

    private Color GetTileColor(Tile tile)
    {
        if (tile.IsPassable)
        {
            return tile.ChunkId == -1 ? Colors.LightBlue : _metroColors[tile.ChunkId % _metroColors.Count];
        }
        return Colors.DarkGray;
    }

    private void DrawCostText(Cell[,] cellCosts)
    {
        ClearText();
        if (!Vm.ShowNumbers) return;
        if (cellCosts is null) return;
        var visual = new DrawingVisual();
        using (var drawingContext = visual.RenderOpen())
        {
            drawingContext.DrawImage(Wb, new(0, 0, Vm.PixelWidth, Vm.PixelHeight));
            const int textSize = 12;
            foreach (var tile in cellCosts)
            {
                if (tile.FCost > int.MaxValue / 3) continue;
                var x = tile.X * TileSize - LeftX;
                var y = tile.Y * TileSize - TopY;
                if (x < 0 || x > Width || y < 0 || y > Height) continue;
                var segoe12 = new FormattedText(tile.FCost.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new("Segoe UI"), textSize / 2.0, Brushes.Black, new(), 96);
                drawingContext.DrawText(segoe12, new(x, y));
            }
        }
        TextImage.Source = new DrawingImage(visual.Drawing);
    }

    private void ClearText() => TextImage.Source = new DrawingImage(new DrawingVisual().Drawing);

    private void Image_MouseLeave(object sender, MouseEventArgs e) => _point = null;

    private void Image_MouseMove(object sender, MouseEventArgs e)
    {
        var thisPosition = e.GetPosition(sender as Image);
        Keyboard.Focus(WriteableBitmapImage);
        if (e.MiddleButton == MouseButtonState.Pressed && _point is not null)
        {
            DrawCostText(_cellBackup);
            //capture mouse moving to the left to increase x
            Vm.Left -= (int)(thisPosition.X - _point!.Value.X);
            Vm.Left = Math.Max(0, Vm.Left);
            //capture mouse moving down to increase y
            Vm.Top -= (int)(thisPosition.Y - _point!.Value.Y);
            Vm.Top = Math.Max(0, Vm.Top);
        }

        _point = PointToVector(e.GetPosition(sender as Image));
        _clicked = e.LeftButton == MouseButtonState.Pressed;
        Vm.LeftButtonClick = _clicked;
    }

    private async void FindPath(object sender, RoutedEventArgs e) => await FindPathAsync();

    private async Task FindPathAsync() => await Vm.PlayerPathFinding();

    private async void UIElement_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = PointToVector(e.GetPosition(sender as Image));
        _clicked = e.LeftButton == MouseButtonState.Pressed;
        Vm.LeftButtonClick = _clicked;
        try
        {
            await Vm.HandleLeftClick(point);
        }
        catch (Exception ee)
        {
            Console.WriteLine(ee);
        }
    }

    private void UIElement_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e) => Vm.HandleRightClick(PointToVector(e.GetPosition(sender as Image)));

    private void TextImage_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        //ToDo: zoom in on the highlighted square?
        if (e.Delta > 0) /*zoom out*/ { Vm.TileSize = Math.Min(Vm.TileSize + 1, 50); }
        else /*zoom in*/ { Vm.TileSize = Math.Max(Vm.TileSize - 1, 5); }

        _clicked = false;
        Vm.LeftButtonClick = _clicked;
        DrawCostText(_cellBackup);
    }

    private void UIElement_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Vm.LeftButtonClick = _clicked = false;

    private void LoadMapString(object sender, RoutedEventArgs e) => Vm.UploadMapString(Vm.TileString);

    [UsedImplicitly]
    private void TextImage_ContextMenuOpening(object sender, ContextMenuEventArgs e) => (_rightClickX, _rightClickY) = (e.CursorLeft, e.CursorTop); /*MessageBox.Show($"{e.CursorLeft},{e.CursorTop}");*/
    /* AdmSnyder is great: MessageBox.Show($"{(_rightClickX, _rightClickY) = (e.CursorLeft, e.CursorTop)}"); */

    private void MenuItem_Click(object sender, RoutedEventArgs e) => MessageBox.Show($"{_rightClickX},{_rightClickY}");

    private Vector2 PointToVector(System.Windows.Point point) => new Vector2((float)point.X, (float)point.Y);
}