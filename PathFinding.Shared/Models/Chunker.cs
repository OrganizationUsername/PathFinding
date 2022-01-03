using PathFinding.Annotations;
using PathFinding.Core;

namespace PathFinding.Shared.Models;

public static class Chunker
{
    [UsedImplicitly]
    public static async Task Chunking(Cell[,] cells, DateTime thisDate, Tile[,] TileGrid, int TileWidth, int TileHeight)
    {
        var chunkSize = 8;
        var superCells = new List<Cell[,]>();
        var xChunks = TileWidth / chunkSize + 1;
        var yChunks = TileHeight / chunkSize + 1;
        //Just throw every tile into a chunk according to location

        SetChunksByGeometry(xChunks, yChunks, chunkSize, cells, superCells, out var chunkId, TileWidth, TileHeight);
        //Let's make sure that all of the cells in each chunk can reach all of the cells.

        await InvalidateDisconnectedCellsInChunks(superCells, chunkSize, thisDate, false);

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
                if (cell is null || TileGrid[cell.X, cell.Y] is null) continue;
                TileGrid[cell.X, cell.Y].ChunkId = cell.ChunkId;
            }
        }
    }

    public static async Task InvalidateDisconnectedCellsInChunks(List<Cell[,]> superCells, int chunkSize, DateTime thisDate, bool allowDiagonal)
    {
        foreach (var superCell in superCells)
        {
            var tempCellGrid = new Cell[chunkSize + 1, chunkSize + 1];
            var initialCell = superCell[0, 0];
            for (var a = 0; a < chunkSize; a++)
            {
                for (var b = 0; b < chunkSize; b++)
                {
                    if (superCell[a, b] is null) continue;
                    tempCellGrid[a, b] = new()
                    {
                        Finished = false,
                        GScore = superCell[a, b].GScore,
                        Id = superCell[a, b].Id,
                        X = superCell[a, b].X % chunkSize,
                        Y = superCell[a, b].Y % chunkSize,
                        HScore = superCell[a, b].HScore,
                        Passable = superCell[a, b].Passable,
                        Destinations = new() { superCell[a, b] }
                    };
                }
            }

            for (var a = 0; a < chunkSize; a++) //Need to find smallest index cell that is passable
            {
                for (var b = 0; b < chunkSize; b++)
                {
                    if (superCell[a, b] is null || !superCell[a, b].Passable) continue;
                    initialCell = tempCellGrid[a, b];
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
                    var result = await Solver.SolveAsync(tempCellGrid, initialCell, destinationCell, null, thisDate, allowDiagonal);
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

    public static void SetChunksByGeometry(int xChunks, int yChunks, int chunkSize, Cell[,] cells, List<Cell[,]> superCells, out int chunkId, int TileWidth, int TileHeight)
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

}