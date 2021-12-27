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
using Point = System.Windows.Point;

namespace PathFinding.Stuff;

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
    public bool AllowDiagonal { get; set; } = true;
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

    public RelayCommand ResetCommand { get; set; }

    public MainWindowViewModel(/*IStatePersistence statePersistence*/)
    {
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
        UploadMapString(@"852_G1MDoI6UrMG9GiZGZmcAQC4OcIxxKhkF2mUv6OGCHTAt23ZocwNyrfhXqphANJ0KX0q1f5I4IwI=");
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
        var sw = new Stopwatch();
        sw.Start();
        //Got all of the cells with H-Score.
        if (LeftButtonClick) return;
        if (!GetCellsOfInterest(out var destCell, out var sourceCell)) return;
        var thisDate = DateTime.Now;
        LastRequestedPathFind = thisDate;

        var cells = new Cell[State.X, State.Y];

        foreach (var t in State.Tiles)
        {
            var hScore = Math.Abs(t.X - destCell.X) + Math.Abs(t.Y - destCell.Y);
            var cell = new Cell() { HScore = hScore * 10, GScore = int.MaxValue / 2, X = t.X, Y = t.Y, Id = t.Id, Passable = t.IsPassable };
            if (t.TileRole == TileRole.Destination) { destCell = cell; }
            if (t.TileRole == TileRole.Source) { sourceCell = cell; }
            cells[t.X, t.Y] = cell;
        }

        //Not really useful at the moment.
        //await Chunking(cells, thisDate);

        Trace.WriteLine($"{sw.ElapsedMilliseconds} ms to set up solver."); //~10
        var solutionIds = await Solver.SolveAsync(cells, sourceCell, destCell, null, thisDate, AllowDiagonal);
        if (solutionIds.SolutionCells is null || !solutionIds.SolutionCells.Any()) return;
        Ms = solutionIds.TimeToSolve;
        Trace.WriteLine($"{Ms}ms to solve.");
        if (solutionIds.thisDate != LastRequestedPathFind) return;
        AnswerCells = solutionIds.AllCells;
        foreach (var cell in solutionIds.SolutionCells)
        {
            var theCell = State.TileGrid[cell.X, cell.Y];
            theCell.IsPartOfSolution = true;
        }
        var notableCount = 0;
        foreach (var cell in solutionIds.AllCells)
        {
            if (cell.FCost > int.MaxValue / 3) continue;
            notableCount++;
        }
        CellsScored = notableCount;
    }

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


        int color = 0;
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

    private bool GetCellsOfInterest(out Cell destCell, out Cell sourceCell)
    {
        //var sw = new Stopwatch();
        //sw.Start();
        destCell = null;
        sourceCell = null;
        var cells = GetCellsFromTile();
        if (cells.sourceCell is null) return false;
        destCell = cells.destinationCell;
        sourceCell = cells.sourceCell;
        //Trace.WriteLine($"{sw.ElapsedMilliseconds} ms to get Cells of interest"); //6
        return true;
    }

    public (Cell sourceCell, Cell destinationCell) GetCellsFromTile()
    {
        //var sw = new Stopwatch();
        //sw.Start();
        //Reset all tiles visually.
        foreach (var stateTile in State.Tiles) { stateTile.IsPartOfSolution = false; }
        var source = State.Tiles.FirstOrDefault(x => x.TileRole == TileRole.Source);
        if (source is null) return (null, null);
        var destinationTile = State.Tiles.FirstOrDefault(x => x.TileRole == TileRole.Destination);
        if (destinationTile is null) return (null, null);
        var cells = GetTileStateCells(destinationTile, State.Tiles);
        //Trace.WriteLine($"{sw.ElapsedMilliseconds} ms to get Cells of interest 2"); //4
        return cells;
    }

    private (Cell sourceCell, Cell destinationCell) GetTileStateCells(Tile destination, IList<Tile> tiles)
    {
        Cell destinationCell = null;
        Cell sourceCell = null;
        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (tile.TileRole == TileRole.Nothing) continue;
            if (tile.TileRole == TileRole.Destination) { destinationCell = new() { Id = tile.Id, HScore = Math.Abs(tile.X - destination.X) + Math.Abs(tile.Y - destination.Y), X = tile.X, Y = tile.Y, Passable = tile.IsPassable }; }
            if (tile.TileRole == TileRole.Source) { sourceCell = new() { Id = tile.Id, HScore = Math.Abs(tile.X - destination.X) + Math.Abs(tile.Y - destination.Y), X = tile.X, Y = tile.Y, Passable = tile.IsPassable }; }
            if (destinationCell is not null && sourceCell is not null) break;
        }
        if (sourceCell is null || destinationCell is null) return (null, null);
        return (sourceCell, destinationCell);
    }

    public async void TryFlipElement(Point? point)
    {
        if (!point.HasValue) return;
        var tile = GetTileAtLocation(point);
        if (tile is null) return;
        tile.IsPassable = !tile.IsPassable;
        if (!tile.IsPassable) { tile.TileRole = TileRole.Nothing; }
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

        if (tile.TileRole == TileRole.Destination || tile.TileRole == TileRole.Source || !tile.IsPassable) { tile.TileRole = TileRole.Nothing; /*Trace.WriteLine($"Time to right-click: {sw.ElapsedMilliseconds} ms");*/ return; }
        if (tile.TileRole == TileRole.Nothing && tile.IsPassable && State.Tiles.All(t => t.TileRole != TileRole.Source)) { tile.TileRole = TileRole.Source; /*Trace.WriteLine($"Time to right-click: {sw.ElapsedMilliseconds} ms");*/  return; }
        if (tile.TileRole == TileRole.Nothing && tile.IsPassable && State.Tiles.All(t => t.TileRole != TileRole.Destination)) tile.TileRole = TileRole.Destination; /*Trace.WriteLine($"Time to right-click: {sw.ElapsedMilliseconds} ms");*/
    }

    public async Task Tick(Point? point, bool clicked)
    {
        GetHoverElements(point);
        if (clicked) { await GetDownClicks(point); }
        else { AlreadyClicked.Clear(); }
    }

    private async Task GetDownClicks(Point? point)
    {
        var tile = GetTileAtLocation(point);
        if (tile is null) return;
        if (AlreadyClicked.Any(t => t.Id == tile.Id)) return;
        tile.IsPassable = !tile.IsPassable;
        AlreadyClicked.Add(tile);
        if (AlwaysPath) await PathFinding();
        SetupMapString();
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
            for (var y = 0; y < Y; y++)
            {
                sb.Append(TileGrid[x, y].IsPassable ? "0" : "1");
            }
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