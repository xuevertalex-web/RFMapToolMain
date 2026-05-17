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
using System.Text.Json;
using System.Text.Json.Serialization;

// Убираем глобальный using SharpGLTF.Materials чтобы не было конфликтов
// Будем указывать типы материалов явно

namespace RFMapToolSharp.Export
{
    using VPOS = SharpGLTF.Geometry.VertexTypes.VertexPositionNormal;
    using VTEX = SharpGLTF.Geometry.VertexTypes.VertexTexture2;
    using VEMPTY = SharpGLTF.Geometry.VertexTypes.VertexEmpty;

    public static class GltfExporter
    {
        private static readonly JsonSerializerOptions SafeJson = new()
        {
            WriteIndented = true,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };
        public sealed class SptExportOptions
        {
            public string Mode { get; set; } = "markers"; // off|markers|real-if-supported
            public bool PivotFix { get; set; } = true;
            public string RotationOrder { get; set; } = "XYZ"; // XYZ|XZY|YXZ|YZX|ZXY|ZYX
            public float ScaleMultiplier { get; set; } = 1.0f;
        }

        public static SptExportOptions SptOptions { get; } = new();
        public static bool FilterStretchedFaces { get; set; } = true;
        public static bool FilterUvAnomalyFaces { get; set; } = true;
        public static bool FilterNormalAnomalyFaces { get; set; } = true;

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
            var stretchedFaces = new List<object>();
            var uvAnomalyFaces = new List<object>();
            var normalAnomalyFaces = new List<object>();
            var bspNodeIndex = new List<object>();

            foreach (var matGroup in groups)
            {
                var groupFaceNormals = new List<Vector3>();
                foreach (var f in matGroup)
                {
                    var gp1 = ToVec3(Get(pos, f.A, default), MirrorWorldY);
                    var gp2 = ToVec3(Get(pos, f.B, default), MirrorWorldY);
                    var gp3 = ToVec3(Get(pos, f.C, default), MirrorWorldY);
                    var gn = Vector3.Cross(gp2 - gp1, gp3 - gp1);
                    if (gn.LengthSquared() > 1e-8f) groupFaceNormals.Add(Vector3.Normalize(gn));
                }
                var groupNormal = Vector3.Zero;
                foreach (var n in groupFaceNormals) groupNormal += n;
                if (groupNormal.LengthSquared() > 1e-8f) groupNormal = Vector3.Normalize(groupNormal);

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

                        if (IsStretchedTriangle(p1, p2, p3, out var maxEdge, out var minEdge, out var area))
                        {
                            stretchedFaces.Add(new
                            {
                                face.MatGroup,
                                face.MatId,
                                face.A,
                                face.B,
                                face.C,
                                MaxEdge = maxEdge,
                                MinEdge = minEdge,
                                Area = area
                            });
                            if (FilterStretchedFaces) continue;
                        }

                        var normal = Vector3.Normalize(Vector3.Cross(p2 - p1, p3 - p1));
                        if (groupNormal.LengthSquared() > 1e-8f)
                        {
                            var dot = Vector3.Dot(normal, groupNormal);
                            if (dot < -0.35f)
                            {
                                normalAnomalyFaces.Add(new
                                {
                                    face.MatGroup,
                                    face.MatId,
                                    face.A,
                                    face.B,
                                    face.C,
                                    Dot = dot
                                });
                                if (FilterNormalAnomalyFaces) continue;
                            }
                        }

                        // ИСПРАВЛЕНИЕ: Теперь переменные FlipUV используются!
                        var u1 = ToVec2Smart(Get(uv0, face.A, default), FlipUV_U, FlipUV_V);
                        var u2 = ToVec2Smart(Get(uv0, face.B, default), FlipUV_U, FlipUV_V);
                        var u3 = ToVec2Smart(Get(uv0, face.C, default), FlipUV_U, FlipUV_V);

                        if (IsUvAnomalyTriangle(p1, p2, p3, u1, u2, u3, out var worldMax, out var uvMax, out var uvRatio))
                        {
                            uvAnomalyFaces.Add(new
                            {
                                face.MatGroup,
                                face.MatId,
                                face.A,
                                face.B,
                                face.C,
                                WorldMax = worldMax,
                                UvMax = uvMax,
                                UvRatio = float.IsFinite(uvRatio) ? uvRatio : 999999f
                            });
                            if (FilterUvAnomalyFaces) continue;
                        }

                        var v1 = new VertexBuilder<VPOS, VTEX, VEMPTY>(new VPOS(p1.X, p1.Y, p1.Z, normal.X, normal.Y, normal.Z), new VTEX(u1, u1), new VEMPTY());
                        var v2 = new VertexBuilder<VPOS, VTEX, VEMPTY>(new VPOS(p2.X, p2.Y, p2.Z, normal.X, normal.Y, normal.Z), new VTEX(u2, u2), new VEMPTY());
                        var v3 = new VertexBuilder<VPOS, VTEX, VEMPTY>(new VPOS(p3.X, p3.Y, p3.Z, normal.X, normal.Y, normal.Z), new VTEX(u3, u3), new VEMPTY());

                        if (MirrorWorldY) prim.AddTriangle(v1, v3, v2);
                        else              prim.AddTriangle(v1, v2, v3);
                    }
                }
                int mgId = matGroup.Key;
                int mgMtlId = -1;
                int mgObjectId = -1;
                int mgAttr = -1;
                if (scene.Bsp.MatGroups != null && mgId >= 0 && mgId < scene.Bsp.MatGroups.Count)
                {
                    var mg = scene.Bsp.MatGroups[mgId];
                    mgMtlId = mg.MtlId;
                    mgObjectId = mg.ObjectId;
                    mgAttr = mg.Attr;
                }
                var nodeName = $"BSP_mg{mgId}_mtl{mgMtlId}_obj{mgObjectId}_attr{mgAttr}";
                var node = gltfScene.CreateNode(nodeName);
                node.Mesh = model.CreateMesh(meshBuilder);
                bspNodeIndex.Add(new
                {
                    NodeName = nodeName,
                    MatGroup = mgId,
                    MtlId = mgMtlId,
                    ObjectId = mgObjectId,
                    Attr = mgAttr,
                    TriangleCount = matGroup.Count()
                });
            }

            // --- SPT (OBJECT MARKERS) ---
            if (!string.Equals(SptOptions.Mode, "off", StringComparison.OrdinalIgnoreCase))
            {
                var debugMesh = CreateDebugCube(model);
                ProcessSpt(scene.RootPath, gltfScene, MirrorWorldY, debugMesh, exportDir);
            }

            model.SaveGLB(Path.Combine(exportDir, $"{name}.glb"));
            var stretchJson = JsonSerializer.Serialize(stretchedFaces, SafeJson);
            File.WriteAllText(Path.Combine(exportDir, "stretched_faces.json"), stretchJson);
            var uvJson = JsonSerializer.Serialize(uvAnomalyFaces, SafeJson);
            File.WriteAllText(Path.Combine(exportDir, "uv_anomaly_faces.json"), uvJson);
            var nJson = JsonSerializer.Serialize(normalAnomalyFaces, SafeJson);
            File.WriteAllText(Path.Combine(exportDir, "normal_anomaly_faces.json"), nJson);
            var idxJson = JsonSerializer.Serialize(bspNodeIndex, SafeJson);
            File.WriteAllText(Path.Combine(exportDir, "bsp_node_index.json"), idxJson);
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
                    bool isExtScript = Path.GetFileName(file).EndsWith("ext.spt", StringComparison.OrdinalIgnoreCase);
                    var pos = new Vector3(obj.Position.X, mirrorY ? -obj.Position.Y : obj.Position.Y, obj.Position.Z);
                    if (!IsFinite(pos)) pos = Vector3.Zero;
                    var node = gltfScene.CreateNode(obj.ModelName);
                    bool usedRealMesh = false;
                    node.Mesh = debugMesh;
                    if (string.Equals(SptOptions.Mode, "real-if-supported", StringComparison.OrdinalIgnoreCase))
                    {
                        var resolved = ResolveModelPath(mapRootPath, obj.ModelName);
                        if (resolved != null)
                        {
                            if (SptModelBridge.TryCreateMesh(gltfScene.LogicalParent, resolved, out var realMesh, out var reason) && realMesh != null)
                            {
                                node.Mesh = realMesh;
                                usedRealMesh = true;
                            }
                            else
                            {
                                resolveLog.Add(new
                                {
                                    SourceFile = file,
                                    obj.ModelName,
                                    ResolvedModelPath = resolved,
                                    MeshLoadError = reason
                                });
                            }
                        }
                    }

                    var objScale = obj.Scale;
                    if (!IsFinite(objScale) || objScale.X <= 0 || objScale.Y <= 0 || objScale.Z <= 0 || objScale.X > 1000 || objScale.Y > 1000 || objScale.Z > 1000)
                    {
                        objScale = Vector3.One;
                    }
                    objScale *= SptOptions.ScaleMultiplier;

                    var transform = Matrix4x4.CreateTranslation(pos);
                    if (!isExtScript && objScale != Vector3.One)
                    {
                        transform = Matrix4x4.CreateScale(objScale) * transform;
                    }
                    if (SptOptions.PivotFix)
                    {
                        // RF helper pivot compensation for marker mode.
                        transform = Matrix4x4.CreateTranslation(0, mirrorY ? 0.5f : -0.5f, 0) * transform;
                    }

                    if (!IsFinite(transform))
                    {
                        transform = Matrix4x4.CreateTranslation(pos);
                    }

                    try
                    {
                        node.LocalTransform = transform;
                    }
                    catch
                    {
                        node.LocalTransform = Matrix4x4.CreateTranslation(pos);
                    }
                    resolveLog.Add(new
                    {
                        SourceFile = file,
                        obj.ModelName,
                        UsedRealMesh = usedRealMesh,
                        ResolvedModelPath = ResolveModelPath(mapRootPath, obj.ModelName),
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
            var logJson = JsonSerializer.Serialize(resolveLog, SafeJson);
            File.WriteAllText(Path.Combine(exportDir, "spt_resolve_log.json"), logJson);
        }

        private static string? ResolveModelPath(string mapRootPath, string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName)) return null;
            var name = modelName.Trim().Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            var exts = new[] { ".msh", ".mod", ".obj", ".fbx", ".glb", ".gltf" };
            var roots = new[]
            {
                mapRootPath,
                Path.Combine(mapRootPath, "Spt"),
                Directory.GetParent(mapRootPath)?.FullName ?? mapRootPath
            }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            foreach (var root in roots)
            {
                foreach (var ext in exts)
                {
                    var p1 = Path.Combine(root, name + ext);
                    if (File.Exists(p1)) return p1;
                    var p2 = Path.Combine(root, Path.GetFileName(name) + ext);
                    if (File.Exists(p2)) return p2;
                }
            }
            return null;
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

        private static bool IsFinite(Vector3 v)
        {
            return !(float.IsNaN(v.X) || float.IsInfinity(v.X) ||
                     float.IsNaN(v.Y) || float.IsInfinity(v.Y) ||
                     float.IsNaN(v.Z) || float.IsInfinity(v.Z));
        }

        private static bool IsFinite(Matrix4x4 m)
        {
            return IsFinite(m.M11) && IsFinite(m.M12) && IsFinite(m.M13) && IsFinite(m.M14) &&
                   IsFinite(m.M21) && IsFinite(m.M22) && IsFinite(m.M23) && IsFinite(m.M24) &&
                   IsFinite(m.M31) && IsFinite(m.M32) && IsFinite(m.M33) && IsFinite(m.M34) &&
                   IsFinite(m.M41) && IsFinite(m.M42) && IsFinite(m.M43) && IsFinite(m.M44);
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        private static bool IsStretchedTriangle(Vector3 a, Vector3 b, Vector3 c, out float maxEdge, out float minEdge, out float area)
        {
            var ab = (b - a).Length();
            var bc = (c - b).Length();
            var ca = (a - c).Length();
            maxEdge = MathF.Max(ab, MathF.Max(bc, ca));
            minEdge = MathF.Min(ab, MathF.Min(bc, ca));
            area = Vector3.Cross(b - a, c - a).Length() * 0.5f;

            if (minEdge <= 0.0001f) return true;
            var ratio = maxEdge / minEdge;
            if (ratio > 120f) return true;
            if (maxEdge > 5000f && area < 0.5f) return true;
            return false;
        }

        private static bool IsUvAnomalyTriangle(Vector3 p1, Vector3 p2, Vector3 p3, Vector2 u1, Vector2 u2, Vector2 u3, out float worldMax, out float uvMax, out float uvRatio)
        {
            var w12 = (p2 - p1).Length();
            var w23 = (p3 - p2).Length();
            var w31 = (p1 - p3).Length();
            worldMax = MathF.Max(w12, MathF.Max(w23, w31));
            var worldMin = MathF.Min(w12, MathF.Min(w23, w31));

            var t12 = (u2 - u1).Length();
            var t23 = (u3 - u2).Length();
            var t31 = (u1 - u3).Length();
            uvMax = MathF.Max(t12, MathF.Max(t23, t31));
            var uvMin = MathF.Min(t12, MathF.Min(t23, t31));

            uvRatio = (uvMin > 1e-6f) ? uvMax / uvMin : float.PositiveInfinity;
            if (worldMax < 0.001f) return false;
            if (uvMax > 250f && worldMax < 300f) return true;
            if (uvRatio > 400f && worldMin > 0.1f) return true;
            return false;
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
