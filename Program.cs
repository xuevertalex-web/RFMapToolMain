using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using RFMapToolSharp.Textures;
using RFMapToolSharp;
using RFMapToolSharp.Export;
using RFMapToolSharp.Models;     // MapScene, MapTexture, MapMaterial*
using RFMapToolSharp.Collision;  // BspFile (СЃС‚СЂСѓРєС‚СѓСЂР° РґР»СЏ СЃС†РµРЅС‹)
using RFMapToolSharp.Materials;
using RFMapToolSharp.Editor;
using RFMapToolSharp.Parsing.Entity;
using RFMapToolSharp.Tools;
using System.Text.Json;
using System.Security.Cryptography;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using SharpGLTF.Schema2;
using SysBuffer = System.Buffer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

class Program
{
    private const string ConfigFile = "rf_path.txt";
    private static bool IsInteractive => !Console.IsInputRedirected;

    static void Main(string[] args)
    {
        bool noObjectTransform = args.Any(a => string.Equals(a, "--no-object-transform", StringComparison.OrdinalIgnoreCase));
        bool strictLegacyObjectTransform = args.Any(a => string.Equals(a, "--strict-legacy-object-transform", StringComparison.OrdinalIgnoreCase));
        float objectFrame = 0f;
        int objectTransformMode = 0;
        int objectTranslationMode = 0;
        int animatedObjectsMode = 0;
        int objectTransformTarget = 0;
        int decompressMode = 0;
        bool forceObjectTransform = args.Any(a => string.Equals(a, "--force-object-transform", StringComparison.OrdinalIgnoreCase));
        string? mapFilterArg = null;
        string? editorTemplateArg = null;
        bool editorDryRun = args.Any(a => string.Equals(a, "--editor-dry-run", StringComparison.OrdinalIgnoreCase));
        bool entityReport = args.Any(a => string.Equals(a, "--entity-report", StringComparison.OrdinalIgnoreCase));
        bool repackMode = args.Any(a => string.Equals(a, "--repack-map", StringComparison.OrdinalIgnoreCase));
        string? repackMapArg = null;
        string? repackOutArg = null;
        string? repackBspArg = null;
        string? bspDumpMapArg = null;
        string? bspDumpOutArg = null;
        string? bspApplySrcArg = null;
        string? bspApplyPatchArg = null;
        string? bspApplyOutArg = null;
        string? glbToBspMapArg = null;
        string? glbToBspGlbArg = null;
        string? glbToBspOutArg = null;
        string? glbInsertMapArg = null;
        string? glbInsertGlbArg = null;
        string? glbInsertOutArg = null;
        int glbInsertMgId = 0;
        string? glbInsertScanGlbArg = null;
        string? glbToBspRepackOutArg = null;
        string? glbToRfMapArg = null;
        string? glbToRfGlbArg = null;
        string? glbToRfOutArg = null;
        string? glbNonBspToSptMapArg = null;
        string? glbNonBspToSptGlbArg = null;
        string? glbNonBspToSptOutArg = null;
        string? glbToRfTextureOverridesArg = null;
        bool glbToRfApplyTextures = args.Any(a => string.Equals(a, "--glb-to-rf-apply-textures", StringComparison.OrdinalIgnoreCase));
        float glbToRfMaxDelta = 80f;
        string? validateMapDirArg = null;
        string? bspCenterMarkerMapArg = null;
        string? bspCenterMarkerOutArg = null;
        float bspCenterMarkerRadius = 250f;
        float bspCenterMarkerHeight = 350f;
        int bspCenterMarkerMaxVerts = 1200;
        string? rfInventoryInputArg = null;
        string? rfInventoryOutArg = null;
        string? rfInventoryResourceRootArg = null;
        string? rfInventoryApprovedResourceRootArg = null;
        string? rfRfsinfoObserveArg = null;
        string? rfRfsinfoObserveOutArg = null;
        bool rfInventorySelfTest = args.Any(a => string.Equals(a, "--rf-inventory-selftest", StringComparison.OrdinalIgnoreCase));
        bool setteCleanIsolated = args.Any(a => string.Equals(a, "--sette-clean-isolated", StringComparison.OrdinalIgnoreCase));
        bool setteRaw = args.Any(a => string.Equals(a, "--sette-raw", StringComparison.OrdinalIgnoreCase));
        string sptMode = "markers";
        bool sptPivotFix = true;
        string sptRotOrder = "XYZ";
        float sptScaleMultiplier = 1.0f;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--bsp-dump", StringComparison.OrdinalIgnoreCase))
            {
                bspDumpMapArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--bsp-dump-out", StringComparison.OrdinalIgnoreCase))
            {
                bspDumpOutArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--bsp-apply", StringComparison.OrdinalIgnoreCase))
            {
                bspApplySrcArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--bsp-patch", StringComparison.OrdinalIgnoreCase))
            {
                bspApplyPatchArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--bsp-out", StringComparison.OrdinalIgnoreCase))
            {
                bspApplyOutArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-insert-scan-glb", StringComparison.OrdinalIgnoreCase))
            {
                glbInsertScanGlbArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-insert-map", StringComparison.OrdinalIgnoreCase))
            {
                glbInsertMapArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-insert-glb", StringComparison.OrdinalIgnoreCase))
            {
                glbInsertGlbArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-insert-out", StringComparison.OrdinalIgnoreCase))
            {
                glbInsertOutArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-insert-mgid", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(args[i + 1], out glbInsertMgId);
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-to-bsp-map", StringComparison.OrdinalIgnoreCase))
            {
                glbToBspMapArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-to-bsp-glb", StringComparison.OrdinalIgnoreCase))
            {
                glbToBspGlbArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-to-bsp-out", StringComparison.OrdinalIgnoreCase))
            {
                glbToBspOutArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-to-bsp-repack-out", StringComparison.OrdinalIgnoreCase))
            {
                glbToBspRepackOutArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-nonbsp-to-spt-map", StringComparison.OrdinalIgnoreCase))
            {
                glbNonBspToSptMapArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-nonbsp-to-spt-glb", StringComparison.OrdinalIgnoreCase))
            {
                glbNonBspToSptGlbArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-nonbsp-to-spt-out", StringComparison.OrdinalIgnoreCase))
            {
                glbNonBspToSptOutArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-to-rf-map", StringComparison.OrdinalIgnoreCase))
            {
                glbToRfMapArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-to-rf-glb", StringComparison.OrdinalIgnoreCase))
            {
                glbToRfGlbArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-to-rf-out", StringComparison.OrdinalIgnoreCase))
            {
                glbToRfOutArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-to-rf-texture-overrides", StringComparison.OrdinalIgnoreCase))
            {
                glbToRfTextureOverridesArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--glb-to-rf-max-delta", StringComparison.OrdinalIgnoreCase))
            {
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out glbToRfMaxDelta);
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--validate-map-dir", StringComparison.OrdinalIgnoreCase))
            {
                validateMapDirArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--bsp-center-marker-map", StringComparison.OrdinalIgnoreCase))
            {
                bspCenterMarkerMapArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--bsp-center-marker-out", StringComparison.OrdinalIgnoreCase))
            {
                bspCenterMarkerOutArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--bsp-center-marker-radius", StringComparison.OrdinalIgnoreCase))
            {
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bspCenterMarkerRadius);
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--bsp-center-marker-height", StringComparison.OrdinalIgnoreCase))
            {
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bspCenterMarkerHeight);
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--bsp-center-marker-maxverts", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(args[i + 1], out bspCenterMarkerMaxVerts);
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--rf-inventory", StringComparison.OrdinalIgnoreCase))
            {
                rfInventoryInputArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--rf-inventory-out", StringComparison.OrdinalIgnoreCase))
            {
                rfInventoryOutArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--rf-inventory-resource-root", StringComparison.OrdinalIgnoreCase))
            {
                rfInventoryResourceRootArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--rf-inventory-approved-resource-root", StringComparison.OrdinalIgnoreCase))
            {
                rfInventoryApprovedResourceRootArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--rf-rfsinfo-observe", StringComparison.OrdinalIgnoreCase))
            {
                rfRfsinfoObserveArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--rf-rfsinfo-observe-out", StringComparison.OrdinalIgnoreCase))
            {
                rfRfsinfoObserveOutArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--repack-map", StringComparison.OrdinalIgnoreCase))
            {
                repackMapArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--repack-out", StringComparison.OrdinalIgnoreCase))
            {
                repackOutArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--repack-bsp", StringComparison.OrdinalIgnoreCase))
            {
                repackBspArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--frame", StringComparison.OrdinalIgnoreCase))
            {
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out objectFrame);
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--object-transform-mode", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(args[i + 1], out objectTransformMode);
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--object-translation-mode", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(args[i + 1], out objectTranslationMode);
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--animated-objects-mode", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(args[i + 1], out animatedObjectsMode);
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--object-transform-target", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(args[i + 1], out objectTransformTarget);
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--decompress-mode", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(args[i + 1], out decompressMode);
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--map", StringComparison.OrdinalIgnoreCase))
            {
                mapFilterArg = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--spt-mode", StringComparison.OrdinalIgnoreCase))
            {
                sptMode = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--editor-template", StringComparison.OrdinalIgnoreCase))
            {
                editorTemplateArg = args[i + 1];
                break;
            }
        }
        sptPivotFix = !args.Any(a => string.Equals(a, "--no-spt-pivot-fix", StringComparison.OrdinalIgnoreCase));
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--spt-rot-order", StringComparison.OrdinalIgnoreCase))
            {
                sptRotOrder = args[i + 1];
                break;
            }
        }
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--spt-scale-mul", StringComparison.OrdinalIgnoreCase))
            {
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sptScaleMultiplier);
                break;
            }
        }
        RFMapToolSharp.Collision.BspFile.DisableObjectTransform = noObjectTransform;
        RFMapToolSharp.Collision.BspFile.ObjectTransformFrame = objectFrame;
        RFMapToolSharp.Collision.BspFile.StrictLegacyObjectTransform = strictLegacyObjectTransform;
        RFMapToolSharp.Collision.BspFile.ObjectTransformMode = objectTransformMode;
        RFMapToolSharp.Collision.BspFile.ObjectTranslationMode = objectTranslationMode;
        RFMapToolSharp.Collision.BspFile.AnimatedObjectsMode = animatedObjectsMode;
        RFMapToolSharp.Collision.BspFile.ObjectTransformTarget = objectTransformTarget;
        RFMapToolSharp.Collision.BspFile.DecompressMode = decompressMode;
        if (forceObjectTransform) RFMapToolSharp.Collision.BspFile.ObjectTransformTarget = 99;
        GltfExporter.SptOptions.Mode = sptMode;
        GltfExporter.SptOptions.PivotFix = sptPivotFix;
        GltfExporter.SptOptions.RotationOrder = sptRotOrder;
        GltfExporter.SptOptions.ScaleMultiplier = sptScaleMultiplier;
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== RF ONLINE MAP BATCH CONVERTER ===\n");
        if (noObjectTransform) Console.WriteLine("[INFO] Object transforms disabled (--no-object-transform).\n");
        if (strictLegacyObjectTransform) Console.WriteLine("[INFO] Strict legacy object transforms enabled (--strict-legacy-object-transform).\n");
        Console.WriteLine($"[INFO] Object transform frame: {objectFrame.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n");
        Console.WriteLine($"[INFO] Object transform mode: {objectTransformMode}\n");
        Console.WriteLine($"[INFO] Object translation mode: {objectTranslationMode}\n");
        Console.WriteLine($"[INFO] Animated objects mode: {animatedObjectsMode}\n");
        Console.WriteLine($"[INFO] Object transform target: {objectTransformTarget}\n");
        Console.WriteLine($"[INFO] Decompress mode: {decompressMode}\n");
        if (forceObjectTransform) Console.WriteLine("[INFO] Force object transform enabled (--force-object-transform)\n");
        Console.WriteLine($"[INFO] SPT mode: {sptMode}\n");
        Console.WriteLine($"[INFO] SPT pivot fix: {sptPivotFix}\n");
        Console.WriteLine($"[INFO] SPT rotation order: {sptRotOrder}\n");
        Console.WriteLine($"[INFO] SPT scale multiplier: {sptScaleMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n");
        Console.ResetColor();

        if (rfInventorySelfTest)
        {
            RfInventoryTool.RunSelfTest();
            Console.WriteLine("[RF-INVENTORY][SELFTEST] PASS");
            return;
        }

        if (!string.IsNullOrWhiteSpace(rfInventoryInputArg))
        {
            var outPath = RfInventoryTool.Run(rfInventoryInputArg, rfInventoryOutArg, rfInventoryResourceRootArg, rfInventoryApprovedResourceRootArg);
            Console.WriteLine($"[RF-INVENTORY] Mode: read-only");
            Console.WriteLine($"[RF-INVENTORY] Report: {outPath}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(rfRfsinfoObserveArg))
        {
            var outPath = RfInventoryTool.RunRfsinfoObserve(rfRfsinfoObserveArg, rfRfsinfoObserveOutArg);
            Console.WriteLine($"[RF-RFSINFO-OBSERVE] Mode: read-only");
            Console.WriteLine($"[RF-RFSINFO-OBSERVE] Report: {outPath}");
            return;
        }

        if (editorDryRun)
        {
            if (string.IsNullOrWhiteSpace(editorTemplateArg) || !File.Exists(editorTemplateArg))
            {
                Console.WriteLine("ERROR: --editor-dry-run requires --editor-template <path-to-json>");
                return;
            }

            var t = EditorGenerator.LoadTemplate(editorTemplateArg);
            var objs = EditorGenerator.Generate(t);
            var outPath = Path.Combine(Environment.CurrentDirectory, "editor_plan.json");
            EditorGenerator.SavePlan(outPath, t, objs);
            Console.WriteLine($"[EDITOR] Dry-run complete. Objects: {objs.Count}");
            Console.WriteLine($"[EDITOR] Plan saved: {outPath}");
            return;
        }

        if (entityReport)
        {
            string entityDir = Path.Combine(Environment.CurrentDirectory, "map", "Entity");
            if (!Directory.Exists(entityDir)) entityDir = Path.Combine(Environment.CurrentDirectory, "Map", "Entity");
            if (!Directory.Exists(entityDir))
            {
                Console.WriteLine("ERROR: Entity folder not found.");
                return;
            }
            string outPath = Path.Combine(Environment.CurrentDirectory, "RF_Release", "Entity", "entity_rpk_report.json");
            string idxPath = Path.Combine(Environment.CurrentDirectory, "RF_Release", "Entity", "entity_rpk_index.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            RpkInspector.WriteEntityReport(entityDir, outPath);
            RpkInspector.WriteEntityIndexReport(entityDir, idxPath);
            Console.WriteLine($"[ENTITY] Report saved: {outPath}");
            Console.WriteLine($"[ENTITY] Index saved: {idxPath}");
            return;
        }

        if (repackMode)
        {
            string? mr = FindMapRoot();
            if (mr == null)
            {
                Console.WriteLine("ERROR: Map folder not found.");
                return;
            }
            if (string.IsNullOrWhiteSpace(repackMapArg))
            {
                Console.WriteLine("ERROR: --repack-map requires map name.");
                return;
            }

            var srcMapDir = Directory.GetDirectories(mr)
                .FirstOrDefault(d => string.Equals(Path.GetFileName(d), repackMapArg, StringComparison.OrdinalIgnoreCase));
            if (srcMapDir == null)
            {
                Console.WriteLine($"ERROR: map '{repackMapArg}' not found in {mr}");
                return;
            }

            var rootOut = string.IsNullOrWhiteSpace(repackOutArg)
                ? Path.Combine(Environment.CurrentDirectory, "RF_Repack")
                : Path.GetFullPath(repackOutArg);
            var outMapRoot = Path.Combine(rootOut, "map");
            var outMapDir = Path.Combine(outMapRoot, Path.GetFileName(srcMapDir));
            Directory.CreateDirectory(outMapRoot);
            CopyDirectoryRecursive(srcMapDir, outMapDir);

            var sourceBsp = FindBspFile(srcMapDir);
            if (sourceBsp == null)
            {
                Console.WriteLine("ERROR: source BSP not found.");
                return;
            }
            var finalBsp = string.IsNullOrWhiteSpace(repackBspArg) ? sourceBsp : Path.GetFullPath(repackBspArg);
            if (!File.Exists(finalBsp))
            {
                Console.WriteLine($"ERROR: repack BSP not found: {finalBsp}");
                return;
            }

            var outBsp = FindBspFile(outMapDir);
            if (outBsp == null)
            {
                outBsp = Path.Combine(outMapDir, Path.GetFileName(finalBsp));
            }
            File.Copy(finalBsp, outBsp, true);

            Console.WriteLine("[REPACK] Completed.");
            Console.WriteLine($"[REPACK] Source map: {srcMapDir}");
            Console.WriteLine($"[REPACK] Output map: {outMapDir}");
            Console.WriteLine($"[REPACK] BSP used: {finalBsp}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(bspDumpMapArg))
        {
            string? mr = FindMapRoot();
            if (mr == null) { Console.WriteLine("ERROR: Map folder not found."); return; }
            var srcMapDir = Directory.GetDirectories(mr)
                .FirstOrDefault(d => string.Equals(Path.GetFileName(d), bspDumpMapArg, StringComparison.OrdinalIgnoreCase));
            if (srcMapDir == null) { Console.WriteLine($"ERROR: map '{bspDumpMapArg}' not found."); return; }
            var srcBsp = FindBspFile(srcMapDir);
            if (srcBsp == null) { Console.WriteLine("ERROR: BSP not found."); return; }

            var bsp = RFMapToolSharp.Collision.BspFile.Load(srcBsp);
            string outPath = string.IsNullOrWhiteSpace(bspDumpOutArg)
                ? Path.Combine(Environment.CurrentDirectory, $"bsp_dump_{Path.GetFileName(srcMapDir)}.json")
                : Path.GetFullPath(bspDumpOutArg);

            var payload = new
            {
                map = Path.GetFileName(srcMapDir),
                bsp_path = srcBsp,
                bsp_sha256 = Sha256File(srcBsp),
                fvertex_count = bsp.FVertices.Count,
                fvertices = bsp.FVertices.Select((v, i) => new { vid = i, x = v.X, y = v.Y, z = v.Z }).ToArray()
            };
            File.WriteAllText(outPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"[BSP-DUMP] Saved: {outPath}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(bspApplySrcArg))
        {
            if (string.IsNullOrWhiteSpace(bspApplyPatchArg) || string.IsNullOrWhiteSpace(bspApplyOutArg))
            {
                Console.WriteLine("ERROR: --bsp-apply requires --bsp-patch <json> and --bsp-out <path>");
                return;
            }
            var srcBsp = Path.GetFullPath(bspApplySrcArg);
            var patchJson = Path.GetFullPath(bspApplyPatchArg);
            var outBsp = Path.GetFullPath(bspApplyOutArg);
            if (!File.Exists(srcBsp) || !File.Exists(patchJson))
            {
                Console.WriteLine("ERROR: source bsp or patch json not found.");
                return;
            }

            var bsp = RFMapToolSharp.Collision.BspFile.Load(srcBsp);
            using var doc = JsonDocument.Parse(File.ReadAllText(patchJson));
            var root = doc.RootElement;
            if (!root.TryGetProperty("fvertices", out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                Console.WriteLine("ERROR: patch json must contain 'fvertices' array.");
                return;
            }

            var bytes = File.ReadAllBytes(srcBsp);
            int fCount = bsp.FVertices.Count;
            int expectedBytes = fCount * 12;
            if ((int)bsp.Header.FVertex.Size < expectedBytes)
            {
                Console.WriteLine("ERROR: invalid FVertex block size.");
                return;
            }

            foreach (var item in arr.EnumerateArray())
            {
                int vid = item.GetProperty("vid").GetInt32();
                if (vid < 0 || vid >= fCount) continue;
                float x = item.GetProperty("x").GetSingle();
                float y = item.GetProperty("y").GetSingle();
                float z = item.GetProperty("z").GetSingle();
                int off = (int)bsp.Header.FVertex.Offset + vid * 12;
                SysBuffer.BlockCopy(BitConverter.GetBytes(x), 0, bytes, off + 0, 4);
                SysBuffer.BlockCopy(BitConverter.GetBytes(y), 0, bytes, off + 4, 4);
                SysBuffer.BlockCopy(BitConverter.GetBytes(z), 0, bytes, off + 8, 4);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outBsp)!);
            File.WriteAllBytes(outBsp, bytes);
            Console.WriteLine($"[BSP-APPLY] Saved: {outBsp}");
            Console.WriteLine($"[BSP-APPLY] SHA256: {Sha256File(outBsp)}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(bspCenterMarkerMapArg))
        {
            string? mr = FindMapRoot();
            if (mr == null) { Console.WriteLine("ERROR: Map folder not found."); return; }
            var srcMapDir = Directory.GetDirectories(mr)
                .FirstOrDefault(d => string.Equals(Path.GetFileName(d), bspCenterMarkerMapArg, StringComparison.OrdinalIgnoreCase));
            if (srcMapDir == null) { Console.WriteLine($"ERROR: map '{bspCenterMarkerMapArg}' not found."); return; }
            var srcBsp = FindBspFile(srcMapDir);
            if (srcBsp == null) { Console.WriteLine("ERROR: BSP not found."); return; }

            var outPath = string.IsNullOrWhiteSpace(bspCenterMarkerOutArg)
                ? Path.Combine(Path.GetDirectoryName(srcBsp)!, $"{Path.GetFileNameWithoutExtension(srcBsp)}.center_marker.bsp")
                : Path.GetFullPath(bspCenterMarkerOutArg);
            var reportPath = Path.Combine(Path.GetDirectoryName(outPath)!, $"{Path.GetFileNameWithoutExtension(outPath)}.json");
            ApplyCenterMarkerPatch(srcBsp, outPath, reportPath, bspCenterMarkerRadius, bspCenterMarkerHeight, bspCenterMarkerMaxVerts);
            Console.WriteLine($"[CENTER-MARKER] Saved BSP: {outPath}");
            Console.WriteLine($"[CENTER-MARKER] Report: {reportPath}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(glbToBspMapArg))
        {
            if (string.IsNullOrWhiteSpace(glbToBspGlbArg))
            {
                Console.WriteLine("ERROR: --glb-to-bsp-map requires --glb-to-bsp-glb <path>");
                return;
            }

            string? mr = FindMapRoot();
            if (mr == null)
            {
                Console.WriteLine("ERROR: Map folder not found.");
                return;
            }

            var srcMapDir = Directory.GetDirectories(mr)
                .FirstOrDefault(d => string.Equals(Path.GetFileName(d), glbToBspMapArg, StringComparison.OrdinalIgnoreCase));
            if (srcMapDir == null)
            {
                Console.WriteLine($"ERROR: map '{glbToBspMapArg}' not found in {mr}");
                return;
            }

            var srcBsp = FindBspFile(srcMapDir);
            if (string.IsNullOrWhiteSpace(srcBsp) || !File.Exists(srcBsp))
            {
                Console.WriteLine("ERROR: source BSP not found.");
                return;
            }

            var glbPath = Path.GetFullPath(glbToBspGlbArg);
            if (!File.Exists(glbPath))
            {
                Console.WriteLine($"ERROR: GLB not found: {glbPath}");
                return;
            }

            var outBspPath = string.IsNullOrWhiteSpace(glbToBspOutArg)
                ? Path.Combine(Path.GetDirectoryName(glbPath)!, $"{Path.GetFileNameWithoutExtension(glbPath)}.patched.bsp")
                : Path.GetFullPath(glbToBspOutArg);

            var patchReportPath = Path.Combine(Path.GetDirectoryName(outBspPath)!, $"{Path.GetFileNameWithoutExtension(outBspPath)}.glb2bsp_report.json");
            var report = PatchBspFromGlb(srcBsp, glbPath, outBspPath, patchReportPath, 80f);
            Console.WriteLine($"[GLB2BSP] Patched BSP: {outBspPath}");
            Console.WriteLine($"[GLB2BSP] Report: {patchReportPath}");
            Console.WriteLine($"[GLB2BSP] MatGroups patched: {report.MatGroupsPatched}, vertices mapped: {report.TotalMappedVertices}, fvertices changed: {report.FVerticesChanged}");
            Console.WriteLine($"[GLB2BSP] SHA256: {Sha256File(outBspPath)}");

            if (!string.IsNullOrWhiteSpace(glbToBspRepackOutArg))
            {
                var repackRoot = Path.GetFullPath(glbToBspRepackOutArg);
                var outMapDir = Path.Combine(repackRoot, "map", Path.GetFileName(srcMapDir));
                CopyDirectoryRecursive(srcMapDir, outMapDir);
                var outBsp = FindBspFile(outMapDir);
                if (string.IsNullOrWhiteSpace(outBsp))
                {
                    Console.WriteLine("ERROR: repack output map has no BSP.");
                    return;
                }
                File.Copy(outBspPath, outBsp, true);
                Console.WriteLine($"[GLB2BSP] Repacked map: {outMapDir}");
            }
            return;
        }
        if (!string.IsNullOrWhiteSpace(glbInsertScanGlbArg))
        {
            string? mr = FindMapRoot();
            if (mr == null) { Console.WriteLine("ERROR: Map folder not found."); return; }
            var srcMapDir = Directory.GetDirectories(mr).FirstOrDefault(d => string.Equals(Path.GetFileName(d), "sette", StringComparison.OrdinalIgnoreCase));
            if (srcMapDir == null) { Console.WriteLine("ERROR: map 'sette' not found."); return; }
            var srcBsp = FindBspFile(srcMapDir);
            if (string.IsNullOrWhiteSpace(srcBsp) || !File.Exists(srcBsp)) { Console.WriteLine("ERROR: source BSP not found."); return; }
            DumpMgDeltas(srcBsp, Path.GetFullPath(glbInsertScanGlbArg));
            return;
        }
        if (!string.IsNullOrWhiteSpace(glbInsertMapArg))
        {
            if (string.IsNullOrWhiteSpace(glbInsertGlbArg))
            {
                Console.WriteLine("ERROR: --glb-insert-map requires --glb-insert-glb <path>");
                return;
            }
            string? mr = FindMapRoot();
            if (mr == null) { Console.WriteLine("ERROR: Map folder not found."); return; }
            var srcMapDir = Directory.GetDirectories(mr).FirstOrDefault(d => string.Equals(Path.GetFileName(d), glbInsertMapArg, StringComparison.OrdinalIgnoreCase));
            if (srcMapDir == null) { Console.WriteLine($"ERROR: map '{glbInsertMapArg}' not found in {mr}"); return; }
            var srcBsp = FindBspFile(srcMapDir);
            if (string.IsNullOrWhiteSpace(srcBsp) || !File.Exists(srcBsp)) { Console.WriteLine("ERROR: source BSP not found."); return; }
            var glbPath = Path.GetFullPath(glbInsertGlbArg);
            var outBspPath = string.IsNullOrWhiteSpace(glbInsertOutArg) ? Path.Combine(Path.GetDirectoryName(glbPath)!, $"{Path.GetFileNameWithoutExtension(glbPath)}.inserted.bsp") : Path.GetFullPath(glbInsertOutArg);
            InsertNewGeometryFromGlb(srcBsp, glbPath, outBspPath, glbInsertMgId);
            Console.WriteLine($"[GLB-INSERT] Saved BSP: {outBspPath}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(glbToRfMapArg))
        {
            if (string.IsNullOrWhiteSpace(glbToRfGlbArg) || string.IsNullOrWhiteSpace(glbToRfOutArg))
            {
                Console.WriteLine("ERROR: --glb-to-rf-map requires --glb-to-rf-glb <path> and --glb-to-rf-out <dir>");
                return;
            }

            string? mr = FindMapRoot();
            if (mr == null)
            {
                Console.WriteLine("ERROR: Map folder not found.");
                return;
            }

            var srcMapDir = Directory.GetDirectories(mr)
                .FirstOrDefault(d => string.Equals(Path.GetFileName(d), glbToRfMapArg, StringComparison.OrdinalIgnoreCase));
            if (srcMapDir == null)
            {
                Console.WriteLine($"ERROR: map '{glbToRfMapArg}' not found in {mr}");
                return;
            }

            var srcBsp = FindBspFile(srcMapDir);
            if (string.IsNullOrWhiteSpace(srcBsp) || !File.Exists(srcBsp))
            {
                Console.WriteLine("ERROR: source BSP not found.");
                return;
            }

            var glbPath = Path.GetFullPath(glbToRfGlbArg);
            if (!File.Exists(glbPath))
            {
                Console.WriteLine($"ERROR: GLB not found: {glbPath}");
                return;
            }

            var outRoot = Path.GetFullPath(glbToRfOutArg);
            var outMapDir = Path.Combine(outRoot, "map", Path.GetFileName(srcMapDir));
            CopyDirectoryRecursive(srcMapDir, outMapDir);

            var patchedBsp = Path.Combine(outMapDir, Path.GetFileName(srcBsp));
            var reportPath = Path.Combine(outMapDir, $"{Path.GetFileNameWithoutExtension(srcBsp)}.glb2bsp_report.json");
            var report = PatchBspFromGlb(srcBsp, glbPath, patchedBsp, reportPath, glbToRfMaxDelta);
            Console.WriteLine($"[GLB2RF] BSP patched: {patchedBsp}");
            Console.WriteLine($"[GLB2RF] MatGroups patched: {report.MatGroupsPatched}, mapped: {report.TotalMappedVertices}, changed: {report.FVerticesChanged}");

            // Re-save R3M/R3T in output map to keep pipeline fully reproducible.
            R3MMaterialFile? r3mLoaded = null;
            var r3mPath = Directory.GetFiles(outMapDir, "*.r3m", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(r3mPath) && File.Exists(r3mPath))
            {
                r3mLoaded = R3MMaterialFile.Load(r3mPath);
                r3mLoaded.Save(r3mPath);
                Console.WriteLine($"[GLB2RF] R3M rebuilt: {r3mPath}");
            }

            var r3tPaths = Directory.GetFiles(outMapDir, "*.r3t", SearchOption.TopDirectoryOnly);
            foreach (var r3tPath in r3tPaths)
            {
                var r3tName = Path.GetFileName(r3tPath);
                if (r3tName.IndexOf("lgt", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine($"[GLB2RF] R3T keep original (engine-sensitive): {r3tPath}");
                    continue;
                }
                try
                {
                    var r3t = R3TFile.Load(r3tPath);
                    if (glbToRfApplyTextures && r3mLoaded != null)
                    {
                        ApplyGlbEmbeddedTexturesToR3t(glbPath, r3mLoaded, r3t);
                    }
                    if (!string.IsNullOrWhiteSpace(glbToRfTextureOverridesArg))
                    {
                        ApplyR3tOverrides(r3t, Path.GetFullPath(glbToRfTextureOverridesArg));
                    }
                    r3t.Save(r3tPath);
                    Console.WriteLine($"[GLB2RF] R3T rebuilt: {r3tPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GLB2RF] R3T skipped (unsupported format): {r3tPath} ({ex.Message})");
                }
            }

            Console.WriteLine($"[GLB2RF] Output map: {outMapDir}");
            Console.WriteLine($"[GLB2RF] Options: applyTextures={glbToRfApplyTextures}, maxDelta={glbToRfMaxDelta.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            return;
        }
        if (!string.IsNullOrWhiteSpace(glbNonBspToSptMapArg))
        {
            if (string.IsNullOrWhiteSpace(glbNonBspToSptGlbArg) || string.IsNullOrWhiteSpace(glbNonBspToSptOutArg))
            {
                Console.WriteLine("ERROR: --glb-nonbsp-to-spt-map requires --glb-nonbsp-to-spt-glb <path> and --glb-nonbsp-to-spt-out <dir>");
                return;
            }
            EmitNonBspSptPatch(glbNonBspToSptMapArg, Path.GetFullPath(glbNonBspToSptGlbArg), Path.GetFullPath(glbNonBspToSptOutArg));
            return;
        }

        if (!string.IsNullOrWhiteSpace(validateMapDirArg))
        {
            var mapDir = Path.GetFullPath(validateMapDirArg);
            if (!Directory.Exists(mapDir))
            {
                Console.WriteLine($"ERROR: map dir not found: {mapDir}");
                return;
            }

            Console.WriteLine($"[VALIDATE] map dir: {mapDir}");
            int ok = 0, fail = 0;

            void Try(string label, Action action)
            {
                try
                {
                    action();
                    Console.WriteLine($"[VALIDATE][OK] {label}");
                    ok++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VALIDATE][FAIL] {label}: {ex.Message}");
                    fail++;
                }
            }

            var bsp = Directory.GetFiles(mapDir, "*.bsp", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(bsp))
                Try(Path.GetFileName(bsp), () => RFMapToolSharp.Collision.BspFile.Load(bsp));

            foreach (var r3m in Directory.GetFiles(mapDir, "*.r3m", SearchOption.TopDirectoryOnly))
                Try(Path.GetFileName(r3m), () => R3MMaterialFile.Load(r3m));

            foreach (var r3t in Directory.GetFiles(mapDir, "*.r3t", SearchOption.TopDirectoryOnly))
                Try(Path.GetFileName(r3t), () => R3TFile.Load(r3t));

            foreach (var r3x in Directory.GetFiles(mapDir, "*.r3x", SearchOption.TopDirectoryOnly))
                Try(Path.GetFileName(r3x), () => R3XMaterialFile.Load(r3x));

            foreach (var spt in Directory.GetFiles(mapDir, "*.spt", SearchOption.AllDirectories))
                Try(Path.GetFileName(spt), () => File.ReadAllBytes(spt));

            Console.WriteLine($"[VALIDATE] Done. ok={ok} fail={fail}");
            return;
        }

        if (setteCleanIsolated)
        {
            string? mr = FindMapRoot();
            if (mr == null)
            {
                Console.WriteLine("ERROR: Map folder not found.");
                return;
            }

            var exeDirIso = AppContext.BaseDirectory;
            var curIso = new DirectoryInfo(exeDirIso);
            string rootDirIso = Environment.CurrentDirectory;
            while (curIso != null)
            {
                bool hasMarkers = curIso.GetFiles("*.csproj").Any() || curIso.GetFiles("*.sln").Any();
                if (hasMarkers) { rootDirIso = curIso.FullName; break; }
                curIso = curIso.Parent;
            }
            var exportRootIso = Path.Combine(rootDirIso, "RF_Release");
            Directory.CreateDirectory(exportRootIso);

            if (!RFMapToolSharp.Export.SetteCleanExporter.Run(mr, exportRootIso))
            {
                Console.WriteLine("ERROR: failed to export Sette in isolated mode.");
                return;
            }
            Console.WriteLine("[OK] Sette clean isolated export completed.");
            return;
        }
        if (setteRaw)
        {
            string? mr = FindMapRoot();
            if (mr == null)
            {
                Console.WriteLine("ERROR: Map folder not found.");
                return;
            }
            var exeDirIso = AppContext.BaseDirectory;
            var curIso = new DirectoryInfo(exeDirIso);
            string rootDirIso = Environment.CurrentDirectory;
            while (curIso != null)
            {
                bool hasMarkers = curIso.GetFiles("*.csproj").Any() || curIso.GetFiles("*.sln").Any();
                if (hasMarkers) { rootDirIso = curIso.FullName; break; }
                curIso = curIso.Parent;
            }
            var exportRootIso = Path.Combine(rootDirIso, "RF_Release");
            Directory.CreateDirectory(exportRootIso);
            if (!RFMapToolSharp.Export.SetteRawExporter.Run(mr, exportRootIso))
            {
                Console.WriteLine("ERROR: failed to export Sette in raw mode.");
                return;
            }
            Console.WriteLine("[OK] Sette raw export completed.");
            return;
        }

        string? mapRoot = FindMapRoot();
        if (mapRoot == null)
        {
            Console.WriteLine("ERROR: Map folder not found.");
            if (IsInteractive) Console.ReadKey();
            return;
        }
        Console.WriteLine($"Map folder found: {mapRoot}\n");

        // РС‰РµРј РєРѕСЂРµРЅСЊ РїСЂРѕРµРєС‚Р° РїРѕ РЅР°Р»РёС‡РёСЋ .csproj/.sln, С‡С‚РѕР±С‹ РєРѕСЂСЂРµРєС‚РЅРѕ РїРёСЃР°С‚СЊ RF_Release РІРЅРµ bin\Debug
        var exeDir = AppContext.BaseDirectory;
        var current = new DirectoryInfo(exeDir);
        string rootDir = Environment.CurrentDirectory;

        while (current != null)
        {
            bool hasProjectMarkers =
                current.GetFiles("*.csproj").Any() ||
                current.GetFiles("*.sln").Any();
            if (hasProjectMarkers)
            {
                rootDir = current.FullName;
                break;
            }
            current = current.Parent;
        }

        // Р­РєСЃРїРѕСЂС‚ РІСЃРµРіРґР° РІ RFMapToolSharp\RF_Release
        var exportRoot = Path.Combine(rootDir, "RF_Release");
        Directory.CreateDirectory(exportRoot);



        var mapDirs = Directory.GetDirectories(mapRoot);
        Console.WriteLine($"Maps found: {mapDirs.Length}.\n");
        var skippedNonBsp = new List<string>();
        var bspCapableMapDirs = mapDirs.Where(d =>
        {
            var hasBsp = FindBspFile(d) != null;
            if (!hasBsp) skippedNonBsp.Add(Path.GetFileName(d));
            return hasBsp;
        }).ToArray();

        // === Р Р•Р–РРњ Р’Р«Р‘РћР Рђ РљРђР Рў ===
        string[] mapsToProcess = bspCapableMapDirs;

        if (!string.IsNullOrWhiteSpace(mapFilterArg))
        {
            mapsToProcess = bspCapableMapDirs
                .Where(d => Path.GetFileName(d).IndexOf(mapFilterArg, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            if (mapsToProcess.Length == 0)
            {
                Console.WriteLine($"No maps found by --map '{mapFilterArg}'.");
                return;
            }
            Console.WriteLine($"Non-interactive mode: --map {mapFilterArg}");
        }
        else
        {
            Console.WriteLine("Select mode:");
            Console.WriteLine("  1 - export all maps");
            Console.WriteLine("  2 - export one map (by number)");
            Console.WriteLine("  3 - export by name / partial name");
            Console.Write("Mode (Enter = 1): ");

            var modeInput = Console.ReadLine();
            int mode = 1;
            int.TryParse(modeInput, out mode);
            if (mode < 1 || mode > 3) mode = 1;

            if (mode == 2)
            {
                Console.WriteLine("\nMap list:");
                for (int i = 0; i < bspCapableMapDirs.Length; i++)
                    Console.WriteLine($"{i + 1,2}. {Path.GetFileName(bspCapableMapDirs[i])}");

                Console.Write("\nEnter map number: ");
                var sel = Console.ReadLine();
                if (!int.TryParse(sel, out int idx) || idx < 1 || idx > bspCapableMapDirs.Length)
                {
                    Console.WriteLine("Invalid number, exiting.");
                    if (IsInteractive) Console.ReadKey();
                    return;
                }

                mapsToProcess = new[] { bspCapableMapDirs[idx - 1] };
                Console.WriteLine($"\nSelected map: {Path.GetFileName(mapsToProcess[0])}\n");
            }
            else if (mode == 3)
            {
                Console.Write("\nEnter map name or part of name: ");
                var filter = (Console.ReadLine() ?? "").Trim();

                if (string.IsNullOrEmpty(filter))
                {
                    Console.WriteLine("Empty filter, exiting.");
                    if (IsInteractive) Console.ReadKey();
                    return;
                }

                mapsToProcess = bspCapableMapDirs
                    .Where(d => Path.GetFileName(d)
                        .IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();

                if (mapsToProcess.Length == 0)
                {
                    Console.WriteLine("No maps found by filter.");
                    if (IsInteractive) Console.ReadKey();
                    return;
                }

                Console.WriteLine($"\nWill be exported {mapsToProcess.Length} map(s):");
                foreach (var d in mapsToProcess)
                    Console.WriteLine(" - " + Path.GetFileName(d));
                Console.WriteLine();
            }
        }

        int success = 0;

        foreach (var dir in mapsToProcess)
        {

            string mapName = Path.GetFileName(dir);
            string bspPath = FindBspFile(dir);

            if (bspPath == null) continue;

            Console.WriteLine($"---> Processing: {mapName}");

            try
            {
                var scene = new MapScene
                {
                    Name = mapName,
                    RootPath = dir
                };

                /// === 1. BSP ===
                scene.Bsp = RFMapToolSharp.Collision.BspFile.Load(bspPath);

                // === 2. РњРђРўР•Р РРђР›Р« ===
                string materialPath = Path.Combine(dir, "materials.r3m");
                string rootMapR3m = Path.Combine(dir, $"{mapName}.r3m");

                // РµСЃР»Рё materials.r3m РЅРµС‚ вЂ“ РёС‰РµРј Р»СЋР±РѕР№ *.r3m РІРЅСѓС‚СЂРё РїР°РїРєРё РєР°СЂС‚С‹
                if (!File.Exists(materialPath))
                {
                    if (File.Exists(rootMapR3m))
                    {
                        materialPath = rootMapR3m;
                    }
                    else
                    {
                        var r3mFiles = Directory.GetFiles(dir, "*.r3m", SearchOption.TopDirectoryOnly);
                        if (r3mFiles.Length > 0)
                        {
                            materialPath =
                                r3mFiles.FirstOrDefault(p =>
                                    string.Equals(
                                        Path.GetFileNameWithoutExtension(p),
                                        mapName,
                                        StringComparison.OrdinalIgnoreCase))
                                ?? r3mFiles[0];
                        }
                    }
                }

                if (File.Exists(materialPath))
                {
                    Console.WriteLine($"[DEBUG] {mapName}: R3M = {materialPath}");
                    scene.MaterialFile = R3MMaterialFile.Load(materialPath);
                }
                else
                {
                    Console.WriteLine($"[WARNING] {mapName}: .r3m not found, using default materials.");
                    scene.MaterialFile = new R3MMaterialFile();
                }

                // === 3. РўР•РљРЎРўРЈР Р« ===
                string texturePath = Path.Combine(dir, $"{mapName}.r3t");

                scene.Textures = new List<RFMapToolSharp.Textures.R3TTextureEntry>();

                // РµСЃР»Рё mapName.r3t РЅРµС‚ вЂ” Р±РµСЂС‘Рј РїРµСЂРІС‹Р№ РїРѕРїР°РІС€РёР№СЃСЏ *.r3t
                if (!File.Exists(texturePath))
                {
                    var r3tFiles = Directory.GetFiles(dir, "*.r3t", SearchOption.TopDirectoryOnly);
                    if (r3tFiles.Length > 0)
                        texturePath = r3tFiles[0];
                }

                if (File.Exists(texturePath))
                {
                    Console.WriteLine($"[DEBUG] {mapName}: R3T = {texturePath}");
                    var r3tFile = RFMapToolSharp.Textures.R3TFile.Load(texturePath);
                    scene.Textures.AddRange(r3tFile.Textures);
                }

                // === 4. Р­РљРЎРџРћР Рў ===
                string targetDir = Path.Combine(exportRoot, mapName);
                Directory.CreateDirectory(targetDir);

                string sourceSpt = Path.Combine(dir, "Spt");
                string destSpt = Path.Combine(targetDir, "Spt");
                CopySptFolder(sourceSpt, destSpt);
                Console.WriteLine($"[DEBUG] {mapName}: BSP = {bspPath}");
                Console.WriteLine($"[DEBUG] {mapName}: SPT root = {sourceSpt}");

                int texCount = scene.Textures?.Count ?? 0;
                int matCount = scene.MaterialFile?.Materials?.Count ?? 0;

                Console.WriteLine($"[DEBUG] {mapName}: BSP={(scene.Bsp != null)}, Mats={matCount}, Textures={texCount}");

                bool oldFilterStretch = GltfExporter.FilterStretchedFaces;
                bool oldFilterUv = GltfExporter.FilterUvAnomalyFaces;
                bool oldFilterNormal = GltfExporter.FilterNormalAnomalyFaces;
                try
                {
                    // Sette currently has a few known long-edge artifacts; hide only those triangles in export.
                    if (string.Equals(mapName, "Sette", StringComparison.OrdinalIgnoreCase))
                    {
                        GltfExporter.FilterStretchedFaces = true;
                        GltfExporter.FilterUvAnomalyFaces = false;
                        GltfExporter.FilterNormalAnomalyFaces = false;
                    }
                    else
                    {
                        GltfExporter.FilterStretchedFaces = false;
                        GltfExporter.FilterUvAnomalyFaces = false;
                        GltfExporter.FilterNormalAnomalyFaces = false;
                    }

                    GltfExporter.Export(scene, targetDir, mapName);
                }
                finally
                {
                    GltfExporter.FilterStretchedFaces = oldFilterStretch;
                    GltfExporter.FilterUvAnomalyFaces = oldFilterUv;
                    GltfExporter.FilterNormalAnomalyFaces = oldFilterNormal;
                }
                try
                {
                    scene.Bsp?.WriteBrokenFacesReport(Path.Combine(targetDir, "broken_faces.json"));
                    scene.Bsp?.WriteObjectMatricesReport(Path.Combine(targetDir, "object_matrices.json"));
                    scene.Bsp?.WriteAnimatedObjectsReport(Path.Combine(targetDir, "animated_objects.json"));
                    scene.Bsp?.WriteMatGroupDebugReport(Path.Combine(targetDir, "matgroup_debug.json"));
                    if (string.Equals(mapName, "Sette", StringComparison.OrdinalIgnoreCase))
                    {
                        scene.Bsp?.WriteDonor89_92Diagnostics(targetDir);
                    }
                }
                catch (OutOfMemoryException)
                {
                    Console.WriteLine($"[WARN] {mapName}: not enough memory to write all debug reports.");
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[OK] {mapName} completed.");
                Console.ResetColor();
                success++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] Error {mapName}: {ex.Message}");
                Console.ResetColor();
            }
        }

        if (skippedNonBsp.Count > 0)
        {
            foreach (var n in skippedNonBsp)
                Console.WriteLine($"[SKIP] {n}: RPK resource pack / no BSP map files.");
        }
        Console.WriteLine($"\nProcessing complete. Exported maps: {success} of {bspCapableMapDirs.Length} BSP map(s).");
        Console.WriteLine($"Exported maps path: {exportRoot}");
        if (IsInteractive)
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    static string? FindMapRoot()
    {
        static string? TryMapDir(string? baseDir)
        {
            if (string.IsNullOrWhiteSpace(baseDir)) return null;

            var mapLower = Path.Combine(baseDir, "map");
            if (Directory.Exists(mapLower)) return mapLower;

            var mapUpper = Path.Combine(baseDir, "Map");
            if (Directory.Exists(mapUpper)) return mapUpper;

            return null;
        }

        // 1) Explicit path from config file (rf_path.txt)
        // File can contain either game root (with Map inside) or direct Map path.
        var cfgPath = Path.Combine(Environment.CurrentDirectory, ConfigFile);
        if (File.Exists(cfgPath))
        {
            var raw = File.ReadAllText(cfgPath).Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(raw))
            {
                if (Directory.Exists(raw))
                {
                    if (string.Equals(Path.GetFileName(raw), "map", StringComparison.OrdinalIgnoreCase))
                        return raw;

                    var fromCfg = TryMapDir(raw);
                    if (fromCfg != null) return fromCfg;
                }
            }
        }

        // 2) Current working directory and exe base directory
        var fromCwd = TryMapDir(Environment.CurrentDirectory);
        if (fromCwd != null) return fromCwd;

        var fromExe = TryMapDir(AppContext.BaseDirectory);
        if (fromExe != null) return fromExe;

        // 3) Walk up from current directory
        string? dir = Environment.CurrentDirectory;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            var found = TryMapDir(dir);
            if (found != null) return found;
            dir = Directory.GetParent(dir)?.FullName;
        }

        // 4) Walk up from exe directory
        dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            var found = TryMapDir(dir);
            if (found != null) return found;
            dir = Directory.GetParent(dir)?.FullName;
        }

        // 5) Legacy fallback path
        if (Directory.Exists(@"C:\Games\RF_Online\Map")) return @"C:\Games\RF_Online\Map";
        return null;
    }

    static string FindBspFile(string dir)
    {
        var f = Directory.GetFiles(dir, "*.bsp");
        if (f.Any()) return f[0];

        var bspDir = Path.Combine(dir, "Bsp");
        if (Directory.Exists(bspDir))
        {
            f = Directory.GetFiles(bspDir, "*.bsp");
            if (f.Any()) return f[0];
        }
        return null;
    }

    static void CopySptFolder(string src, string dest)
    {
        if (!Directory.Exists(src)) return;
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
    }

    static void CopyDirectoryRecursive(string src, string dest)
    {
        var srcDir = new DirectoryInfo(src);
        if (!srcDir.Exists) return;
        Directory.CreateDirectory(dest);
        foreach (var file in srcDir.GetFiles())
        {
            var dst = Path.Combine(dest, file.Name);
            file.CopyTo(dst, true);
        }
        foreach (var dir in srcDir.GetDirectories())
        {
            CopyDirectoryRecursive(dir.FullName, Path.Combine(dest, dir.Name));
        }
    }

    static string Sha256File(string path)
    {
        using var fs = File.OpenRead(path);
        using var h = SHA256.Create();
        return Convert.ToHexString(h.ComputeHash(fs));
    }

    private sealed class Glb2BspReport
    {
        public int MatGroupsPatched { get; set; }
        public int TotalMappedVertices { get; set; }
        public int FVerticesChanged { get; set; }
        public List<object> MatGroups { get; } = new();
    }

    private static Glb2BspReport PatchBspFromGlb(string srcBspPath, string glbPath, string outBspPath, string reportPath, float maxDelta)
    {
        var bsp = RFMapToolSharp.Collision.BspFile.Load(srcBspPath);
        var model = ModelRoot.Load(glbPath);
        var bytes = File.ReadAllBytes(srcBspPath);
        var report = new Glb2BspReport();
        int? targetMg = null;
        var targetMgEnv = Environment.GetEnvironmentVariable("RFMAP_TARGET_MGID");
        if (!string.IsNullOrWhiteSpace(targetMgEnv) && int.TryParse(targetMgEnv, out var parsedTargetMg) && parsedTargetMg >= 0)
            targetMg = parsedTargetMg;

        var mgNodes = model.LogicalNodes
            .Where(n => n.Mesh != null)
            .ToList();

        var fvertAcc = new Dictionary<int, (Vector3 sum, int count)>();

        foreach (var node in mgNodes)
        {
            var tagName = !string.IsNullOrWhiteSpace(node.Name) ? node.Name : node.Mesh?.Name;
            int mgId = ParseMatGroupFromNodeName(tagName);
            if (mgId < 0 || mgId >= bsp.MatGroups.Count) continue;
            if (targetMg.HasValue && mgId != targetMg.Value) continue;
            var mg = bsp.MatGroups[mgId];
            // Keep dynamic/animated object groups intact for the game runtime.
            if (mg.ObjectId > 0) continue;

            var exportedPositions = ReadNodePositions(node);
            if (exportedPositions.Count == 0) continue;

            var refs = BuildMatGroupVertexRefs(bsp, mgId, mg.FaceStartId, mg.FaceNum);
            int mapped = Math.Min(refs.Count, exportedPositions.Count);
            if (mapped == 0) continue;

            for (int i = 0; i < mapped; i++)
            {
                int vid = refs[i];
                if (!fvertAcc.TryGetValue(vid, out var acc)) acc = (Vector3.Zero, 0);
                acc.sum += exportedPositions[i];
                acc.count += 1;
                fvertAcc[vid] = acc;
            }

            report.MatGroupsPatched++;
            report.TotalMappedVertices += mapped;
            report.MatGroups.Add(new
            {
                mg = mgId,
                node = tagName,
                exportedVertices = exportedPositions.Count,
                mappedVertices = mapped,
                faceStartId = mg.FaceStartId,
                faceNum = mg.FaceNum
            });
        }

        int changed = 0;
        int skippedByDelta = 0;
        foreach (var kv in fvertAcc)
        {
            int vid = kv.Key;
            var avg = kv.Value.sum / kv.Value.count;
            int off = (int)bsp.Header.FVertex.Offset + vid * 12;
            float oldX = BitConverter.ToSingle(bytes, off + 0);
            float oldY = BitConverter.ToSingle(bytes, off + 4);
            float oldZ = BitConverter.ToSingle(bytes, off + 8);
            var dx = avg.X - oldX;
            var dy = avg.Y - oldY;
            var dz = avg.Z - oldZ;
            var dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            if (dist > maxDelta)
            {
                skippedByDelta++;
                continue;
            }
            if (Math.Abs(oldX - avg.X) > 1e-6f || Math.Abs(oldY - avg.Y) > 1e-6f || Math.Abs(oldZ - avg.Z) > 1e-6f)
            {
                SysBuffer.BlockCopy(BitConverter.GetBytes(avg.X), 0, bytes, off + 0, 4);
                SysBuffer.BlockCopy(BitConverter.GetBytes(avg.Y), 0, bytes, off + 4, 4);
                SysBuffer.BlockCopy(BitConverter.GetBytes(avg.Z), 0, bytes, off + 8, 4);
                changed++;
            }
        }
        report.FVerticesChanged = changed;
        report.MatGroups.Add(new { safeMaxDelta = maxDelta, skippedByDelta, targetMg = targetMg?.ToString() ?? "all" });

        Directory.CreateDirectory(Path.GetDirectoryName(outBspPath)!);
        File.WriteAllBytes(outBspPath, bytes);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return report;
    }

    private static void ApplyCenterMarkerPatch(string srcBspPath, string outBspPath, string reportPath, float radius, float raiseHeight, int maxVerts)
    {
        var bsp = RFMapToolSharp.Collision.BspFile.Load(srcBspPath);
        var bytes = File.ReadAllBytes(srcBspPath);
        if (bsp.FVertices.Count == 0) throw new InvalidOperationException("No FVertices in BSP.");

        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var v in bsp.FVertices)
        {
            if (v.X < minX) minX = v.X;
            if (v.X > maxX) maxX = v.X;
            if (v.Z < minZ) minZ = v.Z;
            if (v.Z > maxZ) maxZ = v.Z;
        }
        float cx = (minX + maxX) * 0.5f;
        float cz = (minZ + maxZ) * 0.5f;
        float r2 = radius * radius;

        var hits = new List<(int vid, float d2)>();
        for (int i = 0; i < bsp.FVertices.Count; i++)
        {
            var v = bsp.FVertices[i];
            float dx = v.X - cx;
            float dz = v.Z - cz;
            float d2 = dx * dx + dz * dz;
            if (d2 <= r2) hits.Add((i, d2));
        }
        var selected = hits.OrderBy(h => h.d2).Take(Math.Max(1, maxVerts)).ToList();
        foreach (var s in selected)
        {
            int off = (int)bsp.Header.FVertex.Offset + s.vid * 12;
            float oldY = BitConverter.ToSingle(bytes, off + 4);
            float newY = oldY + raiseHeight;
            SysBuffer.BlockCopy(BitConverter.GetBytes(newY), 0, bytes, off + 4, 4);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outBspPath)!);
        File.WriteAllBytes(outBspPath, bytes);
        var report = new
        {
            srcBspPath,
            outBspPath,
            fvertexCount = bsp.FVertices.Count,
            centerX = cx,
            centerZ = cz,
            radius,
            raiseHeight,
            maxVerts,
            selectedVerts = selected.Count
        };
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static int ParseMatGroupFromNodeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return -1;
        var m = Regex.Match(name, @"mg(\d+)", RegexOptions.IgnoreCase);
        if (!m.Success) return -1;
        return int.TryParse(m.Groups[1].Value, out var mg) ? mg : -1;
    }

    private static List<Vector3> ReadNodePositions(Node node)
    {
        var result = new List<Vector3>();
        if (node.Mesh == null) return result;
        foreach (var prim in node.Mesh.Primitives)
        {
            var acc = prim.GetVertexAccessor("POSITION");
            if (acc == null) continue;
            foreach (var p in acc.AsVector3Array())
            {
                result.Add(new Vector3(p.X, p.Y, p.Z));
            }
        }
        return result;
    }

    private static List<int> BuildMatGroupVertexRefs(RFMapToolSharp.Collision.BspFile bsp, int matGroupId, uint faceStartId, uint faceNum)
    {
        var refs = new List<int>();
        int functionId = 4;
        uint fe = faceStartId + faceNum;
        for (uint f = faceStartId; f < fe && f < bsp.Faces.Count; f++)
        {
            var face = bsp.Faces[(int)f];
            if (face.VertexCount < 3) continue;
            for (int k = 0; k < face.VertexCount; k++)
            {
                int vIdx = (int)(face.VertexStartId + (uint)k);
                if (vIdx < 0 || vIdx >= bsp.VertexId.Count) continue;
                uint vid = bsp.VertexId[vIdx];
                if (!IsValidCompressedVertex(functionId, vid, bsp)) continue;
                refs.Add((int)vid);
            }
        }
        return refs;
    }

    private static bool IsValidCompressedVertex(int functionId, uint vid, RFMapToolSharp.Collision.BspFile bsp)
    {
        return functionId switch
        {
            2 => vid < bsp.BVertices.Count,
            3 => vid < bsp.WVertices.Count,
            _ => vid < bsp.FVertices.Count
        };
    }
    private static void InsertNewGeometryFromGlb(string srcBspPath, string glbPath, string outBspPath, int targetMgId)
    {
        var bsp = RFMapToolSharp.Collision.BspFile.Load(srcBspPath);
        var model = ModelRoot.Load(glbPath);
        var src = File.ReadAllBytes(srcBspPath);
        int effectiveTargetMgId = ResolveInsertTargetMgId(model, bsp, targetMgId);
        Console.WriteLine($"[GLB-INSERT] Effective target mg: {effectiveTargetMgId}");
        if (effectiveTargetMgId <= 0)
            throw new InvalidOperationException("[GLB-INSERT] strict-v1: target mg must be explicitly resolvable and > 0.");

        var fverts = bsp.FVertices.Select(v => new Vector3f { X = v.X, Y = v.Y, Z = v.Z }).ToList();
        var uvs = bsp.UV.Select(v => new Vector2f { X = v.X, Y = v.Y }).ToList();
        var colors = bsp.VertexColors.ToList();
        var vertexIds = bsp.VertexId.ToList();
        var faces = bsp.Faces.Select(f => new BspReadFace { VertexCount = f.VertexCount, VertexStartId = f.VertexStartId }).ToList();
        var faceIds = bsp.FaceId.ToList();
        var appendedFaceIds = new List<uint>();
        bool newMgMode = string.Equals(Environment.GetEnvironmentVariable("RFMAP_INSERT_NEW_MG"), "1", StringComparison.OrdinalIgnoreCase);
        int maxInsertFaces = int.MaxValue;
        var maxInsertEnv = Environment.GetEnvironmentVariable("RFMAP_INSERT_MAX_FACES");
        if (!string.IsNullOrWhiteSpace(maxInsertEnv) && int.TryParse(maxInsertEnv, out var parsedMax) && parsedMax > 0) maxInsertFaces = parsedMax;

        int insertedFaces = 0;
        int insertedMeshes = 0;
        BspReadMatGroup? templateMg = null;
        foreach (var node in model.LogicalNodes.Where(n => n.Mesh != null))
        {
            var nodeName = !string.IsNullOrWhiteSpace(node.Name) ? node.Name! : node.Mesh!.Name ?? "";
            if (!nodeName.StartsWith("BSP_mg", StringComparison.OrdinalIgnoreCase)) continue;
            var m = Regex.Match(nodeName, @"mg(\d+)", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            int mgId = int.Parse(m.Groups[1].Value);
            if (mgId < 0 || mgId >= bsp.MatGroups.Count) continue;
            if (mgId != effectiveTargetMgId) continue;
            var mg = bsp.MatGroups[mgId];
            if (mg.ObjectId > 0)
                throw new InvalidOperationException($"[GLB-INSERT] strict-v1: mg{mgId} has ObjectId={mg.ObjectId}, insert blocked.");
            templateMg ??= mg;
            bool isBspNode = true;
            insertedMeshes++;
            Console.WriteLine($"[GLB-INSERT] Source mesh node: {nodeName} -> mg{mgId}");

            // Merge all primitives first; then append only triangles beyond baseline face count.
            var mergedPos = new List<System.Numerics.Vector3>();
            var mergedUv = new List<System.Numerics.Vector2>();
            var mergedTri = new List<(int A, int B, int C)>();
            foreach (var prim in node.Mesh!.Primitives)
            {
                var pos = prim.GetVertexAccessor("POSITION")?.AsVector3Array().ToArray();
                if (pos == null || pos.Length < 3) continue;
                var uv = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array().ToArray();
                int posBase = mergedPos.Count;
                for (int i = 0; i < pos.Length; i++)
                {
                    mergedPos.Add(new System.Numerics.Vector3(pos[i].X, pos[i].Y, pos[i].Z));
                    if (uv != null && i < uv.Length) mergedUv.Add(new System.Numerics.Vector2(uv[i].X, uv[i].Y));
                    else mergedUv.Add(default);
                }
                foreach (var t in prim.GetTriangleIndices())
                    mergedTri.Add((posBase + t.A, posBase + t.B, posBase + t.C));
            }
            if (mergedPos.Count < 3 || mergedTri.Count == 0) continue;

            int triStart = isBspNode ? Math.Min(mergedTri.Count, (int)mg.FaceNum) : 0;
            for (int i = triStart; i < mergedTri.Count; i++)
            {
                if (insertedFaces >= maxInsertFaces) break;
                var t = mergedTri[i];
                uint v0 = (uint)fverts.Count;
                fverts.Add(new Vector3f { X = mergedPos[t.A].X, Y = mergedPos[t.A].Y, Z = mergedPos[t.A].Z });
                uvs.Add(new Vector2f { X = mergedUv[t.A].X, Y = mergedUv[t.A].Y });
                colors.Add(0xffffffff);

                uint v1 = (uint)fverts.Count;
                fverts.Add(new Vector3f { X = mergedPos[t.B].X, Y = mergedPos[t.B].Y, Z = mergedPos[t.B].Z });
                uvs.Add(new Vector2f { X = mergedUv[t.B].X, Y = mergedUv[t.B].Y });
                colors.Add(0xffffffff);

                uint v2 = (uint)fverts.Count;
                fverts.Add(new Vector3f { X = mergedPos[t.C].X, Y = mergedPos[t.C].Y, Z = mergedPos[t.C].Z });
                uvs.Add(new Vector2f { X = mergedUv[t.C].X, Y = mergedUv[t.C].Y });
                colors.Add(0xffffffff);

                uint start = (uint)vertexIds.Count;
                vertexIds.Add(v0);
                vertexIds.Add(v1);
                vertexIds.Add(v2);
                faces.Add(new BspReadFace { VertexCount = 3, VertexStartId = start });
                appendedFaceIds.Add((uint)(faces.Count - 1));
                insertedFaces++;
            }
        }
        if (insertedFaces == 0) throw new Exception("No insertable non-BSP mesh found in GLB.");
        Console.WriteLine($"[GLB-INSERT] Inserted meshes: {insertedMeshes}, faces: {insertedFaces}");

        var entries = new List<(string name, byte[] data, bool isArray)>();
        var originalHeaderEntries = new (uint off, uint size)[87];
        {
            int h = 4;
            for (int i = 0; i < 87; i++)
            {
                uint o = BitConverter.ToUInt32(src, h + i * 8);
                uint s = BitConverter.ToUInt32(src, h + i * 8 + 4);
                originalHeaderEntries[i] = (o, s);
            }
        }
        var mgs = bsp.MatGroups.Select(x => new BspReadMatGroup
        {
            Attr = x.Attr,
            FaceNum = x.FaceNum,
            FaceStartId = x.FaceStartId,
            MtlId = x.MtlId,
            LgtId = x.LgtId,
            BbMin = x.BbMin,
            BbMax = x.BbMax,
            Pos = x.Pos,
            Scale = x.Scale,
            ObjectId = x.ObjectId
        }).ToList();
        if (insertedFaces > 0 && templateMg != null)
        {
            if (newMgMode)
            {
                uint start = (uint)faceIds.Count;
                faceIds.AddRange(appendedFaceIds);
                var t = templateMg;
                mgs.Add(new BspReadMatGroup
                {
                    Attr = t.Attr,
                    FaceNum = (ushort)Math.Min(ushort.MaxValue, insertedFaces),
                    FaceStartId = start,
                    MtlId = t.MtlId,
                    LgtId = t.LgtId,
                    BbMin = t.BbMin,
                    BbMax = t.BbMax,
                    Pos = t.Pos,
                    Scale = t.Scale,
                    ObjectId = 0
                });
            }
            else
            {
                int mgIndex = effectiveTargetMgId;
                if (mgIndex < 0 || mgIndex >= mgs.Count)
                    throw new InvalidOperationException($"[GLB-INSERT] strict-v2: target mg index out of range: {mgIndex}");
                var targetMg = mgs[mgIndex];
                int insertAt = checked((int)targetMg.FaceStartId + targetMg.FaceNum);
                if (insertAt < 0 || insertAt > faceIds.Count)
                    throw new InvalidOperationException($"[GLB-INSERT] strict-v2: faceId splice index out of range: {insertAt}/{faceIds.Count}");

                faceIds.InsertRange(insertAt, appendedFaceIds);

                uint grown = (uint)insertedFaces;
                uint newFaceNum = (uint)targetMg.FaceNum + grown;
                mgs[mgIndex] = new BspReadMatGroup
                {
                    Attr = targetMg.Attr,
                    FaceNum = (ushort)Math.Min(ushort.MaxValue, newFaceNum),
                    FaceStartId = targetMg.FaceStartId,
                    MtlId = targetMg.MtlId,
                    LgtId = targetMg.LgtId,
                    BbMin = targetMg.BbMin,
                    BbMax = targetMg.BbMax,
                    Pos = targetMg.Pos,
                    Scale = targetMg.Scale,
                    ObjectId = targetMg.ObjectId
                };

                for (int i = 0; i < mgs.Count; i++)
                {
                    if (i == mgIndex) continue;
                    if (mgs[i].FaceStartId >= (uint)insertAt)
                    {
                        var mg = mgs[i];
                        mgs[i] = new BspReadMatGroup
                        {
                            Attr = mg.Attr,
                            FaceNum = mg.FaceNum,
                            FaceStartId = mg.FaceStartId + grown,
                            MtlId = mg.MtlId,
                            LgtId = mg.LgtId,
                            BbMin = mg.BbMin,
                            BbMax = mg.BbMax,
                            Pos = mg.Pos,
                            Scale = mg.Scale,
                            ObjectId = mg.ObjectId
                        };
                    }
                }
            }
        }
        byte[] BuildRaw(BspEntry e){ var b = new byte[e.Size]; SysBuffer.BlockCopy(src, (int)e.Offset, b, 0, (int)e.Size); return b; }
        byte[] PackFv(){ var b=new byte[fverts.Count*12]; for(int i=0;i<fverts.Count;i++){SysBuffer.BlockCopy(BitConverter.GetBytes(fverts[i].X),0,b,i*12+0,4);SysBuffer.BlockCopy(BitConverter.GetBytes(fverts[i].Y),0,b,i*12+4,4);SysBuffer.BlockCopy(BitConverter.GetBytes(fverts[i].Z),0,b,i*12+8,4);} return b; }
        byte[] PackUv(){ var b=new byte[uvs.Count*8]; for(int i=0;i<uvs.Count;i++){SysBuffer.BlockCopy(BitConverter.GetBytes(uvs[i].X),0,b,i*8+0,4);SysBuffer.BlockCopy(BitConverter.GetBytes(uvs[i].Y),0,b,i*8+4,4);} return b; }
        byte[] PackU32(List<uint> a){ var b=new byte[a.Count*4]; for(int i=0;i<a.Count;i++) SysBuffer.BlockCopy(BitConverter.GetBytes(a[i]),0,b,i*4,4); return b; }
        byte[] PackFaces(){ var b=new byte[faces.Count*6]; for(int i=0;i<faces.Count;i++){SysBuffer.BlockCopy(BitConverter.GetBytes(faces[i].VertexCount),0,b,i*6+0,2);SysBuffer.BlockCopy(BitConverter.GetBytes(faces[i].VertexStartId),0,b,i*6+2,4);} return b; }
        byte[] PackMatGroups(){ var b=new byte[mgs.Count*42]; for(int i=0;i<mgs.Count;i++){int o=i*42;SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].Attr),0,b,o+0,2);SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].FaceNum),0,b,o+2,2);SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].FaceStartId),0,b,o+4,4);SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].MtlId),0,b,o+8,2);SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].LgtId),0,b,o+10,2);SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].BbMin.X),0,b,o+12,2);SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].BbMin.Y),0,b,o+14,2);SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].BbMin.Z),0,b,o+16,2);SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].BbMax.X),0,b,o+18,2);SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].BbMax.Y),0,b,o+20,2);SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].BbMax.Z),0,b,o+22,2);SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].Pos.X),0,b,o+24,4);SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].Pos.Y),0,b,o+28,4);SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].Pos.Z),0,b,o+32,4);SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].Scale),0,b,o+36,4);SysBuffer.BlockCopy(BitConverter.GetBytes(mgs[i].ObjectId),0,b,o+40,2);} return b; }
        entries.Add(("CPlanes", BuildRaw(bsp.Header.CPlanes), false)); entries.Add(("CFaceId", BuildRaw(bsp.Header.CFaceId), false)); entries.Add(("Node", BuildRaw(bsp.Header.Node), false)); entries.Add(("Leaf", BuildRaw(bsp.Header.Leaf), false)); entries.Add(("MatListInLeaf", BuildRaw(bsp.Header.MatListInLeaf), false));
        entries.Add(("Object", BuildRaw(bsp.Header.Object), false)); entries.Add(("Track", BuildRaw(bsp.Header.Track), false)); entries.Add(("EventObjectId", BuildRaw(bsp.Header.EventObjectId), false));
        for(int i=0;i<35;i++) entries.Add(($"ReadSpare{i}", BuildRaw(bsp.Header.ReadSpare[i]), false));
        entries.Add(("BVertex", BuildRaw(bsp.Header.BVertex), false)); entries.Add(("WVertex", BuildRaw(bsp.Header.WVertex), false)); entries.Add(("FVertex", PackFv(), false)); entries.Add(("VertexColor", PackU32(colors), false)); entries.Add(("UV", PackUv(), false)); entries.Add(("LgtUV", BuildRaw(bsp.Header.LgtUV), false)); entries.Add(("Face", PackFaces(), false)); entries.Add(("FaceId", PackU32(faceIds), false)); entries.Add(("VertexId", PackU32(vertexIds), false)); entries.Add(("ReadMatGroup", PackMatGroups(), false));
        for(int i=0;i<32;i++) entries.Add(($"FreeSpare{i}", BuildRaw(bsp.Header.FreeSpare[i]), false));

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(bsp.Header.Version);
        long headerPos = ms.Position;
        for (int i = 0; i < 87; i++) { bw.Write((uint)0); bw.Write((uint)0); }
        var offs = new List<(uint o, uint s)>();
        foreach (var e in entries) { uint o = (uint)ms.Position; bw.Write(e.data); offs.Add((o, (uint)e.data.Length)); }
        ms.Position = headerPos;
        for (int i = 0; i < offs.Count; i++)
        {
            var p = offs[i];
            // Keep legacy zero-sized entry offsets bit-compatible with original BSP.
            // RF client appears sensitive to these header sentinel values.
            if (i < originalHeaderEntries.Length &&
                p.s == 0 &&
                originalHeaderEntries[i].size == 0)
            {
                bw.Write(originalHeaderEntries[i].off);
                bw.Write((uint)0);
            }
            else
            {
                bw.Write(p.o);
                bw.Write(p.s);
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outBspPath)!);
        File.WriteAllBytes(outBspPath, ms.ToArray());
        ValidateGeneratedBspStrict(outBspPath);
    }

    private static int ResolveInsertTargetMgId(ModelRoot model, RFMapToolSharp.Collision.BspFile bsp, int targetMgId)
    {
        var deltas = new Dictionary<int, int>();
        foreach (var node in model.LogicalNodes.Where(n => n.Mesh != null))
        {
            var nodeName = !string.IsNullOrWhiteSpace(node.Name) ? node.Name! : node.Mesh!.Name ?? "";
            if (!nodeName.StartsWith("BSP_mg", StringComparison.OrdinalIgnoreCase)) continue;
            var m = Regex.Match(nodeName, @"mg(\d+)", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            int mgId = int.Parse(m.Groups[1].Value);
            if (mgId < 0 || mgId >= bsp.MatGroups.Count) continue;
            int triCount = 0;
            foreach (var prim in node.Mesh!.Primitives) triCount += prim.GetTriangleIndices().Count();
            int extra = triCount - bsp.MatGroups[mgId].FaceNum;
            if (!deltas.TryGetValue(mgId, out var prev) || extra > prev) deltas[mgId] = extra;
        }

        if (targetMgId > 0)
        {
            if (deltas.TryGetValue(targetMgId, out var tExtra) && tExtra > 0) return targetMgId;
            var best = deltas.Where(x => x.Value > 0).OrderByDescending(x => x.Value).FirstOrDefault();
            if (best.Value > 0)
                throw new InvalidOperationException($"[GLB-INSERT] strict-v1: requested mg{targetMgId} has no positive delta; fallback disabled.");
            throw new InvalidOperationException($"[GLB-INSERT] strict-v1: requested mg{targetMgId} has no delta and no candidates.");
        }

        var auto = deltas.Where(x => x.Value > 0).OrderByDescending(x => x.Value).FirstOrDefault();
        return auto.Value > 0 ? auto.Key : targetMgId;
    }
    private static void DumpMgDeltas(string srcBspPath, string glbPath)
    {
        var bsp = RFMapToolSharp.Collision.BspFile.Load(srcBspPath);
        var model = ModelRoot.Load(glbPath);
        var rows = new List<(int mg, int tri, int baseFaces, int delta)>();
        foreach (var node in model.LogicalNodes.Where(n => n.Mesh != null))
        {
            var nodeName = !string.IsNullOrWhiteSpace(node.Name) ? node.Name! : node.Mesh!.Name ?? "";
            if (!nodeName.StartsWith("BSP_mg", StringComparison.OrdinalIgnoreCase)) continue;
            var m = Regex.Match(nodeName, @"mg(\d+)", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            int mgId = int.Parse(m.Groups[1].Value);
            if (mgId < 0 || mgId >= bsp.MatGroups.Count) continue;
            int tri = 0;
            foreach (var prim in node.Mesh!.Primitives) tri += prim.GetTriangleIndices().Count();
            int baseFaces = bsp.MatGroups[mgId].FaceNum;
            rows.Add((mgId, tri, baseFaces, tri - baseFaces));
        }
        foreach (var r in rows.OrderByDescending(x => x.delta).Take(20))
            Console.WriteLine($"[MG-DELTA] mg={r.mg} tri={r.tri} baseFaces={r.baseFaces} delta={r.delta}");
        foreach (var node in model.LogicalNodes.Where(n => n.Mesh != null))
        {
            var nodeName = !string.IsNullOrWhiteSpace(node.Name) ? node.Name! : node.Mesh!.Name ?? "";
            if (!nodeName.StartsWith("BSP_mg", StringComparison.OrdinalIgnoreCase))
            {
                int tri = 0;
                foreach (var prim in node.Mesh!.Primitives) tri += prim.GetTriangleIndices().Count();
                Console.WriteLine($"[NON-BSP-NODE] {nodeName} tri={tri}");
            }
        }
    }

    private static void ValidateGeneratedBspStrict(string bspPath)
    {
        var bsp = RFMapToolSharp.Collision.BspFile.Load(bspPath);
        for (int i = 0; i < bsp.Faces.Count; i++)
        {
            var f = bsp.Faces[i];
            if ((uint)f.VertexStartId + f.VertexCount > bsp.VertexId.Count)
                throw new InvalidDataException($"BSP strict check failed: face[{i}] out of VertexId range.");
        }
        for (int i = 0; i < bsp.MatGroups.Count; i++)
        {
            var mg = bsp.MatGroups[i];
            if ((uint)mg.FaceStartId + mg.FaceNum > bsp.FaceId.Count)
                throw new InvalidDataException($"BSP strict check failed: matgroup[{i}] out of FaceId range.");
        }
        Console.WriteLine("[GLB-INSERT] Strict BSP self-check: OK");
    }

    private static void EmitNonBspSptPatch(string mapName, string glbPath, string outRoot)
    {
        string? mr = FindMapRoot();
        if (mr == null) throw new Exception("Map folder not found.");
        var srcMapDir = Directory.GetDirectories(mr).FirstOrDefault(d => string.Equals(Path.GetFileName(d), mapName, StringComparison.OrdinalIgnoreCase));
        if (srcMapDir == null) throw new Exception($"map '{mapName}' not found in {mr}");
        if (!File.Exists(glbPath)) throw new FileNotFoundException($"GLB not found: {glbPath}");

        var outMapDir = Path.Combine(outRoot, "map", Path.GetFileName(srcMapDir));
        CopyDirectoryRecursive(srcMapDir, outMapDir);

        var model = ModelRoot.Load(glbPath);
        var nonBspNodes = model.LogicalNodes.Where(n => n.Mesh != null)
            .Select(n => new
            {
                Node = n,
                Name = !string.IsNullOrWhiteSpace(n.Name) ? n.Name! : n.Mesh!.Name ?? "noname"
            })
            .Where(x => !x.Name.StartsWith("BSP_mg", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var extSpt = Directory.GetFiles(outMapDir, "*ext.spt", SearchOption.TopDirectoryOnly).FirstOrDefault()
            ?? Path.Combine(outMapDir, $"{Path.GetFileName(outMapDir)}ext.spt");

        var lines = new List<string>();
        if (File.Exists(extSpt)) lines.AddRange(File.ReadAllLines(extSpt));
        lines.Add("");
        lines.Add($"// glb-nonbsp-to-spt import from {Path.GetFileName(glbPath)}");
        int idx = 0;
        foreach (var x in nonBspNodes)
        {
            var t = x.Node.WorldMatrix.Translation;
            lines.Add($"// object dummy import #{idx} node={x.Name}");
            lines.Add($"object {x.Name} {t.X.ToString(System.Globalization.CultureInfo.InvariantCulture)} {t.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)} {t.Z.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            idx++;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(extSpt)!);
        File.WriteAllLines(extSpt, lines);

        var reportPath = Path.Combine(outMapDir, "nonbsp_spt_patch_report.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            map = mapName,
            glb = glbPath,
            extSpt,
            nonBspNodeCount = nonBspNodes.Count,
            nodes = nonBspNodes.Select(x => x.Name).ToArray()
        }, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine($"[NONBSP2SPT] Output map: {outMapDir}");
        Console.WriteLine($"[NONBSP2SPT] ext.spt: {extSpt}");
        Console.WriteLine($"[NONBSP2SPT] non-BSP nodes: {nonBspNodes.Count}");
        Console.WriteLine($"[NONBSP2SPT] report: {reportPath}");
    }

    private static void ApplyR3tOverrides(R3TFile r3t, string overridesDir)
    {
        if (!Directory.Exists(overridesDir)) return;
        var ddsFiles = Directory.GetFiles(overridesDir, "*.dds", SearchOption.TopDirectoryOnly);
        if (ddsFiles.Length == 0) return;
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in ddsFiles)
        {
            var name = Path.GetFileNameWithoutExtension(f);
            if (!byName.ContainsKey(name)) byName[name] = f;
        }

        int replaced = 0;
        foreach (var tex in r3t.Textures)
        {
            if (string.IsNullOrWhiteSpace(tex.Name)) continue;
            if (!byName.TryGetValue(tex.Name, out var file)) continue;
            tex.DdsData = File.ReadAllBytes(file);
            replaced++;
        }
        Console.WriteLine($"[GLB2RF] R3T overrides applied: {replaced}");
    }

    private static void ApplyGlbEmbeddedTexturesToR3t(string glbPath, R3MMaterialFile r3m, R3TFile r3t)
    {
        var model = ModelRoot.Load(glbPath);
        Console.WriteLine($"[GLB2RF] GLB stats: materials={model.LogicalMaterials.Count}, textures={model.LogicalTextures.Count}, images={model.LogicalImages.Count}");
        int updated = 0;
        int matchedMaterials = 0;
        int count = Math.Min(model.LogicalMaterials.Count, r3m.Materials.Count);
        for (int matId = 0; matId < count; matId++)
        {
            var rfMat = r3m.Materials[matId];
            if (rfMat.Layers.Count == 0) continue;
            int surface = rfMat.Layers[0].Surface;
            int texIndex = surface <= 0 ? surface : surface - 1;
            if (texIndex < 0 || texIndex >= r3t.Textures.Count) continue;

            var imgBytes = TryGetGlbBaseColorImageBytes(model.LogicalMaterials[matId]);
            if (imgBytes == null || imgBytes.Length == 0) continue;

            matchedMaterials++;
            try
            {
                var ddsLocked = EncodeImageToRfLockedDds(imgBytes);
                if (ddsLocked.Length > 0)
                {
                    r3t.Textures[texIndex].DdsData = ddsLocked;
                    updated++;
                }
            }
            catch
            {
                // keep original texture if conversion fails
            }
        }
        if (updated == 0 && model.LogicalImages.Count > 0)
        {
            int seq = Math.Min(model.LogicalImages.Count, r3t.Textures.Count);
            int seqUpdated = 0;
            for (int i = 0; i < seq; i++)
            {
                var b = TryExtractImageBytes(model.LogicalImages[i]);
                if (b == null || b.Length == 0) continue;
                try
                {
                    r3t.Textures[i].DdsData = EncodeImageToRfLockedDds(b);
                    seqUpdated++;
                }
                catch { }
            }
            updated += seqUpdated;
            Console.WriteLine($"[GLB2RF] Fallback sequential image mapping used: {seqUpdated}");
        }
        Console.WriteLine($"[GLB2RF] GLB embedded textures applied: materials={matchedMaterials}, updated={updated}");
    }

    private static byte[]? TryGetGlbBaseColorImageBytes(Material mat)
    {
        // Fast path for common SharpGLTF schema.
        try
        {
            var findChannel = mat.GetType().GetMethod("FindChannel", new[] { typeof(string) });
            if (findChannel != null)
            {
                var ch = findChannel.Invoke(mat, new object[] { "BaseColor" });
                var b = TryExtractImageBytesFromChannel(ch);
                if (b != null && b.Length > 0) return b;
            }
        }
        catch { }

        var matType = mat.GetType();
        var channelsProp = matType.GetProperty("Channels");
        if (channelsProp == null) return null;
        var channelsObj = channelsProp.GetValue(mat);
        if (channelsObj is not System.Collections.IEnumerable channels) return null;

        foreach (var ch in channels)
        {
            if (ch == null) continue;
            var chType = ch.GetType();
            var keyProp = chType.GetProperty("Key");
            var key = keyProp?.GetValue(ch)?.ToString() ?? string.Empty;
            if (!key.Contains("BaseColor", StringComparison.OrdinalIgnoreCase)) continue;
            var bytes = TryExtractImageBytesFromChannel(ch);
            if (bytes != null && bytes.Length > 0) return bytes;
        }
        // Fallback: deep reflection lookup for base-color linked image.
        return TryFindImageBytesDeep(mat, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private static byte[]? TryFindImageBytesDeep(object obj, int depth, HashSet<object> visited)
    {
        if (obj == null || depth > 4) return null;
        if (!visited.Add(obj)) return null;
        var t = obj.GetType();
        foreach (var p in t.GetProperties())
        {
            if (!p.CanRead) continue;
            object? v;
            try { v = p.GetValue(obj); } catch { continue; }
            if (v == null) continue;

            if (p.Name.Contains("Image", StringComparison.OrdinalIgnoreCase))
            {
                var b = TryExtractImageBytes(v);
                if (b != null && b.Length > 0) return b;
            }
            if (p.Name.Contains("BaseColor", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Texture", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Channel", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Pbr", StringComparison.OrdinalIgnoreCase))
            {
                var b = TryFindImageBytesDeep(v, depth + 1, visited);
                if (b != null && b.Length > 0) return b;
            }
        }
        return null;
    }

    private static byte[]? TryExtractImageBytesFromChannel(object? channelObj)
    {
        if (channelObj == null) return null;
        var chType = channelObj.GetType();
        var texProp = chType.GetProperty("Texture");
        var texObj = texProp?.GetValue(channelObj);
        if (texObj == null) return null;

        var priImgProp = texObj.GetType().GetProperty("PrimaryImage");
        var imgObj = priImgProp?.GetValue(texObj);
        if (imgObj == null) return null;
        return TryExtractImageBytes(imgObj);
    }

    private static byte[]? TryExtractImageBytes(object imgObj)
    {
        var t = imgObj.GetType();
        var contentProp = t.GetProperty("Content");
        if (contentProp != null)
        {
            var content = contentProp.GetValue(imgObj);
            if (content is byte[] b1) return b1;
            if (content is ArraySegment<byte> seg) return seg.ToArray();
            if (content is ReadOnlyMemory<byte> rom) return rom.ToArray();
            if (content != null)
            {
                var ct = content.GetType();
                var cContentProp = ct.GetProperty("Content");
                if (cContentProp != null)
                {
                    var cc = cContentProp.GetValue(content);
                    if (cc is byte[] cb1) return cb1;
                    if (cc is ArraySegment<byte> cseg) return cseg.ToArray();
                    if (cc is ReadOnlyMemory<byte> crom) return crom.ToArray();
                }
                var cSpanProp = ct.GetProperty("Span");
                if (cSpanProp != null)
                {
                    var sv = cSpanProp.GetValue(content);
                    if (sv is ReadOnlyMemory<byte> srom) return srom.ToArray();
                }
                var toArray = content.GetType().GetMethod("ToArray", Type.EmptyTypes);
                if (toArray != null && toArray.ReturnType == typeof(byte[]))
                {
                    return (byte[]?)toArray.Invoke(content, Array.Empty<object>());
                }
            }
        }

        var opProp = t.GetProperty("Open");
        if (opProp != null && typeof(Func<Stream>).IsAssignableFrom(opProp.PropertyType))
        {
            var fn = (Func<Stream>?)opProp.GetValue(imgObj);
            if (fn != null)
            {
                using var s = fn();
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }
        return null;
    }

    private static byte[] EncodeImageToRfLockedDds(byte[] encodedImageBytes)
    {
        using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(encodedImageBytes);
        int width = img.Width;
        int height = img.Height;
        int rowPitch = width * 4;
        int dataSize = rowPitch * height;

        var ms = new MemoryStream(128 + dataSize);
        using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        bw.Write(Encoding.ASCII.GetBytes("DDS "));
        bw.Write(124u);
        bw.Write(0x0002100Fu); // caps|height|width|pitch|pixelformat
        bw.Write((uint)height);
        bw.Write((uint)width);
        bw.Write((uint)rowPitch);
        bw.Write(0u); // depth
        bw.Write(0u); // mipmaps
        for (int i = 0; i < 11; i++) bw.Write(0u);

        bw.Write(32u);          // pfSize
        bw.Write(0x00000041u);  // DDPF_RGB | DDPF_ALPHAPIXELS
        bw.Write(0u);           // fourCC
        bw.Write(32u);          // RGBBitCount
        bw.Write(0x00FF0000u);  // R
        bw.Write(0x0000FF00u);  // G
        bw.Write(0x000000FFu);  // B
        bw.Write(0xFF000000u);  // A

        bw.Write(0x00001000u);  // DDSCAPS_TEXTURE
        bw.Write(0u);
        bw.Write(0u);
        bw.Write(0u);
        bw.Write(0u);

        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    var px = row[x];
                    bw.Write(px.B);
                    bw.Write(px.G);
                    bw.Write(px.R);
                    bw.Write(px.A);
                }
            }
        });
        bw.Flush();

        var dds = ms.ToArray();
        XorLockR3DdsHeader(dds);
        return dds;
    }

    private static void XorLockR3DdsHeader(byte[] data)
    {
        Span<byte> pwd = stackalloc byte[]
        {
            0x2E,0x80,0x4D,0x76,0x2E,0xF8,0xD1,0xF0,0xBD,0x3F,0x86,0x81,0x58,0x2C,0x3F,0x3F,
            0x2E,0x2E,0x67,0x6F,0x3F,0x40,0x3F,0x78,0x3C,0x3F,0xF1,0xC0,0xA5,0xF6,0x3B,0x9F,
            0xC1,0x20,0x3F,0xD7,0xC8,0xC1,0xE9,0x85,0x86,0xBD,0xEF,0x56,0x3F,0xA1,0xFB,0x2E,
            0x87,0x86,0x61,0x4C,0x21,0x3B,0x4E,0xB4,0x78,0x57,0xAE,0x97,0x3F,0x2E,0x4A,0x2E,
            0x3F,0x4C,0x2E,0x44,0xCD,0xC5,0x5F,0xE8,0xE9,0xEC,0xEB,0xBD,0xBE,0xBB,0xF7,0x6C,
            0x2E,0xF2,0xE4,0x2E,0x3F,0x3F,0x97,0x9F,0x9D,0xB3,0x21,0xB9,0x76,0x65,0x54,0x3F,
            0xE6,0xF6,0xC6,0xF0,0x79,0xDB,0xE2,0xB2,0x4B,0x2E,0x2E,0xEB,0xD3,0xD3,0xCA,0xAB,
            0xEA,0xC7,0xED,0x9C,0xC7,0xD9,0xD0,0x65,0x48,0xB4,0xFA,0x35,0x2E,0x2E,0x6A,0x9B
        };
        int len = Math.Min(128, data.Length);
        for (int i = 0; i < len; i++) data[i] ^= pwd[i];
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}

