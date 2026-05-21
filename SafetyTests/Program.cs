using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using RFMapToolSharp.Tools;

var root = Path.Combine(Path.GetTempPath(), "rf_inv_sig_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var d = Path.Combine(root, "m1");
    Directory.CreateDirectory(d);

    File.WriteAllBytes(Path.Combine(d, "m1.bsp"), BuildAsciiUtf16());
    File.WriteAllBytes(Path.Combine(d, "m1.r3t"), Encoding.ASCII.GetBytes("m1.dds m1.dds\0"));
    File.WriteAllBytes(Path.Combine(d, "m1.dds"), Encoding.ASCII.GetBytes("DDS\0"));
    File.WriteAllBytes(Path.Combine(d, "m1.big"), new byte[700000]);

    var outDir = Path.Combine(root, "r");
    var p1 = RfInventoryTool.Run(d, outDir);
    var p2 = RfInventoryTool.Run(d, outDir);
    var j1 = JsonDocument.Parse(File.ReadAllText(p1));
    var j2 = JsonDocument.Parse(File.ReadAllText(p2));

    var inv = j1.RootElement.GetProperty("InventoryTable").EnumerateArray().ToArray();
    var bsp = inv.First(x => x.GetProperty("Extension").GetString() == ".bsp");
    Req(bsp.TryGetProperty("First16BytesHex", out _), "first16 missing");
    Req(bsp.TryGetProperty("First64BytesHex", out _), "first64 missing");
    Req(bsp.TryGetProperty("First256BytesHash", out _), "hash256 missing");
    Req(bsp.TryGetProperty("PrintablePrefixPreview", out _), "preview missing");
    Req(bsp.TryGetProperty("ZeroByteRatioFirst256", out _), "zero ratio missing");
    Req(bsp.TryGetProperty("PrintableRatioFirst256", out _), "printable ratio missing");
    Req(bsp.TryGetProperty("SuspectedTextOrBinary", out _), "type missing");

    var b1 = inv.First(x => x.GetProperty("Extension").GetString()==".bsp").GetProperty("First256BytesHash").GetString();
    var b2 = j2.RootElement.GetProperty("InventoryTable").EnumerateArray().First(x => x.GetProperty("Extension").GetString()==".bsp").GetProperty("First256BytesHash").GetString();
    Req(string.Equals(b1, b2, StringComparison.Ordinal), "hash non deterministic");

    var big = inv.First(x => x.GetProperty("Extension").GetString()==".big");
    Req(big.GetProperty("WholeFileHash").ValueKind == JsonValueKind.Null, "whole hash should be null when cap exceeded");

    var grp = j1.RootElement.GetProperty("SignatureGroupingSummary");
    Req(grp.GetProperty("ByFirst4Bytes").GetArrayLength() > 0, "first4 grouping missing");
    Req(grp.GetProperty("ByFirst16Bytes").GetArrayLength() > 0, "first16 grouping missing");
    var obs = grp.GetProperty("Observation").GetString() ?? "";
    Req(!obs.Contains("field", StringComparison.OrdinalIgnoreCase), "semantic claim detected");

    Console.WriteLine("SAFETYTEST PASS");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}

static byte[] BuildAsciiUtf16()
{
    using var ms = new MemoryStream();
    var a = Encoding.ASCII.GetBytes("m1.r3t m1.dds bad.zzz\0");
    ms.Write(a);
    if ((ms.Length % 2) != 0) ms.WriteByte(0);
    foreach (var c in "m1.dds") { ms.WriteByte((byte)c); ms.WriteByte(0); }
    ms.WriteByte(0); ms.WriteByte(0);
    return ms.ToArray();
}

static void Req(bool cond, string msg) { if (!cond) throw new Exception(msg); }
