using Priority_Queue;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PathFinding.Core
{
    public static class Solver
    {
        //Should probably be able to pass in a dictionary of costs.
        //TODO: try vector field path finding https://www.youtube.com/watch?v=ZJZu3zLMYAc&ab_channel=PDN-PasDeNom
        //TODO: Can also improve this one by separating nodes into groups, where each node in a group can touch all others. Keep them small, and the overall problem size will be reduced considerably.
        //Show groups with this: https://www.youtube.com/watch?v=f1WQpqZKoYw&ab_channel=Ms.Hearn

        public static async Task<(List<CellInfo> SolutionCells, Cell[,] AllCells, long TimeToSolve, DateTime thisDate)> SolveAsync(Cell[,] cellGrid, Cell[] AllCells, Cell sourceCell, Cell destCell, IReadOnlyDictionary<(int, int), List<(int, int)>> targets, DateTime thisDate, bool diagonal = false)
        {
            return await Task.Run(() => Solve(cellGrid, AllCells.AsSpan(), sourceCell, destCell, targets, thisDate, diagonal));
        }

        public static (List<CellInfo> SolutionCells, Cell[,] AllCells, long TimeToSolve, DateTime thisDate) Solve(Cell[,] cellGrid, Span<Cell> AllCells, Cell sourceCell, Cell destCell, IReadOnlyDictionary<(int, int), List<(int, int)>> targets, DateTime thisDate, bool diagonal = false)
        {
            var startMemory = GC.GetAllocatedBytesForCurrentThread();
            var s = new Stopwatch();
            s.Start();
            if (cellGrid is null) return (null, null, s.ElapsedMilliseconds, thisDate);

            FastPriorityQueue<Cell> priorityQueue = new FastPriorityQueue<Cell>(cellGrid.GetLength(0) * cellGrid.GetLength(1));
            var cellInfoPriorityQueue = new FastPriorityQueue<CellInfo>(cellGrid.GetLength(0) * cellGrid.GetLength(1));
            var resultThing = CellInfo.GetCellGrid(AllCells, sourceCell, destCell, cellInfoPriorityQueue, cellGrid.GetLength(0), cellGrid.GetLength(1));

            sourceCell.GScore = 0;
            //var count = 0;

            while (cellInfoPriorityQueue.Contains(resultThing[destCell.X, destCell.Y]))
            {
                if (!cellInfoPriorityQueue.Any()) return (null, null, s.ElapsedMilliseconds, thisDate);
                //count++;
                var changed = PathIteration(resultThing, resultThing[destCell.X, destCell.Y], targets, diagonal, cellInfoPriorityQueue);
                if (changed == 0) return (null, null, s.ElapsedMilliseconds, thisDate);
            }

            //Trace.WriteLine($"Time for path solving: {s.ElapsedMilliseconds}ms for {count} iterations");
            //s.Restart();
            var solutionIds = GetSolutionCells(resultThing[sourceCell.X, sourceCell.Y], resultThing[destCell.X, destCell.Y], resultThing);
            if (solutionIds is null || !solutionIds.Any()) return (null, null, s.ElapsedMilliseconds, thisDate);
            //Trace.WriteLine($"Time for path solution extraction: {s.ElapsedMilliseconds}"); //10
            var endMemory = GC.GetAllocatedBytesForCurrentThread();
            //Trace.WriteLine($"Allocated: {(endMemory - startMemory) / 1024} kb.");
            return (solutionIds, cellGrid, s.ElapsedMilliseconds, thisDate);
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)] //Doesn't help.
        public static int PathIteration(CellInfo[,] cellGrid, CellInfo destCell, IReadOnlyDictionary<(int, int), List<(int, int)>> targets, bool diagonal, FastPriorityQueue<CellInfo> priorityQueue)
        {
            //var s = new Stopwatch();
            //s.Start();
            var changes = 0;
            var best = priorityQueue.Dequeue();
            if (best.FCost > int.MaxValue / 2) return 0;

            var neighbors = GetNeighborCells(best, cellGrid, destCell, targets, diagonal);
            changes += neighbors.Count;
            foreach (var neighbor in neighbors)
            {
                int distance = Math.Abs(best.X - neighbor.X) + Math.Abs(best.Y - neighbor.Y);
                changes += SetNeighbor(best, neighbor, best.GScore + (distance > 1 ? 14 : 10), priorityQueue);// Cell.Distance(neighbor, best) == 1 ? 10 : 14);
            }
            best.Finished = true;

            //Trace.WriteLine($"Time for path PathIteration: {s.ElapsedMilliseconds}"); //
            return changes;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)] //Doesn't help.
        public static List<CellInfo> GetSolutionCells(CellInfo sourceCell, CellInfo tempCell, CellInfo[,] cellGrid)
        {
            var tempList = new List<CellInfo>();
            do
            {
                tempList.Add(tempCell);
                if (tempCell.Predecessor is not null) { tempCell = cellGrid[tempCell.Predecessor.Value.X, tempCell.Predecessor.Value.Y]; }
            } while (tempCell.Predecessor is not null);
            tempList.Add(sourceCell);
            return tempList;
        }

        private static List<CellInfo> GetNeighborCells(CellInfo sourceCell, CellInfo[,] cellGrid, CellInfo destCell, IReadOnlyDictionary<(int, int), List<(int, int)>> targets = null, bool diagonal = false)
        {
            var s = new Stopwatch();
            s.Start();

            if (targets is not null
                && targets.Any() /*Is there a valid target?*/
                && targets.TryGetValue((sourceCell.X, sourceCell.Y), out var neighbors) /*Are any of the targets possible?*/
                && neighbors.Select(x => cellGrid[x.Item1, x.Item2]).Any(x => x.Passable))
            {
                //Get reasonable results.
                return neighbors.Select(x => cellGrid[x.Item1, x.Item2]).ToList();
            }

            var results = new List<CellInfo>();
            for (var x = -1; x < 2; x++)
            {
                if (sourceCell.X + x < 0 || sourceCell.X + x >= cellGrid.GetLength(0)) continue;
                for (var y = -1; y < 2; y++)
                {
                    if (sourceCell.Y + y < 0 || sourceCell.Y + y >= cellGrid.GetLength(1)) continue;
                    int distance = Math.Abs(x) + Math.Abs(y);
                    if (distance == 0) continue;
                    if (!diagonal && distance != 1) continue;
                    if (distance == 2)
                    {
                        if (cellGrid[sourceCell.X + x, sourceCell.Y] is null || cellGrid[sourceCell.X, sourceCell.Y + y] is null || cellGrid[sourceCell.X + x, sourceCell.Y + y] is null || cellGrid[sourceCell.X, sourceCell.Y] is null) continue;
                        if (cellGrid[sourceCell.X + x, sourceCell.Y].Passable && cellGrid[sourceCell.X, sourceCell.Y + y].Passable &&
                            (cellGrid[sourceCell.X + x, sourceCell.Y + y].Passable || cellGrid[sourceCell.X + x, sourceCell.Y + y].Id == destCell.Id))
                        {
                            results.Add(cellGrid[sourceCell.X + x, sourceCell.Y + y]);
                        }
                        continue;
                    }
                    if (cellGrid[sourceCell.X + x, sourceCell.Y + y] is null) continue;
                    if (cellGrid[sourceCell.X + x, sourceCell.Y + y].Passable || cellGrid[sourceCell.X + x, sourceCell.Y + y].Id == destCell.Id) { results.Add(cellGrid[sourceCell.X + x, sourceCell.Y + y]); }
                }
            }
            //Trace.WriteLine($"Time for GetNeighborCells (grid): {s.ElapsedMilliseconds}"); //
            return results;
        }

        public static int SetNeighbor(CellInfo sourceCell, CellInfo neighborCell, int cost, FastPriorityQueue<CellInfo> priorityQueue)
        {
            //var s = new Stopwatch();
            //s.Start();
            if (neighborCell.Finished) return 0;
            if (cost < neighborCell.GScore || neighborCell.Predecessor is null)
            {
                neighborCell.GScore = Math.Min(cost, neighborCell.GScore);
                neighborCell.Predecessor = (sourceCell.X, sourceCell.Y);
                priorityQueue.UpdatePriority(neighborCell, neighborCell.FCost);
            }
            return 1;
        }
    }

    //Need to have a class like "CellInformation". Then I could have a reference to Cell, but many places in the code also require that the Cell know about the CellInformation. Which basically makes it like I have to copy everything.
    //I guess I could have CellInformation[x,y] that I can use to look up Cell -> CellInformation without adding a reference directly in Cell (which would make other clels 

    public class CellInfo : FastPriorityQueueNode
    {
        public int Id;
        public int X;
        public int Y;
        public bool Passable;
        public int ChunkId = -1;
        public int GScore;
        public int HScore;
        public int FCost => GScore + HScore;
        public bool Finished;
        public (int X, int Y)? Predecessor;
        /// <summary>
        /// The idea behind this is: this array can be calculated/tossed every PathFind and calculating it in this class can make it better async.
        /// Something I need to check is... Why not just store id,x,y,passable,chunkId in here as well? Then it's completely standalone.
        /// That would make it easier for this project to be plug-and-play. Also, then I could pass the cells as a ReadOnlySpan.
        /// </summary>
        /// <param name="cells"></param>
        /// <param name="sourceCell"></param>
        /// <param name="destCell"></param>
        /// <param name="priorityQueue"></param>
        /// <param name="xExtent"></param>
        /// <param name="yExtent"></param>
        /// <returns></returns>
        public static CellInfo[,] GetCellGrid(ReadOnlySpan<Cell> cells, Cell sourceCell, Cell destCell, FastPriorityQueue<CellInfo> priorityQueue, int xExtent, int yExtent)
        {
            CellInfo[,] result = new CellInfo[xExtent, yExtent];
            foreach (var t in cells)
            {
                var cellInfo = new CellInfo() { Id = t.Id, X = t.X, Y = t.Y, Passable = t.Passable, ChunkId = t.ChunkId, GScore = int.MaxValue / 2, HScore = Math.Abs(t.X - destCell.X) + Math.Abs(t.Y - destCell.Y) };

                if (t.X != sourceCell.X || t.Y != sourceCell.Y)
                {
                    result[t.X, t.Y] = cellInfo;
                    priorityQueue.Enqueue(cellInfo, cellInfo.FCost);
                }
                else
                {
                    cellInfo.GScore = 0;
                    result[t.X, t.Y] = cellInfo;
                    priorityQueue.Enqueue(result[cellInfo.X, cellInfo.Y], cellInfo.FCost);
                }
            }
            return result;
        }
    }

    public sealed class Cell : FastPriorityQueueNode
    {
        public int Id;
        public int X;
        public int Y;
        public bool Passable;
        public int ChunkId = -1;
        //Everything below is concerns of solver only. Shouldn't be handled/populated in MainViewModel.
        public int GScore;
        public int HScore;
        public int FCost => GScore + HScore;
        public bool Finished;
        public (int X, int Y)? Predecessor;
        //public List<Cell> Destinations;
        public override string ToString() => $"{Id}: {X}, {Y}, Passable: {Passable}, F: {FCost}, G: {GScore}, H: {HScore}. Finished= {Finished}";
    }

    public sealed class SuperCell
    {
        public Cell[,] Cells;
        public bool Checked = false;
        public List<SuperCell> Connections = new();
    }
}