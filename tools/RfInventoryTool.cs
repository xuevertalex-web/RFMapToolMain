using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

    private const int MaxReadBytesPerFile = 2 * 1024 * 1024;
    private const int HeaderBytes = 64;
    private const int PreviewBytes = 256;
    private const int MaxStringsPerFile = 80;
    private const int MaxRefsPerFile = 80;
    private const int MaxFiles = 400;
    private const int MaxEdges = 1500;

    public static string Run(string inputPath, string? explicitOutputRoot)
    {
        if (string.IsNullOrWhiteSpace(inputPath)) throw new InvalidOperationException("rf_inventory requires input path");
        var fullInput = Path.GetFullPath(inputPath);

        if (!File.Exists(fullInput) && !Directory.Exists(fullInput))
            throw new InvalidOperationException("rf_inventory input path not found");

        var context = BuildContext(fullInput);
        var outputRoot = ResolveOutputRoot(explicitOutputRoot);
        Directory.CreateDirectory(outputRoot);

        var report = BuildReport(context, fullInput);
        var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var outPath = Path.Combine(outputRoot, $"rf_inventory_{report.MapOrPrefix}_{ts}.json");
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(outPath, JsonSerializer.Serialize(report, opts));
        return outPath;
    }

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
            var dds = Path.Combine(mapDir, "material.dds");
            var wav = Path.Combine(mapDir, "ambient.wav");
            var unrelated = Path.Combine(mapDir, "other.bin");

            File.WriteAllBytes(bsp, BuildFixtureBytes("map.r3t material.dds", "ambient.wav"));
            File.WriteAllBytes(r3t, BuildFixtureBytes("material.dds", "map.r3m"));
            File.WriteAllBytes(dds, BuildFixtureBytes("DDS"));
            File.WriteAllBytes(wav, BuildFixtureBytes("WAV"));
            File.WriteAllBytes(unrelated, BuildFixtureBytes("junk"));

            var outRoot = Path.Combine(root, "reports");
            var reportPath = Run(bsp, outRoot);
            var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
            var files = doc.RootElement.GetProperty("InventoryTable").EnumerateArray().ToArray();

            if (files.Length == 0) throw new InvalidOperationException("selftest failed: empty inventory");
            var names = files.Select(x => x.GetProperty("Filename").GetString() ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!names.Contains("map.bsp") || !names.Contains("map.r3t")) throw new InvalidOperationException("selftest failed: missing core files");
            if (names.Contains("other.bin")) throw new InvalidOperationException("selftest failed: unrelated file included");

            var deps = doc.RootElement.GetProperty("DependencyGraph").GetProperty("Edges").EnumerateArray().ToArray();
            if (deps.Length == 0) throw new InvalidOperationException("selftest failed: no dependency edges");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static InventoryContext BuildContext(string input)
    {
        if (File.Exists(input))
        {
            if (!string.Equals(Path.GetExtension(input), ".bsp", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("rf_inventory file input must be .bsp");
            var dir = Path.GetDirectoryName(input)!;
            var prefix = Path.GetFileNameWithoutExtension(input);
            return new InventoryContext(dir, new List<string> { prefix }, true);
        }

        var bspFiles = Directory.GetFiles(input, "*.bsp", SearchOption.TopDirectoryOnly);
        if (bspFiles.Length == 0)
            throw new InvalidOperationException("rf_inventory directory input requires at least one .bsp in top level");

        var prefixes = bspFiles.Select(Path.GetFileNameWithoutExtension)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
        return new InventoryContext(input, prefixes, false);
    }

    private static InventoryReport BuildReport(InventoryContext context, string rawInput)
    {
        var candidateFiles = Directory.GetFiles(context.RootDir, "*", SearchOption.TopDirectoryOnly)
            .Where(p => IsCandidate(p, context.Prefixes))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Take(MaxFiles)
            .ToList();

        var entries = new List<InventoryFileRecord>();
        var knownByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in candidateFiles)
        {
            var fi = new FileInfo(path);
            var data = ReadCappedBytes(path, MaxReadBytesPerFile);
            var ascii = ExtractAsciiStrings(data, MaxStringsPerFile);
            var utf16 = ExtractUtf16LeStrings(data, MaxStringsPerFile);
            var refs = ExtractReferencedNames(ascii.Concat(utf16), MaxRefsPerFile);

            var rel = Path.GetRelativePath(context.RootDir, path).Replace('\\', '/');
            var prefixGroup = MatchPrefix(fi.Name, context.Prefixes) ?? "unknown";
            var role = GuessRole(fi.Extension);
            var textLike = IsTextLike(data);
            var uncertainty = BuildUncertainty(fi.Extension, refs.Count, textLike);

            entries.Add(new InventoryFileRecord
            {
                RelativePath = rel,
                Filename = fi.Name,
                Extension = fi.Extension,
                Size = fi.Length,
                SamePrefixGroup = prefixGroup,
                LikelyRoleGuess = role,
                First64BytesHex = ToHex(data.Take(HeaderBytes).ToArray()),
                PrintablePreview = ToPrintablePreview(data, PreviewBytes),
                AsciiStrings = ascii,
                Utf16LeStrings = utf16,
                ReferencedFilenames = refs,
                ReferencedExtensions = refs.Select(Path.GetExtension).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
                ReferencedResourceNames = refs.Select(Path.GetFileNameWithoutExtension).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxRefsPerFile).ToList(),
                BinaryOrTextLike = textLike ? "text-like" : "binary-like",
                UncertaintyNotes = uncertainty
            });
            knownByName[fi.Name] = rel;
        }

        var edges = new List<DependencyEdge>();
        foreach (var e in entries)
        {
            foreach (var r in e.ReferencedFilenames)
            {
                var key = Path.GetFileName(r.Replace('/', Path.DirectorySeparatorChar));
                if (key != null && knownByName.TryGetValue(key, out var target))
                {
                    edges.Add(new DependencyEdge { From = e.RelativePath, To = target, Evidence = r, Confidence = "suspected" });
                    if (edges.Count >= MaxEdges) break;
                }
            }
            if (edges.Count >= MaxEdges) break;
        }

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
            SamePrefixGroups = entries.GroupBy(e => e.SamePrefixGroup, StringComparer.OrdinalIgnoreCase).Select(g => new PrefixGroupSummary { Prefix = g.Key, Count = g.Count(), Files = g.Select(x => x.Filename).Take(50).ToList() }).ToList(),
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
            SuspectedRolePerExtension = entries.GroupBy(e => e.Extension, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().LikelyRoleGuess, StringComparer.OrdinalIgnoreCase),
            UnknownsNextQuestions = new List<string> { "References are evidence-only and may be false positives.", "No parser-level certainty is claimed for binary formats." },
            RecommendedNextExperiment = "Run rf_inventory on the same map directory before and after a known controlled asset rename to compare edge stability."
        };
    }

    private static string ResolveOutputRoot(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot)) return Path.GetFullPath(explicitRoot);

        var artifactRoot = Environment.GetEnvironmentVariable("ArtifactOutputRoot");
        if (!string.IsNullOrWhiteSpace(artifactRoot))
            return Path.Combine(Path.GetFullPath(artifactRoot), "reports", "rf_inventory");

        return Path.Combine(Environment.CurrentDirectory, "_runs", "reports", "rf_inventory");
    }

    private static bool IsCandidate(string path, IReadOnlyCollection<string> prefixes)
    {
        var ext = Path.GetExtension(path);
        var file = Path.GetFileNameWithoutExtension(path);
        var samePrefix = prefixes.Any(p => file.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        if (samePrefix) return true;
        return RecognizedExtensions.Contains(ext) && prefixes.Any(p => string.Equals(file, p, StringComparison.OrdinalIgnoreCase));
    }

    private static string? MatchPrefix(string filename, IReadOnlyCollection<string> prefixes)
    {
        var file = Path.GetFileNameWithoutExtension(filename);
        return prefixes.FirstOrDefault(p => file.StartsWith(p, StringComparison.OrdinalIgnoreCase));
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
        var list = new List<string>();
        var sb = new StringBuilder();
        foreach (var b in data)
        {
            if (b >= 32 && b <= 126) sb.Append((char)b);
            else
            {
                if (sb.Length >= 4) { list.Add(sb.ToString()); if (list.Count >= cap) return list; }
                sb.Clear();
            }
        }
        if (sb.Length >= 4 && list.Count < cap) list.Add(sb.ToString());
        return list;
    }

    private static List<string> ExtractUtf16LeStrings(byte[] data, int cap)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        for (int i = 0; i + 1 < data.Length; i += 2)
        {
            var lo = data[i];
            var hi = data[i + 1];
            if (hi == 0 && lo >= 32 && lo <= 126) sb.Append((char)lo);
            else
            {
                if (sb.Length >= 4) { list.Add(sb.ToString()); if (list.Count >= cap) return list; }
                sb.Clear();
            }
        }
        if (sb.Length >= 4 && list.Count < cap) list.Add(sb.ToString());
        return list;
    }

    private static List<string> ExtractReferencedNames(IEnumerable<string> strings, int cap)
    {
        var rx = new Regex(@"([A-Za-z0-9_\-./\\]+\.[A-Za-z0-9]{1,8})", RegexOptions.Compiled);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in strings)
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
        int printable = data.Count(b => (b >= 32 && b <= 126) || b is 9 or 10 or 13);
        return printable >= data.Length * 0.7;
    }

    private static string ToPrintablePreview(byte[] data, int cap)
    {
        var slice = data.Take(cap).ToArray();
        var sb = new StringBuilder(slice.Length);
        foreach (var b in slice) sb.Append((b >= 32 && b <= 126) ? (char)b : '.');
        return sb.ToString();
    }

    private static string BuildUncertainty(string ext, int refsCount, bool textLike)
    {
        return $"Likely role is heuristic for {ext}; references ({refsCount}) are evidence-only; binary/text signal is {(textLike ? "possibly text-like" : "possibly binary-like")}.";
    }

    private static List<string> BuildExtensionSummary(List<DependencyEdge> edges, List<InventoryFileRecord> entries)
    {
        var byPathExt = entries.ToDictionary(x => x.RelativePath, x => x.Extension, StringComparer.OrdinalIgnoreCase);
        return edges.Select(e => $"{byPathExt.GetValueOrDefault(e.From, "?")} -> {byPathExt.GetValueOrDefault(e.To, "?")}")
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(g => $"{g.Key}: {g.Count()}")
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string DetectPattern(List<DependencyEdge> edges, string srcExt, string[] dstExt)
    {
        var found = edges.Any(e => e.From.EndsWith(srcExt, StringComparison.OrdinalIgnoreCase) && dstExt.Any(d => e.To.EndsWith(d, StringComparison.OrdinalIgnoreCase)));
        return found ? "suspected linkage present (evidence-only)" : "not observed in bounded scan";
    }

    private static string GuessRole(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            ".bsp" => "Likely world container (uncertain)",
            ".r3t" => "Likely texture/material index (uncertain)",
            ".r3m" => "Possibly material mapping",
            ".rsm" => "Possibly model reference",
            ".dds" or ".tga" => "Likely texture image",
            ".dat" => "Possibly config/data linkage",
            ".ani" => "Possibly animation linkage",
            ".eff" => "Possibly effect linkage",
            ".snd" => "Possibly sound descriptor",
            ".wav" or ".ogg" => "Likely audio resource",
            _ => "Unknown same-prefix related file"
        };
    }
    private static string ToHex(byte[] bytes) => string.Join(" ", bytes.Select(b => b.ToString("X2")));

    private static string SanitizePath(string fullPath)
    {
        try
        {
            var cwd = Path.GetFullPath(Environment.CurrentDirectory);
            if (fullPath.StartsWith(cwd, StringComparison.OrdinalIgnoreCase))
                return Path.GetRelativePath(cwd, fullPath).Replace('\\', '/');
        }
        catch { }
        return Path.GetFileName(fullPath);
    }

    private static byte[] BuildFixtureBytes(params string[] asciiAndUtf16)
    {
        using var ms = new MemoryStream();
        foreach (var s in asciiAndUtf16)
        {
            var a = Encoding.ASCII.GetBytes(s);
            ms.Write(a, 0, a.Length);
            ms.WriteByte(0);
            foreach (var c in s) { ms.WriteByte((byte)c); ms.WriteByte(0); }
            ms.WriteByte(0); ms.WriteByte(0);
        }
        return ms.ToArray();
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
    private sealed class DependencyEdge { public string From { get; set; } = ""; public string To { get; set; } = ""; public string Evidence { get; set; } = ""; public string Confidence { get; set; } = "suspected"; }
    private sealed class PrefixGroupSummary { public string Prefix { get; set; } = ""; public int Count { get; set; } public List<string> Files { get; set; } = new(); }
    private sealed class StringReference { public string File { get; set; } = ""; public List<string> AsciiStrings { get; set; } = new(); public List<string> Utf16LeStrings { get; set; } = new(); public List<string> ReferencedFilenames { get; set; } = new(); }
    private sealed class SignatureSummary { public string File { get; set; } = ""; public long Size { get; set; } public string BinaryOrTextLike { get; set; } = ""; public string First64BytesHex { get; set; } = ""; }
    private sealed class DependencyGraphReport { public List<DependencyEdge> Edges { get; set; } = new(); public List<string> ExtensionRelationshipSummary { get; set; } = new(); public string BspToModelMaterialDetection { get; set; } = ""; public string R3tToTextureDetection { get; set; } = ""; public string DatEffSndWavLinkageDetection { get; set; } = ""; }
}

