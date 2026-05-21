using System;
using System.IO;
using System.Linq;
using System.Text;
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

    File.WriteAllBytes(bsp, BuildLarge("map.r3t map.dds map.dds", "ambient.wav"));
    File.WriteAllBytes(r3t, BuildLarge("map.dds", "map.r3m", repeat: 120));
    File.WriteAllBytes(dds, BuildLarge("DDS"));
    File.WriteAllBytes(wav, BuildLarge("WAV"));
    File.WriteAllBytes(unrelated, BuildLarge("junk"));

    var before = Directory.GetFiles(mapDir).ToDictionary(p => Path.GetFileName(p)!, Hash);
    var outRoot = Path.Combine(root, "reports");

    var report1 = RfInventoryTool.Run(bsp, outRoot);
    var report2 = RfInventoryTool.Run(bsp, outRoot);

    var d1 = JsonDocument.Parse(File.ReadAllText(report1));
    var d2 = JsonDocument.Parse(File.ReadAllText(report2));

    var inv1 = d1.RootElement.GetProperty("InventoryTable").EnumerateArray().ToArray();
    var names = inv1.Select(x => x.GetProperty("Filename").GetString() ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
    Req(names.Contains("map.bsp") && names.Contains("map.r3t"), "related missing");
    Req(!names.Contains("other.bin"), "unrelated included");

    Req(inv1.All(x => x.GetProperty("AsciiStrings").GetArrayLength() <= 64), "ascii cap fail");
    Req(inv1.All(x => x.GetProperty("Utf16LeStrings").GetArrayLength() <= 64), "utf16 cap fail");

    var e1 = d1.RootElement.GetProperty("DependencyGraph").GetProperty("Edges").EnumerateArray().Select(e =>
        $"{e.GetProperty("From").GetString()}|{e.GetProperty("To").GetString()}|{e.GetProperty("EvidenceType").GetString()}|{e.GetProperty("Confidence").GetString()}"
    ).ToArray();
    var e2 = d2.RootElement.GetProperty("DependencyGraph").GetProperty("Edges").EnumerateArray().Select(e =>
        $"{e.GetProperty("From").GetString()}|{e.GetProperty("To").GetString()}|{e.GetProperty("EvidenceType").GetString()}|{e.GetProperty("Confidence").GetString()}"
    ).ToArray();

    Req(e1.SequenceEqual(e2), "non-deterministic edges");
    Req(e1.Distinct(StringComparer.OrdinalIgnoreCase).Count() == e1.Length, "duplicate edges present");
    Req(e1.Length > 0, "no edges");

    var reportBytes = new FileInfo(report1).Length;
    Req(reportBytes <= 2 * 1024 * 1024, "report size cap fail");

    bool sanitized = false;
    try { RfInventoryTool.Run(Path.Combine(root, "missing", "a.bsp"), outRoot); }
    catch (Exception ex) { sanitized = !ex.Message.Contains(root, StringComparison.OrdinalIgnoreCase); }
    Req(sanitized, "unsanitized error");

    var badDir = Path.Combine(root, "reparse_test");
    Directory.CreateDirectory(badDir);
    bool traversalRejected = false;
    try { RfInventoryTool.Run(badDir, outRoot); }
    catch { traversalRejected = true; }
    Req(traversalRejected, "traversal/reparse rejection missing");

    var after = Directory.GetFiles(mapDir).ToDictionary(p => Path.GetFileName(p)!, Hash);
    foreach (var kv in before) Req(after[kv.Key] == kv.Value, "source file changed");

    Console.WriteLine("SAFETYTEST PASS");
}
finally { try { Directory.Delete(root, true); } catch { } }

static byte[] BuildLarge(string a, string? b = null, int repeat = 10)
{
    using var ms = new MemoryStream();
    for (int i = 0; i < repeat; i++)
    {
        Write(ms, a);
        if (!string.IsNullOrWhiteSpace(b)) Write(ms, b!);
    }
    return ms.ToArray();
}

static void Write(Stream s, string txt)
{
    var asc = Encoding.ASCII.GetBytes(txt);
    s.Write(asc); s.WriteByte(0);
    foreach (var c in txt) { s.WriteByte((byte)c); s.WriteByte(0); }
    s.WriteByte(0); s.WriteByte(0);
}

static string Hash(string p)
{
    using var sha = System.Security.Cryptography.SHA256.Create();
    using var fs = File.OpenRead(p);
    return Convert.ToHexString(sha.ComputeHash(fs));
}

static void Req(bool cond, string msg) { if (!cond) throw new Exception(msg); }
