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
using RFMapToolSharp.Collision;  // BspFile (структура для сцены)
using RFMapToolSharp.Materials;

class Program
{
    private const string ConfigFile = "rf_path.txt";

    static void Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== RF ONLINE MAP BATCH CONVERTER ===\n");
        Console.ResetColor();

        string? mapRoot = FindMapRoot();
        if (mapRoot == null)
        {
            Console.WriteLine("ОШИБКА: Папка 'Map' не найдена.");
            Console.ReadKey();
            return;
        }
        Console.WriteLine($"Найдена папка map: {mapRoot}\n");

        // Ищем корень проекта RFMapToolSharp (чтобы не лезть в bin\Debug)
        var exeDir = AppContext.BaseDirectory;
        var current = new DirectoryInfo(exeDir);
        string rootDir = exeDir;

        while (current != null)
        {
            if (string.Equals(current.Name, "RFMapToolSharp", StringComparison.OrdinalIgnoreCase))
            {
                rootDir = current.FullName;
                break;
            }
            current = current.Parent;
        }

        // Экспорт всегда в RFMapToolSharp\RF_Release
        var exportRoot = Path.Combine(rootDir, "RF_Release");
        Directory.CreateDirectory(exportRoot);



        var mapDirs = Directory.GetDirectories(mapRoot);
        Console.WriteLine($"Найдено карт: {mapDirs.Length}.\n");

        // === РЕЖИМ ВЫБОРА КАРТ ===
        string[] mapsToProcess = mapDirs;

        Console.WriteLine("Выбери режим:");
        Console.WriteLine("  1 - экспорт всех карт");
        Console.WriteLine("  2 - экспорт одной карты (по номеру)");
        Console.WriteLine("  3 - экспорт по имени / части имени");
        Console.Write("Режим (Enter = 1): ");

        var modeInput = Console.ReadLine();
        int mode = 1;
        int.TryParse(modeInput, out mode);
        if (mode < 1 || mode > 3) mode = 1;

        if (mode == 2)
        {
            Console.WriteLine("\nСписок карт:");
            for (int i = 0; i < mapDirs.Length; i++)
                Console.WriteLine($"{i + 1,2}. {Path.GetFileName(mapDirs[i])}");

            Console.Write("\nВведите номер карты: ");
            var sel = Console.ReadLine();
            if (!int.TryParse(sel, out int idx) || idx < 1 || idx > mapDirs.Length)
            {
                Console.WriteLine("Неверный номер, выходим.");
                Console.ReadKey();
                return;
            }

            mapsToProcess = new[] { mapDirs[idx - 1] };
            Console.WriteLine($"\nВыбрана карта: {Path.GetFileName(mapsToProcess[0])}\n");
        }
        else if (mode == 3)
        {
            Console.Write("\nВведите имя или часть имени карты: ");
            var filter = (Console.ReadLine() ?? "").Trim();

            if (string.IsNullOrEmpty(filter))
            {
                Console.WriteLine("Пустой фильтр, выходим.");
                Console.ReadKey();
                return;
            }

            mapsToProcess = mapDirs
                .Where(d => Path.GetFileName(d)
                    .IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            if (mapsToProcess.Length == 0)
            {
                Console.WriteLine("По фильтру карты не найдены.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"\nБудут экспортированы {mapsToProcess.Length} карт(ы):");
            foreach (var d in mapsToProcess)
                Console.WriteLine(" - " + Path.GetFileName(d));
            Console.WriteLine();
        }

        int success = 0;

        foreach (var dir in mapsToProcess)
        {

            string mapName = Path.GetFileName(dir);
            string bspPath = FindBspFile(dir);

            if (bspPath == null) continue;

            Console.WriteLine($"---> Обработка: {mapName}");

            try
            {
                var scene = new MapScene
                {
                    Name = mapName,
                    RootPath = dir
                };

                /// === 1. BSP ===
                scene.Bsp = RFMapToolSharp.Collision.BspFile.Load(bspPath);

                // === 2. МАТЕРИАЛЫ ===
                string materialPath = Path.Combine(dir, "materials.r3m");

                // если materials.r3m нет – ищем любой *.r3m внутри папки карты
                if (!File.Exists(materialPath))
                {
                    // Ищем во всех подпапках текущей карты
                    var r3mFiles = Directory.GetFiles(dir, "*.r3m", SearchOption.AllDirectories);

                    if (r3mFiles.Length > 0)
                    {
                        // Сначала пытаемся найти файл с именем карты: mapName.r3m
                        materialPath =
                            r3mFiles.FirstOrDefault(p =>
                                string.Equals(
                                    Path.GetFileNameWithoutExtension(p),
                                    mapName,
                                    StringComparison.OrdinalIgnoreCase))
                            // если такого нет – берём первый попавшийся .r3m
                            ?? r3mFiles[0];
                    }
                }

                if (File.Exists(materialPath))
                {
                    Console.WriteLine($"[DEBUG] {mapName}: R3M = {Path.GetFileName(materialPath)}");
                    scene.MaterialFile = R3MMaterialFile.Load(materialPath);
                }
                else
                {
                    Console.WriteLine($"[WARNING] {mapName}: .r3m не найден, будут белые материалы.");
                    scene.MaterialFile = new R3MMaterialFile();
                }

                // === 3. ТЕКСТУРЫ ===
                string texturePath = Path.Combine(dir, $"{mapName}.r3t");

                scene.Textures = new List<RFMapToolSharp.Textures.R3TTextureEntry>();

                // если mapName.r3t нет — берём первый попавшийся *.r3t
                if (!File.Exists(texturePath))
                {
                    var r3tFiles = Directory.GetFiles(dir, "*.r3t");
                    if (r3tFiles.Length > 0)
                        texturePath = r3tFiles[0];
                }

                if (File.Exists(texturePath))
                {
                    var r3tFile = RFMapToolSharp.Textures.R3TFile.Load(texturePath);
                    scene.Textures.AddRange(r3tFile.Textures);
                }

                // === 4. ЭКСПОРТ ===
                string targetDir = Path.Combine(exportRoot, mapName);
                Directory.CreateDirectory(targetDir);

                string sourceSpt = Path.Combine(dir, "Spt");
                string destSpt = Path.Combine(targetDir, "Spt");
                CopySptFolder(sourceSpt, destSpt);

                int texCount = scene.Textures?.Count ?? 0;
                int matCount = scene.MaterialFile?.Materials?.Count ?? 0;

                Console.WriteLine($"[DEBUG] {mapName}: BSP={(scene.Bsp != null)}, Mats={matCount}, Textures={texCount}");


                GltfExporter.Export(scene, targetDir, mapName);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[OK] {mapName} завершена.");
                Console.ResetColor();
                success++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] Ошибка {mapName}: {ex.Message}");
                Console.ResetColor();
            }
        }

        Console.WriteLine($"\nОбработка завершена. Успешно экспортировано карт: {success} из {mapDirs.Length}.");
        Console.WriteLine($"Экспортированные карты находятся в папке: {exportRoot}");
        Console.WriteLine("Нажми кнопку и уёбывай...");
        Console.ReadKey();
    }

    static string? FindMapRoot()
    {
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 4 && dir != null; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "map"))) return Path.Combine(dir, "map");
            if (Directory.Exists(Path.Combine(dir, "Map"))) return Path.Combine(dir, "Map");
            dir = Directory.GetParent(dir)?.FullName;
        }
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