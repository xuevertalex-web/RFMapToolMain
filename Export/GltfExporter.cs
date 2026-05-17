using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using RFMapToolSharp.Models;
using RFMapToolSharp.Parsing.Bsp;
using RFMapToolSharp.Parsing; 
using SharpGLTF.Schema2;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Memory;

// Убираем глобальный using SharpGLTF.Materials чтобы не было конфликтов
// Будем указывать типы материалов явно

namespace RFMapToolSharp.Export
{
    using VPOS = SharpGLTF.Geometry.VertexTypes.VertexPositionNormal;
    using VTEX = SharpGLTF.Geometry.VertexTypes.VertexTexture2;
    using VEMPTY = SharpGLTF.Geometry.VertexTypes.VertexEmpty;

    public static class GltfExporter
    {
        public sealed class SptExportOptions
        {
            public string Mode { get; set; } = "markers"; // off|markers
            public bool PivotFix { get; set; } = true;
            public string RotationOrder { get; set; } = "XYZ"; // XYZ|XZY|YXZ|YZX|ZXY|ZYX
            public float ScaleMultiplier { get; set; } = 1.0f;
        }

        public static SptExportOptions SptOptions { get; } = new();

        public static void Export(MapScene scene, string exportDir, string name)
        {
            if (scene.Bsp == null) throw new InvalidOperationException("BSP not loaded.");

            const bool MirrorWorldY = true;
            const bool FlipUV_U = false;
            const bool FlipUV_V = false;

            Console.WriteLine($"[GLTF] Exporting: {name}...");
            Directory.CreateDirectory(exportDir);

            var model = ModelRoot.CreateModel();
            var gltfScene = model.UseScene("Scene");

            var imageCache = new Dictionary<int, MemoryImage>();
            MemoryImage GetOrLoadImage(int texId)
            {
                if (imageCache.TryGetValue(texId, out var img)) return img;
                if (texId < 0 || texId >= scene.Textures.Count) return default;
                try
                {
                    var pngBytes = TextureConverter.ToPngBytes(scene.Textures[texId].DdsData);
                    var newImg = new MemoryImage(pngBytes);
                    imageCache[texId] = newImg;
                    return newImg;
                }
                catch { return default; }
            }

            var materialCache = new Dictionary<int, SharpGLTF.Materials.MaterialBuilder>();
            
            SharpGLTF.Materials.MaterialBuilder GetOrCreateMaterial(int matId)
            {
                if (materialCache.TryGetValue(matId, out var cached)) return cached;

                var matBuilder = new SharpGLTF.Materials.MaterialBuilder($"mat_{matId}")
                    .WithDoubleSide(true)
                    .WithMetallicRoughnessShader()
                    .WithBaseColor(new Vector4(1, 1, 1, 1))
                    .WithMetallicRoughness(0.0f, 0.8f);

                if (scene.MaterialFile != null && matId >= 0 && matId < scene.MaterialFile.Materials.Count)
                {
                    var rfMat = scene.MaterialFile.Materials[matId];
                    if (rfMat.Layers.Count > 0)
                    {
                        var layer0 = rfMat.Layers[0];
                        int texId = layer0.Surface - 1 < 0 ? layer0.Surface : layer0.Surface - 1;

                        if (texId >= 0 && texId < scene.Textures.Count)
                        {
                            var img = GetOrLoadImage(texId);
                            if (!img.IsEmpty)
                            {
                                var wrap = SharpGLTF.Schema2.TextureWrapMode.REPEAT;

                                matBuilder.UseChannel(SharpGLTF.Materials.KnownChannel.BaseColor)
                                          .UseTexture()
                                          .WithPrimaryImage(img)
                                          .WithSampler(wrap, wrap);

                                var tName = scene.Textures[texId].Name?.ToLowerInvariant() ?? "";
                                bool isWater = tName.Contains("water") || tName.Contains("river");
                                bool isGlass = tName.Contains("glass") || tName.Contains("win");

                                // ИСПРАВЛЕНИЕ: Полный путь к AlphaMode
                                if (isWater || isGlass || layer0.AlphaType == 2)
                                {
                                    matBuilder.WithAlpha(SharpGLTF.Materials.AlphaMode.BLEND);
                                    matBuilder.WithMetallicRoughness(0.0f, 0.3f);
                                    if (isWater) matBuilder.WithBaseColor(new Vector4(1, 1, 1, 0.7f));
                                }
                                else if (layer0.AlphaType == 1)
                                {
                                    matBuilder.WithAlpha(SharpGLTF.Materials.AlphaMode.MASK, 0.5f);
                                    matBuilder.WithMetallicRoughness(0.0f, 0.9f);
                                }
                            }
                        }
                    }
                }
                materialCache[matId] = matBuilder;
                return matBuilder;
            }

            var faces = scene.Bsp.RealFaces;
            var pos   = scene.Bsp.Vertices;
            var uv0   = scene.Bsp.RealUv;

            var groups = faces.GroupBy(f => f.MatGroup).OrderBy(g => g.Key);

            foreach (var matGroup in groups)
            {
                var meshBuilder = new MeshBuilder<VPOS, VTEX, VEMPTY>($"MatGroup_{matGroup.Key:D4}");

                foreach (var byMat in matGroup.GroupBy(f => f.MatId))
                {
                    var material = GetOrCreateMaterial(byMat.Key);
                    var prim = meshBuilder.UsePrimitive(material);

                    foreach (var face in byMat)
                    {
                        var p1 = ToVec3(Get(pos, face.A, default), MirrorWorldY);
                        var p2 = ToVec3(Get(pos, face.B, default), MirrorWorldY);
                        var p3 = ToVec3(Get(pos, face.C, default), MirrorWorldY);

                        var normal = Vector3.Normalize(Vector3.Cross(p2 - p1, p3 - p1));

                        // ИСПРАВЛЕНИЕ: Теперь переменные FlipUV используются!
                        var u1 = ToVec2Smart(Get(uv0, face.A, default), FlipUV_U, FlipUV_V);
                        var u2 = ToVec2Smart(Get(uv0, face.B, default), FlipUV_U, FlipUV_V);
                        var u3 = ToVec2Smart(Get(uv0, face.C, default), FlipUV_U, FlipUV_V);

                        var v1 = new VertexBuilder<VPOS, VTEX, VEMPTY>(new VPOS(p1.X, p1.Y, p1.Z, normal.X, normal.Y, normal.Z), new VTEX(u1, u1), new VEMPTY());
                        var v2 = new VertexBuilder<VPOS, VTEX, VEMPTY>(new VPOS(p2.X, p2.Y, p2.Z, normal.X, normal.Y, normal.Z), new VTEX(u2, u2), new VEMPTY());
                        var v3 = new VertexBuilder<VPOS, VTEX, VEMPTY>(new VPOS(p3.X, p3.Y, p3.Z, normal.X, normal.Y, normal.Z), new VTEX(u3, u3), new VEMPTY());

                        if (MirrorWorldY) prim.AddTriangle(v1, v3, v2);
                        else              prim.AddTriangle(v1, v2, v3);
                    }
                }
                var node = gltfScene.CreateNode($"BSP_{matGroup.Key}");
                node.Mesh = model.CreateMesh(meshBuilder);
            }

            // --- SPT (OBJECT MARKERS) ---
            if (!string.Equals(SptOptions.Mode, "off", StringComparison.OrdinalIgnoreCase))
            {
                var debugMesh = CreateDebugCube(model);
                ProcessSpt(scene.RootPath, gltfScene, MirrorWorldY, debugMesh, exportDir);
            }

            model.SaveGLB(Path.Combine(exportDir, $"{name}.glb"));
            Console.WriteLine("[GLTF] Saved!");
        }

        private static void ProcessSpt(string mapRootPath, Scene gltfScene, bool mirrorY, Mesh debugMesh, string exportDir)
        {
            var sptDir = Path.Combine(mapRootPath, "Spt");
            var files = new List<string>();
            var resolveLog = new List<object>();

            if (Directory.Exists(sptDir))
                files.AddRange(Directory.GetFiles(sptDir, "*.spt", SearchOption.TopDirectoryOnly));

            // Some maps keep *.spt directly in the map root (for example, *EXT.spt scripts)
            files.AddRange(Directory.GetFiles(mapRootPath, "*.spt", SearchOption.TopDirectoryOnly));

            files = files
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Console.WriteLine($"[SPT] Candidate files: {files.Count}");
            int count = 0;

            foreach (var file in files)
            {
                var objects = SptMapParser.Parse(file);

                foreach (var obj in objects)
                {
                    var pos = new Vector3(obj.Position.X, mirrorY ? -obj.Position.Y : obj.Position.Y, obj.Position.Z);
                    var node = gltfScene.CreateNode(obj.ModelName);
                    
                    node.Mesh = debugMesh;

                    var objScale = obj.Scale == Vector3.Zero ? Vector3.One : obj.Scale;
                    objScale *= SptOptions.ScaleMultiplier;

                    var transform = Matrix4x4.CreateTranslation(pos);
                    if (objScale != Vector3.One) transform = Matrix4x4.CreateScale(objScale) * transform;

                    var radRot = obj.Rotation * (MathF.PI / 180f);
                    transform = BuildRotation(radRot, SptOptions.RotationOrder) * transform;
                    if (SptOptions.PivotFix)
                    {
                        // RF helper pivot compensation for marker mode.
                        transform = Matrix4x4.CreateTranslation(0, mirrorY ? 0.5f : -0.5f, 0) * transform;
                    }

                    node.LocalTransform = transform;
                    resolveLog.Add(new
                    {
                        SourceFile = file,
                        obj.ModelName,
                        obj.Position,
                        obj.Rotation,
                        Scale = objScale,
                        SptOptions.Mode,
                        SptOptions.PivotFix,
                        SptOptions.RotationOrder,
                        SptOptions.ScaleMultiplier
                    });
                    count++;
                }
            }
            Console.WriteLine($"[SPT] Created markers: {count}");
            var logJson = System.Text.Json.JsonSerializer.Serialize(resolveLog, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(exportDir, "spt_resolve_log.json"), logJson);
        }

        private static Matrix4x4 BuildRotation(Vector3 r, string order)
        {
            var rx = Matrix4x4.CreateRotationX(r.X);
            var ry = Matrix4x4.CreateRotationY(r.Y);
            var rz = Matrix4x4.CreateRotationZ(r.Z);
            return order.ToUpperInvariant() switch
            {
                "XZY" => rx * rz * ry,
                "YXZ" => ry * rx * rz,
                "YZX" => ry * rz * rx,
                "ZXY" => rz * rx * ry,
                "ZYX" => rz * ry * rx,
                _ => rx * ry * rz
            };
        }

        private static Mesh CreateDebugCube(ModelRoot model)
        {
            var meshBuilder = new MeshBuilder<VPOS, VTEX, VEMPTY>("DebugCube");
            var redMat = new SharpGLTF.Materials.MaterialBuilder("RedDebug")
                .WithBaseColor(new Vector4(1, 0, 0, 1))
                .WithDoubleSide(true);
            
            var prim = meshBuilder.UsePrimitive(redMat);

            float s = 50f;
            var p0 = new Vector3(-s, -s, -s); var p1 = new Vector3( s, -s, -s);
            var p2 = new Vector3( s,  s, -s); var p3 = new Vector3(-s,  s, -s);
            var p4 = new Vector3(-s, -s,  s); var p5 = new Vector3( s, -s,  s);
            var p6 = new Vector3( s,  s,  s); var p7 = new Vector3(-s,  s,  s);

            AddQuad(prim, p0, p1, p2, p3);
            AddQuad(prim, p5, p4, p7, p6);
            AddQuad(prim, p3, p2, p6, p7);
            AddQuad(prim, p4, p5, p1, p0);
            AddQuad(prim, p4, p0, p3, p7);
            AddQuad(prim, p1, p5, p6, p2);

            return model.CreateMesh(meshBuilder);
        }

        private static void AddQuad(PrimitiveBuilder<SharpGLTF.Materials.MaterialBuilder, VPOS, VTEX, VEMPTY> prim, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var vt = new VTEX(Vector2.Zero, Vector2.Zero);
            var ve = new VEMPTY();
            var n = Vector3.UnitY;
            var va = new VertexBuilder<VPOS, VTEX, VEMPTY>(new VPOS(a, n), vt, ve);
            var vb = new VertexBuilder<VPOS, VTEX, VEMPTY>(new VPOS(b, n), vt, ve);
            var vc = new VertexBuilder<VPOS, VTEX, VEMPTY>(new VPOS(c, n), vt, ve);
            var vd = new VertexBuilder<VPOS, VTEX, VEMPTY>(new VPOS(d, n), vt, ve);
            prim.AddTriangle(va, vb, vc);
            prim.AddTriangle(va, vc, vd);
        }

        private static Vector3 ToVec3(dynamic v, bool mirrorY) {
            try { return new Vector3((float)v.X, mirrorY ? -(float)v.Y : (float)v.Y, (float)v.Z); } catch { return Vector3.Zero; }
        }
        private static Vector2 ToVec2Smart(dynamic v, bool fU, bool fV) {
            try {
                float x=0, y=0;
                try { x=(float)v.X; y=(float)v.Y; } catch {
                   try { x=(float)v.U; y=(float)v.V; } catch {
                       try { x=(float)v.Tu; y=(float)v.Tv; } catch {}
                   }
                }
                return new Vector2(fU ? 1f - x : x, fV ? 1f - y : y);
            } catch { return Vector2.Zero; }
        }
        private static T Get<T>(IReadOnlyList<T> l, int i, T d) => (i>=0 && i<l.Count) ? l[i] : d;
    }
}
