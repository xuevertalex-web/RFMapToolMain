using RFMapToolSharp.Collision;
using RFMapToolSharp.Materials;
using RFMapToolSharp.Models;
using RFMapToolSharp.Textures;

namespace RFMapToolSharp.Export;

public static class SetteCleanExporter
{
    public static bool Run(string mapRoot, string exportRoot)
    {
        // Raw baseline mode: no map-specific adjustments.
        BspFile.SkipTransformForAttr8192 = false;
        BspFile.DisableObjectTransform = true;

        GltfExporter.SptOptions.Mode = "off";
        GltfExporter.FilterStretchedFaces = false;
        GltfExporter.FilterUvAnomalyFaces = false;
        GltfExporter.FilterNormalAnomalyFaces = false;

        var setteDir = Directory.GetDirectories(mapRoot)
            .FirstOrDefault(d => string.Equals(Path.GetFileName(d), "Sette", StringComparison.OrdinalIgnoreCase));
        if (setteDir == null) return false;

        var bspPath = Directory.GetFiles(setteDir, "*.bsp", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), "Sette.bsp", StringComparison.OrdinalIgnoreCase))
            ?? Directory.GetFiles(setteDir, "*.bsp", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (bspPath == null) return false;

        var scene = new MapScene
        {
            Name = "Sette",
            RootPath = setteDir,
            Bsp = BspFile.Load(bspPath),
            MaterialFile = new R3MMaterialFile(),
            Textures = new List<R3TTextureEntry>()
        };

        var materialsPath = Path.Combine(setteDir, "materials.r3m");
        var setteR3mPath = Path.Combine(setteDir, "Sette.r3m");
        if (File.Exists(materialsPath)) scene.MaterialFile = R3MMaterialFile.Load(materialsPath);
        else if (File.Exists(setteR3mPath)) scene.MaterialFile = R3MMaterialFile.Load(setteR3mPath);

        var r3tPath = Path.Combine(setteDir, "Sette.r3t");
        if (!File.Exists(r3tPath))
        {
            var r3tFiles = Directory.GetFiles(setteDir, "*.r3t", SearchOption.TopDirectoryOnly);
            if (r3tFiles.Length > 0) r3tPath = r3tFiles[0];
        }
        if (File.Exists(r3tPath))
        {
            var r3t = R3TFile.Load(r3tPath);
            scene.Textures.AddRange(r3t.Textures);
        }

        var outDir = Path.Combine(exportRoot, "Sette_Clean_Isolated");
        Directory.CreateDirectory(outDir);
        GltfExporter.Export(scene, outDir, "Sette_Clean_Isolated");
        scene.Bsp.WriteBrokenFacesReport(Path.Combine(outDir, "broken_faces.json"));
        scene.Bsp.WriteMatGroupDebugReport(Path.Combine(outDir, "matgroup_debug.json"));
        scene.Bsp.WriteObjectMatricesReport(Path.Combine(outDir, "object_matrices.json"));
        return true;
    }
}
