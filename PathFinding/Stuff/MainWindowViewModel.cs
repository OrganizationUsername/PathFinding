using Microsoft.Toolkit.Mvvm.ComponentModel;
using PathFinding.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Toolkit.Mvvm.Input;
using PathFinding.Annotations;
using Point = System.Windows.Point;

namespace PathFinding.Stuff;

public enum ClickMode { Player = 0, Conveyor = 1 }

public class MainWindowViewModel : ObservableObject
{
    public int RandomInt { get; set; } = 0;
    public bool Paused { get; set; } = true;
    public string HoveredEntityDescription { get => _hoveredEntityDescription; set => SetProperty(ref _hoveredEntityDescription, value); }
    public WriteableBitmap Wb { get => _wb; set => SetProperty(ref _wb, value); }
    public State State { get; set; }
    public List<Tile> EntitiesToHighlight { get; set; } = new();
    public List<Tile> AlreadyClicked { get; } = new();
    public Cell[,] AnswerCells { get; set; }
    private int _fps;
    private string _hoveredEntityDescription;
    private string _tileString;
    private long _ms;
    private WriteableBitmap _wb;
#pragma warning disable CS4014
    public bool AllowDiagonal { get => _allowDiagonal; set { _allowDiagonal = value; PathFinding(); } }
#pragma warning restore CS4014
    public bool AlwaysPath { get; set; } = true;
    public int TileSize { get; set; }
    public int Top { get; set; }
    public int Left { get; set; }
    public bool ShowNumbers { get; set; } = false;
    public long Ms { get => _ms; set => SetProperty(ref _ms, value); }
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public bool LeftButtonClick { get; set; }
    public string TileString { get => _tileString; set => SetProperty(ref _tileString, value); }
    public int TileWidth { get; set; }
    public int TileHeight { get; set; }
    public int CellsScored { get => _cellsScored; set => SetProperty(ref _cellsScored, value); }
    public string NewTileWidth { get; set; }
    public string NewTileHeight { get; set; }
    public int Fps { get => _fps; set => SetProperty(ref _fps, value); }
    public IStatePersistence _sp;
    public DateTime LastRequestedPathFind { get; set; }
    private int _cellsScored;
    private int _currentPlayer = 1;
    public int CurrentPlayer { get => _currentPlayer; set => SetProperty(ref _currentPlayer, value); }
    public Dictionary<int, (Tile Source, Tile Destination)> PlayerDictionary = new();
    private bool _allowDiagonal = true;
    public int PlayerCount { get; set; }
    public ClickMode ClickMode { get; set; } = ClickMode.Player;

    public RelayCommand ResetCommand { get; set; }

    public MainWindowViewModel(/*IStatePersistence statePersistence*/)
    {
        PlayerCount = 3;

        for (var i = 1; i <= PlayerCount; i++) { PlayerDictionary.Add(i, (null, null)); }

        PixelWidth = 600;
        PixelHeight = 600;
        TileWidth = 20;
        TileHeight = 20;
        var dpi = 96;
        var tileSize = 10;
        TileSize = tileSize;
        Wb = new(PixelWidth, PixelHeight, dpi, dpi, PixelFormats.Bgra32, null);
        _sp = new StatePersistence();
        State = new(TileWidth, TileHeight, tileSize, _sp);
        ResetCommand = new(Reset);
        SetupMapString();
        UploadMapString(@"3972_G4MPKI6UrMG9OgaGZmcAmLpKbFnWit9TRq0AVogGrRYWkQKSyyR9MuH9AenBv5sSeR+axQ4OHRwOESlSJAS5Z9t9tbIp5bwH/db7lgtKvQ3fH/yDCwonLSau8Y5aty1SCrbmxv20+I7bysl9AkepUN0bb8SGsANKLOiJCCOVU9u/PdYBet5gBsp6XKAeo8WuNkoNM9i8x35DUGYmR2HshCll3M4bPrVsPmGmOrfbSlJsVi5AcEMLzgbP00rTOd1HKeRUSX4C8L9Z9J5BfOKtSR8zs44M8O4CnJ34LyKDi1+JtWLy3HcqERkmHi4KuYeEAVZOkn7jlh3Ids0oomZVmhr0uiun2U/QT+4nJNkSyHAzQT3YYiIzDiVYB7yJesuCYk2e3Df2H0LT9zIwdRrIDAGbymDcWdFEvewh74DIswbs9HJiTy4feNxBJYXpdsockTBZH82r/DHk/7rdEm5CteuHA4xBLV4z55I+3SfLdkRAOvlY8Pk2kj0A");
    }

    public void Reset()
    {
        if (int.TryParse(NewTileWidth, out var tileWidth) && tileWidth > 10 && int.TryParse(NewTileHeight, out var tileHeight) && tileHeight > 10)
        {
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            TileSize = Math.Max(Math.Min(PixelWidth / tileWidth, PixelHeight / tileHeight), 3);
            State = new(TileWidth, TileHeight, TileSize, _sp);
            AnswerCells = null;
            PlayerDictionary.Clear();
            for (var i = 1; i <= PlayerCount; i++) { PlayerDictionary.Add(i, (null, null)); }
        }
    }

    public void UploadMapString(string mapString)
    {
        var mapStringResult = State.UploadMapString(mapString);
        if (!mapStringResult.Success) return;

        var lineNumber = 1;
        State = new(mapStringResult.X, mapStringResult.Y, State.TileSize, _sp);
        for (var x = 0; x < mapStringResult.X; x++)
        {
            lineNumber++;
            var row = mapStringResult.mapStringArray[lineNumber];
            for (var y = 0; y < mapStringResult.Y; y++)
            {
                State.TileGrid[x, y].IsPassable = row[y] == '0';
            }
        }

        TileWidth = State.X;
        TileHeight = State.Y;
    }

    public void SetupMapString() => TileString = State.SetupMapString();

    public void GetHoverElements(Point? point)
    {
        if (!point.HasValue) return;
        var tileR = GetTileAtLocation(point);
        if (tileR is null) return;
        if (tileR == EntitiesToHighlight.FirstOrDefault()) return;
        HoveredEntityDescription = $"{tileR.X},{tileR.Y}";
        EntitiesToHighlight.Clear();
        EntitiesToHighlight.Add(tileR);
    }

    public async Task PathFinding()
    {
        foreach (var tile in State.TileGrid) { tile.IsPartOfSolution = false; }

        var keys = PlayerDictionary.Keys.ToArray();
        var notableCount = 0;
        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index];
            var (source, destination) = PlayerDictionary[key];

            if (destination is null || source is null) { continue; }

            var sw = new Stopwatch();
            sw.Start();
            //Got all of the cells with H-Score.
            //if (LeftButtonClick) continue;
            if (!GetCellsOfInterest(out var destCell, out var sourceCell, key)) continue;
            var thisDate = DateTime.Now;
            LastRequestedPathFind = thisDate;
            Trace.WriteLine(thisDate);
            var cells = new Cell[State.X, State.Y];

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
                    Passable = t.IsPassable
                };
                if (t == destination) { destCell = cell; }
                if (t == source) { sourceCell = cell; }

                cells[t.X, t.Y] = cell;
            }

            //Not really useful at the moment.
            //await Chunking(cells, thisDate);
            Trace.WriteLine($"Player number: {key}. ({sourceCell.X},{sourceCell.Y}) to ({destCell.X},{destCell.Y})");
            Trace.WriteLine($"{sw.ElapsedMilliseconds} ms to set up solver."); //~10
            var solutionIds = await Solver.SolveAsync(cells, sourceCell, destCell, null, thisDate, AllowDiagonal);
            if (solutionIds.SolutionCells is null || !solutionIds.SolutionCells.Any()) continue;
            Ms = solutionIds.TimeToSolve;
            Trace.WriteLine($"{Ms}ms to solve ({sourceCell.X},{sourceCell.Y}) to ({destCell.X},{destCell.Y}).");
            if (solutionIds.thisDate != LastRequestedPathFind) continue;
            AnswerCells = solutionIds.AllCells;
            foreach (var cell in solutionIds.SolutionCells) { State.TileGrid[cell.X, cell.Y].IsPartOfSolution = true; /*Trace.WriteLine($"{State.TileGrid[cell.X, cell.Y].X},{State.TileGrid[cell.X, cell.Y].Y}");*/ }

            foreach (var cell in solutionIds.AllCells)
            {
                if (cell.FCost > int.MaxValue / 3) continue;
                notableCount++;
            }
            CellsScored = notableCount;
        }
    }

    [UsedImplicitly]
    private async Task Chunking(Cell[,] cells, DateTime thisDate)
    {
        var chunkSize = 8;
        var superCells = new List<Cell[,]>();
        var xChunks = TileWidth / chunkSize + 1;
        var yChunks = TileHeight / chunkSize + 1;
        //Just throw every tile into a chunk according to location

        SetChunksByGeometry(xChunks, yChunks, chunkSize, cells, superCells, out var chunkId);
        //Let's make sure that all of the cells in each chunk can reach all of the cells.

        await InvalidateDisconnectedCellsInChunks(superCells, chunkSize, thisDate);

        //let's go through all of the Chunk== -1 ones 

        foreach (var cell in cells)
        {
            if (cell.ChunkId == -1)
            {
            }
        }


        var color = 0;
        foreach (var supercell in superCells)
        {
            color++;
            foreach (var cell in supercell)
            {
                if (cell is null || State.TileGrid[cell.X, cell.Y] is null) continue;
                State.TileGrid[cell.X, cell.Y].ChunkId = cell.ChunkId;
            }
        }
    }

    private async Task InvalidateDisconnectedCellsInChunks(List<Cell[,]> superCells, int chunkSize, DateTime thisDate)
    {
        foreach (var supercell in superCells)
        {
            var tempCellGrid = new Cell[chunkSize + 1, chunkSize + 1];
            var initialCell = supercell[0, 0];
            for (var a = 0; a < chunkSize; a++)
            {
                for (var b = 0; b < chunkSize; b++)
                {
                    if (supercell[a, b] is null) continue;
                    tempCellGrid[a, b] = new()
                    {
                        Finished = false,
                        GScore = supercell[a, b].GScore,
                        Id = supercell[a, b].Id,
                        X = supercell[a, b].X % chunkSize,
                        Y = supercell[a, b].Y % chunkSize,
                        HScore = supercell[a, b].HScore,
                        Passable = supercell[a, b].Passable,
                        Destinations = new() { supercell[a, b] }
                    };
                }
            }

            Cell tempInitial = null;
            for (var a = 0; a < chunkSize; a++) //Need to find smallest index cell that is passable
            {
                for (var b = 0; b < chunkSize; b++)
                {
                    if (supercell[a, b] is null || !supercell[a, b].Passable) continue;
                    initialCell = tempCellGrid[a, b];
                    tempInitial = initialCell.Destinations.First();
                    //Trace.WriteLine($"Initial at: {tempInitial.X}, {tempInitial.Y}");
                    goto gitInitial;
                }
            }

        gitInitial:

            for (var a = 0; a < chunkSize; a++)
            {
                for (var b = 0; b < chunkSize; b++)
                {
                    if (tempCellGrid[a, b] is null || !tempCellGrid[a, b].Passable) continue;
                    var destinationCell = tempCellGrid[a, b];
                    if (initialCell.Id == destinationCell.Id) continue;
                    var result = await Solver.SolveAsync(tempCellGrid, initialCell, destinationCell, null, thisDate,
                        AllowDiagonal);
                    var resultCell = destinationCell.Destinations.First();

                    if (result.SolutionCells is null || !result.SolutionCells.Any())
                    {
                        /*Trace.WriteLine($"Could not navigate from {tempInitial.X}, {tempInitial.Y} to {resultCell.X}, {resultCell.Y}");*/
                        destinationCell.Destinations.First().ChunkId = -1;
                    }
                    else
                    {
                        /*Trace.WriteLine($"Navigated from {tempInitial.X}, {tempInitial.Y} to {resultCell.X}, {resultCell.Y} via :{string.Join(", ", result.SolutionCells.Select(c => $"({c.X},{c.Y})"))}");*/
                    }
                }
            }
        }
    }

    private void SetChunksByGeometry(int xChunks, int yChunks, int chunkSize, Cell[,] cells, List<Cell[,]> superCells, out int chunkId)
    {
        chunkId = -1;
        for (var x = 0; x < xChunks; x++)
        {
            for (var y = 0; y < yChunks; y++)
            {
                chunkId++;
                var tempChunk = new Cell[chunkSize + 1, chunkSize + 1];
                for (var a = 0; a <= chunkSize; a++)
                {
                    for (var b = 0; b <= chunkSize; b++)
                    {
                        if (x * chunkSize + a >= TileWidth || y * chunkSize + b >= TileHeight) { continue; }

                        tempChunk[a, b] = cells[x * chunkSize + a, y * chunkSize + b];
                        cells[x * chunkSize + a, y * chunkSize + b].ChunkId = chunkId;
                    }
                }
                superCells.Add(tempChunk);
            }
        }
    }

    private bool GetCellsOfInterest(out Cell destCell, out Cell sourceCell, int playerNumber)
    {
        //var sw = new Stopwatch();
        //sw.Start();
        (sourceCell, destCell) = GetCellsFromTiles(playerNumber);
        //Trace.WriteLine($"{sw.ElapsedMilliseconds} ms to get Cells of interest"); //6
        return !(sourceCell is null || destCell is null);
    }

    public (Cell sourceCell, Cell destinationCell) GetCellsFromTiles(int playerNumber)
    {
        //var sw = new Stopwatch();
        //sw.Start();
        if (PlayerDictionary[playerNumber].Destination is null || PlayerDictionary[playerNumber].Source is null) return (null, null);
        var destination = PlayerDictionary[playerNumber].Destination;
        var source = PlayerDictionary[playerNumber].Source;
        Cell destinationCell = new() { Id = destination.Id, HScore = Math.Abs(destination.X - destination.X) + Math.Abs(destination.Y - destination.Y), X = destination.X, Y = destination.Y, Passable = destination.IsPassable };
        Cell sourceCell = new() { Id = source.Id, HScore = Math.Abs(source.X - destination.X) + Math.Abs(source.Y - destination.Y), X = source.X, Y = source.Y, Passable = source.IsPassable };
        //Trace.WriteLine($"{sw.ElapsedMilliseconds} ms to get Cells of interest 2"); //4
        return (sourceCell, destinationCell);
    }

    public async Task TryFlipElement(Point? point)
    {
        if (!point.HasValue) return;
        var tile = GetTileAtLocation(point);
        if (tile is null) return;
        if (ClickMode == ClickMode.Player) { await ProcessPlayerModeClick(tile); }

    }

    private async Task ProcessPlayerModeClick(Tile tile)
    {
        if (AlreadyClicked.Any(t => t.Id == tile.Id)) return;
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
        if (AlwaysPath) await PathFinding();
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

    public void FlipElementSourceDestination(Point? point)
    {
        //Stopwatch sw = new Stopwatch(); //2 ms
        //sw.Start();
        if (!point.HasValue) return;
        var tile = GetTileAtLocation(point);
        if (tile is null) return;
        if (ClickMode == ClickMode.Player) { HandleRightClickPlayerMode(tile); }
    }

    private async void HandleRightClickPlayerMode(Tile tile)
    {
        //ToDo: Make sure right-clicking source/destination doesn't put it in a bad state.
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
                if (AlwaysPath) await PathFinding();
                return;
            }

            if (PlayerDictionary[CurrentPlayer].Destination is null)
            {
                PlayerDictionary[CurrentPlayer] = (PlayerDictionary[CurrentPlayer].Source, tile);
                tile.TileRole = TileRole.Destination;
                if (AlwaysPath) await PathFinding();
                return;
            }
        }
        
    }

    public async Task Tick(Point? point, bool clicked)
    {
        GetHoverElements(point);
        if (clicked) { await TryFlipElement(point); }
        else { AlreadyClicked.Clear(); }
    }
}

public class Tile
{
    public string Name { get; set; }
    public bool IsPassable;
    public readonly int Id;
    internal TileRole TileRole = TileRole.Nothing;
    public bool IsPartOfSolution;
    public int X;
    public int Y;
    public int ChunkId = -1;
    public string Description => Name;

    public Tile(int x, int y, bool isPassable, int id)
    {
        Id = id;
        X = x;
        Y = y;
        IsPassable = isPassable;
        Name = $"{x},{y}";
    }
}

public enum TileRole { Nothing = 0, Source = 1, Destination = 2 }

public class State
{
    public int X;
    public int Y;
    private readonly IStatePersistence _sp;
    private Tile[,] _tileGrid;
    public List<Tile> Tiles { get; set; } = new();
    public int TileSize { get; }
    public Tile[,] TileGrid { get => _tileGrid; set => _tileGrid = value; }

    public State(int x, int y, int tileSize, IStatePersistence sp)
    {
        X = x;
        Y = y;
        _sp = sp;
        TileSize = tileSize;
        TileGrid = new Tile[x, y];
        SetTiles();
    }

    private void SetTiles()
    {
        var i = 0;
        for (var x = 0; x < X; x++)
        {
            for (var y = 0; y < Y; y++)
            {
                var tempTile = new Tile(x, y, true, i++);
                TileGrid[x, y] = tempTile;
                Tiles.Add(tempTile);
            }
        }
    }

    public string SetupMapString() => _sp.SetupMapString(ref X, ref Y, ref _tileGrid);

    public (int X, int Y, string[] mapStringArray, bool Success) UploadMapString(string mapString) => _sp.UploadMapString(mapString);
}

public interface IStatePersistence
{
    string SetupMapString(ref int X, ref int Y, ref Tile[,] TileGrid);
    (int X, int Y, string[] mapStringArray, bool Success) UploadMapString(string mapString);
}

public class StatePersistence : IStatePersistence
{
    public string SetupMapString(ref int X, ref int Y, ref Tile[,] TileGrid)
    {
        var sb = new StringBuilder();
        sb.Append(X + ";");
        sb.Append(Y + ";");
        for (var x = 0; x < X; x++)
        {
            for (var y = 0; y < Y; y++) { sb.Append(TileGrid[x, y].IsPassable ? "0" : "1"); }
            sb.Append(";");
        }

        var result = GetCompressedString(sb.ToString());
        return result;
    }

    private string GetCompressedString(string x)
    {
        var input = Encoding.Unicode.GetBytes(x);
        var memory = new byte[input.Length];
        var encoded = BrotliEncoder.TryCompress(input, memory, out var outputLength);
        if (!encoded) return null;
        return $"{memory.Length}_" + Convert.ToBase64String(memory.Take(outputLength).ToArray());
    }

    private string DecompressString(string x)
    {
        var splitStrings = x.Split('_');
        var input = Convert.FromBase64String(splitStrings[1]);
        if (!int.TryParse(splitStrings[0], out var decompressSize)) return null;
        var output = new byte[decompressSize];
        if (!BrotliDecoder.TryDecompress(input, output, out var _)) return null;
        var str = Encoding.Unicode.GetString(output);
        return str;
    }

    public (int X, int Y, string[] mapStringArray, bool Success) UploadMapString(string mapString)
    {
        mapString = mapString.Trim();
        var decompressed = DecompressString(mapString);
        if (decompressed is not null) mapString = decompressed;

        var strings = mapString.Split(';');

        if (!int.TryParse(strings[0], out var newWidth)) return (-1, -1, null, false);
        if (!int.TryParse(strings[1], out var newHeight)) return (-1, -1, null, false);

        return (newWidth, newHeight, mapString.Split(';'), true);
    }
}