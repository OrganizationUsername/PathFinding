using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.Foundation.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using PathFinding.Core;
using PathFinding.Shared.Models;
using PathFinding.Shared.ViewModels;

using Color = Windows.UI.Color;
using Key = Windows.System.VirtualKey;

namespace Pathfinding.WinUI;

[ObservableObject]
public sealed partial class MainWindow : Window
{
    private MainWindowViewModel _viewModel;
    public MainWindowViewModel ViewModel
    {
        get => _viewModel;
        set => SetProperty(ref _viewModel, value);
    }
    [ObservableProperty] private int _frameTime;

    public MainWindowViewModel Vm => _viewModel;

    private Point? _point;
    private bool _clicked;
    private int BubbleSize => _viewModel.State.TileSize / 3;
    private int TileSize => _viewModel.TileSize;
    private Cell[,] _cellBackup;
    private int LeftX => _viewModel.Left;
    private int TopY => _viewModel.Top;
    private DateTime _dt = DateTime.Now;
    private int _ticksPerSecond;
    private double _rightClickX;
    private double _rightClickY;
    private readonly List<Color> _metroColors;
    private readonly Dictionary<Key, int> _numberKeys;
    private readonly Dictionary<Key, (int X, int Y)> _movementKeys;
    public List<Key> LastPressedKeys = new List<Key>();

    private CanvasSwapChain _swapChain;
    private readonly CanvasDevice _device;
    private readonly CanvasTextFormat _cellScoreTextFormat;
    private CanvasGeometry[] _triangles;

    public MainWindow()
    {
        ViewModel = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<MainWindowViewModel>();

        _metroColors = new()
        {
            ColorHelper.FromArgb(0xFF, 0x00, 0x39, 0xA6),
            ColorHelper.FromArgb(0xFF, 0xFF, 0x63, 0x19),
            ColorHelper.FromArgb(0xFF, 0x6C, 0xBE, 0x45),
            ColorHelper.FromArgb(0xFF, 0x99, 0x66, 0x33),
            ColorHelper.FromArgb(0xFF, 0xFC, 0xCC, 0x0A),
            ColorHelper.FromArgb(0xFF, 0xEE, 0x35, 0x2E),
            ColorHelper.FromArgb(0xFF, 0x00, 0x93, 0x3C),
            ColorHelper.FromArgb(0xFF, 0xB9, 0x33, 0xAD),
        };

        _numberKeys = new() { { Key.Number1, 1 }, { Key.Number2, 2 }, { Key.Number3, 3 }, { Key.Number4, 4 }, { Key.Number5, 5 }, { Key.NumberPad1, 1 }, { Key.NumberPad2, 2 }, { Key.NumberPad3, 3 }, { Key.NumberPad4, 4 }, { Key.NumberPad5, 5 } };
        _movementKeys = new() { { Key.W, (0, -1) }, { Key.S, (0, 1) }, { Key.A, (-1, 0) }, { Key.D, (1, 0) }, { Key.Up, (0, -1) }, { Key.Down, (0, 1) }, { Key.Left, (-1, 0) }, { Key.Right, (1, 0) } };

        this.InitializeComponent();

        _device = new CanvasDevice();
        _cellScoreTextFormat = new CanvasTextFormat()
        {
            FontFamily = "Segoe UI",
            FontSize = 10,
            Direction = CanvasTextDirection.LeftToRightThenTopToBottom,
            WordWrapping = CanvasWordWrapping.NoWrap,
            Options = CanvasDrawTextOptions.NoPixelSnap
        };

        _triangles = CreateConveyorTriangles();

        CompositionTarget.Rendering += CompositionTarget_Rendering;
    }

    private CanvasGeometry[] CreateConveyorTriangles()
    {
        var a = Vm.TileSize / 2f;
        var b = Vm.TileSize;

        return new CanvasGeometry[]
        {
            CreateTriangle(new(0, 0), new(0, b), new(-a, a)),  // Left
            CreateTriangle(new(0, 0), new(0, b), new(a, a)),  // Right
            CreateTriangle(new(0, 0), new(b, 0), new(a, a)),  // Up
            CreateTriangle(new(0, 0), new(b, 0), new(a, -a)), // Down
        };

        CanvasGeometry CreateTriangle(Vector2 a, Vector2 b, Vector2 c)
        {
            var triUpBuilder = new CanvasPathBuilder(_device);
            triUpBuilder.BeginFigure(a.X, a.Y);
            triUpBuilder.AddLine(b.X, b.Y);
            triUpBuilder.AddLine(c.X, c.Y);
            triUpBuilder.EndFigure(CanvasFigureLoop.Closed);
            return CanvasGeometry.CreatePath(triUpBuilder);
        }
    }

    private void EnsureSwapChainReady()
    {
        if (_swapChain is null)
        {
            _swapChain = new CanvasSwapChain(
                _device,
                Vm.PixelWidth,
                Vm.PixelHeight,
                96,
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                CanvasAlphaMode.Ignore);

            swapChainPanel.SwapChain = _swapChain;
        }
        else
        {
            _swapChain.ResizeBuffers(
                Vm.PixelWidth,
                Vm.PixelHeight,
                96,
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2);
        }
    }

    private async void CompositionTarget_Rendering(object sender, object e)
    {
        EnsureSwapChainReady();

        var dt = DateTime.Now;
        if (dt - _dt > TimeSpan.FromMilliseconds(1000))
        {
            Vm.Fps = _ticksPerSecond;
            _ticksPerSecond = 0;
            _dt = dt;
        }
        _ticksPerSecond++;

        await Vm.Tick(PointToVector(_point), _clicked);

        using (var ds = swapChainPanel.SwapChain.CreateDrawingSession(Colors.CornflowerBlue))
        {
            RenderFrame(ds);
            RenderCostTextOverlay(ds, Vm.AnswerCells);
        }

        _swapChain.Present(1);
        FrameTime = (DateTime.Now - dt).Milliseconds;
    }

    //private void CheckKeys()
    //{
    //    if (FocusManager.GetFocusedElement() is TextBox)
    //        return;

    //    foreach (var kvp in _numberKeys.Where(x => IsKeyPressed(x.Key)))
    //    {
    //        Vm.CurrentPlayer = Math.Min(kvp.Value, Vm.PlayerCount);
    //    }

    //    foreach (var kvp in _movementKeys.Where(x => IsKeyPressed(x.Key)))
    //    {
    //        Vm.Left += 10 * kvp.Value.X;
    //        Vm.Top += 10 * kvp.Value.Y;
    //        //DrawCostText(_cellBackup);
    //    }

    //    if (IsKeyPressed(Key.C) && !LastPressedKeys.Contains(Key.C))
    //    {
    //        Vm.ClickMode = 1 - Vm.ClickMode; LastPressedKeys.Add(Key.C);
    //    }

    //    if (!IsKeyPressed(Key.C) && LastPressedKeys.Contains(Key.C))
    //    {
    //        LastPressedKeys.Remove(Key.C);
    //    }
    //}

    //private bool IsKeyPressed(Key key)
    //{
    //    return CoreWindow.GetKeyState(key) == Windows.UI.Core.CoreVirtualKeyStates.Down;
    //}

    private void RenderFrame(CanvasDrawingSession ds)
    {
        if (ds == null)
            return;

        var minX = Math.Max(0, LeftX / TileSize - 1);
        var minY = Math.Max(0, TopY / TileSize - 1);
        var maxX = Math.Min(Vm.State.X, minX + Vm.PixelWidth / TileSize + 3);
        var maxY = Math.Min(Vm.State.Y, minY + Vm.PixelHeight / TileSize + 3);

        for (var x = minX; x < maxX; x++)
        {
            for (var y = minY; y < maxY; y++)
            {
                var tile = Vm.State.TileGrid[x, y];
                if ((tile.X + 1) * TileSize - LeftX < 0 || tile.X * TileSize - LeftX > Vm.PixelWidth || (tile.Y + 1) * TileSize - TopY < 0 || tile.Y * TileSize - TopY > Vm.PixelHeight)
                    continue;

                Color color;
                if (tile.IsPassable)
                {
                    color = tile.ChunkId == -1 ? Colors.LightBlue : _metroColors[tile.ChunkId % _metroColors.Count];
                    if (Vm.EntitiesToHighlight.Contains(tile)) { color = Colors.Peru; }
                }
                else
                {
                    color = Colors.DarkGray;
                }

                if (tile.TileRole == TileRole.Conveyor)
                    color = Colors.LightBlue;

                var cellRect = new Rect(tile.X * TileSize + 1 - LeftX, tile.Y * TileSize + 1 - TopY, TileSize - 1, TileSize - 1);
                ds.FillRectangle(cellRect, color);

                if (tile.IsPartOfSolution)
                {
                    ds.FillEllipse(tile.X * TileSize + (TileSize / 2 - LeftX), tile.Y * TileSize + TileSize / 2 - TopY, BubbleSize, BubbleSize, Colors.White);
                }

                //TODO: Replace this switch with just looking up Players that have things that are populated. 
                switch (tile.TileRole)
                {
                    case TileRole.Source:
                        ds.FillEllipse(tile.X * TileSize + TileSize / 2 - LeftX, tile.Y * TileSize + TileSize / 2 - TopY, BubbleSize, BubbleSize, Colors.Green); continue;
                    case TileRole.Destination:
                        ds.FillEllipse(tile.X * TileSize + TileSize / 2 - LeftX, tile.Y * TileSize + TileSize / 2 - TopY, BubbleSize, BubbleSize, Colors.Black); continue;
                    case TileRole.Nothing:
                        break;
                    case TileRole.Conveyor:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        using (var conveyorLayer = ds.CreateLayer(0.6f))
        {
            ds.Transform = Matrix3x2.CreateScale(Vm.TileSize / 10f);

            for (var i = 0; i < Vm.Conveyors.Count; i++)
            {
                var conveyor = Vm.Conveyors[i];
                var color = _metroColors[i % _metroColors.Count];
                for (var index = 0; index < conveyor.ConveyorTile.Count; index++)
                {
                    var cell = conveyor.ConveyorTile[index].Tile;

                    var (x, y) = conveyor.ConveyorTile[index].Direction;
                    (CanvasGeometry geometry, float dx, float dy) triangle = (x, y) switch
                    {
                        (1, 0) => (_triangles[0], TileSize / 2f, 0f),
                        (-1, 0) => (_triangles[1], TileSize / 2f, 0f),
                        (0, -1) => (_triangles[2], 0f, TileSize / 2f),
                        (0, 1) => (_triangles[3], 0f, TileSize / 2f),
                        _ => (null, 0f, 0f)
                    };

                    if (triangle.geometry is null)
                        continue;

                    ds.FillGeometry(triangle.geometry, new Vector2(Unscale(cell.X * TileSize + 1 - LeftX + triangle.dx), Unscale(cell.Y * TileSize + 1 - TopY + triangle.dy)), color);
                }
            }
        }

        ds.Transform = Matrix3x2.Identity;

        var partialTile = TileSize / Vm.MaxCellNumber;
        foreach (var item in Vm.Items)
        {
            var tile = item.ConveyorTile.Tile;
            if (tile.X > minX && tile.X <= maxX && tile.Y > minY && tile.Y <= maxY)
            {
                var leftPixel = tile.X * TileSize + partialTile * item.X;
                var topPixel = tile.Y * TileSize + partialTile * item.Y;
                var rect = new Rect(leftPixel - LeftX, topPixel - TopY, partialTile, partialTile);
                ds.DrawRectangle(rect, item.Left ? Colors.SaddleBrown : Colors.DeepPink);
            }
        }

        float Unscale(float x) => x / (Vm.TileSize / 10f);
    }

    private void RenderCostTextOverlay(CanvasDrawingSession ds, Cell[,] cellCosts)
    {
        if (cellCosts is null || !Vm.ShowNumbers)
            return;

        foreach (var tile in cellCosts)
        {
            if (tile.FCost > int.MaxValue / 3)
                continue;
            var x = tile.X * TileSize - LeftX;
            var y = tile.Y * TileSize - TopY;
            if (x < 0 || x > swapChainPanel.ActualWidth || y < 0 || y > swapChainPanel.ActualHeight)
                continue;

            ds.DrawText(tile.FCost.ToString(), new Vector2(x, y), Colors.Black, _cellScoreTextFormat);
        }
    }

    private void Canvas_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var element = FocusManager.GetFocusedElement();
        if (FocusManager.GetFocusedElement() is TextBox)
            return;

        foreach (var kvp in _numberKeys.Where(x => x.Key == e.Key))
        {
            Vm.CurrentPlayer = Math.Min(kvp.Value, Vm.PlayerCount);
        }

        foreach (var kvp in _movementKeys.Where(x => x.Key == e.Key))
        {
            Vm.Left += 10 * kvp.Value.X;
            Vm.Top += 10 * kvp.Value.Y;
            //DrawCostText(_cellBackup);
        }

        if (e.Key == Key.C && !LastPressedKeys.Contains(Key.C))
        {
            Vm.ClickMode = 1 - Vm.ClickMode;
            LastPressedKeys.Add(Key.C);
        }

        if (e.Key != Key.C && LastPressedKeys.Contains(Key.C))
        {
            LastPressedKeys.Remove(Key.C);
        }
    }

    private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(swapChainPanel);
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
        {
            if (point.Properties.IsMiddleButtonPressed && _point is not null)
            {
                //DrawCostText(_cellBackup);
                //capture mouse moving to the left to increase x
                Vm.Left -= (int)(point.Position.X - _point!.Value.X);
                Vm.Left = Math.Max(0, Vm.Left);
                //capture mouse moving down to increase y
                Vm.Top -= (int)(point.Position.Y - _point!.Value.Y);
                Vm.Top = Math.Max(0, Vm.Top);
            }
            if (point.Properties.IsLeftButtonPressed)
            {
                _clicked = true;
            }
        }

        _point = point.Position;
    }

    private void Canvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _point = null;
    }

    private async void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
        {
            var point = e.GetCurrentPoint(swapChainPanel);
            if (point.Properties.IsLeftButtonPressed)
            {
                _clicked = true;
                Vm.LeftButtonClick = _clicked;
                await Vm.HandleLeftClick(PointToVector(point.Position));
            }
            else if(point.Properties.IsRightButtonPressed)
            {
                Vm.HandleRightClick(PointToVector(point.Position));
            }
            else if (point.Properties.IsMiddleButtonPressed)
            {
                _point = point.Position;
            }
        }
    }

    private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(swapChainPanel);
        if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
        {
            _clicked = false;
            Vm.LeftButtonClick = _clicked;
        }
    }

    private void Canvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(swapChainPanel);

        if (point.Properties.MouseWheelDelta > 0) // zoom out
        {
            Vm.TileSize = Math.Min(Vm.TileSize + 1, 50);
        }
        else if (point.Properties.MouseWheelDelta < 0) // zoom in
        {
            Vm.TileSize = Math.Max(Vm.TileSize - 1, 5); 
        }
    }

    private async void FindPath(object sender, RoutedEventArgs e) => await FindPathAsync();

    private async Task FindPathAsync() => await Vm.PlayerPathFinding();

    private void LoadMapString(object sender, RoutedEventArgs e)
    {
        Vm.UploadMapString(Vm.TileString);
    }

    private Vector2? PointToVector(Point? point)
    {
        if (point.HasValue)
            return new Vector2((float)point.Value.X, (float)point.Value.Y);
        return null;
    }
}
