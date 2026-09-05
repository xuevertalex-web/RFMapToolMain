using System;
using System.IO;
using System.Text;

namespace RFMapToolSharp.Export;

/// <summary>
/// Единая точка маршрутизации отладочного вывода (Этап «Часть Б»).
/// Результат экспорта (GLB) остаётся в ReadyMaps/&lt;map&gt;/,
/// диагностика уходит в ReadyMaps/_diagnostics/&lt;map&gt;/,
/// отчёты — в ReadyMaps/_reports/, логи — в ReadyMaps/_logs/.
/// </summary>
public static class DiagnosticsOutput
{
    /// <summary>Корень экспорта (ReadyMaps). Задаётся из Program перед циклом карт.</summary>
    public static string ExportRoot { get; set; } = Environment.CurrentDirectory;

    public static string DiagnosticDir(string mapName)
    {
        var dir = Path.Combine(ExportRoot, "_diagnostics", mapName);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string DiagnosticPath(string mapName, string fileName)
        => Path.Combine(DiagnosticDir(mapName), fileName);

    public static void WriteDiagnostic(string mapName, string fileName, string content)
        => File.WriteAllText(DiagnosticPath(mapName, fileName), content);

    public static string ReportDir()
    {
        var dir = Path.Combine(ExportRoot, "_reports");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string ReportPath(string fileName) => Path.Combine(ReportDir(), fileName);

    public static string LogDir()
    {
        var dir = Path.Combine(ExportRoot, "_logs");
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ===== Per-map console log (tee: консоль + файл) =====

    private sealed class TeeWriter : TextWriter
    {
        private readonly TextWriter _inner;
        private readonly StringWriter _buffer;
        public TeeWriter(TextWriter inner, StringWriter buffer) { _inner = inner; _buffer = buffer; }
        public override Encoding Encoding => _inner.Encoding;
        public override void Write(char value) { _inner.Write(value); _buffer.Write(value); }
        public override void Write(string? value) { _inner.Write(value); _buffer.Write(value); }
        public override void WriteLine(string? value) { _inner.WriteLine(value); _buffer.WriteLine(value); }
        public override void Flush() { _inner.Flush(); _buffer.Flush(); }
    }

    private static TextWriter? _originalOut;
    private static StringWriter? _mapBuffer;
    private static string? _logMapName;

    /// <summary>Начать запись лога карты: всё, что пишется в Console, дублируется в буфер.</summary>
    public static void BeginMapLog(string mapName)
    {
        EndMapLog(); // на случай незакрытого предыдущего
        _originalOut = Console.Out;
        _mapBuffer = new StringWriter();
        _logMapName = mapName;
        Console.SetOut(new TeeWriter(_originalOut, _mapBuffer));
    }

    /// <summary>Завершить запись и сохранить _logs/&lt;map&gt;_export_&lt;timestamp&gt;.log.</summary>
    public static void EndMapLog()
    {
        if (_originalOut == null) return;
        Console.SetOut(_originalOut);
        if (_mapBuffer != null && _logMapName != null)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var path = Path.Combine(LogDir(), $"{_logMapName}_export_{stamp}.log");
            File.WriteAllText(path, _mapBuffer.ToString());
            Console.WriteLine($"[LOG] Saved: {path}");
        }
        _originalOut = null;
        _mapBuffer?.Dispose();
        _mapBuffer = null;
        _logMapName = null;
    }

    // ===== Ручная чистка старых диагностических файлов (--cleanup-diagnostics) =====

    /// <summary>Известные имена debug-файлов, которые раньше писались рядом с GLB.</summary>
    private static readonly string[] LegacyPatterns =
    {
        "stretched_faces.json", "uv_anomaly_faces.json", "normal_anomaly_faces.json",
        "bsp_node_index.json", "mg_trace_89_92.json", "mg_face_trace_89_92_bspbuild.json",
        "mg91_face_rebuild_log.json", "spt_resolve_log.json", "broken_faces.json",
        "object_matrices.json", "animated_objects.json", "matgroup_debug.json",
        "texture_report.json",
        "build_trace_*.json", "mg_*_*.json", "vertex_pool_ranges.json",
        "face_plane_metrics_*.json", "transform_usage_*.json", "corner_order_signature_*.json",
        "triangle_quality_*.json", "uv_gradient_*.json", "face_neighbor_graph_*.json"
    };

    /// <summary>
    /// Удаляет legacy-диагностику из ReadyMaps/&lt;map&gt;/ ТОЛЬКО после явного подтверждения.
    /// В неинтерактивном режиме ничего не удаляет, только перечисляет.
    /// </summary>
    public static void CleanupLegacyDiagnostics(bool isInteractive, Func<string?> readLine)
    {
        if (!Directory.Exists(ExportRoot)) return;
        foreach (var mapDir in Directory.GetDirectories(ExportRoot))
        {
            var name = Path.GetFileName(mapDir);
            if (name.StartsWith("_")) continue; // пропускаем _diagnostics/_logs/_reports

            var victims = new System.Collections.Generic.List<string>();
            foreach (var pattern in LegacyPatterns)
                victims.AddRange(Directory.GetFiles(mapDir, pattern, SearchOption.TopDirectoryOnly));
            if (victims.Count == 0) continue;

            Console.WriteLine($"[CLEANUP] {name}: {victims.Count} legacy diagnostic file(s):");
            foreach (var v in victims) Console.WriteLine("   " + Path.GetFileName(v));

            bool ok = false;
            if (isInteractive)
            {
                Console.Write($"[CLEANUP] Delete them from '{name}'? [y/N]: ");
                ok = string.Equals(readLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Console.WriteLine("[CLEANUP] Non-interactive mode: skipped (no files deleted).");
            }

            if (ok)
            {
                foreach (var v in victims) File.Delete(v);
                Console.WriteLine($"[CLEANUP] {name}: deleted {victims.Count} file(s).");
            }
        }
    }
}
