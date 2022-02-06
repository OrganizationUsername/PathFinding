using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PathFinding.Core;
using PathFinding.Persistence;
using PathFinding.Shared.Helpers;
using PathFinding.Shared.Models;
using Point = System.Numerics.Vector2;

namespace PathFinding.Shared.ViewModels;

public enum ClickMode { Player = 0 }

public class MainWindowViewModel : ObservableObject
{
    private readonly Random _rand = new(1);
    public bool Paused { get; set; } = false;
    public string HoveredEntityDescription { get => _hoveredEntityDescription; set => SetProperty(ref _hoveredEntityDescription, value); }
    public State State { get; set; }
    public List<Tile> EntitiesToHighlight { get; set; } = new();
    public List<Tile> AlreadyClicked { get; } = new();
    public Cell[,] AnswerCells { get; set; }
    private int _fps;
    private string _hoveredEntityDescription;
    private string _tileString;
    public bool AllowDiagonal { get => _allowDiagonal; set => SetProperty(ref _allowDiagonal, value); }
    public bool AlwaysPath { get; set; } = true;
    public int TileSize { get; set; }
    public int Top { get; set; }
    public int Left { get; set; }
    public bool ShowNumbers { get; set; } = false;
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public bool LeftButtonClick { get; set; }
    public bool SpawnItems { get; set; }
    public string TileString { get => _tileString; set => SetProperty(ref _tileString, value); }
    public int TileWidth { get; set; }
    public int TileHeight { get; set; }
    public int CellsScored { get => _cellsScored; set => SetProperty(ref _cellsScored, value); }
    public string NewTileWidth { get; set; }
    public string NewTileHeight { get; set; }
    public int Fps { get => _fps; set => SetProperty(ref _fps, value); }
    public IStatePersistence Sp;
    public DateTime LastRequestedPathFind { get; set; }
    private int _cellsScored;
    private int _currentPlayer = 1;
    public int CurrentPlayer { get => _currentPlayer; set => SetProperty(ref _currentPlayer, value); }
    public Dictionary<int, (Tile Source, Tile Destination)> PlayerDictionary = new();
    public Dictionary<int, List<(int, int)>> SolutionDictionary = new();
    private bool _allowDiagonal = true;
    public int PlayerCount { get; set; }
    public List<Item> Items { get; set; } = new();
    public int ItemsCount => Items.Count;
    public readonly int MaxCellNumber = 1; //1 or 2
    private string _selectedStringMode = Enum.GetNames(typeof(ClickMode)).First();
    private ClickMode _clickMode = ClickMode.Player;
    public List<string> StringModes { get; set; } = Enum.GetNames(typeof(ClickMode)).ToList();
    public string SelectedStringMode { get => _selectedStringMode; set { SetProperty(ref _clickMode, (ClickMode)Enum.Parse(typeof(ClickMode), value)); SetProperty(ref _selectedStringMode, value); } }
    public ClickMode ClickMode { get => _clickMode; }
    public RelayCommand ResetCommand { get; set; }
    public AsyncRelayCommand<bool> ChangeDiagonalCommand { get; }
    public List<Coordinate> ListOfDirections = new() { (1, 0), (0, 1), (-1, 0), (0, -1) };

    public MainWindowViewModel(IStatePersistence statePersistence)
    {
        Sp = statePersistence;
        PlayerCount = 3;
        for (var i = 1; i <= PlayerCount; i++) { PlayerDictionary.Add(i, (null, null)); }
        for (var i = 1; i <= PlayerCount; i++) { SolutionDictionary.Add(i, new()); }

        PixelWidth = 600;
        PixelHeight = 600;
        TileWidth = 10;
        TileHeight = 10;
        var tileSize = 10;
        TileSize = tileSize;
        State = new(TileWidth, TileHeight, tileSize, Sp);

        Item.MainWindowViewModel = this;
        Item.MaxCellNumber = MaxCellNumber;

        ResetCommand = new(Reset);
        //ChangeDiagonalCommand = new(async (x) => { AllowDiagonal = x; await PlayerPathFinding(); });
        SetupMapString();
        UploadMapString(@"3972_G4MPKI6UrMG9OgaGZmcAmLpKbFnWit9TRq0AVogGrRYWkQKSyyR9MuH9AenBv5sSeR+axQ4OHRwOESlSJAS5Z9t9tbIp5bwH/db7lgtKvQ3fH/yDCwonLSau8Y5aty1SCrbmxv20+I7bysl9AkepUN0bb8SGsANKLOiJCCOVU9u/PdYBet5gBsp6XKAeo8WuNkoNM9i8x35DUGYmR2HshCll3M4bPrVsPmGmOrfbSlJsVi5AcEMLzgbP00rTOd1HKeRUSX4C8L9Z9J5BfOKtSR8zs44M8O4CnJ34LyKDi1+JtWLy3HcqERkmHi4KuYeEAVZOkn7jlh3Ids0oomZVmhr0uiun2U/QT+4nJNkSyHAzQT3YYiIzDiVYB7yJesuCYk2e3Df2H0LT9zIwdRrIDAGbymDcWdFEvewh74DIswbs9HJiTy4feNxBJYXpdsockTBZH82r/DHk/7rdEm5CteuHA4xBLV4z55I+3SfLdkRAOvlY8Pk2kj0A");
    }

    static void Log(string message, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
    {
        Trace.WriteLine($" {Path.GetFileName(file)}, {line}, {message}");
    }

    public void Tick(Point? point, bool leftClicked)
    {
        GetHoverElements(point);
        if (!Paused)
        {

        }
        if (leftClicked) { HandleLeftClick(point); }
        else { AlreadyClicked.Clear(); }
    }

    public void HandleLeftClick(Point? point)
    {
        if (!point.HasValue) return;
        var tile = GetTileAtLocation(point);
        if (tile is null) return;
        switch (ClickMode)
        {
            case ClickMode.Player:
                TryFlipElement(tile); return;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void HandleRightClick(Point? point)
    {
        if (!point.HasValue) return;
        var tile = GetTileAtLocation(point);
        if (tile is null) return;
        switch (ClickMode)
        {
            case ClickMode.Player:
                HandleRightClickPlayerMode(tile);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void Reset()
    {
        if (int.TryParse(NewTileWidth, out var tileWidth) && tileWidth > 10 && int.TryParse(NewTileHeight, out var tileHeight) && tileHeight > 10)
        {
            Items.Clear();
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            TileSize = Math.Max(Math.Min(PixelWidth / tileWidth, PixelHeight / tileHeight), 10);
            State = new(TileWidth, TileHeight, TileSize, Sp);
            AnswerCells = null;
            PlayerDictionary.Clear();
            SolutionDictionary.Clear();
            for (var i = 1; i <= PlayerCount; i++) { PlayerDictionary.Add(i, (null, null)); SolutionDictionary.Add(i, new()); }
        }
    }

    public void UploadMapString(string mapString)
    {
        var mapStringResult = State.UploadMapString(mapString);
        if (!mapStringResult.Success) return;

        var lineNumber = 1;
        State = new(mapStringResult.X, mapStringResult.Y, State.TileSize, Sp);
        for (var x = 0; x < mapStringResult.X; x++)
        {
            lineNumber++;
            var row = mapStringResult.mapStringArray[lineNumber];
            for (var y = 0; y < mapStringResult.Y; y++) { State.TileGrid[x, y].IsPassable = row[y] == '0'; }
        }

        TileWidth = State.X;
        TileHeight = State.Y;
    }

    public void SetupMapString() => TileString = State.SetupMapString();

    public void GetHoverElements(Point? point)
    {
        if (!point.HasValue) return;
        var tile = GetTileAtLocation(point);
        if (tile is null) return;
        if (tile == EntitiesToHighlight.FirstOrDefault()) return;
        EntitiesToHighlight.Clear();
        EntitiesToHighlight.Add(tile);
    }

    private (Cell destCell, Cell sourceCell) GetStateCells(Tile destination, Tile source, Cell destCell, Cell[,] cells, Cell sourceCell)
    {
        foreach (var t in State.Tiles)
        {
            var hScore = Math.Abs(t.X - destCell.X) + Math.Abs(t.Y - destCell.Y);
            var cell = new Cell()
            {
                HScore = hScore * 10,
                GScore = int.MaxValue / 2,
                X = t.X,
                Y = t.Y,
                Id = t.Id,
                Passable = t.IsPassable,
                ChunkId = -1,
            };
            if (t == destination) { destCell = cell; }
            if (t == source) { sourceCell = cell; }
            cells[t.X, t.Y] = cell;
        }
        return (destCell, sourceCell);
    }

    public unsafe void PlayerPathFinding()
    {
        var requestDate = DateTime.Now;
        LastRequestedPathFind = requestDate;

        var key = 1;
        var (source, destination) = PlayerDictionary[key];
        SolutionDictionary[key].Clear();

        if (destination is null || source is null) { return; }

        Cell destCell = new() { Id = destination.Id, HScore = 0, X = destination.X, Y = destination.Y, Passable = destination.IsPassable, ChunkId = -1 };
        Cell sourceCell = new() { Id = source.Id, HScore = Math.Abs(source.X - destination.X) + Math.Abs(source.Y - destination.Y), X = source.X, Y = source.Y, Passable = source.IsPassable, ChunkId = -1 };


        Trace.WriteLine($"Size= {sizeof(Cell)}");

        var cells = new Cell[State.X, State.Y];

        (destCell, sourceCell) = GetStateCells(destination, source, destCell, cells, sourceCell);
        var beforeSolve = DateTime.Now.TimeOfDay;
        var solution = Solver.Solve(cells, sourceCell, destCell, null, requestDate, _allowDiagonal);
        var arfterSolve = DateTime.Now.TimeOfDay;

        Trace.WriteLine($"DateTime solve time = {(arfterSolve - beforeSolve).TotalMilliseconds} ms.");

        if (solution.SolutionCells is null || !solution.SolutionCells.Any() || solution.thisDate != LastRequestedPathFind) return;

        Trace.WriteLine($"{solution.TimeToSolve}ms to solve ({source.X},{source.Y}) to ({destination.X},{destination.Y}).");

        foreach (var cell in solution.SolutionCells) { SolutionDictionary[key].Add((cell.X, cell.Y)); }

        var finishedCount = 0;
        foreach (var cell in solution.AllCells) { finishedCount += (cell.Finished) ? 1 : 0; }
        AnswerCells = solution.AllCells; //Wrong at the moment
    }

    private void TryFlipElement(Tile tile)
    {
        if (AlreadyClicked.Any(t => t.Id == tile.Id)) return; //ToDo: This could be done with storing a dictionary better, I think.
        tile.IsPassable = !tile.IsPassable;
        if (!tile.IsPassable)
        {
            tile.TileRole = TileRole.Nothing;
            foreach (var kvp in PlayerDictionary)
            {
                if (kvp.Value.Destination == tile) PlayerDictionary[kvp.Key] = (kvp.Value.Source, null);
                if (kvp.Value.Source == tile) PlayerDictionary[kvp.Key] = (null, kvp.Value.Source);
            }
        }

        AlreadyClicked.Add(tile);
        EntitiesToHighlight.Clear();
        EntitiesToHighlight.Add(tile);
        if (AlwaysPath) PlayerPathFinding();
        SetupMapString();
    }

    public Tile GetTileAtLocation(Point? point)
    {
        if (!point.HasValue) return null;
        var xThing = (int)Math.Floor((point.Value.X + Left) / TileSize);
        var yThing = (int)Math.Floor((point.Value.Y + Top) / TileSize);

        if (xThing >= TileWidth || yThing >= TileHeight) return null;

        return State.TileGrid[xThing, yThing];
    }

    private void HandleRightClickPlayerMode(Tile tile)
    {
        //ToDo: Make sure right-clicking source/destination doesn't put it in a bad state.
        if (tile is null) return;
        var keys = PlayerDictionary.Keys.ToArray();
        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index];

            var kvp = PlayerDictionary[key];
            if (kvp.Destination is not null && !kvp.Destination.IsPassable) kvp = (kvp.Source, null);
            if (kvp.Source is not null && !kvp.Source.IsPassable) kvp = (null, kvp.Destination);
            if (kvp.Destination == tile)
            {
                PlayerDictionary[key] = (kvp.Source, null);
                tile.TileRole = TileRole.Nothing;
                return;
            }

            if (kvp.Source == tile)
            {
                PlayerDictionary[key] = (null, kvp.Source);
                tile.TileRole = TileRole.Nothing;
                return;
            }
        }

        if (tile.TileRole == TileRole.Nothing && tile.IsPassable)
        {
            if (PlayerDictionary[CurrentPlayer].Source is null)
            {
                PlayerDictionary[CurrentPlayer] = (tile, PlayerDictionary[CurrentPlayer].Destination);
                tile.TileRole = TileRole.Source;
                if (AlwaysPath) PlayerPathFinding();
                return;
            }

            if (PlayerDictionary[CurrentPlayer].Destination is null)
            {
                PlayerDictionary[CurrentPlayer] = (PlayerDictionary[CurrentPlayer].Source, tile);
                tile.TileRole = TileRole.Destination;
                if (AlwaysPath) PlayerPathFinding();
                return;
            }
        }
    }

}