using Priority_Queue;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace PathFinding.Core
{
    public static class Solver
    {
        //Should probably be able to pass in a dictionary of costs.
        //TODO: try vector field path finding https://www.youtube.com/watch?v=ZJZu3zLMYAc&ab_channel=PDN-PasDeNom
        //TODO: Can also improve this one by separating nodes into groups, where each node in a group can touch all others. Keep them small, and the overall problem size will be reduced considerably.
        //Show groups with this: https://www.youtube.com/watch?v=f1WQpqZKoYw&ab_channel=Ms.Hearn

        public static async Task<(List<Cell> SolutionCells, Cell[,] AllCells, long TimeToSolve, DateTime thisDate)> SolveAsync(Cell[,] cellGrid, Cell sourceCell, Cell destCell, IReadOnlyDictionary<(int, int), List<(int, int)>> targets, DateTime thisDate, bool diagonal = false)
        {
            return await Task.Run(() => Solve(cellGrid, sourceCell, destCell, targets, thisDate, diagonal));
        }

        public static (List<Cell> SolutionCells, Cell[,] AllCells, long TimeToSolve, DateTime thisDate) Solve(Cell[,] cellGrid, Cell sourceCell, Cell destCell, IReadOnlyDictionary<(int, int), List<(int, int)>> targets, DateTime thisDate, bool diagonal = false)
        {
            var startMemory = GC.GetAllocatedBytesForCurrentThread();
            var s = new Stopwatch();
            s.Start();
            if (cellGrid is null) return (null, null, s.ElapsedMilliseconds, thisDate);
            //Trace.WriteLine($"Source at: {sourceCell.X},{sourceCell.Y}");
            //Trace.WriteLine($"Destination at: {destCell.X},{destCell.Y}");

            FastPriorityQueue<Cell> priorityQueue = new FastPriorityQueue<Cell>(cellGrid.GetLength(0) * cellGrid.GetLength(1));
            //var priorityQueue = new SimplePriorityQueue<Cell>();
            for (var x = 0; x < cellGrid.GetLength(0); x++)
            {
                for (var y = 0; y < cellGrid.GetLength(1); y++)
                {
                    if (true /*cellGrid[x, y].Passable || (x == destCell.X && y == destCell.Y)*/)
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
            }

            sourceCell.GScore = 0;
            //var count = 0;
            while (destCell.Finished == false)
            {
                if (!priorityQueue.Any()) return (null, null, s.ElapsedMilliseconds, thisDate);
                //count++;
                var changed = PathIteration(cellGrid, destCell, targets, diagonal, priorityQueue);
                if (changed == 0) return (null, null, s.ElapsedMilliseconds, thisDate);
            }

            //Trace.WriteLine($"Time for path solving: {s.ElapsedMilliseconds}ms for {count} iterations");
            //s.Restart();
            var solutionIds = GetSolutionCells(sourceCell, destCell, cellGrid);
            if (solutionIds is null || !solutionIds.Any()) return (null, null, s.ElapsedMilliseconds, thisDate);
            //Trace.WriteLine($"Time for path solution extraction: {s.ElapsedMilliseconds}"); //10
            var endMemory = GC.GetAllocatedBytesForCurrentThread();
            //Trace.WriteLine($"Allocated: {(endMemory - startMemory) / 1024} kb.");
            return (solutionIds, cellGrid, s.ElapsedMilliseconds, thisDate);
        }

        public static int PathIteration(Cell[,] cellGrid, Cell destCell, IReadOnlyDictionary<(int, int), List<(int, int)>> targets, bool diagonal, FastPriorityQueue<Cell> priorityQueue)
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

        public static List<Cell> GetSolutionCells(Cell sourceCell, Cell destinationCell, Cell[,] cellGrid)
        {
            var tempList = new List<Cell>();
            Cell tempCell = destinationCell;
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
                //Is there a valid target?
                targets.TryGetValue((sourceCell.X, sourceCell.Y), out var neighbors) &&
                //Are any of the targets possible?
                neighbors.Select(x => cellGrid[x.Item1, x.Item2]).Any(x => x.Passable))
            {
                //Get reasonable results.
                return neighbors.Select(x => cellGrid[x.Item1, x.Item2]).ToList();
            }

            var results = new List<Cell>();
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

        public static int SetNeighbor(Cell sourceCell, Cell neighborCell, int cost, FastPriorityQueue<Cell> priorityQueue)
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

    public sealed class Cell : FastPriorityQueueNode
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





//void SimulateSetup(ref Cell[] cells, ref Cell[,] cellGrid, int xExtent, int yExtent)
//{
//    int id = 0;
//    //So here's where I would have a collection of cells already.
//    for (ushort i = 0; i < xExtent; i++)
//    {
//        for (ushort j = 0; j < yExtent; j++)
//        {
//            var tempCell = new Cell { Id = id, X = i, Y = j };
//            //cells.Add(tempCell);
//            cells[id] = tempCell;
//            id++;
//            cellGrid[i, j] = tempCell;
//        }
//    }
//}

//var startMemory = GC.GetAllocatedBytesForCurrentThread();

//var xExtent = 425;
//var yExtent = 425;
////var cells = new List<Cell>(xExtent * yExtent); //1kb, 1740kb with 333x333
//var cells = new Cell[(xExtent * yExtent)]; //1kb, 1740kb with 333x333
//                                           //Cell * cells = stackalloc Cell[xExtent * yExtent]; //873k saved with 333x333 //Fails at 65535*2 and ~425x425
//                                           //This isn't worth it since all of it will already be allocated in program.
//var sourceX = 1;
//var sourceY = 1;
//var destinationX = xExtent / 2;
//var destinationY = yExtent / 2;

//unsafe { sizeof(Cell).Dump("Cell size"); }

//Cell[,] cellGrid = new Cell[xExtent, yExtent];

//SimulateSetup(ref cells, ref cellGrid, xExtent, yExtent);
//Cell destinationCell = cellGrid[destinationX, destinationY];


//((GC.GetAllocatedBytesForCurrentThread() - startMemory) / 1024 / 0001).Dump("kb in setup.");
//startMemory = GC.GetAllocatedBytesForCurrentThread();

//var pq = new SimplePriorityQueue<int>();
//for (int i = 0; i < xExtent * yExtent; i++)
//{
//    var c = cells[i];
//    var cost = (Math.Abs(c.X - destinationCell.X) + Math.Abs(c.Y - destinationCell.Y)) + (int.MaxValue * 0 / 16);
//    pq.Enqueue(c.Id, cost);
//    //c.Dump($"Cost: {cost}");
//}
//cells[pq.Dequeue()].Dump(); //Simulate being in the middle of something

//var costThing = -1;
//for (int i = 0; i < 9; i++)
//{
//    if (costThing < 0)
//    {
//        var dequeued = pq.Dequeue();
//        var cell = cells[dequeued];
//        var cost = (Math.Abs(cell.X - destinationCell.X) + Math.Abs(cell.Y - destinationCell.Y));
//        if (costThing < 0) costThing = cost;
//        cell.Dump($"Cell. Cost= {cost}. Id = {dequeued}");
//    }
//    else
//    {
//        var TempThing = pq.First();
//        var tempCell = cells[TempThing];
//        var TakeIt = costThing == (Math.Abs(tempCell.X - destinationCell.X) + Math.Abs(tempCell.Y - destinationCell.Y));
//        if (TakeIt)
//        {
//            costThing = -1;
//        }
//    }
//}

//pq.First().Dump();

//((GC.GetAllocatedBytesForCurrentThread() - startMemory) / 1024 / 0001).Dump("kb used for priority queue.");


////Since this has to be stable, I can't have anything that represents mutable state.
//public struct Cell
//{
//    public int Id;
//    public ushort X; //int    => 3465
//    public ushort Y; //ushort => 2599 for 333x333
//                     //public bool Passable; //Save 867 without these for 333x333
//                     //public sbyte ChunkId;
//}

