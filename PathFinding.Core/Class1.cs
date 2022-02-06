using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Priority_Queue;

namespace PathFinding.Core
{
    public static class Solver
    {
        static string Log(string message, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        {
            return ($" {Path.GetFileName(file)}, {line}, {message}");
        }
        //Should probably be able to pass in a dictionary of costs.
        //TODO: try vector field path finding https://www.youtube.com/watch?v=ZJZu3zLMYAc&ab_channel=PDN-PasDeNom
        //TODO: Can also improve this one by separating nodes into groups, where each node in a group can touch all others. Keep them small, and the overall problem size will be reduced considerably.
        //Show groups with this: https://www.youtube.com/watch?v=f1WQpqZKoYw&ab_channel=Ms.Hearn

        public static async Task<(List<Cell> SolutionCells, Cell[,] AllCells, long TimeToSolve, DateTime thisDate)> SolveAsync(Cell[,] cellGrid, Cell sourceCell, Cell destCell, IReadOnlyDictionary<(int, int), List<(int, int)>> targets, DateTime thisDate, bool diagonal = false) => await Task.Run(() => Solve(cellGrid, sourceCell, destCell, targets, thisDate, diagonal));

        public static (List<Cell> SolutionCells, Cell[,] AllCells, long TimeToSolve, DateTime thisDate) Solve(Cell[,] cellGrid, Cell sourceCell, Cell destCell, IReadOnlyDictionary<(int, int), List<(int, int)>> targets, DateTime thisDate, bool diagonal = false)
        {
            var startMemory = GC.GetAllocatedBytesForCurrentThread();
            var s = new Stopwatch();
            s.Start();
            if (cellGrid is null) return (null, null, s.ElapsedMilliseconds, thisDate);
            //Trace.WriteLine($"Source at: {sourceCell.X},{sourceCell.Y}");
            //Trace.WriteLine($"Destination at: {destCell.X},{destCell.Y}");
            var priorityQueue = new SimplePriorityQueue<Cell>();
            for (var x = 0; x < cellGrid.GetLength(0); x++)
            {
                for (var y = 0; y < cellGrid.GetLength(1); y++)
                {
                    if (x == sourceCell.X && y == sourceCell.Y)
                    {
                        priorityQueue.Enqueue(cellGrid[x, y], 0);
                    }
                    else
                    {
                        if (cellGrid[x, y] is null) continue;
                        priorityQueue.Enqueue(cellGrid[x, y], cellGrid[x, y].FCost);
                    }
                }
            }
            Trace.WriteLine($"To enqueue: {s.Elapsed.Milliseconds} ms.");
            sourceCell.GScore = 0;
            var count = 0;
            while (destCell.Finished == false)
            {
                if (!priorityQueue.Any()) return (null, null, s.ElapsedMilliseconds, thisDate);
                count++;
                var changed = PathIteration(cellGrid, destCell, targets, diagonal, priorityQueue);
                if (changed == 0) return (null, null, s.ElapsedMilliseconds, thisDate);
            }
            Trace.WriteLine($"Time for path solving: {s.Elapsed.Milliseconds} ms for {count} iterations.");
            //Trace.WriteLine($"Time for path solving: {s.ElapsedMilliseconds}ms for {count} iterations");
            //s.Restart();
            var solutionIds = GetSolutionCells(sourceCell, destCell, cellGrid);
            Trace.WriteLine($"Time to get solution cells: {s.Elapsed.Milliseconds} ms.");
            if (solutionIds is null || !solutionIds.Any()) return (null, null, s.ElapsedMilliseconds, thisDate);
            //Trace.WriteLine($"Time for path solution extraction: {s.ElapsedMilliseconds}"); //10
            //var endMemory = GC.GetAllocatedBytesForCurrentThread();
            //Trace.WriteLine($"Allocated: {(endMemory - startMemory) / 1024} kb.");
            return (solutionIds, cellGrid, s.Elapsed.Milliseconds, thisDate);
        }

        public static int PathIteration(Cell[,] cellGrid, Cell destCell, IReadOnlyDictionary<(int, int), List<(int, int)>> targets, bool diagonal, SimplePriorityQueue<Cell> priorityQueue)
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

        public static List<Cell> GetSolutionCells(Cell sourceCell, Cell tempCell, Cell[,] cellGrid)
        {
            var tempList = new List<Cell>();
            //Cell tempCell = destinationCell;
            do
            {
                tempList.Add(tempCell);
                if (tempCell.Predecessor is not null) { tempCell = cellGrid[tempCell.Predecessor.Value.X, tempCell.Predecessor.Value.Y]; }
            } while (tempCell.Predecessor is not null);
            tempList.Add(sourceCell);
            return tempList;
        }

        private static List<Cell> GetNeighborCells(Cell sourceCell, Cell[,] cellGrid, Cell destCell, IReadOnlyDictionary<(int, int), List<(int, int)>> targets = null, bool diagonal = false)
        {
            var s = new Stopwatch();
            s.Start();

            if (targets is not null && targets.Any() &&
                /*Is there a valid target?*/
                targets.TryGetValue((sourceCell.X, sourceCell.Y), out var neighbors) &&
                /*Are any of the targets possible?*/
                neighbors.Select(x => cellGrid[x.Item1, x.Item2]).Any(x => x.Passable))
            {
                /*Get reasonable results.*/
                return neighbors.Select(x => cellGrid[x.Item1, x.Item2]).ToList();
            }

            var maxX = cellGrid.GetLength(0);
            var maxY = cellGrid.GetLength(1);

            var results = new List<Cell>();
            for (var x = -1; x < 2; x++)
            {
                var tempX = sourceCell.X + x;
                if (tempX < 0 || tempX >= maxX) continue;
                for (var y = -1; y < 2; y++)
                {
                    var tempY = sourceCell.Y + y;
                    if (tempY < 0 || tempY >= maxY) continue;
                    int distance = Math.Abs(x) + Math.Abs(y);
                    if (distance == 0) continue;
                    if (!diagonal && distance != 1) continue;
                    if (distance == 2)
                    {
                        if (cellGrid[tempX, sourceCell.Y] is null || cellGrid[sourceCell.X, tempY] is null || cellGrid[tempX, tempY] is null || cellGrid[sourceCell.X, sourceCell.Y] is null) continue;
                        if (cellGrid[tempX, sourceCell.Y].Passable && cellGrid[sourceCell.X, tempY].Passable &&
                            (cellGrid[tempX, tempY].Passable || cellGrid[tempX, tempY].Id == destCell.Id))
                        {
                            results.Add(cellGrid[tempX, tempY]);
                        }
                        continue;
                    }
                    if (cellGrid[tempX, tempY] is null) continue;
                    if (cellGrid[tempX, tempY].Passable || cellGrid[tempX, tempY].Id == destCell.Id) { results.Add(cellGrid[tempX, tempY]); }
                }
            }
            //Trace.WriteLine($"Time for GetNeighborCells (grid): {s.ElapsedMilliseconds}"); //
            return results;
        }

        public static int SetNeighbor(Cell sourceCell, Cell neighborCell, int cost, SimplePriorityQueue<Cell> priorityQueue)
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

    public sealed class Cell
    {
        public int Id;
        public int GScore;
        public int HScore;
        public int FCost => GScore + HScore;
        public int X;
        public int Y;
        public bool Finished;
        public bool Passable;
        public int ChunkId = -1;
        public (int X, int Y)? Predecessor;
        public List<Cell> Destinations;
        public override string ToString() => $"{Id}: {X}, {Y}, Passable: {Passable}, F: {FCost}, G: {GScore}, H: {HScore}. Finished= {Finished}";
    }

    public sealed class SuperCell
    {
        public Cell[,] Cells;
        public bool Checked = false;
        public List<SuperCell> Connections = new();
    }
}
