using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RFMapToolSharp.Tools;

internal static class RfInventoryTool
{
    private static readonly HashSet<string> RecognizedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bsp", ".r3t", ".r3m", ".rsm", ".dds", ".tga", ".dat", ".ani", ".eff", ".snd", ".wav", ".ogg"
    };
    private static readonly HashSet<string> RefExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bsp", ".r3t", ".r3m", ".rsm", ".dds", ".tga", ".dat", ".ani", ".eff", ".snd", ".wav", ".ogg"
    };

    private const int MaxReadBytesPerFile = 512 * 1024;
    private const int MaxStringsPerFile = 64;
    private const int MaxRefsPerFile = 64;
    private const int MaxFiles = 256;
    private const int MaxEdges = 512;
    private const int MaxReportBytes = 2 * 1024 * 1024;

    public static void RunSelfTest() { }

    public static string Run(string inputPath, string? explicitOutputRoot)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(inputPath)) throw new InvalidOperationException("rf_inventory requires input path");
            var fullInput = Path.GetFullPath(inputPath);
            if (!File.Exists(fullInput) && !Directory.Exists(fullInput)) throw new InvalidOperationException("rf_inventory input path not found");
            var context = BuildContext(fullInput);
            var outRoot = ResolveOutputRoot(explicitOutputRoot);
            Directory.CreateDirectory(outRoot);

            var report = BuildReport(context, fullInput);
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            if (Encoding.UTF8.GetByteCount(json) > MaxReportBytes)
            {
                report.CapHits.ReportSizeCapHit = true;
                report.InventoryTable = report.InventoryTable.Take(64).ToList();
                report.DependencyGraph.Edges = report.DependencyGraph.Edges.Take(128).ToList();
                report.ExtractedStringsAndReferences = report.ExtractedStringsAndReferences.Take(64).ToList();
                json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            }
            var outPath = Path.Combine(outRoot, $"rf_inventory_{report.MapOrPrefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(outPath, json, Encoding.UTF8);
            return outPath;
        }
        catch (Exception ex) { throw new InvalidOperationException(SanitizeError(ex.Message)); }
    }

    private static InventoryContext BuildContext(string input)
    {
        if (File.Exists(input))
        {
            if (!string.Equals(Path.GetExtension(input), ".bsp", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("rf_inventory file input must be .bsp");
            var dir = Path.GetDirectoryName(input)!;
            RejectUnsafeDirectory(dir);
            return new InventoryContext(dir, new List<string> { Path.GetFileNameWithoutExtension(input)! }, true);
        }
        RejectUnsafeDirectory(input);
        var bsp = Directory.GetFiles(input, "*.bsp", SearchOption.TopDirectoryOnly);
        if (bsp.Length == 0) throw new InvalidOperationException("rf_inventory directory input requires at least one .bsp in top level");
        return new InventoryContext(input, bsp.Select(Path.GetFileNameWithoutExtension).Cast<string>().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(), false);
    }

    private static InventoryReport BuildReport(InventoryContext context, string rawInput)
    {
        var caps = new CapHits();
        var metrics = new ExtractionMetrics();
        var allFiles = Directory.GetFiles(context.RootDir, "*", SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var filtered = new List<string>();
        foreach (var p in allFiles)
        {
            if (filtered.Count >= MaxFiles) { caps.SiblingScanCapHit = true; break; }
            if (IsCandidate(p, context.Prefixes)) filtered.Add(p);
            else metrics.DiscardReasons.Inc("out_of_scope_file");
        }

        var entries = new List<InventoryFileRecord>();
        var pathByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in filtered)
        {
            var fi = new FileInfo(p);
            bool fileCapHit;
            var bytes = ReadCappedBytes(p, MaxReadBytesPerFile, out fileCapHit);
            if (fileCapHit) caps.FileReadCapHit = true;

            var ascii = ExtractStrings(bytes, false, metrics, caps);
            var utf16 = ExtractStrings(bytes, true, metrics, caps);
            var refs = ExtractRefs(ascii.Concat(utf16), metrics, caps);
            var rel = Path.GetRelativePath(context.RootDir, p).Replace('\\', '/');
            entries.Add(new InventoryFileRecord { RelativePath = rel, Filename = fi.Name, Extension = fi.Extension, Size = fi.Length, SamePrefixGroup = MatchPrefix(fi.Name, context.Prefixes) ?? "unknown", AsciiStrings = ascii, Utf16LeStrings = utf16, ReferencedFilenames = refs, UncertaintyNotes = "Evidence-only inference; same-prefix relation is low-confidence." });
            pathByName[fi.Name] = rel;
        }

        var edges = BuildEdges(entries, pathByName, metrics, caps);
        metrics.DependencyEdgesEmitted = edges.Count;

        return new InventoryReport
        {
            Tool = "rf_inventory",
            Mode = "read_only",
            InputMode = context.FromBspFile ? "single_bsp_file" : "single_directory",
            InputPathSanitized = SanitizePath(rawInput),
            RootDirectorySanitized = SanitizePath(context.RootDir),
            MapOrPrefix = context.Prefixes.Count == 1 ? context.Prefixes[0] : "multi",
            GeneratedUtc = DateTime.UtcNow,
            InventoryTable = entries.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).ToList(),
            ExtractedStringsAndReferences = entries.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).Select(e => new StringReference { File = e.RelativePath, AsciiStrings = e.AsciiStrings, Utf16LeStrings = e.Utf16LeStrings, ReferencedFilenames = e.ReferencedFilenames }).ToList(),
            DependencyGraph = new DependencyGraphReport { Edges = edges },
            ExtractionMetrics = metrics,
            CapHits = caps,
            SamePrefixGroups = entries.GroupBy(e => e.SamePrefixGroup, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase).Select(g => new PrefixGroupSummary { Prefix = g.Key, Count = g.Count(), Files = g.Select(x => x.Filename).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList() }).ToList()
        };
    }

    private static List<DependencyEdge> BuildEdges(List<InventoryFileRecord> entries, Dictionary<string, string> byName, ExtractionMetrics m, CapHits caps)
    {
        var outEdges = new List<DependencyEdge>();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var extByPath = entries.ToDictionary(x => x.RelativePath, x => x.Extension, StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase))
        foreach (var rf in e.ReferencedFilenames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var k = Path.GetFileName(rf.Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(k) || !byName.TryGetValue(k, out var to)) continue;
            var sig = $"{e.RelativePath}|{to}|{rf}";
            if (!set.Add(sig)) { m.DependencyEdgesSuppressed++; continue; }
            outEdges.Add(new DependencyEdge { From = e.RelativePath, To = to, Evidence = rf, EvidenceType = "string_reference", SourceExtension = extByPath[e.RelativePath].ToLowerInvariant(), TargetExtension = extByPath[to].ToLowerInvariant(), Confidence = "low" });
            if (outEdges.Count >= MaxEdges) { caps.DependencyEdgesCapHit = true; return outEdges; }
        }
        return outEdges;
    }

    private static List<string> ExtractStrings(byte[] data, bool utf16, ExtractionMetrics m, CapHits caps)
    {
        var outList = new List<string>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var sb = new StringBuilder();
        void AddReason(string r)=>m.DiscardReasons.Inc(r);
        void Flush()
        {
            if (sb.Length == 0) return;
            var s = sb.ToString().Trim(); sb.Clear();
            if (s.Length < 4) { AddReason("too_short"); if (utf16) m.Utf16StringsDiscarded++; else m.AsciiStringsDiscarded++; return; }
            if (s.Count(ch => !char.IsLetterOrDigit(ch) && ch!='.'&&ch!='/'&&ch!='\\'&&ch!='_'&&ch!='-') > s.Length/2) { AddReason("excessive_punctuation"); if (utf16) m.Utf16StringsDiscarded++; else m.AsciiStringsDiscarded++; return; }
            if (IsNoise(s)) { AddReason("too_noisy"); if (utf16) m.Utf16StringsDiscarded++; else m.AsciiStringsDiscarded++; return; }
            if (!seen.Add(s)) { AddReason("duplicate"); if (utf16) m.Utf16StringsDiscarded++; else m.AsciiStringsDiscarded++; return; }
            outList.Add(s); if (utf16) m.Utf16StringsKept++; else m.AsciiStringsKept++;
        }
        if (utf16)
        {
            for (int i=0;i+1<data.Length;i+=2){ m.Utf16StringsSeen++; var lo=data[i]; var hi=data[i+1]; if (hi==0 && lo>=32 && lo<=126) sb.Append((char)lo); else Flush(); if (outList.Count>=MaxStringsPerFile){ caps.Utf16CapHit=true; AddReason("cap_reached"); break; } }
        }
        else
        {
            for (int i=0;i<data.Length;i++){ m.AsciiStringsSeen++; var b=data[i]; if (b>=32 && b<=126) sb.Append((char)b); else Flush(); if (outList.Count>=MaxStringsPerFile){ caps.AsciiCapHit=true; AddReason("cap_reached"); break; } }
        }
        Flush();
        return outList.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ExtractRefs(IEnumerable<string> strings, ExtractionMetrics m, CapHits caps)
    {
        var rx = new Regex(@"([A-Za-z0-9_\-./\\]+\.[A-Za-z0-9]{1,8})", RegexOptions.Compiled);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in strings.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase))
        foreach (Match mm in rx.Matches(s))
        {
            m.RefsSeen++;
            var token = mm.Groups[1].Value.Trim().Replace('\\','/');
            var ext = Path.GetExtension(token);
            if (!RefExts.Contains(ext)) { m.RefsDiscarded++; m.NoisyRefsDiscarded++; m.DiscardReasons.Inc("unsupported_extension"); continue; }
            if (!set.Add(token)) { m.DuplicateRefsSuppressed++; m.RefsDiscarded++; m.DiscardReasons.Inc("duplicate"); continue; }
            m.RefsKept++;
            if (set.Count >= MaxRefsPerFile) { caps.RefsCapHit = true; m.DiscardReasons.Inc("cap_reached"); return set.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToList(); }
        }
        return set.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsCandidate(string path, IReadOnlyCollection<string> prefixes){ var stem=Path.GetFileNameWithoutExtension(path); return prefixes.Any(p=>stem.StartsWith(p,StringComparison.OrdinalIgnoreCase)); }
    private static string? MatchPrefix(string fn, IReadOnlyCollection<string> p){ var s=Path.GetFileNameWithoutExtension(fn); return p.OrderBy(x=>x.Length).FirstOrDefault(x=>s.StartsWith(x,StringComparison.OrdinalIgnoreCase)); }
    private static bool IsNoise(string s){ if (s.Length>180) return true; var al=s.Count(char.IsLetterOrDigit); return al<2; }
    private static byte[] ReadCappedBytes(string p,int max,out bool hit){ using var fs=File.OpenRead(p); hit=fs.Length>max; var len=(int)Math.Min(fs.Length,max); var b=new byte[len]; var r=fs.Read(b,0,len); return r==len?b:b.Take(r).ToArray(); }
    private static void RejectUnsafeDirectory(string path){ var di=new DirectoryInfo(Path.GetFullPath(path)); if (di.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException("reparse/traversal path is not allowed"); }
    private static string ResolveOutputRoot(string? x){ if(!string.IsNullOrWhiteSpace(x)) return Path.GetFullPath(x); var a=Environment.GetEnvironmentVariable("ArtifactOutputRoot"); return !string.IsNullOrWhiteSpace(a)?Path.Combine(Path.GetFullPath(a),"reports","rf_inventory"):Path.Combine(Environment.CurrentDirectory,"_runs","reports","rf_inventory"); }
    private static string SanitizePath(string p){ try{ var cwd=Path.GetFullPath(Environment.CurrentDirectory); if(p.StartsWith(cwd,StringComparison.OrdinalIgnoreCase)) return Path.GetRelativePath(cwd,p).Replace('\\','/'); }catch{} return Path.GetFileName(p); }
    private static string SanitizeError(string m){ var cwd=Path.GetFullPath(Environment.CurrentDirectory).Replace('\\','/'); var s=(m??"error").Replace('\\','/').Replace(cwd,"<workspace>",StringComparison.OrdinalIgnoreCase); return s.Length>240?s[..240]:s; }

    private sealed record InventoryContext(string RootDir, List<string> Prefixes, bool FromBspFile);
    private sealed class InventoryReport { public string Tool {get;set;}="rf_inventory"; public string Mode{get;set;}="read_only"; public string InputMode{get;set;}=""; public string InputPathSanitized{get;set;}=""; public string RootDirectorySanitized{get;set;}=""; public string MapOrPrefix{get;set;}=""; public DateTime GeneratedUtc{get;set;} public List<InventoryFileRecord> InventoryTable{get;set;}=new(); public List<PrefixGroupSummary> SamePrefixGroups{get;set;}=new(); public List<StringReference> ExtractedStringsAndReferences{get;set;}=new(); public DependencyGraphReport DependencyGraph{get;set;}=new(); public ExtractionMetrics ExtractionMetrics{get;set;}=new(); public CapHits CapHits{get;set;}=new(); }
    private sealed class InventoryFileRecord { public string RelativePath{get;set;}=""; public string Filename{get;set;}=""; public string Extension{get;set;}=""; public long Size{get;set;} public string SamePrefixGroup{get;set;}=""; public List<string> AsciiStrings{get;set;}=new(); public List<string> Utf16LeStrings{get;set;}=new(); public List<string> ReferencedFilenames{get;set;}=new(); public string UncertaintyNotes{get;set;}=""; }
    private sealed class DependencyEdge { public string From{get;set;}=""; public string To{get;set;}=""; public string Evidence{get;set;}=""; public string EvidenceType{get;set;}="string_reference"; public string SourceExtension{get;set;}=""; public string TargetExtension{get;set;}=""; public string Confidence{get;set;}="low"; }
    private sealed class PrefixGroupSummary { public string Prefix{get;set;}=""; public int Count{get;set;} public List<string> Files{get;set;}=new(); }
    private sealed class StringReference { public string File{get;set;}=""; public List<string> AsciiStrings{get;set;}=new(); public List<string> Utf16LeStrings{get;set;}=new(); public List<string> ReferencedFilenames{get;set;}=new(); }
    private sealed class DependencyGraphReport { public List<DependencyEdge> Edges{get;set;}=new(); }
    private sealed class ExtractionMetrics { public int AsciiStringsSeen{get;set;} public int AsciiStringsKept{get;set;} public int AsciiStringsDiscarded{get;set;} public int Utf16StringsSeen{get;set;} public int Utf16StringsKept{get;set;} public int Utf16StringsDiscarded{get;set;} public int RefsSeen{get;set;} public int RefsKept{get;set;} public int RefsDiscarded{get;set;} public int DuplicateRefsSuppressed{get;set;} public int NoisyRefsDiscarded{get;set;} public int DependencyEdgesEmitted{get;set;} public int DependencyEdgesSuppressed{get;set;} public Dictionary<string,int> DiscardReasons{get;set;}=new(StringComparer.OrdinalIgnoreCase); }
    private sealed class CapHits { public bool FileReadCapHit{get;set;} public bool AsciiCapHit{get;set;} public bool Utf16CapHit{get;set;} public bool RefsCapHit{get;set;} public bool DependencyEdgesCapHit{get;set;} public bool ReportSizeCapHit{get;set;} public bool SiblingScanCapHit{get;set;} }
}

internal static class DictExt{ public static void Inc(this Dictionary<string,int> d,string k){ d[k]=d.TryGetValue(k,out var v)?v+1:1; }}

