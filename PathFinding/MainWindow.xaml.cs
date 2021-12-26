using PathFinding.Core;
using PathFinding.Stuff;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PathFinding;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
// ReSharper disable once UnusedMember.Global
public partial class MainWindow
{
    private MainWindowViewModel Vm { get; }
    private WriteableBitmap Wb => Vm.Wb;
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
    private double rightClickX;
    private double rightClickY;
    private readonly List<Color> metroColors;

    public MainWindow()
    {
        InitializeComponent();
        //CompositionTarget.Rendering += RenderTickAsync; /* https://docs.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-render-on-a-per-frame-interval-using-compositiontarget?view=netframeworkdesktop-4.8&viewFallbackFrom=netdesktop-6.0 */
        var dt = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1) };
        dt.Tick += RenderTickAsync;
        dt.Start();
        Vm = DataContext as MainWindowViewModel;
        if (Vm is null) throw new("Bah.");

        metroColors = new()
        {
            (Color)ColorConverter.ConvertFromString("#0039A6")!,
            (Color)ColorConverter.ConvertFromString("#FF6319")!,
            (Color)ColorConverter.ConvertFromString("#6CBE45")!,
            (Color)ColorConverter.ConvertFromString("#996633")!,
            (Color)ColorConverter.ConvertFromString("#FCCC0A")!,
            (Color)ColorConverter.ConvertFromString("#EE352E")!,
            (Color)ColorConverter.ConvertFromString("#00933C")!,
            (Color)ColorConverter.ConvertFromString("#B933AD")!
        };
    }

    private async void RenderTickAsync(object sender, EventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.D1) && Keyboard.FocusedElement is not TextBox)
        {
            MessageBox.Show("Got 1");
        }

        DateTime dt = DateTime.Now;
        if (dt - _dt > TimeSpan.FromMilliseconds(1000))
        {
            Vm.Fps = _ticksPerSecond;
            _ticksPerSecond = 0;
            _dt = dt;
        }
        _ticksPerSecond++;
        await Vm.Tick(_point, _clicked);
        //var sw = new Stopwatch();
        //sw.Start();
        Wb.Lock();
        Wb.Clear(Colors.CornflowerBlue);

        var minX = Math.Max(0, LeftX / TileSize - 1);
        var minY = Math.Max(0, TopY / TileSize - 1);
        var maxX = Math.Min(Vm.State.X, minX + Vm.PixelWidth / TileSize + 2);
        var maxY = Math.Min(Vm.State.Y, minY + Vm.PixelHeight / TileSize + 2);

        for (var x = minX; x < maxX; x++)
        {
            for (var y = minY; y < maxY; y++)
            {
                var tile = Vm.State.TileGrid[x, y];
                if ((tile.X + 1) * TileSize - LeftX < 0 || tile.X * TileSize - LeftX > Vm.PixelWidth || (tile.Y + 1) * TileSize - TopY < 0 || tile.Y * TileSize - TopY > Vm.PixelHeight) continue;
                Color color;
                if (tile.IsPassable)
                {
                    color = tile.ChunkId == -1 ? Colors.LightBlue : metroColors[tile.ChunkId % metroColors.Count];
                    if (Vm.EntitiesToHighlight.Contains(tile)) { color = Colors.Peru; }
                }
                else { color = Colors.DarkGray; }

                Wb.FillRectangle(tile.X * TileSize + 1 - LeftX, tile.Y * TileSize + 1 - TopY, tile.X * TileSize + TileSize - 1 - LeftX, tile.Y * TileSize + TileSize - 1 - TopY, color);

                switch (tile.TileRole)
                {
                    case TileRole.Source:
                        Wb.FillEllipseCentered(tile.X * TileSize + TileSize / 2 - LeftX, tile.Y * TileSize + TileSize / 2 - TopY, BubbleSize, BubbleSize, Colors.White); continue;
                    case TileRole.Destination:
                        Wb.FillEllipseCentered(tile.X * TileSize + TileSize / 2 - LeftX, tile.Y * TileSize + TileSize / 2 - TopY, BubbleSize, BubbleSize, Colors.Black); continue;
                    case TileRole.Nothing:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                foreach (var tile1 in Vm.EntitiesToHighlight)
                {

                }
                if (tile.IsPartOfSolution) { Wb.FillEllipseCentered(tile.X * TileSize + (TileSize / 2 - LeftX), tile.Y * TileSize + TileSize / 2 - TopY, BubbleSize, BubbleSize, Colors.White); }
            }
        }

        //if (_ran.NextDouble() < -0.0005)
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
        //Trace.WriteLine($"Time to draw everything: {sw.ElapsedMilliseconds}"); //~20
    }

    private void DrawCostText(Cell[,] cellCosts)
    {
        if (!Vm.ShowNumbers) return;
        if (cellCosts is null) return;
        var visual = new DrawingVisual();
        using (DrawingContext drawingContext = visual.RenderOpen())
        {
            drawingContext.DrawImage(Wb, new(0, 0, Vm.PixelWidth, Vm.PixelHeight));
            var _textSize = 12;
            foreach (var tile in cellCosts)
            {
                if (tile.FCost > int.MaxValue / 3) continue;
                var x = tile.X * TileSize - LeftX;
                var y = tile.Y * TileSize - TopY;
                if (x < 0 || x > Width || y < 0 || y > Height) continue;
                var segoe12 = new FormattedText(tile.FCost.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new("Segoe UI"), _textSize / 2.0, Brushes.Black, new(), 96);
                drawingContext.DrawText(segoe12, new(x, y));
            }
        }
        TextImage.Source = new DrawingImage(visual.Drawing);
    }

    private void ClearText()
    {
        TextImage.Source = new DrawingImage(new DrawingVisual().Drawing);
    }

    private void Image_MouseLeave(object sender, MouseEventArgs e)
    {
        _point = null;
    }

    private void Image_MouseMove(object sender, MouseEventArgs e)
    {
        var thisPosition = e.GetPosition(sender as Image);
        if (e.MiddleButton == MouseButtonState.Pressed && _point is not null)
        {
            ClearText();
            DrawCostText(_cellBackup);
            //capture mouse moving to the left to increase x
            Vm.Left -= (int)(thisPosition.X - _point!.Value.X);
            Vm.Left = Math.Max(0, Vm.Left);
            //capture mouse moving down to increase y
            Vm.Top -= (int)(thisPosition.Y - _point!.Value.Y);
            Vm.Top = Math.Max(0, Vm.Top);
        }

        _point = e.GetPosition(sender as Image);
        _clicked = e.LeftButton == MouseButtonState.Pressed;
        Vm.LeftButtonClick = _clicked;
    }

    private async void FindPath(object sender, RoutedEventArgs e)
    {
        await FindPathAsync();
    }

    private async Task FindPathAsync()
    {
        await Vm.PathFinding();
    }

    private void UIElement_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(sender as Image);
        _clicked = e.LeftButton == MouseButtonState.Pressed;
        Vm.LeftButtonClick = _clicked;
        Vm.TryFlipElement(point);
    }

    private async void UIElement_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(sender as Image);
        Vm.FlipElementSourceDestination(point);
        await FindPathAsync();
    }

    private void TextImage_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        //ToDo: zoom in on the highlighted square?
        if (e.Delta > 0) /*zoom out*/ { Vm.TileSize = Math.Min(Vm.TileSize + 1, 50); }
        else /*zoom in*/ { Vm.TileSize = Math.Max(Vm.TileSize - 1, 3); }

        _clicked = false;
        Vm.LeftButtonClick = _clicked;
        ClearText();
        DrawCostText(_cellBackup);
    }

    private async void UIElement_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _clicked = false;
        Vm.LeftButtonClick = _clicked;
        if (Vm.AlwaysPath) await Vm.PathFinding();
    }

    private void LoadMapString(object sender, RoutedEventArgs e) => Vm.UploadMapString(Vm.TileString);

    private void TextImage_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        rightClickX = e.CursorLeft;
        rightClickY = e.CursorTop;
        //MessageBox.Show($"{e.CursorLeft},{e.CursorTop}");
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show($"{rightClickX},{rightClickY}");
    }
}