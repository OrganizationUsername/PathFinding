using BenchmarkDotNet.Running;

namespace PathFinding.Benchmarks;

public class Program
{
    static void Main(string[] args)
    {
        BenchmarkRunner.Run<Benchmark>();
    }
}