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
    private const int ObserveMaxRead = 1024 * 1024;
    private const int ObserveMaxRegions = 160;
    private const int ObserveMaxSamples = 8;
    private const int ObserveMaxClusters = 512;

    public static void RunSelfTest() { }

    public static string Run(string input, string? outRoot, string? resourceRoot = null, string? approvedExternalResourceRoot = null)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new InvalidOperationException("rf_inventory requires input path");
        var full = Path.GetFullPath(input);
        if (!File.Exists(full) && !Directory.Exists(full)) throw new InvalidOperationException("rf_inventory input path not found");

        var ctx = BuildContext(full);
        var outDir = ResolveOutputRoot(outRoot);
        Directory.CreateDirectory(outDir);

        var report = BuildReport(ctx, full, resourceRoot, approvedExternalResourceRoot);
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

    public static string RunRfsinfoObserve(string inputFile, string? outRoot)
    {
        var explicitFile = ValidateObserveInput(inputFile);
        var outDir = ResolveObserveOutputRoot(outRoot);
        Directory.CreateDirectory(outDir);

        bool readCapHit;
        var bytes = Read(explicitFile, ObserveMaxRead, out readCapHit);
        var sig = BuildSignature(explicitFile, bytes, readCapHit);

        var metrics = new Metrics();
        var caps = new Caps { FileReadCapHit = readCapHit };
        var ascii = ExtractStrings(bytes, false, metrics, caps);
        var utf16 = ExtractStrings(bytes, true, metrics, caps);
        var regions = BuildObserveRegions(ascii, utf16, bytes.Length);
        var clusters = BuildObserveClusters(regions, bytes);
        if (clusters.Count > ObserveMaxClusters)
        {
            clusters = clusters
                .OrderByDescending(x => x.SupportCount)
                .ThenBy(x => x.RegionStartOffset)
                .Take(ObserveMaxClusters)
                .ToList();
            caps.ReportSizeCapHit = true;
        }

        var contradictions = BuildObserveContradictions(regions, clusters);
        var confidenceSummary = BuildObserveConfidenceSummary(regions, clusters, contradictions);

        var report = new ObserveReport
        {
            Tool = "rf_rfsinfo_observe",
            Mode = "read_only",
            ExactInputLabel = SanObservePath(explicitFile),
            GeneratedUtc = DateTime.UtcNow,
            CapsUsed = new ObserveCaps
            {
                MaxBytesRead = ObserveMaxRead,
                MaxRegions = ObserveMaxRegions,
                MaxSamplesPerRegion = ObserveMaxSamples,
                ReadCapped = readCapHit
            },
            HeaderFingerprints = new ObserveHeader
            {
                FileSize = new FileInfo(explicitFile).Length,
                First4BytesHex = string.Join(" ", sig.First16BytesHex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(4)),
                First8BytesHex = string.Join(" ", sig.First16BytesHex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(8)),
                First16BytesHex = sig.First16BytesHex,
                First32BytesHex = ToHex(bytes.Take(32).ToArray()),
                First64BytesHex = sig.First64BytesHex,
                First128BytesHash = HashBytes(bytes.Take(128).ToArray()),
                First256BytesHash = sig.First256BytesHash,
                ZeroByteRatioFirst256 = sig.ZeroByteRatioFirst256,
                AsciiVisibleRatioFirst256 = sig.PrintableRatioFirst256,
                SuspectedTextOrBinary = sig.SuspectedTextOrBinary
            },
            StringRegions = regions,
            CandidateClusters = clusters,
            OffsetAlignmentObservations = BuildObserveOffsetAlignment(regions, clusters),
            Contradictions = contradictions,
            ConfidenceSummary = confidenceSummary,
            UncertaintyNotes = new List<string>
            {
                "uncertainty note: observational analyzer only; no semantic interpretation.",
                "uncertainty note: candidate cluster relationships can include coincidental byte similarity."
            },
            ExplicitNoFullParserImplementation = "No full parser implementation occurred.",
            ExplicitNoExtraction = "No extraction occurred.",
            ExplicitNoMutation = "No mutation occurred.",
            ExplicitReadOnlyOnly = "Read-only only."
        };

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        if (Encoding.UTF8.GetByteCount(json) > MaxReport)
        {
            report.CapsUsed.ReportTruncated = true;
            report.StringRegions = report.StringRegions.Take(64).ToList();
            report.CandidateClusters = report.CandidateClusters.Take(128).ToList();
            report.Contradictions = report.Contradictions.Take(64).ToList();
            json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        }

        var outPath = Path.Combine(outDir, "rf_rfsinfo_observe_report.json");
        File.WriteAllText(outPath, json, Encoding.UTF8);
        return outPath;
    }

    private static string ValidateObserveInput(string inputFile)
    {
        if (string.IsNullOrWhiteSpace(inputFile))
            throw new InvalidOperationException("rf_rfsinfo_observe requires explicit file path");

        var full = Path.GetFullPath(inputFile);
        if (Directory.Exists(full))
            throw new InvalidOperationException("rf_rfsinfo_observe requires file input, directory is not allowed");
        if (!File.Exists(full))
            throw new InvalidOperationException("rf_rfsinfo_observe input file not found");
        if (!string.Equals(Path.GetExtension(full), ".dat", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("rf_rfsinfo_observe file extension must be .dat");
        if (!string.Equals(Path.GetFileName(full), "rfsinfo.dat", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("rf_rfsinfo_observe file name must be rfsinfo.dat");

        var attrs = File.GetAttributes(full);
        if ((attrs & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("rf_rfsinfo_observe rejected reparse path");

        return full;
    }

    private static string ResolveObserveOutputRoot(string? outRoot)
    {
        if (!string.IsNullOrWhiteSpace(outRoot))
            return Path.GetFullPath(outRoot);
        var a = Environment.GetEnvironmentVariable("ArtifactOutputRoot");
        return !string.IsNullOrWhiteSpace(a)
            ? Path.Combine(Path.GetFullPath(a), "reports", "rf_rfsinfo_observe")
            : Path.Combine(Environment.CurrentDirectory, "_runs", "reports", "rf_rfsinfo_observe");
    }

    private static string SanObservePath(string p) => San(p);

    private static List<ObserveRegion> BuildObserveRegions(List<StrTok> ascii, List<StrTok> utf16, int totalLen)
    {
        var outL = new List<ObserveRegion>();
        void Add(StrTok t)
        {
            var len = t.Encoding == "utf16le" ? t.Text.Length * 2 : t.Text.Length;
            var end = t.Offset + Math.Max(0, len - 1);
            outL.Add(new ObserveRegion
            {
                StartOffset = t.Offset,
                EndOffset = end,
                LengthBytes = len,
                Encoding = t.Encoding,
                TokenCountEstimate = Math.Max(1, t.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length),
                SampleTokens = t.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(ObserveMaxSamples).ToList(),
                RegionDensity = totalLen == 0 ? 0d : len / (double)totalLen
            });
        }
        foreach (var t in ascii.OrderBy(x => x.Offset).ThenBy(x => x.Text, StringComparer.OrdinalIgnoreCase)) Add(t);
        foreach (var t in utf16.OrderBy(x => x.Offset).ThenBy(x => x.Text, StringComparer.OrdinalIgnoreCase)) Add(t);
        return outL
            .OrderBy(x => x.StartOffset)
            .ThenBy(x => x.Encoding, StringComparer.OrdinalIgnoreCase)
            .Take(ObserveMaxRegions)
            .ToList();
    }

    private static List<ObserveCluster> BuildObserveClusters(List<ObserveRegion> regions, byte[] bytes)
    {
        var clusters = new List<ObserveCluster>();
        foreach (var r in regions)
        {
            var before = ReadWindow(bytes, r.StartOffset - 8, 8);
            var after = ReadWindow(bytes, r.EndOffset + 1, 8);
            var boundary = ReadWindow(bytes, r.StartOffset - 4, 16);
            clusters.Add(new ObserveCluster
            {
                RegionStartOffset = r.StartOffset,
                RegionEndOffset = r.EndOffset,
                ClusterType = "candidate_cluster_boundary",
                PatternHexOrHash = ToHex(boundary),
                SupportCount = 1,
                NearbyStringSamples = r.SampleTokens.Take(ObserveMaxSamples).ToList(),
                Confidence = "low_medium",
                UncertaintyNote = "uncertainty note: boundary bytes are observational only."
            });
            clusters.Add(new ObserveCluster
            {
                RegionStartOffset = r.StartOffset,
                RegionEndOffset = r.EndOffset,
                ClusterType = "candidate_cluster_before_after",
                PatternHexOrHash = $"{ToHex(before)}|{ToHex(after)}",
                SupportCount = 1,
                NearbyStringSamples = r.SampleTokens.Take(ObserveMaxSamples).ToList(),
                Confidence = "low",
                UncertaintyNote = "uncertainty note: before/after bytes can repeat by chance."
            });
        }

        var grouped = clusters
            .GroupBy(x => $"{x.ClusterType}|{x.PatternHexOrHash}", StringComparer.OrdinalIgnoreCase)
            .Select(g => new ObserveCluster
            {
                RegionStartOffset = g.Min(x => x.RegionStartOffset),
                RegionEndOffset = g.Max(x => x.RegionEndOffset),
                ClusterType = g.First().ClusterType,
                PatternHexOrHash = g.First().PatternHexOrHash,
                SupportCount = g.Count(),
                NearbyStringSamples = g.SelectMany(x => x.NearbyStringSamples).Distinct(StringComparer.OrdinalIgnoreCase).Take(ObserveMaxSamples).ToList(),
                Confidence = g.Count() >= 4 ? "medium" : g.Count() >= 2 ? "low_medium" : "low",
                UncertaintyNote = "uncertainty note: candidate cluster support is observational."
            })
            .OrderByDescending(x => x.SupportCount)
            .ThenBy(x => x.RegionStartOffset)
            .ToList();
        return grouped;
    }

    private static List<ObserveOffsetAlignment> BuildObserveOffsetAlignment(List<ObserveRegion> regions, List<ObserveCluster> clusters)
    {
        var outL = new List<ObserveOffsetAlignment>();
        foreach (var r in regions)
        {
            outL.Add(new ObserveOffsetAlignment
            {
                StartOffset = r.StartOffset,
                AlignmentBucket = $"mod16_{r.StartOffset % 16}",
                RelatedClusterCount = clusters.Count(x => x.RegionStartOffset <= r.EndOffset && x.RegionEndOffset >= r.StartOffset),
                Observation = "alignment observation"
            });
        }
        return outL.OrderBy(x => x.StartOffset).ToList();
    }

    private static List<string> BuildObserveContradictions(List<ObserveRegion> regions, List<ObserveCluster> clusters)
    {
        var outL = new List<string>();
        if (regions.Count == 0) outL.Add("no_string_regions_found");
        if (clusters.Count == 0) outL.Add("no_candidate_clusters_found");
        if (regions.Count > 0 && clusters.Count > 0)
        {
            var uncovered = regions.Count(r => !clusters.Any(c => c.RegionStartOffset <= r.EndOffset && c.RegionEndOffset >= r.StartOffset));
            if (uncovered > 0) outL.Add($"uncovered_regions={uncovered}");
        }
        return outL;
    }

    private static ObserveConfidenceSummary BuildObserveConfidenceSummary(List<ObserveRegion> regions, List<ObserveCluster> clusters, List<string> contradictions)
    {
        var high = clusters.Count(x => x.Confidence == "medium");
        var mid = clusters.Count(x => x.Confidence == "low_medium");
        var low = clusters.Count(x => x.Confidence == "low");
        var overall = contradictions.Count > Math.Max(3, regions.Count / 2) ? "low" : high >= 3 ? "medium" : "low_medium";
        return new ObserveConfidenceSummary
        {
            HighConfidenceClusterCount = high,
            MediumConfidenceClusterCount = mid,
            LowConfidenceClusterCount = low,
            Overall = overall
        };
    }

    private static byte[] ReadWindow(byte[] src, int start, int len)
    {
        if (src.Length == 0 || len <= 0) return Array.Empty<byte>();
        var s = Math.Max(0, start);
        if (s >= src.Length) return Array.Empty<byte>();
        var e = Math.Min(src.Length - 1, s + len - 1);
        var outL = new byte[e - s + 1];
        Buffer.BlockCopy(src, s, outL, 0, outL.Length);
        return outL;
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

    private static Report BuildReport(Ctx c, string raw, string? resourceRoot, string? approvedExternalResourceRoot)
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
        var suggestions = BuildCandidateRootSuggestions(c, unresolvedAudit);
        var candidateProbe = BuildCandidateProbe(resourceRoot, approvedExternalResourceRoot, unresolvedAudit);
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
            CandidateRootSuggestions = suggestions,
            CandidateResourceRootProbe = candidateProbe,
            ExtractionMetrics = m,
            CapHits = cap
        };
    }

    private static List<CandidateRootSuggestion> BuildCandidateRootSuggestions(Ctx c, List<UnresolvedReferenceRow> unresolved)
    {
        var knownMapNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "accretia","bellato","cora","sette","platform01"
        };
        var groups = new Dictionary<string, SuggestionAccumulator>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in unresolved)
        {
            var token = u.NormalizedTarget ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token)) continue;
            if (Regex.IsMatch(token, @"^[0-9.\-]+$")) continue;

            string? frag = null;
            string reason;
            string confidence;
            string category;
            string demotion = "";
            string probeRecommendation = "recommended";
            string notRecommendedReason = "";

            var norm = token.Replace('\\', '/').Trim();
            var slash = norm.IndexOf('/');
            if (slash > 0 && !norm.StartsWith("./", StringComparison.Ordinal))
            {
                frag = norm[..slash].ToLowerInvariant();
                reason = "observed from unresolved token path prefix";
                confidence = "medium";
                category = "path_prefix_resource_candidate";
            }
            else if (norm.StartsWith("./", StringComparison.Ordinal) && norm.Length > 2)
            {
                var tail = norm[2..];
                var s2 = tail.IndexOf('/');
                if (s2 > 0)
                {
                    frag = tail[..s2].ToLowerInvariant();
                    reason = "observed from unresolved token path prefix";
                    confidence = "medium";
                    category = "path_prefix_resource_candidate";
                }
                else
                {
                    var ext = Path.GetExtension(norm).ToLowerInvariant();
                    frag = ext switch { ".dds" or ".tga" => "texture", ".eff" => "effect", ".wav" or ".ogg" or ".snd" => "sound", _ => null };
                    reason = "observed from unresolved token extension-only guess";
                    confidence = "low";
                    category = "extension_only_guess";
                }
            }
            else
            {
                var ext = Path.GetExtension(norm).ToLowerInvariant();
                frag = ext switch { ".dds" or ".tga" => "texture", ".eff" => "effect", ".wav" or ".ogg" or ".snd" => "sound", _ => null };
                reason = "observed from unresolved token extension-only guess";
                confidence = "low";
                category = "extension_only_guess";
            }

            if (string.IsNullOrWhiteSpace(frag)) continue;
            if (Regex.IsMatch(frag, @"^[0-9.\-]+$") || frag is "." or "..")
            {
                continue;
            }
            var looksLikeFilename = Regex.IsMatch(frag, @"\.[A-Za-z0-9]{1,4}$");
            var cleanScore = ComputeCleanFragmentScore(frag, looksLikeFilename);
            if (frag.Equals("map", StringComparison.OrdinalIgnoreCase) || frag.Equals("ex", StringComparison.OrdinalIgnoreCase))
            {
                category = "noisy_or_numeric_rejected";
                confidence = "low";
                probeRecommendation = "not_recommended";
                notRecommendedReason = "generic fragment denied";
            }
            else if (frag.Length < 3)
            {
                category = "noisy_or_numeric_rejected";
                confidence = "low";
                probeRecommendation = "not_recommended";
                notRecommendedReason = "very short fragment rejected";
            }
            else if (frag.Contains(".dd.", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(frag, @"[^\w\-]$"))
            {
                category = "noisy_or_numeric_rejected";
                confidence = "low";
                probeRecommendation = "not_recommended";
                notRecommendedReason = "artifact-like or punctuation-ending fragment";
            }
            else if (looksLikeFilename)
            {
                category = "noisy_or_numeric_rejected";
                confidence = "low";
                probeRecommendation = "not_recommended";
                notRecommendedReason = "looks like filename not directory";
            }
            var mapName = u.MapName?.ToLowerInvariant() ?? "";
            if (string.Equals(frag, mapName, StringComparison.OrdinalIgnoreCase) || string.Equals(frag, Path.GetFileNameWithoutExtension(u.SourceFile), StringComparison.OrdinalIgnoreCase) || knownMapNames.Contains(frag))
            {
                category = "map_name_or_same_prefix_fragment";
                confidence = "low";
                demotion = "fragment matches map-name/same-prefix/known-map";
                probeRecommendation = "not_recommended";
                notRecommendedReason = "map-name/same-prefix fragment demoted";
            }
            if (!groups.TryGetValue(frag, out var acc))
            {
                acc = new SuggestionAccumulator();
                groups[frag] = acc;
            }
            acc.Count++;
            acc.SourceMaps.Add(u.MapName);
            acc.SourceExts.Add(Path.GetExtension(u.SourceFile));
            acc.TargetExts.Add(u.TargetExtension);
            if (acc.Examples.Count < 6) acc.Examples.Add(norm);
            acc.Confidence = MaxConfidence(acc.Confidence, confidence);
            acc.Reason = reason;
            acc.Category = category;
            if (!string.IsNullOrWhiteSpace(demotion)) acc.DemotionReason = demotion;
            if (probeRecommendation == "not_recommended") acc.ProbeRecommendation = "not_recommended";
            if (!string.IsNullOrWhiteSpace(notRecommendedReason)) acc.NotRecommendedReason = notRecommendedReason;
            acc.CleanFragmentScore = Math.Max(acc.CleanFragmentScore, cleanScore);
            acc.LooksLikeFilename = acc.LooksLikeFilename || looksLikeFilename;
        }

        var outList = new List<CandidateRootSuggestion>();
        foreach (var kv in groups.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var acc = kv.Value;
            var conf = acc.Confidence;
            if (acc.SourceMaps.Count >= 2 && conf == "medium" && acc.Category == "path_prefix_resource_candidate") conf = "high";
            outList.Add(new CandidateRootSuggestion
            {
                SuggestedRootFragment = kv.Key,
                EvidenceTokensCount = acc.Count,
                SourceMaps = acc.SourceMaps.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                SourceExtensions = acc.SourceExts.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                TargetExtensions = acc.TargetExts.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                ExampleTokens = acc.Examples.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                Confidence = conf,
                Reason = acc.Reason,
                Category = acc.Category,
                DemotionReason = acc.DemotionReason,
                ProbeRecommendation = acc.ProbeRecommendation,
                NotRecommendedReason = acc.NotRecommendedReason,
                CleanFragmentScore = acc.CleanFragmentScore,
                LooksLikeFilename = acc.LooksLikeFilename,
                RequiresExplicitApprovedRootProbe = true
            });
        }
        return outList;
    }

    private static int ComputeCleanFragmentScore(string frag, bool looksLikeFilename)
    {
        var score = 0;
        if (frag.Length >= 3) score += 2;
        if (Regex.IsMatch(frag, "^[a-zA-Z0-9_\\-]+$")) score += 2;
        if (!looksLikeFilename) score += 2;
        if (!frag.Equals("map", StringComparison.OrdinalIgnoreCase) && !frag.Equals("ex", StringComparison.OrdinalIgnoreCase)) score += 1;
        return score;
    }

    private static string MaxConfidence(string a, string b)
    {
        static int Score(string c) => c == "high" ? 3 : c == "medium" ? 2 : 1;
        return Score(a) >= Score(b) ? a : b;
    }

    private static CandidateResourceRootProbeSummary BuildCandidateProbe(string? resourceRoot, string? approvedExternalResourceRoot, List<UnresolvedReferenceRow> unresolved)
    {
        if (!string.IsNullOrWhiteSpace(approvedExternalResourceRoot))
        {
            return BuildCandidateProbeCore(approvedExternalResourceRoot, unresolved, allowExternal: true);
        }
        if (string.IsNullOrWhiteSpace(resourceRoot))
        {
            return new CandidateResourceRootProbeSummary { ResourceRootMode = "workspace_root", PolicyDecision = "no_root", SanitizedRootLabel = "none", Notes = "no explicit candidate resource root provided" };
        }
        return BuildCandidateProbeCore(resourceRoot, unresolved, allowExternal: false);
    }

    private static CandidateResourceRootProbeSummary BuildCandidateProbeCore(string rootArg, List<UnresolvedReferenceRow> unresolved, bool allowExternal)
    {
        string full;
        var isRelative = !Path.IsPathFullyQualified(rootArg);
        try { full = Path.GetFullPath(rootArg); } catch { return new CandidateResourceRootProbeSummary { ResourceRootMode = allowExternal ? "approved_external_candidate_root" : "workspace_root", PolicyDecision = "rejected_invalid_path", SanitizedRootLabel = "invalid", Notes = "sanitized path error" }; }
        var ws = Path.GetFullPath(Environment.CurrentDirectory);
        var mode = allowExternal ? "approved_external_candidate_root" : "workspace_root";
        if (!allowExternal && !full.StartsWith(ws, StringComparison.OrdinalIgnoreCase))
        {
            return new CandidateResourceRootProbeSummary { ResourceRootMode = "rejected_outside_workspace", PolicyDecision = "rejected_outside_workspace", SanitizedRootLabel = Path.GetFileName(full), IsRelativeInput = isRelative, Notes = "candidate root rejected by workspace policy" };
        }
        if (!Directory.Exists(full))
        {
            return new CandidateResourceRootProbeSummary { ResourceRootMode = mode, PolicyDecision = "rejected_missing", SanitizedRootLabel = Path.GetFileName(full), IsRelativeInput = isRelative, Missing = true, Notes = "candidate root missing" };
        }
        var di = new DirectoryInfo(full);
        if (di.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return new CandidateResourceRootProbeSummary { ResourceRootMode = mode, PolicyDecision = "rejected_reparse", SanitizedRootLabel = Path.GetFileName(full), IsRelativeInput = isRelative, ReparseRejected = true, Notes = "reparse/symlink rejected" };
        }
        const int cap = 2000;
        var files = Directory.GetFiles(full, "*", SearchOption.TopDirectoryOnly).Take(cap + 1).Select(Path.GetFileName).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var skipped = files.Count > cap;
        if (skipped) files = files.Take(cap).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matches = unresolved.Where(u => files.Contains(Path.GetFileName(u.NormalizedTarget))).ToList();
        return new CandidateResourceRootProbeSummary
        {
            ResourceRootMode = mode,
            PolicyDecision = "accepted",
            SanitizedRootLabel = Path.GetFileName(full),
            IsRelativeInput = isRelative,
            FilesConsidered = files.Count,
            MatchesFound = matches.Count,
            UnresolvedRefsResolvedAsCandidates = matches.Count,
            UnresolvedRefsStillMissing = Math.Max(0, unresolved.Count - matches.Count),
            SkippedDueToCaps = skipped ? 1 : 0,
            Notes = "candidate match; observed nearby resource candidate"
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

    private sealed class Report { public string Tool { get; set; } = "rf_inventory"; public string Mode { get; set; } = "read_only"; public string InputMode { get; set; } = ""; public string InputPathSanitized { get; set; } = ""; public string RootDirectorySanitized { get; set; } = ""; public string MapOrPrefix { get; set; } = ""; public DateTime GeneratedUtc { get; set; } public List<FileRec> InventoryTable { get; set; } = new(); public List<RefEvidence> ReferenceEvidence { get; set; } = new(); public Graph DependencyGraph { get; set; } = new(); public OffsetSummary OffsetEvidenceSummary { get; set; } = new(); public SignatureGroupingSummary SignatureGroupingSummary { get; set; } = new(); public CompanionFileMatrixSummary CompanionFileMatrix { get; set; } = new(); public List<UnresolvedReferenceRow> UnresolvedReferenceAudit { get; set; } = new(); public UnresolvedReferenceAggregation UnresolvedReferenceAggregation { get; set; } = new(); public List<CandidateRootSuggestion> CandidateRootSuggestions { get; set; } = new(); public CandidateResourceRootProbeSummary CandidateResourceRootProbe { get; set; } = new(); public Metrics ExtractionMetrics { get; set; } = new(); public Caps CapHits { get; set; } = new(); }
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
    private sealed class CandidateRootSuggestion { public string SuggestedRootFragment { get; set; } = ""; public int EvidenceTokensCount { get; set; } public List<string> SourceMaps { get; set; } = new(); public List<string> SourceExtensions { get; set; } = new(); public List<string> TargetExtensions { get; set; } = new(); public List<string> ExampleTokens { get; set; } = new(); public string Confidence { get; set; } = "low"; public string Reason { get; set; } = ""; public string Category { get; set; } = ""; public string DemotionReason { get; set; } = ""; public string ProbeRecommendation { get; set; } = "recommended"; public string NotRecommendedReason { get; set; } = ""; public int CleanFragmentScore { get; set; } public bool LooksLikeFilename { get; set; } public string NotProbedReason { get; set; } = ""; public bool RequiresExplicitApprovedRootProbe { get; set; } = true; }
    private sealed class SuggestionAccumulator { public int Count; public HashSet<string> SourceMaps { get; } = new(StringComparer.OrdinalIgnoreCase); public HashSet<string> SourceExts { get; } = new(StringComparer.OrdinalIgnoreCase); public HashSet<string> TargetExts { get; } = new(StringComparer.OrdinalIgnoreCase); public List<string> Examples { get; } = new(); public string Confidence { get; set; } = "low"; public string Reason { get; set; } = ""; public string Category { get; set; } = "extension_only_guess"; public string DemotionReason { get; set; } = ""; public string ProbeRecommendation { get; set; } = "recommended"; public string NotRecommendedReason { get; set; } = ""; public int CleanFragmentScore { get; set; } = 0; public bool LooksLikeFilename { get; set; } = false; }
    private sealed class CandidateResourceRootProbeSummary { public string ResourceRootMode { get; set; } = ""; public string PolicyDecision { get; set; } = ""; public string SanitizedRootLabel { get; set; } = ""; public bool IsRelativeInput { get; set; } public bool Missing { get; set; } public bool ReparseRejected { get; set; } public int FilesConsidered { get; set; } public int MatchesFound { get; set; } public int UnresolvedRefsResolvedAsCandidates { get; set; } public int UnresolvedRefsStillMissing { get; set; } public int SkippedDueToCaps { get; set; } public string Notes { get; set; } = ""; }
    private sealed class KV { public string Key { get; set; } = ""; public int Value { get; set; } }
    private sealed class Metrics { public int AsciiStringsSeen { get; set; } public int AsciiStringsKept { get; set; } public int AsciiStringsDiscarded { get; set; } public int Utf16StringsSeen { get; set; } public int Utf16StringsKept { get; set; } public int Utf16StringsDiscarded { get; set; } public int RefsSeen { get; set; } public int RefsKept { get; set; } public int RefsDiscarded { get; set; } public int DuplicateRefsSuppressed { get; set; } public int NoisyRefsDiscarded { get; set; } public int DependencyEdgesEmitted { get; set; } public int DependencyEdgesSuppressed { get; set; } public Dictionary<string, int> DiscardReasons { get; set; } = new(StringComparer.OrdinalIgnoreCase); }
    private sealed class Caps { public bool FileReadCapHit { get; set; } public bool AsciiCapHit { get; set; } public bool Utf16CapHit { get; set; } public bool RefsCapHit { get; set; } public bool DependencyEdgesCapHit { get; set; } public bool ReportSizeCapHit { get; set; } public bool SiblingScanCapHit { get; set; } }

    private sealed class ObserveReport
    {
        public string Tool { get; set; } = "rf_rfsinfo_observe";
        public string Mode { get; set; } = "read_only";
        public string ExactInputLabel { get; set; } = "";
        public DateTime GeneratedUtc { get; set; }
        public ObserveCaps CapsUsed { get; set; } = new();
        public ObserveHeader HeaderFingerprints { get; set; } = new();
        public List<ObserveRegion> StringRegions { get; set; } = new();
        public List<ObserveCluster> CandidateClusters { get; set; } = new();
        public List<ObserveOffsetAlignment> OffsetAlignmentObservations { get; set; } = new();
        public List<string> Contradictions { get; set; } = new();
        public ObserveConfidenceSummary ConfidenceSummary { get; set; } = new();
        public List<string> UncertaintyNotes { get; set; } = new();
        public string ExplicitNoFullParserImplementation { get; set; } = "No full parser implementation occurred.";
        public string ExplicitNoExtraction { get; set; } = "No extraction occurred.";
        public string ExplicitNoMutation { get; set; } = "No mutation occurred.";
        public string ExplicitReadOnlyOnly { get; set; } = "Read-only only.";
    }
    private sealed class ObserveCaps { public int MaxBytesRead { get; set; } public int MaxRegions { get; set; } public int MaxSamplesPerRegion { get; set; } public bool ReadCapped { get; set; } public bool ReportTruncated { get; set; } }
    private sealed class ObserveHeader
    {
        public long FileSize { get; set; }
        public string First4BytesHex { get; set; } = "";
        public string First8BytesHex { get; set; } = "";
        public string First16BytesHex { get; set; } = "";
        public string First32BytesHex { get; set; } = "";
        public string First64BytesHex { get; set; } = "";
        public string First128BytesHash { get; set; } = "";
        public string First256BytesHash { get; set; } = "";
        public double ZeroByteRatioFirst256 { get; set; }
        public double AsciiVisibleRatioFirst256 { get; set; }
        public string SuspectedTextOrBinary { get; set; } = "";
    }
    private sealed class ObserveRegion
    {
        public int StartOffset { get; set; }
        public int EndOffset { get; set; }
        public int LengthBytes { get; set; }
        public string Encoding { get; set; } = "";
        public int TokenCountEstimate { get; set; }
        public List<string> SampleTokens { get; set; } = new();
        public double RegionDensity { get; set; }
    }
    private sealed class ObserveCluster
    {
        public int RegionStartOffset { get; set; }
        public int RegionEndOffset { get; set; }
        public string ClusterType { get; set; } = "";
        public string PatternHexOrHash { get; set; } = "";
        public int SupportCount { get; set; }
        public List<string> NearbyStringSamples { get; set; } = new();
        public string Confidence { get; set; } = "low";
        public string UncertaintyNote { get; set; } = "";
    }
    private sealed class ObserveOffsetAlignment { public int StartOffset { get; set; } public string AlignmentBucket { get; set; } = ""; public int RelatedClusterCount { get; set; } public string Observation { get; set; } = ""; }
    private sealed class ObserveConfidenceSummary { public int HighConfidenceClusterCount { get; set; } public int MediumConfidenceClusterCount { get; set; } public int LowConfidenceClusterCount { get; set; } public string Overall { get; set; } = "low"; }
}

internal static class D { public static void Inc(this Dictionary<string, int> d, string k) { d[k] = d.TryGetValue(k, out var v) ? v + 1 : 1; } }
