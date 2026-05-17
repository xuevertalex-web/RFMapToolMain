using System.Text.Json;

namespace RFMapToolSharp.Parsing.Entity;

public static class RpkInspector
{
    public static void WriteEntityReport(string entityDir, string outputPath)
    {
        var files = Directory.GetFiles(entityDir, "*.rpk", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rows = new List<object>();
        foreach (var f in files)
        {
            var fi = new FileInfo(f);
            rows.Add(new
            {
                File = fi.Name,
                Size = fi.Length,
                HeaderHex = ReadHeaderHex(f, 32)
            });
        }

        var payload = new
        {
            Directory = entityDir,
            Count = rows.Count,
            Files = rows
        };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void WriteEntityIndexReport(string entityDir, string outputPath)
    {
        var files = Directory.GetFiles(entityDir, "*.rpk", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rows = new List<object>();
        foreach (var f in files)
        {
            var ids = TryReadIdListV1(f, out var version, out var count);
            rows.Add(new
            {
                File = Path.GetFileName(f),
                Version = version,
                Count = count,
                ParsedIds = ids?.Count ?? 0,
                Ids = ids ?? new List<int>()
            });
        }

        var payload = new
        {
            Directory = entityDir,
            Files = rows
        };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static List<int>? TryReadIdListV1(string path, out float version, out int count)
    {
        version = 0f;
        count = 0;
        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            version = br.ReadSingle();
            count = br.ReadInt32();
            _ = br.ReadInt32(); // reserved
            if (count < 0 || count > 100000) return null;
            if (fs.Length < 12 + (count * 4L)) return null;
            var ids = new List<int>(count);
            for (int i = 0; i < count; i++) ids.Add(br.ReadInt32());
            return ids;
        }
        catch
        {
            return null;
        }
    }

    private static string ReadHeaderHex(string path, int bytes)
    {
        using var fs = File.OpenRead(path);
        var b = new byte[Math.Min(bytes, (int)fs.Length)];
        fs.ReadExactly(b, 0, b.Length);
        return BitConverter.ToString(b).Replace("-", " ");
    }
}
