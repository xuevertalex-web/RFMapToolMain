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
        bool setteCleanIsolated = args.Any(a => string.Equals(a, "--sette-clean-isolated", StringComparison.OrdinalIgnoreCase));
        bool setteRaw = args.Any(a => string.Equals(a, "--sette-raw", StringComparison.OrdinalIgnoreCase));
        bool setteDonorPath = args.Any(a => string.Equals(a, "--sette-donor-path", StringComparison.OrdinalIgnoreCase));
        string sptMode = "markers";
        bool sptPivotFix = true;
        string sptRotOrder = "XYZ";
        float sptScaleMultiplier = 1.0f;
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
                MapScene? donorScene = null;
                // Sette requires legacy-like handling for Attr=8192 object groups.
                RFMapToolSharp.Collision.BspFile.SkipTransformForAttr8192 =
                    !string.Equals(mapName, "Sette", StringComparison.OrdinalIgnoreCase);

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


                GltfExporter.Export(scene, targetDir, mapName);
                if (setteDonorPath && string.Equals(mapName, "Sette", StringComparison.OrdinalIgnoreCase))
                {
                    string donorRootDir = Path.Combine(exportRoot, "Sette_Donor");
                    Directory.CreateDirectory(donorRootDir);
                    Console.WriteLine("[INFO] --sette-donor-path enabled: exporting isolated Sette donor output.");
                    donorScene = new MapScene
                    {
                        Name = mapName,
                        RootPath = dir,
                        MaterialFile = scene.MaterialFile,
                        Textures = scene.Textures
                    };
                    RFMapToolSharp.Collision.BspFile.SetteDonorPathMode = true;
                    donorScene.Bsp = RFMapToolSharp.Collision.BspFile.Load(bspPath);
                    GltfExporter.Export(donorScene, donorRootDir, "Sette_Donor");
                    RFMapToolSharp.Collision.BspFile.SetteDonorPathMode = false;
                }
                try
                {
                    scene.Bsp?.WriteBrokenFacesReport(Path.Combine(targetDir, "broken_faces.json"));
                    scene.Bsp?.WriteObjectMatricesReport(Path.Combine(targetDir, "object_matrices.json"));
                    scene.Bsp?.WriteAnimatedObjectsReport(Path.Combine(targetDir, "animated_objects.json"));
                    scene.Bsp?.WriteMatGroupDebugReport(Path.Combine(targetDir, "matgroup_debug.json"));
                    if (setteDonorPath && string.Equals(mapName, "Sette", StringComparison.OrdinalIgnoreCase))
                    {
                        donorScene?.Bsp?.WriteMg91BorderStitchLog(Path.Combine(exportRoot, "Sette_Donor", "mg91_border_stitch_log.json"));
                        donorScene?.Bsp?.WriteMg91DonorInjectionReport(Path.Combine(exportRoot, "Sette_Donor", "mg91_donor_injection_report.json"));
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
}

