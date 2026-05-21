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
    private static readonly HashSet<string> RefExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bsp", ".r3t", ".r3m", ".rsm", ".dds", ".tga", ".dat", ".ani", ".eff", ".snd", ".wav", ".ogg"
    };

    private const int MaxRead = 512 * 1024;
    private const int MaxStrings = 64;
    private const int MaxRefs = 64;
    private const int MaxFiles = 256;
    private const int MaxEdges = 512;
    private const int MaxReport = 2 * 1024 * 1024;

    public static void RunSelfTest() { }

    public static string Run(string input, string? outRoot)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new InvalidOperationException("rf_inventory requires input path");
        var full = Path.GetFullPath(input);
        if (!File.Exists(full) && !Directory.Exists(full)) throw new InvalidOperationException("rf_inventory input path not found");

        var ctx = BuildContext(full);
        var outDir = ResolveOutputRoot(outRoot);
        Directory.CreateDirectory(outDir);

        var report = BuildReport(ctx, full);
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        if (Encoding.UTF8.GetByteCount(json) > MaxReport)
        {
            report.CapHits.ReportSizeCapHit = true;
            report.ReferenceEvidence = report.ReferenceEvidence.Take(256).ToList();
            report.DependencyGraph.Edges = report.DependencyGraph.Edges.Take(128).ToList();
            json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        }

        var outPath = Path.Combine(outDir, $"rf_inventory_{report.MapOrPrefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
        File.WriteAllText(outPath, json, Encoding.UTF8);
        return outPath;
    }

    private static Ctx BuildContext(string input)
    {
        if (File.Exists(input))
        {
            if (!string.Equals(Path.GetExtension(input), ".bsp", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("rf_inventory file input must be .bsp");
            return new Ctx(Path.GetDirectoryName(input)!, new() { Path.GetFileNameWithoutExtension(input)! }, true);
        }

        var bsp = Directory.GetFiles(input, "*.bsp", SearchOption.TopDirectoryOnly);
        if (bsp.Length == 0) throw new InvalidOperationException("rf_inventory directory input requires at least one .bsp in top level");
        return new Ctx(input, bsp.Select(Path.GetFileNameWithoutExtension).Cast<string>().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(), false);
    }

    private static Report BuildReport(Ctx c, string raw)
    {
        var m = new Metrics();
        var cap = new Caps();

        var files = Directory.GetFiles(c.Root, "*", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(x => c.Prefixes.Any(p => Path.GetFileNameWithoutExtension(x).StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .Take(MaxFiles)
            .ToList();
        if (files.Count == MaxFiles) cap.SiblingScanCapHit = true;

        var inv = new List<FileRec>();
        var ev = new List<RefEvidence>();
        var unresolved = new List<UnresolvedReferenceRow>();
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in files)
        {
            bool hit;
            var b = Read(f, MaxRead, out hit);
            if (hit) cap.FileReadCapHit = true;
            var rel = Path.GetRelativePath(c.Root, f).Replace('\\', '/');

            var ascii = ExtractStrings(b, false, m, cap);
            var utf16 = ExtractStrings(b, true, m, cap);
            var refs = ExtractRefs(ascii.Concat(utf16).ToList(), rel, c.Prefixes.FirstOrDefault() ?? "multi", m, cap, out var unresolvedFromExtract);
            ev.AddRange(refs);
            unresolved.AddRange(unresolvedFromExtract);

            var sig = BuildSignature(f, b, hit);
            inv.Add(new FileRec
            {
                RelativePath = rel,
                Filename = Path.GetFileName(f),
                Extension = Path.GetExtension(f),
                Size = new FileInfo(f).Length,
                SamePrefixGroup = Prefix(Path.GetFileName(f), c.Prefixes),
                ReferencedFilenames = refs.Select(x => x.NormalizedTarget).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                FileSize = new FileInfo(f).Length,
                First16BytesHex = sig.First16BytesHex,
                First64BytesHex = sig.First64BytesHex,
                First256BytesHash = sig.First256BytesHash,
                WholeFileHash = sig.WholeFileHash,
                PrintablePrefixPreview = sig.PrintablePrefixPreview,
                ZeroByteRatioFirst256 = sig.ZeroByteRatioFirst256,
                PrintableRatioFirst256 = sig.PrintableRatioFirst256,
                SuspectedTextOrBinary = sig.SuspectedTextOrBinary
            });
            byName[Path.GetFileName(f)] = rel;
        }

        ev = ev.OrderBy(x => x.SourceFile, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.ByteOffset).ThenBy(x => x.ExtractedToken, StringComparer.OrdinalIgnoreCase).ToList();
        var unresolvedAudit = BuildUnresolvedAudit(c, inv, ev, unresolved);
        var unresolvedAgg = BuildUnresolvedAggregation(c, unresolvedAudit);
        var edges = BuildEdges(inv, ev, byName, m, cap);
        m.DependencyEdgesEmitted = edges.Count;

        return new Report
        {
            Tool = "rf_inventory",
            Mode = "read_only",
            InputMode = c.FromBsp ? "single_bsp_file" : "single_directory",
            InputPathSanitized = San(raw),
            RootDirectorySanitized = San(c.Root),
            MapOrPrefix = c.Prefixes.Count == 1 ? c.Prefixes[0] : "multi",
            GeneratedUtc = DateTime.UtcNow,
            InventoryTable = inv.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).ToList(),
            ReferenceEvidence = ev,
            DependencyGraph = new Graph { Edges = edges },
            OffsetEvidenceSummary = BuildOffsetSummary(ev),
            SignatureGroupingSummary = BuildSignatureGrouping(inv),
            CompanionFileMatrix = BuildCompanionMatrix(c, inv, ev),
            UnresolvedReferenceAudit = unresolvedAudit,
            UnresolvedReferenceAggregation = unresolvedAgg,
            ExtractionMetrics = m,
            CapHits = cap
        };
    }

    private static List<UnresolvedReferenceRow> BuildUnresolvedAudit(Ctx c, List<FileRec> inv, List<RefEvidence> ev, List<UnresolvedReferenceRow> seed)
    {
        var rows = new List<UnresolvedReferenceRow>(seed);
        var present = new HashSet<string>(inv.Select(x => x.Filename), StringComparer.OrdinalIgnoreCase);
        foreach (var e in ev.OrderBy(x => x.SourceFile, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.ByteOffset).ThenBy(x => x.ExtractedToken, StringComparer.OrdinalIgnoreCase))
        {
            var targetName = Path.GetFileName(e.NormalizedTarget);
            var ext = Path.GetExtension(e.NormalizedTarget);
            string reason;
            if (!RefExts.Contains(ext))
            {
                reason = "unsupported_extension";
            }
            else if (e.NormalizedTarget.Contains("../", StringComparison.Ordinal) || e.NormalizedTarget.Contains("..\\", StringComparison.Ordinal))
            {
                reason = "outside_bounded_scope";
            }
            else if (string.IsNullOrWhiteSpace(targetName))
            {
                reason = "candidate_missing";
            }
            else if (present.Contains(targetName))
            {
                continue;
            }
            else if (e.NormalizedTarget.Contains("/") || e.NormalizedTarget.Contains("\\"))
            {
                reason = "ambiguous_case_or_path";
            }
            else
            {
                reason = "not_present_in_same_directory";
            }

            rows.Add(new UnresolvedReferenceRow
            {
                MapName = c.Prefixes.FirstOrDefault() ?? "multi",
                SourceFile = e.SourceFile,
                ExtractedToken = e.ExtractedToken,
                NormalizedTarget = e.NormalizedTarget,
                TargetExtension = ext,
                Encoding = e.Encoding,
                ByteOffset = e.ByteOffset,
                EvidenceType = e.EvidenceType,
                Confidence = e.Confidence,
                ReasonUnresolved = reason
            });
        }
        return rows;
    }

    private static UnresolvedReferenceAggregation BuildUnresolvedAggregation(Ctx c, List<UnresolvedReferenceRow> rows)
    {
        return new UnresolvedReferenceAggregation
        {
            ByExtension = rows.GroupBy(x => x.TargetExtension, StringComparer.OrdinalIgnoreCase).Select(g => new KV { Key = g.Key, Value = g.Count() }).OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList(),
            BySourceExtension = rows.GroupBy(x => Path.GetExtension(x.SourceFile), StringComparer.OrdinalIgnoreCase).Select(g => new KV { Key = g.Key, Value = g.Count() }).OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList(),
            ByMap = rows.GroupBy(x => x.MapName, StringComparer.OrdinalIgnoreCase).Select(g => new KV { Key = g.Key, Value = g.Count() }).OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList(),
            MostCommonMissingTargets = rows.GroupBy(x => Path.GetFileName(x.NormalizedTarget), StringComparer.OrdinalIgnoreCase).Where(g => !string.IsNullOrWhiteSpace(g.Key)).Select(g => new KV { Key = g.Key!, Value = g.Count() }).OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Take(20).ToList(),
            SourceFilesMostUnresolved = rows.GroupBy(x => x.SourceFile, StringComparer.OrdinalIgnoreCase).Select(g => new KV { Key = g.Key, Value = g.Count() }).OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Take(20).ToList(),
            Observation = "observed unresolved reference; candidate external resource folder"
        };
    }

    private static SignatureInfo BuildSignature(string filePath, byte[] buffer, bool readCapHit)
    {
        var first16 = buffer.Take(16).ToArray();
        var first64 = buffer.Take(64).ToArray();
        var first256 = buffer.Take(256).ToArray();
        var zeroRatio = first256.Length == 0 ? 0d : first256.Count(x => x == 0) / (double)first256.Length;
        var printableRatio = first256.Length == 0 ? 0d : first256.Count(x => x >= 32 && x <= 126) / (double)first256.Length;
        var preview = new string(first256.Take(64).Select(x => (x >= 32 && x <= 126) ? (char)x : '.').ToArray());
        return new SignatureInfo(
            ToHex(first16),
            ToHex(first64),
            HashBytes(first256),
            readCapHit ? null : HashFile(filePath),
            preview,
            zeroRatio,
            printableRatio,
            printableRatio >= 0.70 ? "possible_text" : "possible_binary");
    }

    private static SignatureGroupingSummary BuildSignatureGrouping(List<FileRec> inv)
    {
        string SizeBucket(long n) => n < 1024 ? "<1KB" : n < 64 * 1024 ? "1KB-64KB" : n < 1024 * 1024 ? "64KB-1MB" : ">=1MB";
        return new SignatureGroupingSummary
        {
            ByExtension = inv.GroupBy(x => x.Extension, StringComparer.OrdinalIgnoreCase).Select(g => new KV { Key = g.Key, Value = g.Count() }).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList(),
            ByFirst4Bytes = inv.GroupBy(x => string.Join(" ", x.First16BytesHex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(4)), StringComparer.OrdinalIgnoreCase).Select(g => new KV { Key = g.Key, Value = g.Count() }).OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList(),
            ByFirst16Bytes = inv.GroupBy(x => x.First16BytesHex, StringComparer.OrdinalIgnoreCase).Select(g => new KV { Key = g.Key, Value = g.Count() }).OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList(),
            ByFileSizeBucket = inv.GroupBy(x => SizeBucket(x.FileSize), StringComparer.OrdinalIgnoreCase).Select(g => new KV { Key = g.Key, Value = g.Count() }).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList(),
            ByHashPrefix = inv.GroupBy(x => x.First256BytesHash[..Math.Min(8, x.First256BytesHash.Length)], StringComparer.OrdinalIgnoreCase).Select(g => new KV { Key = g.Key, Value = g.Count() }).OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList(),
            Observation = "observed header pattern; same first_16_bytes group; possible format family"
        };
    }

    private static CompanionFileMatrixSummary BuildCompanionMatrix(Ctx c, List<FileRec> inv, List<RefEvidence> ev)
    {
        var rows = new List<CompanionMatrixRow>();
        foreach (var g in inv.GroupBy(x => x.SamePrefixGroup, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var eg in g.GroupBy(x => x.Extension, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new CompanionMatrixRow { MapName = c.Prefixes.FirstOrDefault() ?? "multi", SamePrefixGroup = g.Key, Extension = eg.Key, FileCount = eg.Count(), EvidenceSource = "present_file" });
            }
            rows.Add(new CompanionMatrixRow { MapName = c.Prefixes.FirstOrDefault() ?? "multi", SamePrefixGroup = g.Key, Extension = "same_prefix_observation", FileCount = g.Count(), EvidenceSource = "same_prefix_observation" });
        }
        foreach (var rg in ev.GroupBy(x => Path.GetExtension(x.NormalizedTarget), StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(new CompanionMatrixRow { MapName = c.Prefixes.FirstOrDefault() ?? "multi", SamePrefixGroup = "reference_scope", Extension = rg.Key, FileCount = rg.Count(), EvidenceSource = "string_reference" });
            rows.Add(new CompanionMatrixRow { MapName = c.Prefixes.FirstOrDefault() ?? "multi", SamePrefixGroup = "reference_scope", Extension = rg.Key, FileCount = rg.Select(x => x.ByteOffset).Distinct().Count(), EvidenceSource = "offset_reference" });
        }

        var present = new HashSet<string>(inv.Select(x => x.Extension), StringComparer.OrdinalIgnoreCase);
        var observations = new List<string>();
        if (present.Contains(".bsp") && !present.Contains(".r3t")) observations.Add("observed .bsp with missing in bounded scope: .r3t");
        if (present.Contains(".bsp") && !present.Contains(".r3m")) observations.Add("observed .bsp with missing in bounded scope: .r3m");
        if (present.Contains(".r3t") && !ev.Any(x => string.Equals(Path.GetExtension(x.NormalizedTarget), ".dds", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(x.NormalizedTarget), ".tga", StringComparison.OrdinalIgnoreCase)))
            observations.Add("observed .r3t with no texture refs in bounded scope");
        var presentFiles = new HashSet<string>(inv.Select(x => x.Filename), StringComparer.OrdinalIgnoreCase);
        var missingRefTargets = ev.Select(x => Path.GetFileName(x.NormalizedTarget)).Where(x => !string.IsNullOrWhiteSpace(x) && !presentFiles.Contains(x!)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        if (missingRefTargets.Count > 0) observations.Add($"observed texture refs missing in bounded scope: {string.Join(',', missingRefTargets.Take(10))}");

        var byCombo = inv.GroupBy(x => x.SamePrefixGroup, StringComparer.OrdinalIgnoreCase)
            .Select(g => string.Join("+", g.Select(x => x.Extension).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new KV { Key = g.Key, Value = g.Count() }).ToList();

        var frequent = byCombo.Where(x => x.Value >= 2).Select(x => x.Key).ToList();
        var rare = byCombo.Where(x => x.Value == 1).Select(x => x.Key).ToList();
        return new CompanionFileMatrixSummary
        {
            Rows = rows.OrderBy(x => x.MapName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.SamePrefixGroup, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Extension, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.EvidenceSource, StringComparer.OrdinalIgnoreCase).ToList(),
            CommonExtensionCombinations = byCombo.Take(20).ToList(),
            MissingCompanionObservations = observations,
            RareCompanionObservations = rare,
            InconsistentCompanionObservations = frequent.Count == 0 ? new List<string> { "observed together patterns vary in bounded scope" } : new List<string>(),
            UniversalCompanions = frequent,
            FrequentCompanions = frequent,
            RareCompanions = rare,
            MissingCompanions = observations,
            UnstableOptionalCompanions = rare,
            RecommendedNextReadOnlyProbe = "Run the same bounded matrix over more maps and compare observed together sets."
        };
    }

    private static List<Edge> BuildEdges(List<FileRec> inv, List<RefEvidence> ev, Dictionary<string, string> byName, Metrics m, Caps c)
    {
        var ext = inv.ToDictionary(x => x.RelativePath, x => x.Extension, StringComparer.OrdinalIgnoreCase);
        var grp = ev.GroupBy(x => $"{x.SourceFile}|{Path.GetFileName(x.NormalizedTarget)}", StringComparer.OrdinalIgnoreCase);
        var outE = new List<Edge>();
        foreach (var g in grp.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var p = g.Key.Split('|');
            if (!byName.TryGetValue(p[1], out var to)) continue;
            var offs = g.Select(x => x.ByteOffset).Distinct().OrderBy(x => x).ToList();
            outE.Add(new Edge { From = p[0], To = to, EvidenceType = "string_reference", Confidence = "low", SourceExtension = ext.GetValueOrDefault(p[0], "").ToLowerInvariant(), TargetExtension = ext.GetValueOrDefault(to, "").ToLowerInvariant(), PrimaryReferenceOffset = offs.FirstOrDefault(), ReferenceOffsets = offs });
            if (outE.Count >= MaxEdges) { c.DependencyEdgesCapHit = true; break; }
        }
        return outE;
    }

    private static List<RefEvidence> ExtractRefs(List<StrTok> toks, string src, string mapName, Metrics m, Caps c, out List<UnresolvedReferenceRow> unresolved)
    {
        unresolved = new List<UnresolvedReferenceRow>();
        var rx = new Regex(@"([A-Za-z0-9_\-./\\]+\.[A-Za-z0-9]{1,8})", RegexOptions.Compiled);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outL = new List<RefEvidence>();
        foreach (var t in toks.OrderBy(x => x.Offset).ThenBy(x => x.Text, StringComparer.OrdinalIgnoreCase))
        {
            foreach (Match mm in rx.Matches(t.Text))
            {
                m.RefsSeen++;
                var tok = mm.Groups[1].Value.Trim().Replace('\\', '/');
                var ext = Path.GetExtension(tok);
                if (!RefExts.Contains(ext))
                {
                    m.RefsDiscarded++; m.NoisyRefsDiscarded++; m.DiscardReasons.Inc("unsupported_extension");
                    unresolved.Add(new UnresolvedReferenceRow { MapName = mapName, SourceFile = src, ExtractedToken = tok, NormalizedTarget = tok, TargetExtension = ext, Encoding = t.Encoding, ByteOffset = t.Offset + mm.Groups[1].Index * (t.Encoding == "utf16le" ? 2 : 1), EvidenceType = "string_reference", Confidence = "low", ReasonUnresolved = "unsupported_extension" });
                    continue;
                }
                var key = $"{src}|{t.Offset}|{tok}|{t.Encoding}";
                if (!set.Add(key)) { m.DuplicateRefsSuppressed++; m.RefsDiscarded++; m.DiscardReasons.Inc("duplicate"); continue; }
                outL.Add(new RefEvidence { ByteOffset = t.Offset + mm.Groups[1].Index * (t.Encoding == "utf16le" ? 2 : 1), Encoding = t.Encoding, TokenLength = tok.Length, SourceFile = src, ExtractedToken = tok, NormalizedTarget = tok, EvidenceType = "string_reference", Confidence = "low" });
                m.RefsKept++;
                if (outL.Count >= MaxRefs)
                {
                    c.RefsCapHit = true; m.DiscardReasons.Inc("cap_reached");
                    unresolved.Add(new UnresolvedReferenceRow { MapName = mapName, SourceFile = src, ExtractedToken = tok, NormalizedTarget = tok, TargetExtension = ext, Encoding = t.Encoding, ByteOffset = t.Offset + mm.Groups[1].Index * (t.Encoding == "utf16le" ? 2 : 1), EvidenceType = "string_reference", Confidence = "low", ReasonUnresolved = "discarded_by_cap" });
                    return outL;
                }
            }
        }
        return outL;
    }

    private static List<StrTok> ExtractStrings(byte[] d, bool utf16, Metrics m, Caps c)
    {
        var outL = new List<StrTok>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        int start = -1;
        void Flush()
        {
            if (sb.Length == 0) return;
            var s = sb.ToString().Trim();
            var off = start;
            sb.Clear();
            start = -1;
            if (s.Length < 4) { m.DiscardReasons.Inc("too_short"); if (utf16) m.Utf16StringsDiscarded++; else m.AsciiStringsDiscarded++; return; }
            if (s.Count(ch => !char.IsLetterOrDigit(ch) && ch != '.' && ch != '/' && ch != '\\' && ch != '_' && ch != '-') > s.Length / 2) { m.DiscardReasons.Inc("excessive_punctuation"); if (utf16) m.Utf16StringsDiscarded++; else m.AsciiStringsDiscarded++; return; }
            if (!seen.Add(s)) { m.DiscardReasons.Inc("duplicate"); if (utf16) m.Utf16StringsDiscarded++; else m.AsciiStringsDiscarded++; return; }
            outL.Add(new StrTok(s, off, utf16 ? "utf16le" : "ascii"));
            if (utf16) m.Utf16StringsKept++; else m.AsciiStringsKept++;
        }

        if (utf16)
        {
            for (int i = 0; i + 1 < d.Length; i += 2)
            {
                m.Utf16StringsSeen++;
                var lo = d[i]; var hi = d[i + 1];
                if (hi == 0 && lo >= 32 && lo <= 126) { if (sb.Length == 0) start = i; sb.Append((char)lo); }
                else Flush();
                if (outL.Count >= MaxStrings) { c.Utf16CapHit = true; m.DiscardReasons.Inc("cap_reached"); break; }
            }
            Flush();
        }
        else
        {
            for (int i = 0; i < d.Length; i++)
            {
                m.AsciiStringsSeen++;
                var b = d[i];
                if (b >= 32 && b <= 126) { if (sb.Length == 0) start = i; sb.Append((char)b); }
                else Flush();
                if (outL.Count >= MaxStrings) { c.AsciiCapHit = true; m.DiscardReasons.Inc("cap_reached"); break; }
            }
            Flush();
        }
        return outL.OrderBy(x => x.Offset).ThenBy(x => x.Text, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static OffsetSummary BuildOffsetSummary(List<RefEvidence> e)
    {
        var byFile = e.GroupBy(x => x.SourceFile, StringComparer.OrdinalIgnoreCase).Select(g => new KV { Key = g.Key, Value = g.Count() }).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList();
        var byExt = e.GroupBy(x => Path.GetExtension(x.NormalizedTarget), StringComparer.OrdinalIgnoreCase).Select(g => new KV { Key = g.Key, Value = g.Count() }).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList();
        var offs = e.Select(x => x.ByteOffset).ToList();
        return new OffsetSummary
        {
            RefsByFile = byFile,
            RefsByExtension = byExt,
            MinOffset = offs.Count == 0 ? 0 : offs.Min(),
            MaxOffset = offs.Count == 0 ? 0 : offs.Max(),
            RepeatedOffsets = e.GroupBy(x => x.ByteOffset).Where(g => g.Count() > 1).Select(g => g.Key).OrderBy(x => x).ToList(),
            RepeatedTokens = e.GroupBy(x => x.NormalizedTarget, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            SuspiciousClustersObservation = "Observation only: repeated offsets/tokens can indicate packed string tables."
        };
    }

    private static string Prefix(string fn, List<string> p)
    {
        var s = Path.GetFileNameWithoutExtension(fn);
        return p.OrderBy(x => x.Length).FirstOrDefault(x => s.StartsWith(x, StringComparison.OrdinalIgnoreCase)) ?? "unknown";
    }

    private static byte[] Read(string p, int m, out bool h)
    {
        using var f = File.OpenRead(p);
        h = f.Length > m;
        var l = (int)Math.Min(f.Length, m);
        var b = new byte[l];
        var r = f.Read(b, 0, l);
        return r == l ? b : b.Take(r).ToArray();
    }

    private static string ResolveOutputRoot(string? x)
    {
        if (!string.IsNullOrWhiteSpace(x)) return Path.GetFullPath(x);
        var a = Environment.GetEnvironmentVariable("ArtifactOutputRoot");
        return !string.IsNullOrWhiteSpace(a)
            ? Path.Combine(Path.GetFullPath(a), "reports", "rf_inventory")
            : Path.Combine(Environment.CurrentDirectory, "_runs", "reports", "rf_inventory");
    }

    private static string San(string p)
    {
        try
        {
            var c = Path.GetFullPath(Environment.CurrentDirectory);
            if (p.StartsWith(c, StringComparison.OrdinalIgnoreCase)) return Path.GetRelativePath(c, p).Replace('\\', '/');
        }
        catch { }
        return Path.GetFileName(p);
    }

    private static string ToHex(byte[] data) => string.Join(" ", data.Select(x => x.ToString("X2")));
    private static string HashBytes(byte[] data)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(data));
    }

    private static string HashFile(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs));
    }

    private sealed record Ctx(string Root, List<string> Prefixes, bool FromBsp);
    private sealed record StrTok(string Text, int Offset, string Encoding);
    private sealed record SignatureInfo(string First16BytesHex, string First64BytesHex, string First256BytesHash, string? WholeFileHash, string PrintablePrefixPreview, double ZeroByteRatioFirst256, double PrintableRatioFirst256, string SuspectedTextOrBinary);

    private sealed class Report { public string Tool { get; set; } = "rf_inventory"; public string Mode { get; set; } = "read_only"; public string InputMode { get; set; } = ""; public string InputPathSanitized { get; set; } = ""; public string RootDirectorySanitized { get; set; } = ""; public string MapOrPrefix { get; set; } = ""; public DateTime GeneratedUtc { get; set; } public List<FileRec> InventoryTable { get; set; } = new(); public List<RefEvidence> ReferenceEvidence { get; set; } = new(); public Graph DependencyGraph { get; set; } = new(); public OffsetSummary OffsetEvidenceSummary { get; set; } = new(); public SignatureGroupingSummary SignatureGroupingSummary { get; set; } = new(); public CompanionFileMatrixSummary CompanionFileMatrix { get; set; } = new(); public List<UnresolvedReferenceRow> UnresolvedReferenceAudit { get; set; } = new(); public UnresolvedReferenceAggregation UnresolvedReferenceAggregation { get; set; } = new(); public Metrics ExtractionMetrics { get; set; } = new(); public Caps CapHits { get; set; } = new(); }
    private sealed class FileRec { public string RelativePath { get; set; } = ""; public string Filename { get; set; } = ""; public string Extension { get; set; } = ""; public long Size { get; set; } public string SamePrefixGroup { get; set; } = ""; public List<string> ReferencedFilenames { get; set; } = new(); public long FileSize { get; set; } public string First16BytesHex { get; set; } = ""; public string First64BytesHex { get; set; } = ""; public string First256BytesHash { get; set; } = ""; public string? WholeFileHash { get; set; } public string PrintablePrefixPreview { get; set; } = ""; public double ZeroByteRatioFirst256 { get; set; } public double PrintableRatioFirst256 { get; set; } public string SuspectedTextOrBinary { get; set; } = ""; }
    private sealed class RefEvidence { public int ByteOffset { get; set; } public string Encoding { get; set; } = ""; public int TokenLength { get; set; } public string SourceFile { get; set; } = ""; public string ExtractedToken { get; set; } = ""; public string NormalizedTarget { get; set; } = ""; public string EvidenceType { get; set; } = "string_reference"; public string Confidence { get; set; } = "low"; }
    private sealed class Edge { public string From { get; set; } = ""; public string To { get; set; } = ""; public string EvidenceType { get; set; } = "string_reference"; public string Confidence { get; set; } = "low"; public string SourceExtension { get; set; } = ""; public string TargetExtension { get; set; } = ""; public int PrimaryReferenceOffset { get; set; } public List<int> ReferenceOffsets { get; set; } = new(); }
    private sealed class Graph { public List<Edge> Edges { get; set; } = new(); }
    private sealed class OffsetSummary { public List<KV> RefsByFile { get; set; } = new(); public List<KV> RefsByExtension { get; set; } = new(); public int MinOffset { get; set; } public int MaxOffset { get; set; } public List<int> RepeatedOffsets { get; set; } = new(); public List<string> RepeatedTokens { get; set; } = new(); public string SuspiciousClustersObservation { get; set; } = ""; }
    private sealed class SignatureGroupingSummary { public List<KV> ByExtension { get; set; } = new(); public List<KV> ByFirst4Bytes { get; set; } = new(); public List<KV> ByFirst16Bytes { get; set; } = new(); public List<KV> ByFileSizeBucket { get; set; } = new(); public List<KV> ByHashPrefix { get; set; } = new(); public string Observation { get; set; } = ""; }
    private sealed class CompanionMatrixRow { public string MapName { get; set; } = ""; public string SamePrefixGroup { get; set; } = ""; public string Extension { get; set; } = ""; public int FileCount { get; set; } public string EvidenceSource { get; set; } = ""; }
    private sealed class CompanionFileMatrixSummary { public List<CompanionMatrixRow> Rows { get; set; } = new(); public List<KV> CommonExtensionCombinations { get; set; } = new(); public List<string> MissingCompanionObservations { get; set; } = new(); public List<string> RareCompanionObservations { get; set; } = new(); public List<string> InconsistentCompanionObservations { get; set; } = new(); public List<string> UniversalCompanions { get; set; } = new(); public List<string> FrequentCompanions { get; set; } = new(); public List<string> RareCompanions { get; set; } = new(); public List<string> MissingCompanions { get; set; } = new(); public List<string> UnstableOptionalCompanions { get; set; } = new(); public string RecommendedNextReadOnlyProbe { get; set; } = ""; }
    private sealed class UnresolvedReferenceRow { public string MapName { get; set; } = ""; public string SourceFile { get; set; } = ""; public string ExtractedToken { get; set; } = ""; public string NormalizedTarget { get; set; } = ""; public string TargetExtension { get; set; } = ""; public string Encoding { get; set; } = ""; public int ByteOffset { get; set; } public string EvidenceType { get; set; } = ""; public string Confidence { get; set; } = ""; public string ReasonUnresolved { get; set; } = ""; }
    private sealed class UnresolvedReferenceAggregation { public List<KV> ByExtension { get; set; } = new(); public List<KV> BySourceExtension { get; set; } = new(); public List<KV> ByMap { get; set; } = new(); public List<KV> MostCommonMissingTargets { get; set; } = new(); public List<KV> SourceFilesMostUnresolved { get; set; } = new(); public string Observation { get; set; } = ""; }
    private sealed class KV { public string Key { get; set; } = ""; public int Value { get; set; } }
    private sealed class Metrics { public int AsciiStringsSeen { get; set; } public int AsciiStringsKept { get; set; } public int AsciiStringsDiscarded { get; set; } public int Utf16StringsSeen { get; set; } public int Utf16StringsKept { get; set; } public int Utf16StringsDiscarded { get; set; } public int RefsSeen { get; set; } public int RefsKept { get; set; } public int RefsDiscarded { get; set; } public int DuplicateRefsSuppressed { get; set; } public int NoisyRefsDiscarded { get; set; } public int DependencyEdgesEmitted { get; set; } public int DependencyEdgesSuppressed { get; set; } public Dictionary<string, int> DiscardReasons { get; set; } = new(StringComparer.OrdinalIgnoreCase); }
    private sealed class Caps { public bool FileReadCapHit { get; set; } public bool AsciiCapHit { get; set; } public bool Utf16CapHit { get; set; } public bool RefsCapHit { get; set; } public bool DependencyEdgesCapHit { get; set; } public bool ReportSizeCapHit { get; set; } public bool SiblingScanCapHit { get; set; } }
}

internal static class D { public static void Inc(this Dictionary<string, int> d, string k) { d[k] = d.TryGetValue(k, out var v) ? v + 1 : 1; } }
