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
    File.WriteAllBytes(Path.Combine(d, "m1.r3t"), Encoding.ASCII.GetBytes("m1.dds texture/foo.dds effect/bar.eff 12345 ../outside/tx.tga bad.zzz\0"));
    File.WriteAllBytes(Path.Combine(d, "m1.r3m"), Encoding.ASCII.GetBytes("MAT\0"));
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
    var matrix = j1.RootElement.GetProperty("CompanionFileMatrix");
    var rows = matrix.GetProperty("Rows").EnumerateArray().ToArray();
    Req(rows.Any(r => r.GetProperty("Extension").GetString() == ".bsp"), "matrix missing bsp");
    Req(rows.Any(r => r.GetProperty("Extension").GetString() == ".r3t"), "matrix missing r3t");
    Req(rows.Any(r => r.GetProperty("Extension").GetString() == ".r3m"), "matrix missing r3m");
    Req(matrix.GetProperty("MissingCompanionObservations").GetArrayLength() >= 0, "missing companion observations absent");
    Req(rows.SequenceEqual(rows.OrderBy(r => r.GetProperty("MapName").GetString()).ThenBy(r => r.GetProperty("SamePrefixGroup").GetString()).ThenBy(r => r.GetProperty("Extension").GetString()).ThenBy(r => r.GetProperty("EvidenceSource").GetString()), JsonElementComparer.Instance), "matrix ordering not deterministic");
    var ua = j1.RootElement.GetProperty("UnresolvedReferenceAudit").EnumerateArray().ToArray();
    Req(ua.Any(x => (x.GetProperty("NormalizedTarget").GetString() ?? "").Contains("outside", StringComparison.OrdinalIgnoreCase) && x.GetProperty("ReasonUnresolved").GetString() == "outside_bounded_scope"), "outside bounded unresolved missing");
    Req(ua.Any(x => x.GetProperty("ReasonUnresolved").GetString() == "unsupported_extension"), "unsupported extension unresolved missing");
    Req(ua.All(x => !(x.GetProperty("SourceFile").GetString() ?? "").Contains(":\\", StringComparison.OrdinalIgnoreCase)), "raw absolute path leaked");
    var uAgg = j1.RootElement.GetProperty("UnresolvedReferenceAggregation");
    Req(uAgg.GetProperty("ByExtension").EnumerateArray().SequenceEqual(uAgg.GetProperty("ByExtension").EnumerateArray().OrderBy(x => -x.GetProperty("Value").GetInt32()).ThenBy(x => x.GetProperty("Key").GetString()), JsonElementComparer.Instance), "unresolved aggregation not deterministic");
    var uObs = uAgg.GetProperty("Observation").GetString() ?? "";
    Req(uObs.Contains("observed unresolved reference", StringComparison.OrdinalIgnoreCase), "observational wording missing");
    var probeNone = j1.RootElement.GetProperty("CandidateResourceRootProbe");
    Req((probeNone.GetProperty("PolicyDecision").GetString() ?? "") == "no_root", "no-root probe state invalid");

    var rr = Path.Combine(Directory.GetCurrentDirectory(), "_runs", "safety_resroot");
    Directory.CreateDirectory(rr);
    File.WriteAllBytes(Path.Combine(rr, "tx.tga"), Encoding.ASCII.GetBytes("X"));
    var p3 = RfInventoryTool.Run(d, outDir, null, rr);
    var j3 = JsonDocument.Parse(File.ReadAllText(p3));
    var probe = j3.RootElement.GetProperty("CandidateResourceRootProbe");
    Req(probe.GetProperty("MatchesFound").GetInt32() >= 1, "candidate root did not resolve missing target");
    Req((probe.GetProperty("Notes").GetString() ?? "").Contains("candidate match", StringComparison.OrdinalIgnoreCase), "candidate wording missing");

    var p4 = RfInventoryTool.Run(d, outDir, "C:\\Windows");
    var j4 = JsonDocument.Parse(File.ReadAllText(p4));
    var probeBad = j4.RootElement.GetProperty("CandidateResourceRootProbe");
    Req((probeBad.GetProperty("Notes").GetString() ?? "").Contains("rejected", StringComparison.OrdinalIgnoreCase), "outside workspace not rejected");
    Req((probeBad.GetProperty("ResourceRootMode").GetString() ?? "") == "rejected_outside_workspace", "resource root mode not set");

    var sug = j1.RootElement.GetProperty("CandidateRootSuggestions").EnumerateArray().ToArray();
    Req(sug.Any(x => (x.GetProperty("SuggestedRootFragment").GetString() ?? "") == "texture"), "path-like token did not suggest texture");
    Req(sug.Any(x => (x.GetProperty("SuggestedRootFragment").GetString() ?? "") == "effect"), "path-like token did not suggest effect");
    Req(sug.Any(x => (x.GetProperty("SuggestedRootFragment").GetString() ?? "") == "sound" || (x.GetProperty("SuggestedRootFragment").GetString() ?? "") == "texture"), "extension-based low confidence suggestion missing");
    Req(!sug.Any(x => (x.GetProperty("SuggestedRootFragment").GetString() ?? "") == "12345"), "noisy numeric token produced suggestion");
    Req(sug.All(x => x.GetProperty("RequiresExplicitApprovedRootProbe").GetBoolean()), "suggestions must require explicit probe");
    Req(sug.SequenceEqual(sug.OrderBy(x => x.GetProperty("SuggestedRootFragment").GetString()), JsonElementComparer.Instance), "suggestion ordering not deterministic");
    Req((sug.First(x => (x.GetProperty("SuggestedRootFragment").GetString() ?? "") == "texture").GetProperty("Confidence").GetString() ?? "") != "low", "repeated path prefix did not increase confidence");

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
sealed class JsonElementComparer : IEqualityComparer<JsonElement>
{
    public static readonly JsonElementComparer Instance = new();
    public bool Equals(JsonElement x, JsonElement y) => x.GetRawText() == y.GetRawText();
    public int GetHashCode(JsonElement obj) => obj.GetRawText().GetHashCode();
}
