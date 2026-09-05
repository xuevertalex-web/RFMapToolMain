using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RFMapToolSharp.Export;

/// <summary>
/// Диагностика текстурного конвейера одной карты:
/// стадия [TEX] — загрузка .r3t (Program), стадия [CONV] — DDS→PNG (TextureConverter),
/// стадия [GLTF] — привязка к материалам (GltfExporter).
/// В конце экспорта пишет texture_report.json (см. Этап 3.1 проектного плана).
/// </summary>
public sealed class TextureDiagnostics
{
    private static TextureDiagnostics _current = new();
    public static TextureDiagnostics Current => _current;

    public static void Reset(string mapName) => _current = new TextureDiagnostics { MapName = mapName };

    public string MapName { get; private set; } = string.Empty;

    // --- Стадия TEX: источник текстур ---
    public string? R3tPath { get; private set; }
    public bool R3tExists { get; private set; }
    public long R3tSizeBytes { get; private set; }
    public DateTime? R3tLastWriteUtc { get; private set; }
    public int R3tTextureCount { get; private set; }

    public sealed class TexRecord
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public long DdsBytes { get; set; }
        public string Format { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public long PngBytes { get; set; }
        /// <summary>ok | convert_failed | not_used</summary>
        public string Status { get; set; } = "not_used";
        public string? Error { get; set; }
    }

    public sealed class MatRecord
    {
        public int MatId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Surface { get; set; }
        public int TexId { get; set; }
        public string TextureName { get; set; } = string.Empty;
        public bool TextureAssigned { get; set; }
        /// <summary>ok | no_layers | bad_surface | tex_out_of_range | convert_failed</summary>
        public string Status { get; set; } = "ok";
    }

    public List<TexRecord> Textures { get; } = new();
    public List<MatRecord> Materials { get; } = new();

    // ===== TEX =====

    public void LogR3tSource(string mapName, string candidatePath, bool exists)
    {
        R3tPath = Path.GetFullPath(candidatePath);
        R3tExists = exists;
        if (exists)
        {
            var fi = new FileInfo(candidatePath);
            R3tSizeBytes = fi.Length;
            R3tLastWriteUtc = fi.LastWriteTimeUtc;
        }

        Console.WriteLine(exists
            ? $"[TEX] Map={mapName} Path=\"{R3tPath}\" Exists=YES Size={FormatBytes(R3tSizeBytes)} MTime={R3tLastWriteUtc:yyyy-MM-dd HH:mm:ss}Z"
            : $"[TEX] Map={mapName} Path=\"{R3tPath}\" Exists=NO [MISSING]");
    }

    public void RegisterTextureEntries(IReadOnlyList<(string Name, long Size)> entries)
    {
        R3tTextureCount = entries.Count;
        for (int i = 0; i < entries.Count; i++)
        {
            Textures.Add(new TexRecord { Index = i, Name = entries[i].Name, DdsBytes = entries[i].Size });
            Console.WriteLine($"[TEX] Map={MapName} Tex[{i}]=\"{entries[i].Name}\" DdsSize={FormatBytes(entries[i].Size)}");
        }
    }

    // ===== CONV =====

    public void LogConversion(int index, string name, string format, int srcW, int srcH, long pngBytes)
    {
        var rec = Textures.FirstOrDefault(t => t.Index == index);
        if (rec == null)
        {
            rec = new TexRecord { Index = index, Name = name };
            Textures.Add(rec);
        }
        rec.Format = format;
        rec.Width = srcW;
        rec.Height = srcH;
        rec.PngBytes = pngBytes;
        rec.Status = "ok";
        Console.WriteLine($"[CONV] {name} {format} {srcW}x{srcH} -> PNG {FormatBytes(pngBytes)} [OK]");
    }

    public void LogConversionFailed(int index, string name, string format, string error)
    {
        var rec = Textures.FirstOrDefault(t => t.Index == index);
        if (rec == null)
        {
            rec = new TexRecord { Index = index, Name = name };
            Textures.Add(rec);
        }
        rec.Format = format;
        rec.Status = "convert_failed";
        rec.Error = error;
        Console.WriteLine($"[CONV] {name} {format} -> FAILED: {error}");
    }

    // ===== GLTF =====

    public void LogMaterial(MatRecord rec)
    {
        Materials.Add(rec);
        if (rec.TextureAssigned)
        {
            Console.WriteLine($"[GLTF] Mat[{rec.MatId}]={rec.Name} Surface={rec.Surface} TexId={rec.TexId} Texture=\"{rec.TextureName}\" Embedded=YES UV=TEXCOORD_0 [OK]");
        }
        else
        {
            Console.WriteLine($"[GLTF] Mat[{rec.MatId}]={rec.Name} Surface={rec.Surface} TexId={rec.TexId} Texture=NOT_ASSIGNED [{rec.Status.ToUpperInvariant()}]");
        }
    }

    // ===== Report =====

    /// <summary>Пишет texture_report в указанный файл (маршрутизация через DiagnosticsOutput).</summary>
    public void WriteReport(string outputFilePath)
    {
        var failed = new List<object>();
        foreach (var t in Textures)
        {
            if (t.Status == "convert_failed")
                failed.Add(new { name = t.Name, stage = "convert", error = t.Error ?? "unknown" });
        }
        foreach (var m in Materials)
        {
            if (!m.TextureAssigned && (m.Status == "tex_out_of_range"))
                failed.Add(new { name = m.Name, stage = "not_found", error = $"Surface={m.Surface} -> texId={m.TexId} out of range (textures={R3tTextureCount})" });
        }

        var report = new
        {
            map = MapName,
            r3t = new
            {
                path = R3tPath,
                exists = R3tExists,
                sizeBytes = R3tSizeBytes,
                lastWriteUtc = R3tLastWriteUtc,
                textureCount = R3tTextureCount
            },
            textures = new
            {
                total = R3tTextureCount,
                found = R3tExists ? R3tTextureCount : 0,
                converted = Textures.Count(t => t.Status == "ok"),
                embedded = Textures.Count(t => t.Status == "ok"),
                failed
            },
            materials = new
            {
                total = Materials.Count,
                with_textures = Materials.Count(m => m.TextureAssigned),
                missing_textures = Materials.Where(m => !m.TextureAssigned).Select(m => string.IsNullOrEmpty(m.Name) ? $"mat_{m.MatId}" : m.Name).ToList()
            }
        };

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        });
        File.WriteAllText(outputFilePath, json);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_000_000) return $"{bytes / 1_000_000.0:0.0}MB";
        if (bytes >= 1_000) return $"{bytes / 1_000.0:0.0}KB";
        return $"{bytes}B";
    }
}
