using System;
using System.IO;
using RFMapToolSharp.Collision;
using RFMapToolSharp.Materials;
using RFMapToolSharp.Models;
using RFMapToolSharp.Rvp;
using RFMapToolSharp.Spt;
using RFMapToolSharp.Textures;

namespace RFMapToolSharp;

public static class MapLoader
{
    public static MapScene LoadMap(string gameRoot, string mapName)
    {
        var scene = new MapScene
        {
            Name = mapName,
            RootPath = Path.Combine(gameRoot, "map", mapName)
        };

        var mapDir = scene.RootPath;
        if (!Directory.Exists(mapDir))
            throw new DirectoryNotFoundException($"Папка карты не найдена: {mapDir}");

        // R3T
        var r3tPath = Path.Combine(mapDir, mapName + ".r3t");
        if (File.Exists(r3tPath))
        {
            var r3t = R3TFile.Load(r3tPath);
            scene.Textures.AddRange(r3t.Textures);
        }

        // R3M — материалы (пока заглушка)
        var r3mPath = Path.Combine(mapDir, mapName + ".r3m");
        if (File.Exists(r3mPath))
        {
            scene.MaterialFile = R3MMaterialFile.Load(r3mPath);
        }
        var r3xPath = Path.Combine(mapDir, mapName + ".r3x");
        if (File.Exists(r3xPath))
        {
            scene.MaterialExt = R3XMaterialFile.Load(r3xPath);
        }

        // BSP / EBP
        var bspPath = Path.Combine(mapDir, mapName + ".bsp");
        if (File.Exists(bspPath))
        {
            scene.Bsp = BspFile.Load(bspPath);
        }

        var ebpPath = Path.Combine(mapDir, mapName + ".ebp");
        if (File.Exists(ebpPath))
        {
            scene.ExtBsp = ExtBspFile.Load(ebpPath);
        }

        // SPT
        var sptPath = Path.Combine(mapDir, mapName + ".spt");
        if (File.Exists(sptPath))
        {
            var spt = SptFile.Load(sptPath);
            scene.Particles.AddRange(spt.Particles);
        }

        // RVP
        var rvpPath = Path.Combine(mapDir, mapName + ".rvp");
        if (File.Exists(rvpPath))
        {
            scene.Movie = RvpFile.Load(rvpPath);
        }

        return scene;
    }
}
