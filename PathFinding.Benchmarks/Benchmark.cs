using BenchmarkDotNet.Attributes;
using Priority_Queue;

namespace PathFinding.Benchmarks;

[ShortRunJob]
[MemoryDiagnoser]
public class Benchmark
{
    [Params(333, 1111)]
    public int Count { get; set; }
    int destinationX;
    int destinationY;
    ushort sourceX = 1;
    ushort sourceY = 1;
    private Cell[] cells;
    private Cell[,] cellGrid;
    int xExtent;
    int yExtent;
    private List<Cell> cellList;

    [GlobalSetup]
    public void GlobalSetup()
    {
        xExtent = Count;
        yExtent = Count;
        cellList = new List<Cell>(xExtent * yExtent); //1kb, 1740kb with 333x333
        cells = new Cell[(xExtent * yExtent)]; //1kb, 1740kb with 333x333
        //Cell * cells = stackalloc Cell[xExtent * yExtent]; //873k saved with 333x333 //Fails at 65535*2 and ~425x425
        //This isn't worth it since all of it will already be allocated in program.
        destinationX = xExtent / 2;
        destinationY = yExtent / 2;
        cellGrid = new Cell[xExtent, yExtent];
        SimulateSetup(ref cells, ref cellGrid, ref cellList, xExtent, yExtent);
    }

    public void SimulateSetup(ref Cell[] cells, ref Cell[,] cellGrid, ref List<Cell> cellList, int xExtent, int yExtent)
    {
        var id = 0;
        for (ushort i = 0; i < xExtent; i++)
        {
            for (ushort j = 0; j < yExtent; j++)
            {
                var tempCell = new Cell { Id = id, X = i, Y = j };
                cells[id] = tempCell;
                cellList.Add(tempCell);
                id++;
                cellGrid[i, j] = tempCell;
            }
        }
    }


    [Benchmark(Baseline = true)]
    public int SimpleSolve()
    {
        var pq = new SimplePriorityQueue<int>();
        for (var i = 0; i < xExtent * yExtent; i++)
        {
            var c = cells[i];
            pq.Enqueue(c.Id, Math.Abs(c.X - destinationX) + Math.Abs(c.Y - destinationY));
        }
        _ = pq.Dequeue();

        var costThing = -1;
        for (var i = 0; i < 9; i++)
        {
            if (costThing < 0)
            {
                var dequeued = pq.Dequeue();
                var cell = cells[dequeued];
                var cost = (Math.Abs(cell.X - destinationX) + Math.Abs(cell.Y - destinationY));
                if (costThing < 0) costThing = cost;
            }
            else
            {
                var tempCell = cells[pq.First()];
                var TakeIt = costThing == (Math.Abs(tempCell.X - destinationX) + Math.Abs(tempCell.Y - destinationY));
                if (TakeIt)
                {
                    costThing = -1;
                }
            }
        }
        return pq.Count;
    }

    [Benchmark]
    public int FastSolve()
    {
        FastPriorityQueue<Cell> fpq = new FastPriorityQueue<Cell>(xExtent * yExtent);
        for (var i = 0; i < xExtent * yExtent; i++)
        {
            var c = cells[i];
            fpq.Enqueue(c, Math.Abs(c.X - destinationX) + Math.Abs(c.Y - destinationY));
        }

        _ = fpq.Dequeue();

        var costThing = -1;
        for (var i = 0; i < 9; i++)
        {
            if (costThing < 0)
            {
                var cell = fpq.Dequeue();
                var cost = (Math.Abs(cell.X - destinationX) + Math.Abs(cell.Y - destinationY));
                if (costThing < 0) costThing = cost;
            }
            else
            {
                var tempCell = fpq.First();
                var TakeIt = costThing == (Math.Abs(tempCell.X - destinationX) + Math.Abs(tempCell.Y - destinationY));
                if (TakeIt)
                {
                    costThing = -1;
                }
            }
        }
        return fpq.Count;
    }

    [Benchmark]
    public int FastSolveGrid()
    {
        FastPriorityQueue<Cell> fpq = new FastPriorityQueue<Cell>(xExtent * yExtent);
        for (var x = 0; x < cellGrid.GetLength(0); x++)
        {
            for (var y = 0; y < cellGrid.GetLength(1); y++)
            {
                var c = cellGrid[x, y];
                fpq.Enqueue(c, Math.Abs(c.X - destinationX) + Math.Abs(c.Y - destinationY));
            }
        }

        _ = fpq.Dequeue();

        var costThing = -1;
        for (var i = 0; i < 9; i++)
        {
            if (costThing < 0)
            {
                var cell = fpq.Dequeue();
                var cost = (Math.Abs(cell.X - destinationX) + Math.Abs(cell.Y - destinationY));
                if (costThing < 0) costThing = cost;
            }
            else
            {
                var tempCell = fpq.First();
                var TakeIt = costThing == (Math.Abs(tempCell.X - destinationX) + Math.Abs(tempCell.Y - destinationY));
                if (TakeIt)
                {
                    costThing = -1;
                }
            }
        }
        return fpq.Count;
    }

    [Benchmark]
    public int FastSolveUnchecked()
    {
        unchecked
        {
            FastPriorityQueue<Cell> fpq = new FastPriorityQueue<Cell>(xExtent * yExtent);
            for (var i = 0; i < xExtent * yExtent; i++)
            {
                var c = cells[i];
                fpq.Enqueue(c, Math.Abs(c.X - destinationX) + Math.Abs(c.Y - destinationY));
            }

            _ = fpq.Dequeue();

            var costThing = -1;
            for (var i = 0; i < 9; i++)
            {
                if (costThing < 0)
                {
                    var cell = fpq.Dequeue();
                    var cost = (Math.Abs(cell.X - destinationX) + Math.Abs(cell.Y - destinationY));
                    if (costThing < 0) costThing = cost;
                }
                else
                {
                    var tempCell = fpq.First();
                    var TakeIt = costThing == (Math.Abs(tempCell.X - destinationX) + Math.Abs(tempCell.Y - destinationY));
                    if (TakeIt)
                    {
                        costThing = -1;
                    }
                }
            }
            return fpq.Count;
        }
    }

    [Benchmark]
    public int SimpleFromScratch()
    {
        var localCells = new Cell[(xExtent * yExtent)];
        var localCellGrid = new Cell[xExtent, yExtent];
        var localCellList = new List<Cell>(xExtent * yExtent);
        SimulateSetup(ref localCells, ref localCellGrid, ref localCellList, xExtent, yExtent);

        var pq = new SimplePriorityQueue<int>();
        for (var i = 0; i < xExtent * yExtent; i++)
        {
            var c = localCells[i];
            pq.Enqueue(c.Id, Math.Abs(c.X - destinationX) + Math.Abs(c.Y - destinationY));
        }

        _ = pq.Dequeue();

        var costThing = -1;
        for (var i = 0; i < 9; i++)
        {
            if (costThing < 0)
            {
                var dequeued = pq.Dequeue();
                var cell = localCells[dequeued];
                var cost = (Math.Abs(cell.X - destinationX) + Math.Abs(cell.Y - destinationY));
                if (costThing < 0) costThing = cost;
            }
            else
            {
                var tempCell = localCells[pq.First()];
                var TakeIt = costThing == (Math.Abs(tempCell.X - destinationX) + Math.Abs(tempCell.Y - destinationY));
                if (TakeIt)
                {
                    costThing = -1;
                }
            }
        }
        return pq.Count;
    }

    [Benchmark]
    public int FastFromScratch()
    {
        var localCells = new Cell[(xExtent * yExtent)];
        var localCellGrid = new Cell[xExtent, yExtent];
        var localCellList = new List<Cell>(xExtent * yExtent);
        SimulateSetup(ref localCells, ref localCellGrid, ref localCellList, xExtent, yExtent);

        FastPriorityQueue<Cell> fpq = new FastPriorityQueue<Cell>(xExtent * yExtent);
        for (var i = 0; i < xExtent * yExtent; i++)
        {
            var c = localCells[i];
            fpq.Enqueue(c, Math.Abs(c.X - destinationX) + Math.Abs(c.Y - destinationY));
        }

        _ = fpq.Dequeue();

        var costThing = -1;
        for (var i = 0; i < 9; i++)
        {
            if (costThing < 0)
            {
                var cell = fpq.Dequeue();
                var cost = (Math.Abs(cell.X - destinationX) + Math.Abs(cell.Y - destinationY));
                if (costThing < 0) costThing = cost;
            }
            else
            {
                var tempCell = fpq.First();
                var TakeIt = costThing == (Math.Abs(tempCell.X - destinationX) + Math.Abs(tempCell.Y - destinationY));
                if (TakeIt)
                {
                    costThing = -1;
                }
            }
        }
        return fpq.Count;
    }


}

public class Cell : FastPriorityQueueNode
{
    public int Id;
    public ushort X; //int    => 3465
    public ushort Y; //ushort => 2599 for 333x333
    public bool Passable; //Save 867 without these for 333x333
    //public sbyte ChunkId;
}