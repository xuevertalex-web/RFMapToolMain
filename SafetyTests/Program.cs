using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using RFMapToolSharp.Tools;

var root = Path.Combine(Path.GetTempPath(), "rf_inventory_fixture_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var mapDir = Path.Combine(root, "map");
    Directory.CreateDirectory(mapDir);
    var bsp = Path.Combine(mapDir, "map.bsp");
    var r3t = Path.Combine(mapDir, "map.r3t");
    var dds = Path.Combine(mapDir, "map.dds");
    var wav = Path.Combine(mapDir, "map.wav");
    var unrelated = Path.Combine(mapDir, "other.bin");

    File.WriteAllBytes(bsp, BuildBytes("map.r3t map.dds", "ambient.wav"));
    File.WriteAllBytes(r3t, BuildBytes("map.dds", "map.r3m"));
    File.WriteAllBytes(dds, BuildBytes("DDS"));
    File.WriteAllBytes(wav, BuildBytes("WAV"));
    File.WriteAllBytes(unrelated, BuildBytes("junk"));

    var before = Directory.GetFiles(mapDir).ToDictionary(p => Path.GetFileName(p)!, Hash);

    var outRoot = Path.Combine(root, "reports");
    var reportPath = RfInventoryTool.Run(bsp, outRoot);
    if (!reportPath.StartsWith(outRoot, StringComparison.OrdinalIgnoreCase)) throw new Exception("report path not in report area");

    using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
    var inventory = doc.RootElement.GetProperty("InventoryTable").EnumerateArray().ToArray();
    var names = inventory.Select(x => x.GetProperty("Filename").GetString() ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);

    Require(names.Contains("map.bsp"), "bsp missing");
    Require(names.Contains("map.r3t"), "r3t missing");
    Require(names.Contains("map.dds"), "dds missing");
    Require(names.Contains("map.wav"), "wav missing");
    Require(!names.Contains("other.bin"), "unrelated included");

    Require(inventory.All(x => x.GetProperty("AsciiStrings").GetArrayLength() <= 80), "ascii cap failed");
    Require(inventory.All(x => x.GetProperty("Utf16LeStrings").GetArrayLength() <= 80), "utf16 cap failed");
    Require(inventory.Any(x => x.GetProperty("Utf16LeStrings").EnumerateArray().Any(s => (s.GetString() ?? "").Contains("map.r3m", StringComparison.OrdinalIgnoreCase))), "utf16 refs missing");
    Require(inventory.Any(x => (x.GetProperty("UncertaintyNotes").GetString() ?? "").Contains("evidence-only", StringComparison.OrdinalIgnoreCase)), "uncertainty wording missing");

    var edges = doc.RootElement.GetProperty("DependencyGraph").GetProperty("Edges").EnumerateArray().ToArray();
    Require(edges.Length > 0, "dependency edges missing");

    var after = Directory.GetFiles(mapDir).ToDictionary(p => Path.GetFileName(p)!, Hash);
    foreach (var kv in before) Require(after[kv.Key] == kv.Value, "source files changed");

    bool sanitized = false;
    try { RfInventoryTool.Run(Path.Combine(root, "missing", "file.bsp"), outRoot); }
    catch (Exception ex) { sanitized = !ex.Message.Contains(root, StringComparison.OrdinalIgnoreCase); }
    Require(sanitized, "error not sanitized");

    Console.WriteLine("SAFETYTEST PASS");
}
finally { try { Directory.Delete(root, true); } catch { } }

static byte[] BuildBytes(params string[] v)
{
    using var ms = new MemoryStream();
    foreach (var s in v)
    {
        var a = System.Text.Encoding.ASCII.GetBytes(s);
        ms.Write(a); ms.WriteByte(0);
        foreach (var c in s) { ms.WriteByte((byte)c); ms.WriteByte(0); }
        ms.WriteByte(0); ms.WriteByte(0);
    }
    return ms.ToArray();
}

static string Hash(string p)
{
    using var sha = System.Security.Cryptography.SHA256.Create();
    using var fs = File.OpenRead(p);
    return Convert.ToHexString(sha.ComputeHash(fs));
}

static void Require(bool c, string m) { if (!c) throw new Exception(m); }

