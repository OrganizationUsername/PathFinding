using System;
using System.IO.Compression;
using System.Linq;
using System.Text;
using PathFinding.Models;

namespace PathFinding.Persistence;

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