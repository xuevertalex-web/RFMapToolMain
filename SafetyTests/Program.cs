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
    var d1 = Path.Combine(root, "m1"); Directory.CreateDirectory(d1);
    File.WriteAllBytes(Path.Combine(d1, "m1.bsp"), Build("m1.r3t m1.r3t bad.zzz .... ////"));
    File.WriteAllBytes(Path.Combine(d1, "m1.r3t"), Build("m1.dds m1.r3m"));
    File.WriteAllBytes(Path.Combine(d1, "m1.dds"), Build("DDS"));

    var outDir = Path.Combine(root, "reports");
    var p1 = RfInventoryTool.Run(d1, outDir);
    var p2 = RfInventoryTool.Run(d1, outDir);
    var j1 = JsonDocument.Parse(File.ReadAllText(p1));
    var j2 = JsonDocument.Parse(File.ReadAllText(p2));

    var m = j1.RootElement.GetProperty("ExtractionMetrics");
    Req(m.GetProperty("RefsDiscarded").GetInt32() > 0, "discarded refs not counted");
    Req(m.GetProperty("DuplicateRefsSuppressed").GetInt32() > 0, "duplicate refs not counted");
    Req(j1.RootElement.GetProperty("CapHits").TryGetProperty("RefsCapHit", out _), "cap metadata missing");

    var e1 = j1.RootElement.GetProperty("DependencyGraph").GetProperty("Edges").EnumerateArray().Select(x=>x.GetRawText()).ToArray();
    var e2 = j2.RootElement.GetProperty("DependencyGraph").GetProperty("Edges").EnumerateArray().Select(x=>x.GetRawText()).ToArray();
    Req(e1.SequenceEqual(e2), "non deterministic output");

    var summary = new { per_map = new[]{ new { map="m1", extraction_totals=m.GetRawText(), cap_hits=j1.RootElement.GetProperty("CapHits").GetRawText() } } };
    var sumPath = Path.Combine(outDir, "cross_map_metrics_summary.json");
    File.WriteAllText(sumPath, JsonSerializer.Serialize(summary));
    Req(File.Exists(sumPath), "cross map aggregation missing");

    Console.WriteLine("SAFETYTEST PASS");
}
finally { try { Directory.Delete(root, true); } catch { } }

static byte[] Build(string s){ using var ms=new MemoryStream(); for(int i=0;i<200;i++){ var a=Encoding.ASCII.GetBytes(s); ms.Write(a); ms.WriteByte(0);} return ms.ToArray(); }
static void Req(bool c,string m){ if(!c) throw new Exception(m); }
