using PathFinding.Core;
using PathFinding.Persistence;

namespace PathFinding.Shared.Models;

public class State
{
    public int X;
    public int Y;
    private readonly IStatePersistence _sp;
    private Tile[,] _tileGrid;
    public List<Tile> Tiles { get; set; } = new();
    public int TileSize { get; }
    public Tile[,] TileGrid { get => _tileGrid; set => _tileGrid = value; }
    public Cell[,] CellGrid;
    public Cell[] Cells;

    public State(int x, int y, int tileSize, IStatePersistence sp)
    {
        X = x;
        Y = y;
        _sp = sp;
        TileSize = tileSize;
        TileGrid = new Tile[x, y];
        CellGrid = new Cell[x, y];
        Cells = new Cell[x * y];
        SetTiles();
    }

    private void SetTiles()
    {
        var i = 0;
        for (var x = 0; x < X; x++)
        {
            for (var y = 0; y < Y; y++)
            {
                var tempCell = new Cell() { X = x, Y = y, Passable = true, Id = i };
                Cells[i] = tempCell;
                var tempTile = new Tile(x, y, true, i++);
                TileGrid[x, y] = tempTile;
                CellGrid[x, y] = tempCell;
                Tiles.Add(tempTile);
            }
        }
    }

    public string SetupMapString() => _sp.SetupMapString(ref X, ref Y, ref _tileGrid);

    public (int X, int Y, string[] mapStringArray, bool Success) UploadMapString(string mapString) => _sp.UploadMapString(mapString);
}

