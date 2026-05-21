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

    private const int MaxReadBytesPerFile = 512 * 1024;
    private const int HeaderBytes = 64;
    private const int PreviewBytes = 256;
    private const int MaxStringsPerFile = 64;
    private const int MaxRefsPerFile = 64;
    private const int MaxFiles = 256;
    private const int MaxEdges = 512;
    private const int MaxReportBytes = 2 * 1024 * 1024;

    public static void RunSelfTest()
    {
        var root = Path.Combine(Path.GetTempPath(), "rf_inventory_selftest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var mapDir = Path.Combine(root, "map");
            Directory.CreateDirectory(mapDir);
            var bsp = Path.Combine(mapDir, "map.bsp");
            var r3t = Path.Combine(mapDir, "map.r3t");
            File.WriteAllBytes(bsp, Encoding.ASCII.GetBytes("map.r3t\0"));
            File.WriteAllBytes(r3t, Encoding.ASCII.GetBytes("map.dds\0"));
            var outRoot = Path.Combine(root, "reports");
            var outPath = Run(bsp, outRoot);
            if (!File.Exists(outPath)) throw new InvalidOperationException("selftest report missing");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    public static string Run(string inputPath, string? explicitOutputRoot)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(inputPath)) throw new InvalidOperationException("rf_inventory requires input path");
            var fullInput = Path.GetFullPath(inputPath);
            if (!File.Exists(fullInput) && !Directory.Exists(fullInput)) throw new InvalidOperationException("rf_inventory input path not found");

            var context = BuildContext(fullInput);
            var outputRoot = ResolveOutputRoot(explicitOutputRoot);
            Directory.CreateDirectory(outputRoot);

            var report = BuildReport(context, fullInput);
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            if (Encoding.UTF8.GetByteCount(json) > MaxReportBytes)
            {
                report.UnknownsNextQuestions.Add("Report was truncated due to size cap.");
                report.InventoryTable = report.InventoryTable.Take(64).ToList();
                report.PerFileSignatureSummary = report.PerFileSignatureSummary.Take(64).ToList();
                report.ExtractedStringsAndReferences = report.ExtractedStringsAndReferences.Take(64).ToList();
                report.DependencyGraph.Edges = report.DependencyGraph.Edges.Take(128).ToList();
                json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            }

            var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var outPath = Path.Combine(outputRoot, $"rf_inventory_{report.MapOrPrefix}_{ts}.json");
            File.WriteAllText(outPath, json, Encoding.UTF8);
            return outPath;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(SanitizeError(ex.Message));
        }
    }

    private static InventoryContext BuildContext(string input)
    {
        if (File.Exists(input))
        {
            if (!string.Equals(Path.GetExtension(input), ".bsp", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("rf_inventory file input must be .bsp");
            var dir = Path.GetDirectoryName(input)!;
            RejectUnsafeDirectory(dir);
            var prefix = Path.GetFileNameWithoutExtension(input);
            return new InventoryContext(dir, new List<string> { prefix }, true);
        }

        RejectUnsafeDirectory(input);
        var bspFiles = Directory.GetFiles(input, "*.bsp", SearchOption.TopDirectoryOnly);
        if (bspFiles.Length == 0) throw new InvalidOperationException("rf_inventory directory input requires at least one .bsp in top level");

        var prefixes = bspFiles.Select(Path.GetFileNameWithoutExtension).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Cast<string>().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        return new InventoryContext(input, prefixes, false);
    }

    private static InventoryReport BuildReport(InventoryContext context, string rawInput)
    {
        var files = Directory.GetFiles(context.RootDir, "*", SearchOption.TopDirectoryOnly)
            .Where(p => IsCandidate(p, context.Prefixes))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Take(MaxFiles)
            .ToList();

        var entries = new List<InventoryFileRecord>();
        var byFilename = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in files)
        {
            var fi = new FileInfo(path);
            var data = ReadCappedBytes(path, MaxReadBytesPerFile);
            var ascii = ExtractAsciiStrings(data, MaxStringsPerFile);
            var utf16 = ExtractUtf16LeStrings(data, MaxStringsPerFile);
            var refs = ExtractReferencedNames(ascii.Concat(utf16), MaxRefsPerFile);
            var rel = Path.GetRelativePath(context.RootDir, path).Replace('\\', '/');
            var extRefs = refs.Select(r => Path.GetExtension(r) ?? "").Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

            entries.Add(new InventoryFileRecord
            {
                RelativePath = rel,
                Filename = fi.Name,
                Extension = fi.Extension,
                Size = fi.Length,
                SamePrefixGroup = MatchPrefix(fi.Name, context.Prefixes) ?? "unknown",
                LikelyRoleGuess = GuessRole(fi.Extension),
                First64BytesHex = ToHex(data.Take(HeaderBytes).ToArray()),
                PrintablePreview = ToPrintablePreview(data, PreviewBytes),
                AsciiStrings = ascii,
                Utf16LeStrings = utf16,
                ReferencedFilenames = refs,
                ReferencedExtensions = extRefs,
                ReferencedResourceNames = refs.Select(Path.GetFileNameWithoutExtension).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(MaxRefsPerFile).ToList()!,
                BinaryOrTextLike = IsTextLike(data) ? "text-like" : "binary-like",
                UncertaintyNotes = "Evidence-only inference; same-prefix relation is low-confidence."
            });
            byFilename[fi.Name] = rel;
        }

        entries = entries.OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();

        var edges = BuildNormalizedEdges(entries, byFilename);
        var groups = entries.GroupBy(e => e.SamePrefixGroup, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new PrefixGroupSummary { Prefix = g.Key, Count = g.Count(), Files = g.Select(x => x.Filename).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(64).ToList() })
            .ToList();

        return new InventoryReport
        {
            Tool = "rf_inventory",
            Mode = "read_only",
            InputMode = context.FromBspFile ? "single_bsp_file" : "single_directory",
            InputPathSanitized = SanitizePath(rawInput),
            RootDirectorySanitized = SanitizePath(context.RootDir),
            MapOrPrefix = context.Prefixes.Count == 1 ? context.Prefixes[0] : "multi",
            GeneratedUtc = DateTime.UtcNow,
            InventoryTable = entries,
            SamePrefixGroups = groups,
            PerFileSignatureSummary = entries.Select(e => new SignatureSummary { File = e.RelativePath, Size = e.Size, BinaryOrTextLike = e.BinaryOrTextLike, First64BytesHex = e.First64BytesHex }).ToList(),
            ExtractedStringsAndReferences = entries.Select(e => new StringReference { File = e.RelativePath, AsciiStrings = e.AsciiStrings, Utf16LeStrings = e.Utf16LeStrings, ReferencedFilenames = e.ReferencedFilenames }).ToList(),
            DependencyGraph = new DependencyGraphReport
            {
                Edges = edges,
                ExtensionRelationshipSummary = BuildExtensionSummary(edges, entries),
                BspToModelMaterialDetection = DetectPattern(edges, ".bsp", new[] { ".r3t", ".r3m", ".rsm" }),
                R3tToTextureDetection = DetectPattern(edges, ".r3t", new[] { ".dds", ".tga", ".r3m" }),
                DatEffSndWavLinkageDetection = DetectPattern(edges, ".dat", new[] { ".eff", ".snd", ".wav", ".ogg" })
            },
            SuspectedRolePerExtension = entries.GroupBy(e => e.Extension, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().LikelyRoleGuess, StringComparer.OrdinalIgnoreCase),
            UnknownsNextQuestions = new List<string> { "References are evidence-only and may include false positives.", "No parser-level certainty is claimed." },
            RecommendedNextExperiment = "Use controlled rename in same folder and compare normalized edges across two runs."
        };
    }

    private static List<DependencyEdge> BuildNormalizedEdges(List<InventoryFileRecord> entries, Dictionary<string, string> byFilename)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<DependencyEdge>();
        var extByPath = entries.ToDictionary(e => e.RelativePath, e => e.Extension, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            foreach (var rf in entry.ReferencedFilenames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var key = Path.GetFileName(rf.Replace('/', Path.DirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(key) || !byFilename.TryGetValue(key, out var to)) continue;

                var fromExt = extByPath.GetValueOrDefault(entry.RelativePath, "").ToLowerInvariant();
                var toExt = extByPath.GetValueOrDefault(to, "").ToLowerInvariant();
                var signature = $"{entry.RelativePath}|{to}|{fromExt}|{toExt}";
                if (!set.Add(signature)) continue;

                edges.Add(new DependencyEdge
                {
                    From = entry.RelativePath,
                    To = to,
                    Evidence = rf,
                    EvidenceType = "string_reference",
                    SourceExtension = fromExt,
                    TargetExtension = toExt,
                    Confidence = "low"
                });
                if (edges.Count >= MaxEdges) return edges.OrderBy(e => e.From, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.To, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        return edges.OrderBy(e => e.From, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.To, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Evidence, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string ResolveOutputRoot(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot)) return Path.GetFullPath(explicitRoot);
        var artifactRoot = Environment.GetEnvironmentVariable("ArtifactOutputRoot");
        if (!string.IsNullOrWhiteSpace(artifactRoot)) return Path.Combine(Path.GetFullPath(artifactRoot), "reports", "rf_inventory");
        return Path.Combine(Environment.CurrentDirectory, "_runs", "reports", "rf_inventory");
    }

    private static void RejectUnsafeDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        var di = new DirectoryInfo(full);
        if (di.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException("reparse/traversal path is not allowed");
    }

    private static bool IsCandidate(string path, IReadOnlyCollection<string> prefixes)
    {
        var ext = Path.GetExtension(path);
        var stem = Path.GetFileNameWithoutExtension(path);
        return prefixes.Any(p => stem.StartsWith(p, StringComparison.OrdinalIgnoreCase) && (RecognizedExtensions.Contains(ext) || true));
    }

    private static string? MatchPrefix(string filename, IReadOnlyCollection<string> prefixes)
    {
        var stem = Path.GetFileNameWithoutExtension(filename);
        return prefixes.OrderBy(x => x.Length).FirstOrDefault(p => stem.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static byte[] ReadCappedBytes(string path, int maxBytes)
    {
        using var fs = File.OpenRead(path);
        var len = (int)Math.Min(fs.Length, maxBytes);
        var buffer = new byte[len];
        var read = fs.Read(buffer, 0, len);
        return read == len ? buffer : buffer.Take(read).ToArray();
    }

    private static List<string> ExtractAsciiStrings(byte[] data, int cap)
    {
        var outList = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        foreach (var b in data)
        {
            if (b >= 32 && b <= 126) sb.Append((char)b);
            else Flush();
            if (outList.Count >= cap) break;
        }
        Flush();
        return outList.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

        void Flush()
        {
            if (sb.Length < 4) { sb.Clear(); return; }
            var s = sb.ToString().Trim();
            sb.Clear();
            if (IsNoise(s)) return;
            if (seen.Add(s)) outList.Add(s);
        }
    }

    private static List<string> ExtractUtf16LeStrings(byte[] data, int cap)
    {
        var outList = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        for (int i = 0; i + 1 < data.Length; i += 2)
        {
            var lo = data[i]; var hi = data[i + 1];
            if (hi == 0 && lo >= 32 && lo <= 126) sb.Append((char)lo);
            else Flush();
            if (outList.Count >= cap) break;
        }
        Flush();
        return outList.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

        void Flush()
        {
            if (sb.Length < 4) { sb.Clear(); return; }
            var s = sb.ToString().Trim();
            sb.Clear();
            if (IsNoise(s)) return;
            if (seen.Add(s)) outList.Add(s);
        }
    }

    private static bool IsNoise(string s)
    {
        if (s.Length < 4 || s.Length > 180) return true;
        if (s.All(ch => ch == '.' || ch == '-' || ch == '_' || ch == ' ')) return true;
        int alpha = s.Count(char.IsLetterOrDigit);
        return alpha < 2;
    }

    private static List<string> ExtractReferencedNames(IEnumerable<string> strings, int cap)
    {
        var rx = new Regex(@"([A-Za-z0-9_\-./\\]+\.[A-Za-z0-9]{1,8})", RegexOptions.Compiled);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in strings.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            foreach (Match m in rx.Matches(s))
            {
                var token = m.Groups[1].Value.Trim().Replace('\\', '/');
                if (token.Length < 3) continue;
                set.Add(token);
                if (set.Count >= cap) return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
        return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsTextLike(byte[] data)
    {
        if (data.Length == 0) return false;
        var printable = data.Count(b => (b >= 32 && b <= 126) || b is 9 or 10 or 13);
        return printable >= data.Length * 0.70;
    }

    private static string ToPrintablePreview(byte[] data, int cap)
    {
        var slice = data.Take(cap);
        var sb = new StringBuilder(cap);
        foreach (var b in slice) sb.Append((b >= 32 && b <= 126) ? (char)b : '.');
        return sb.ToString();
    }

    private static List<string> BuildExtensionSummary(List<DependencyEdge> edges, List<InventoryFileRecord> entries)
    {
        var rel2ext = entries.ToDictionary(e => e.RelativePath, e => e.Extension, StringComparer.OrdinalIgnoreCase);
        return edges.Select(e => $"{rel2ext.GetValueOrDefault(e.From, "?").ToLowerInvariant()} -> {rel2ext.GetValueOrDefault(e.To, "?").ToLowerInvariant()}")
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => $"{g.Key}: {g.Count()}")
            .ToList();
    }

    private static string DetectPattern(List<DependencyEdge> edges, string srcExt, string[] dstExt)
    {
        var found = edges.Any(e => string.Equals(e.SourceExtension, srcExt, StringComparison.OrdinalIgnoreCase) && dstExt.Contains(e.TargetExtension, StringComparer.OrdinalIgnoreCase));
        return found ? "suspected linkage present (evidence-only, low-confidence)" : "not observed in bounded scan";
    }

    private static string GuessRole(string ext) => ext.ToLowerInvariant() switch
    {
        ".bsp" => "Likely world container (uncertain)",
        ".r3t" => "Likely texture/material index (uncertain)",
        ".r3m" => "Possibly material mapping",
        ".rsm" => "Possibly static model reference",
        ".dds" or ".tga" => "Likely texture image",
        ".dat" => "Possibly data/config linkage",
        ".ani" => "Possibly animation linkage",
        ".eff" => "Possibly effect linkage",
        ".snd" => "Possibly sound descriptor",
        ".wav" or ".ogg" => "Likely audio resource",
        _ => "Unknown same-prefix related file"
    };

    private static string ToHex(byte[] bytes) => string.Join(" ", bytes.Select(b => b.ToString("X2")));

    private static string SanitizePath(string fullPath)
    {
        try
        {
            var cwd = Path.GetFullPath(Environment.CurrentDirectory);
            if (fullPath.StartsWith(cwd, StringComparison.OrdinalIgnoreCase)) return Path.GetRelativePath(cwd, fullPath).Replace('\\', '/');
        }
        catch { }
        return Path.GetFileName(fullPath);
    }

    private static string SanitizeError(string message)
    {
        var cwd = Path.GetFullPath(Environment.CurrentDirectory).Replace('\\', '/');
        var m = (message ?? "error").Replace('\\', '/');
        m = m.Replace(cwd, "<workspace>", StringComparison.OrdinalIgnoreCase);
        return m.Length > 240 ? m[..240] : m;
    }

    private sealed record InventoryContext(string RootDir, List<string> Prefixes, bool FromBspFile);
    private sealed class InventoryReport
    {
        public string Tool { get; set; } = "rf_inventory";
        public string Mode { get; set; } = "read_only";
        public string InputMode { get; set; } = "";
        public string InputPathSanitized { get; set; } = "";
        public string RootDirectorySanitized { get; set; } = "";
        public string MapOrPrefix { get; set; } = "";
        public DateTime GeneratedUtc { get; set; }
        public List<InventoryFileRecord> InventoryTable { get; set; } = new();
        public List<PrefixGroupSummary> SamePrefixGroups { get; set; } = new();
        public List<SignatureSummary> PerFileSignatureSummary { get; set; } = new();
        public List<StringReference> ExtractedStringsAndReferences { get; set; } = new();
        public DependencyGraphReport DependencyGraph { get; set; } = new();
        public Dictionary<string, string> SuspectedRolePerExtension { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> UnknownsNextQuestions { get; set; } = new();
        public string RecommendedNextExperiment { get; set; } = "";
    }
    private sealed class InventoryFileRecord { public string RelativePath { get; set; } = ""; public string Filename { get; set; } = ""; public string Extension { get; set; } = ""; public long Size { get; set; } public string SamePrefixGroup { get; set; } = ""; public string LikelyRoleGuess { get; set; } = ""; public string First64BytesHex { get; set; } = ""; public string PrintablePreview { get; set; } = ""; public List<string> AsciiStrings { get; set; } = new(); public List<string> Utf16LeStrings { get; set; } = new(); public List<string> ReferencedFilenames { get; set; } = new(); public List<string> ReferencedExtensions { get; set; } = new(); public List<string> ReferencedResourceNames { get; set; } = new(); public string BinaryOrTextLike { get; set; } = ""; public string UncertaintyNotes { get; set; } = ""; }
    private sealed class DependencyEdge { public string From { get; set; } = ""; public string To { get; set; } = ""; public string Evidence { get; set; } = ""; public string EvidenceType { get; set; } = "string_reference"; public string SourceExtension { get; set; } = ""; public string TargetExtension { get; set; } = ""; public string Confidence { get; set; } = "low"; }
    private sealed class PrefixGroupSummary { public string Prefix { get; set; } = ""; public int Count { get; set; } public List<string> Files { get; set; } = new(); }
    private sealed class StringReference { public string File { get; set; } = ""; public List<string> AsciiStrings { get; set; } = new(); public List<string> Utf16LeStrings { get; set; } = new(); public List<string> ReferencedFilenames { get; set; } = new(); }
    private sealed class SignatureSummary { public string File { get; set; } = ""; public long Size { get; set; } public string BinaryOrTextLike { get; set; } = ""; public string First64BytesHex { get; set; } = ""; }
    private sealed class DependencyGraphReport { public List<DependencyEdge> Edges { get; set; } = new(); public List<string> ExtensionRelationshipSummary { get; set; } = new(); public string BspToModelMaterialDetection { get; set; } = ""; public string R3tToTextureDetection { get; set; } = ""; public string DatEffSndWavLinkageDetection { get; set; } = ""; }
}
