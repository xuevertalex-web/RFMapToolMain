using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using RFMapToolSharp.Models;
using RFMapToolSharp.Parsing.Bsp;
using RFMapToolSharp.Parsing;
using RFMapToolSharp.Collision;
using RFMapToolSharp.Rvp;
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
        // Keep geometry intact by default: diagnostics are logged, triangles are not removed.
        public static bool FilterStretchedFaces { get; set; } = false;
        public static bool FilterUvAnomalyFaces { get; set; } = false;
        public static bool FilterNormalAnomalyFaces { get; set; } = false;

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
                    var texName = scene.Textures[texId].Name;
                    var pngBytes = TextureConverter.ToPngBytes(scene.Textures[texId].DdsData, texId, texName);
                    var newImg = new MemoryImage(pngBytes);
                    imageCache[texId] = newImg;
                    return newImg;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GLTF] TexId={texId} Name=\"{scene.Textures[texId].Name}\" image load FAILED: {ex.Message}");
                    imageCache[texId] = default; // не повторять неудачную конверсию
                    return default;
                }
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

                        var matRec = new TextureDiagnostics.MatRecord
                        {
                            MatId = matId,
                            Name = rfMat.Name ?? string.Empty,
                            Surface = layer0.Surface,
                            TexId = texId
                        };

                        if (texId >= 0 && texId < scene.Textures.Count)
                        {
                            matRec.TextureName = scene.Textures[texId].Name ?? string.Empty;
                            var img = GetOrLoadImage(texId);
                            if (!img.IsEmpty)
                            {
                                matRec.TextureAssigned = true;
                                matRec.Status = "ok";
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
                            else
                            {
                                matRec.Status = "convert_failed";
                            }
                        }
                        else
                        {
                            matRec.Status = "tex_out_of_range";
                        }

                        TextureDiagnostics.Current.LogMaterial(matRec);
                    }
                    else
                    {
                        TextureDiagnostics.Current.LogMaterial(new TextureDiagnostics.MatRecord
                        {
                            MatId = matId,
                            Name = rfMat.Name ?? string.Empty,
                            Surface = -1,
                            TexId = -1,
                            Status = "no_layers"
                        });
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
            var mgTrace = new List<object>();
            var mgNodesByObjectId = new Dictionary<int, List<(Node Node, int Attr)>>();

            // --- Иерархия нод по parent-chain BSP-объектов ---
            // Активна только в «родном» режиме запекания вершин: вершины групп запечены
            // через ObjectMatrices (converted world), значит mesh-ноды можно повесить
            // под объектные ноды, а анимацию задать дельтами от запечённой позы:
            //   T_i(f) = S · WiB⁻¹ · Wi(f) · Wp(f)⁻¹ · WpB · S   (S = mirror Y, B = baked frame)
            // Телескопируется с каналами родителя к S·WiB⁻¹·Wi(f)·S, на baked-кадре = identity.
            // Если дельта содержит shear (анизотропный scale × поворот — glTF не хранит shear),
            // для корневых объектов используется точная факторизация в цепочку из трёх TRS-нод:
            //   t(f) = U·T(-pB)·SC_B⁻¹·V · U·R_B⁻¹R(f)·V · U·SC(f)·T(p(f))·V,  U = S·P, V = P·S,
            // где P — перестановка Y/Z из ConvertFrom3dsMaxMatrix, p/rot/SC — локальные треки.
            bool hierarchyMode = MirrorWorldY
                && !Collision.BspFile.DisableObjectTransform
                && Collision.BspFile.ObjectTransformMode == 0
                && Collision.BspFile.AnimatedObjectsMode == 0
                && Collision.BspFile.ObjectTransformTarget == 0;

            var objectNodes = new Dictionary<int, Node>();       // корень объекта (identity)
            var objectAttachNodes = new Dictionary<int, Node>(); // нода, несущая world-движение (mode 2 — конец цепочки)
            var objectChainNodes = new Dictionary<int, (Node Inv, Node Rot, Node Scl)>();
            var objectNodeParent = new Dictionary<int, int>();
            var preparedChannels = new Dictionary<int, List<PreparedChannel>>();
            int maxFrames = 0;
            var mirror = Matrix4x4.CreateScale(1f, -1f, 1f);
            var yzSwap = new Matrix4x4(
                1f, 0f, 0f, 0f,
                0f, 0f, 1f, 0f,
                0f, 1f, 0f, 0f,
                0f, 0f, 0f, 1f);

            bool IsBakedGroup(int oid, int attr) =>
                oid > 0
                && !(Collision.BspFile.SkipTransformForAttr8192 && attr == 8192)
                && !Collision.BspFile.SkipTransformObjectIds.Contains(oid)
                && scene.Bsp.GetBakedObjectMatrix(oid).HasValue;

            if (hierarchyMode)
            {
                // Кандидаты: объекты из MatGroups + их предки по parent-chain.
                var candidateIds = new SortedSet<int>();
                if (scene.Bsp.MatGroups != null)
                    foreach (var mg in scene.Bsp.MatGroups)
                        if (IsBakedGroup(mg.ObjectId, mg.Attr)) candidateIds.Add(mg.ObjectId);
                var ancestorQueue = new Queue<int>(candidateIds);
                while (ancestorQueue.Count > 0)
                {
                    int p = scene.Bsp.GetObjectParent1Based(ancestorQueue.Dequeue());
                    if (p > 0 && scene.Bsp.GetBakedObjectMatrix(p).HasValue && candidateIds.Add(p))
                        ancestorQueue.Enqueue(p);
                }

                // Эффективный родитель: только если он тоже кандидат и chain без циклов.
                foreach (var oid in candidateIds)
                {
                    int parent = scene.Bsp.GetObjectParent1Based(oid);
                    if (parent <= 0 || parent == oid || !candidateIds.Contains(parent))
                    {
                        objectNodeParent[oid] = 0;
                        continue;
                    }
                    var seen = new HashSet<int> { oid };
                    int p = parent;
                    bool cycle = false;
                    while (p > 0)
                    {
                        if (!seen.Add(p)) { cycle = true; break; }
                        int np = scene.Bsp.GetObjectParent1Based(p);
                        p = candidateIds.Contains(np) ? np : 0;
                    }
                    if (cycle)
                    {
                        Console.WriteLine($"[GLTF] WARN: obj{oid}: цикл в parent-chain, нода уходит в корень сцены");
                        objectNodeParent[oid] = 0;
                    }
                    else
                    {
                        objectNodeParent[oid] = parent;
                    }
                }

                foreach (var oid in candidateIds)
                    maxFrames = Math.Max(maxFrames, scene.Bsp.GetObjectFrames(oid));

                // Согласованность: world@ObjectTransformFrame должна совпадать с матрицей запекания.
                var noAnimObjects = new HashSet<int>();
                foreach (var oid in candidateIds)
                {
                    var baked = scene.Bsp.GetBakedObjectMatrix(oid)!.Value;
                    var atFrame = scene.Bsp.GetObjectWorldMatrixAtFrame(oid, Collision.BspFile.ObjectTransformFrame);
                    if (atFrame == null || !NearlyEqual(baked, atFrame.Value))
                    {
                        Console.WriteLine($"[GLTF] WARN: obj{oid}: world@frame({Collision.BspFile.ObjectTransformFrame}) != baked matrix, каналы пропущены");
                        noAnimObjects.Add(oid);
                    }
                }

                if (maxFrames > 0)
                {
                    // Сэмплим world-матрицы один раз на кадр для всей иерархии.
                    var frameCount = maxFrames + 1;
                    var allFrames = new Matrix4x4[frameCount][];
                    for (int f = 0; f < frameCount; f++)
                        allFrames[f] = scene.Bsp.GetObjectWorldMatricesAtFrame(f);
                    var worldFramesByObject = new Dictionary<int, Matrix4x4[]>();
                    foreach (var oid in candidateIds)
                    {
                        var arr = new Matrix4x4[frameCount];
                        int idx = oid - 1;
                        for (int f = 0; f < frameCount; f++)
                            arr[f] = idx >= 0 && idx < allFrames[f].Length
                                ? allFrames[f][idx]
                                : scene.Bsp.GetBakedObjectMatrix(oid)!.Value;
                        worldFramesByObject[oid] = arr;
                    }

                    // Отладочный дамп world-матриц и сырых треков (вкл.: RF_DEBUG_OBJANIM=1)
                    if (Environment.GetEnvironmentVariable("RF_DEBUG_OBJANIM") == "1")
                    {
                        var dump = worldFramesByObject.ToDictionary(
                            kv => $"obj{kv.Key}",
                            kv => kv.Value.Select(MatrixToFloats).ToArray());
                        DiagnosticsOutput.WriteDiagnostic(name, "objanim_frames.json",
                            JsonSerializer.Serialize(dump, SafeJson));
                        foreach (var oid in candidateIds)
                            DiagnosticsOutput.WriteDiagnostic(name, $"objanim_tracks_obj{oid}.json",
                                scene.Bsp.DumpObjectTracksJson(oid));
                    }

                    // Подготовка каналов (до создания нод: от режима зависит структура иерархии).
                    foreach (var oid in candidateIds)
                    {
                        if (noAnimObjects.Contains(oid)) continue;
                        PrepareObjectChannels(oid, worldFramesByObject);
                    }
                }
            }

            // Прямой путь: дельта world-матрицы в TRS-канал на объектной ноде.
            // При shear — факторизация в цепочку inv/rot/scl (только корневые объекты).
            void PrepareObjectChannels(int oid, Dictionary<int, Matrix4x4[]> worldFramesByObject)
            {
                var w = worldFramesByObject[oid];
                var baked = scene.Bsp.GetBakedObjectMatrix(oid)!.Value;
                if (!Matrix4x4.Invert(baked, out var w0inv))
                {
                    Console.WriteLine($"[GLTF] WARN: obj{oid}: baked matrix необратима, каналы пропущены");
                    return;
                }
                int parent = objectNodeParent[oid];
                var pw = parent > 0 ? worldFramesByObject[parent] : null;
                Matrix4x4 pwB = default;
                if (pw != null) pwB = scene.Bsp.GetBakedObjectMatrix(parent)!.Value;

                var trs = new Dictionary<float, Vector3>();
                var rts = new Dictionary<float, Quaternion>();
                var scs = new Dictionary<float, Vector3>();
                bool varies = false, directOk = true;
                Quaternion prevRot = default;
                for (int f = 0; f <= maxFrames && directOk; f++)
                {
                    var t = mirror * (w0inv * w[f]);
                    if (pw != null)
                    {
                        if (!Matrix4x4.Invert(pw[f], out var pfInv)) { directOk = false; break; }
                        t = t * (pfInv * pwB);
                    }
                    t = t * mirror;
                    if (!Matrix4x4.Decompose(t, out var sc, out var rot, out var tr)) { directOk = false; break; }
                    if (f > 0 && Quaternion.Dot(rot, prevRot) < 0f) rot = Negate(rot);
                    prevRot = rot;
                    float time = f / 30f;
                    trs[time] = tr;
                    rts[time] = rot;
                    scs[time] = sc;
                    if (!varies && !NearlyIdentity(t)) varies = true;
                }
                if (directOk)
                {
                    if (varies)
                        preparedChannels[oid] = new List<PreparedChannel> { PreparedChannel.Animated("", trs, rts, scs) };
                    return;
                }

                if (parent > 0)
                {
                    Console.WriteLine($"[GLTF] WARN: obj{oid}: world-дельта не раскладывается в TRS (shear), объект с родителем — каналы пропущены");
                    return;
                }
                var chain = BuildFactoredChannels(oid, w, w0inv);
                if (chain == null)
                {
                    Console.WriteLine($"[GLTF] WARN: obj{oid}: world-дельта не раскладывается в TRS, факторизация не удалась — каналы пропущены");
                    return;
                }
                if (chain.Count > 0)
                    preparedChannels[oid] = chain;
            }

            // Точная факторизация дельты корневого объекта в цепочку из трёх TRS-нод.
            // Возвращает null при неудаче; пустой список — если движения нет.
            List<PreparedChannel>? BuildFactoredChannels(int oid, Matrix4x4[] w, Matrix4x4 w0inv)
            {
                bool dbg = Environment.GetEnvironmentVariable("RF_DEBUG_OBJANIM") == "1";
                float baseFrame = Collision.BspFile.ObjectTransformFrame;
                if (!scene.Bsp.TryGetObjectLocalComponents(oid, baseFrame, out var p0, out var r0q, out var sc0))
                    { if (dbg) Console.WriteLine($"[GLTF] DEBUG obj{oid}: factored: нет local components @base"); return null; }
                var R0 = Matrix4x4.CreateFromQuaternion(r0q);
                if (!Matrix4x4.Invert(R0, out var R0inv)) { if (dbg) Console.WriteLine($"[GLTF] DEBUG obj{oid}: factored: R0 необратима"); return null; }
                if (!Matrix4x4.Invert(sc0, out var sc0inv)) { if (dbg) Console.WriteLine($"[GLTF] DEBUG obj{oid}: factored: SC0 необратима"); return null; }
                var U = mirror * yzSwap;
                if (!Matrix4x4.Invert(U, out var V)) { if (dbg) Console.WriteLine($"[GLTF] DEBUG obj{oid}: factored: U необратима"); return null; }
                var F1 = Matrix4x4.CreateTranslation(-p0) * sc0inv;

                var t1 = new Dictionary<float, Vector3>(); var r1 = new Dictionary<float, Quaternion>(); var s1 = new Dictionary<float, Vector3>();
                var t2 = new Dictionary<float, Vector3>(); var r2 = new Dictionary<float, Quaternion>(); var s2 = new Dictionary<float, Vector3>();
                var t3 = new Dictionary<float, Vector3>(); var r3 = new Dictionary<float, Quaternion>(); var s3 = new Dictionary<float, Vector3>();
                bool varies1 = false, varies2 = false, varies3 = false;
                Quaternion prev1 = default, prev2 = default, prev3 = default;
                SharpGLTF.Transforms.AffineTransform const1 = default, const2 = default, const3 = default;
                Matrix4x4 L10 = default, L20 = default, L30 = default;

                for (int f = 0; f <= maxFrames; f++)
                {
                    if (!scene.Bsp.TryGetObjectLocalComponents(oid, f, out var pf, out var rqf, out var scf))
                        { if (dbg) Console.WriteLine($"[GLTF] DEBUG obj{oid}: factored: нет local components @{f}"); return null; }
                    var Rf = Matrix4x4.CreateFromQuaternion(rqf);
                    var L1 = U * F1 * V;
                    var L2 = U * (R0inv * Rf) * V;
                    var L3 = U * (scf * Matrix4x4.CreateTranslation(pf)) * V;

                    // Контроль: цепочка обязана телескопироваться в world-дельту.
                    // Допуск масштабируется по обусловленности: w0inv содержит 1/minScale
                    // (у obj1/2 ~1/0.005 ≈ 200), float32-шум в w0inv·w[f] даёт ~1e-2 абс.
                    var tCheck = mirror * (w0inv * w[f]) * mirror;
                    var chainProd = L1 * L2 * L3;
                    float chainTol = 5e-5f * MaxAbs(w0inv) * MathF.Max(1f, MaxAbs(w[f]));
                    if (MaxAbsDiff(chainProd, tCheck) > chainTol)
                    {
                        if (dbg)
                            Console.WriteLine($"[GLTF] DEBUG obj{oid}: factored: цепочка != дельта на кадре {f}, maxdiff={MaxAbsDiff(chainProd, tCheck):G6}, tol={chainTol:G6}");
                        return null;
                    }

                    if (!Matrix4x4.Decompose(L1, out var sc1, out var rt1, out var tr1)) { if (dbg) Console.WriteLine($"[GLTF] DEBUG obj{oid}: factored: Decompose L1 @{f}"); return null; }
                    if (!Matrix4x4.Decompose(L2, out var sc2, out var rt2, out var tr2)) { if (dbg) Console.WriteLine($"[GLTF] DEBUG obj{oid}: factored: Decompose L2 @{f}"); return null; }
                    if (!Matrix4x4.Decompose(L3, out var sc3, out var rt3, out var tr3)) { if (dbg) Console.WriteLine($"[GLTF] DEBUG obj{oid}: factored: Decompose L3 @{f}"); return null; }
                    if (f > 0)
                    {
                        if (Quaternion.Dot(rt1, prev1) < 0f) rt1 = Negate(rt1);
                        if (Quaternion.Dot(rt2, prev2) < 0f) rt2 = Negate(rt2);
                        if (Quaternion.Dot(rt3, prev3) < 0f) rt3 = Negate(rt3);
                    }
                    prev1 = rt1; prev2 = rt2; prev3 = rt3;
                    if (f == 0)
                    {
                        const1 = new SharpGLTF.Transforms.AffineTransform(sc1, rt1, tr1);
                        const2 = new SharpGLTF.Transforms.AffineTransform(sc2, rt2, tr2);
                        const3 = new SharpGLTF.Transforms.AffineTransform(sc3, rt3, tr3);
                        L10 = L1; L20 = L2; L30 = L3;
                    }
                    float time = f / 30f;
                    t1[time] = tr1; r1[time] = rt1; s1[time] = sc1;
                    t2[time] = tr2; r2[time] = rt2; s2[time] = sc2;
                    t3[time] = tr3; r3[time] = rt3; s3[time] = sc3;
                    // «varies» = значение меняется по кадрам (сравнение с кадром 0),
                    // а не отличие от identity: константная нода получает static local.
                    if (!varies1 && !NearlyEqual(L1, L10)) varies1 = true;
                    if (!varies2 && !NearlyEqual(L2, L20)) varies2 = true;
                    if (!varies3 && !NearlyEqual(L3, L30)) varies3 = true;
                }

                var result = new List<PreparedChannel>();
                if (!varies1 && !varies2 && !varies3)
                    return result; // движения нет — цепочка не нужна, объект статичен
                result.Add(varies1 ? PreparedChannel.Animated("inv", t1, r1, s1) : PreparedChannel.Constant("inv", const1));
                result.Add(varies2 ? PreparedChannel.Animated("rot", t2, r2, s2) : PreparedChannel.Constant("rot", const2));
                result.Add(varies3 ? PreparedChannel.Animated("scl", t3, r3, s3) : PreparedChannel.Constant("scl", const3));
                return result;
            }

            Node GetOrCreateObjectNode(int oid)
            {
                if (objectAttachNodes.TryGetValue(oid, out var existing)) return existing;
                int parent = objectNodeParent.TryGetValue(oid, out var pp) ? pp : 0;
                var objNode = parent > 0
                    ? GetOrCreateObjectNode(parent).CreateNode($"BSP_obj{oid}")
                    : gltfScene.CreateNode($"BSP_obj{oid}");
                objectNodes[oid] = objNode;

                Node attach = objNode;
                if (preparedChannels.TryGetValue(oid, out var ch) && ch.Count > 0 && ch[0].Role == "inv")
                {
                    // Факторизованный объект: obj(identity) → inv → rot → scl → mesh/дети.
                    var inv = objNode.CreateNode($"BSP_obj{oid}_inv");
                    var rot = inv.CreateNode($"BSP_obj{oid}_rot");
                    var scl = rot.CreateNode($"BSP_obj{oid}_scl");
                    objectChainNodes[oid] = (inv, rot, scl);
                    attach = scl;
                }
                objectAttachNodes[oid] = attach;
                return attach;
            }

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
                        if (face.MatGroup >= 89 && face.MatGroup <= 92)
                        {
                            mgTrace.Add(new
                            {
                                MatGroup = face.MatGroup,
                                MatId = face.MatId,
                                A = face.A,
                                B = face.B,
                                C = face.C,
                                PA = new[] { Get(pos, face.A, default).X, Get(pos, face.A, default).Y, Get(pos, face.A, default).Z },
                                PB = new[] { Get(pos, face.B, default).X, Get(pos, face.B, default).Y, Get(pos, face.B, default).Z },
                                PC = new[] { Get(pos, face.C, default).X, Get(pos, face.C, default).Y, Get(pos, face.C, default).Z },
                                UVA = new[] { Get(uv0, face.A, default).X, Get(uv0, face.A, default).Y },
                                UVB = new[] { Get(uv0, face.B, default).X, Get(uv0, face.B, default).Y },
                                UVC = new[] { Get(uv0, face.C, default).X, Get(uv0, face.C, default).Y }
                            });
                        }

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
                Node node;
                if (hierarchyMode && IsBakedGroup(mgObjectId, mgAttr))
                {
                    // Вершины запечены world-матрицей объекта: local mesh-ноды = identity,
                    // поза задаётся каналами на объектной ноде (см. блок анимации ниже).
                    node = GetOrCreateObjectNode(mgObjectId).CreateNode(nodeName);
                }
                else
                {
                    node = gltfScene.CreateNode(nodeName);
                }
                node.Mesh = model.CreateMesh(meshBuilder);
                if (mgObjectId > 0)
                {
                    if (!mgNodesByObjectId.TryGetValue(mgObjectId, out var lst))
                    {
                        lst = new List<(Node, int)>();
                        mgNodesByObjectId[mgObjectId] = lst;
                    }
                    lst.Add((node, mgAttr));
                }
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

            // --- BSP object animations ---
            if (hierarchyMode)
            {
                // Каналы подготовлены в pre-pass (до создания нод). Остаётся создать
                // Animation и развесить каналы/константные local-трансформы по нодам.
                Animation? anim = null;
                int animatedNodes = 0;
                foreach (var (oid, channels) in preparedChannels.OrderBy(kv => kv.Key))
                {
                    foreach (var ch in channels)
                    {
                        Node target = ch.Role switch
                        {
                            "inv" => objectChainNodes[oid].Inv,
                            "rot" => objectChainNodes[oid].Rot,
                            "scl" => objectChainNodes[oid].Scl,
                            _ => objectNodes[oid]
                        };
                        if (ch.T != null)
                        {
                            anim ??= model.CreateAnimation($"{name}_BSP_Objects");
                            anim.CreateTranslationChannel(target, ch.T, true);
                            anim.CreateRotationChannel(target, ch.R!, true);
                            anim.CreateScaleChannel(target, ch.S!, true);
                            animatedNodes++;
                        }
                        else if (ch.ConstantLocal.HasValue)
                        {
                            target.LocalTransform = ch.ConstantLocal.Value;
                        }
                    }
                }
                if (anim != null)
                    Console.WriteLine($"[GLTF] BSP animations: objects={preparedChannels.Count}, nodes={animatedNodes}, frames={maxFrames + 1}, mode=hierarchy");
            }
            else if (scene.Bsp != null && !Collision.BspFile.DisableObjectTransform && mgNodesByObjectId.Count > 0)
            {
                // Плоский fallback для экзотических CLI-режимов запекания:
                // анимируем только ноды, чьи вершины запечены с object transform (иначе
                // канал анимации двигал бы raw-геометрию из локального пространства).
                var eligibleObjects = new List<(int Oid, List<Node> Nodes, IReadOnlyList<Collision.BspFile.BspObjectAnimSample> Samples)>();
                foreach (var (oid, nodes) in mgNodesByObjectId.OrderBy(kv => kv.Key))
                {
                    if (Collision.BspFile.SkipTransformObjectIds.Contains(oid)) continue;

                    var eligible = nodes
                        .Where(n => !Collision.BspFile.SkipTransformForAttr8192 || n.Attr != 8192)
                        .Select(n => n.Node)
                        .ToList();
                    if (eligible.Count == 0) continue;

                    var samples = scene.Bsp.GetObjectAnimationSamples(oid);
                    if (samples.Count <= 1) continue;
                    if (!SamplesVary(samples)) continue; // статичная поза — каналы не нужны

                    eligibleObjects.Add((oid, eligible, samples));
                }

                if (eligibleObjects.Count > 0)
                {
                    var anim = model.CreateAnimation($"{name}_BSP_Objects");
                    int animatedNodes = 0;
                    foreach (var (oid, nodes, samples) in eligibleObjects)
                    {
                        var trs = samples.ToDictionary(s => s.Time, s => s.Translation);
                        var rts = samples.ToDictionary(s => s.Time, s => s.Rotation);
                        var scs = samples.ToDictionary(s => s.Time, s => s.Scale);
                        foreach (var node in nodes)
                        {
                            anim.CreateTranslationChannel(node, trs, true);
                            anim.CreateRotationChannel(node, rts, true);
                            anim.CreateScaleChannel(node, scs, true);
                            animatedNodes++;
                        }
                    }
                    Console.WriteLine($"[GLTF] BSP animations: objects={eligibleObjects.Count}, nodes={animatedNodes}, mode=flat");
                }
            }

            // --- SPT (OBJECT MARKERS) ---
            if (!string.Equals(SptOptions.Mode, "off", StringComparison.OrdinalIgnoreCase))
            {
                var helperMeshes = CreateHelperMeshes(model);
                ProcessSpt(scene.RootPath, gltfScene, MirrorWorldY, helperMeshes, name);
            }

            // --- EBP (FX / SOUND EMITTERS) ---
            if (!string.Equals(SptOptions.Mode, "off", StringComparison.OrdinalIgnoreCase))
            {
                ProcessEbp(scene.RootPath, gltfScene, MirrorWorldY, name, model);
            }

            // --- RVP (CUTSCENE / FLYING OBJECTS) ---
            if (!string.Equals(SptOptions.Mode, "off", StringComparison.OrdinalIgnoreCase))
            {
                ProcessRvp(scene.RootPath, gltfScene, MirrorWorldY, name, model);
            }

            model.SaveGLB(Path.Combine(exportDir, $"{name}.glb"));
            DiagnosticsOutput.WriteDiagnostic(name, "stretched_faces.json", JsonSerializer.Serialize(stretchedFaces, SafeJson));
            DiagnosticsOutput.WriteDiagnostic(name, "uv_anomaly_faces.json", JsonSerializer.Serialize(uvAnomalyFaces, SafeJson));
            DiagnosticsOutput.WriteDiagnostic(name, "normal_anomaly_faces.json", JsonSerializer.Serialize(normalAnomalyFaces, SafeJson));
            DiagnosticsOutput.WriteDiagnostic(name, "bsp_node_index.json", JsonSerializer.Serialize(bspNodeIndex, SafeJson));
            DiagnosticsOutput.WriteDiagnostic(name, "mg_trace_89_92.json", JsonSerializer.Serialize(mgTrace, SafeJson));
            scene.Bsp.WriteMgFaceTrace89_92Report(DiagnosticsOutput.DiagnosticPath(name, "mg_face_trace_89_92_bspbuild.json"));
            if (string.Equals(name, "Sette", StringComparison.OrdinalIgnoreCase))
            {
                var mg91Only = mgTrace.Where(x => (int)x.GetType().GetProperty("MatGroup")!.GetValue(x)! == 91).ToList();
                DiagnosticsOutput.WriteDiagnostic(name, "mg91_face_rebuild_log.json", JsonSerializer.Serialize(mg91Only, SafeJson));
            }
            Console.WriteLine("[GLTF] Saved!");

            var diag = TextureDiagnostics.Current;
            diag.WriteReport(DiagnosticsOutput.ReportPath($"texture_report_{name}.json"));
            int matsWithTex = diag.Materials.Count(m => m.TextureAssigned);
            int texOk = diag.Textures.Count(t => t.Status == "ok");
            int texFailed = diag.Textures.Count(t => t.Status == "convert_failed");
            Console.WriteLine($"[GLTF] Summary: materials={diag.Materials.Count} (textured={matsWithTex}), textures converted={texOk}, failed={texFailed}");
            Console.WriteLine($"[GLTF] texture_report_{name}.json written to _reports.");
        }

        private static string ClassifySptHelper(RFMapToolSharp.Parsing.SptMapObject obj)
        {
            if (string.Equals(obj.Tag, "music", StringComparison.OrdinalIgnoreCase)) return "music";
            var n = obj.ModelName ?? string.Empty;
            if (n.StartsWith("dpgoto", StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith("dpfrom", StringComparison.OrdinalIgnoreCase)) return "portal";
            if (n.StartsWith("dsstart", StringComparison.OrdinalIgnoreCase)) return "spawn";
            return "helper";
        }

        /// <summary>Конвертация node_tm в mirrored space: M' = S·M·S, S = diag(1,-1,1,1)
        /// (инвертируются элементы ровно с одним Y-индексом). Сохраняет поворот и масштаб.</summary>
        private static Matrix4x4 MirrorSptMatrix(Matrix4x4 m, bool mirrorY)
        {
            if (!mirrorY) return m;
            m.M12 = -m.M12; m.M21 = -m.M21;
            m.M23 = -m.M23; m.M32 = -m.M32;
            m.M24 = -m.M24; m.M42 = -m.M42;
            return m;
        }

        private static void ProcessSpt(string mapRootPath, Scene gltfScene, bool mirrorY, Dictionary<string, Mesh> helperMeshes, string mapName)
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
                    var kind = ClassifySptHelper(obj);
                    var node = gltfScene.CreateNode(obj.ModelName);
                    bool usedRealMesh = false;
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

                    // Трансформ: при наличии сырой node_tm (текстовые helper-скрипты)
                    // берём её целиком — с поворотом и масштабом; иначе legacy (pos + scale).
                    Matrix4x4 transform;
                    bool usedNodeTm = false;
                    if (obj.NodeTm.HasValue)
                    {
                        transform = MirrorSptMatrix(obj.NodeTm.Value, mirrorY);
                        usedNodeTm = true;
                    }
                    else
                    {
                        var objScale = obj.Scale;
                        if (!IsFinite(objScale) || objScale.X <= 0 || objScale.Y <= 0 || objScale.Z <= 0 || objScale.X > 1000 || objScale.Y > 1000 || objScale.Z > 1000)
                        {
                            objScale = Vector3.One;
                        }
                        objScale *= SptOptions.ScaleMultiplier;

                        transform = Matrix4x4.CreateTranslation(pos);
                        if (!isExtScript && objScale != Vector3.One)
                        {
                            transform = Matrix4x4.CreateScale(objScale) * transform;
                        }
                        if (SptOptions.PivotFix)
                        {
                            // RF helper pivot compensation for marker mode.
                            transform = Matrix4x4.CreateTranslation(0, mirrorY ? 0.5f : -0.5f, 0) * transform;
                        }
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

                    // Маркер-бокс реального размера helper'а (bbox из SPT) — дочерней нодой,
                    // чтобы поворот/масштаб node_tm применялись к боксу.
                    if (!usedRealMesh)
                    {
                        var size = obj.HasBbox ? obj.BboxMax - obj.BboxMin : new Vector3(100f);
                        var center = obj.HasBbox ? (obj.BboxMax + obj.BboxMin) * 0.5f : Vector3.Zero;
                        size = new Vector3(
                            Math.Clamp(MathF.Abs(size.X), 1f, 200000f),
                            Math.Clamp(MathF.Abs(size.Y), 1f, 200000f),
                            Math.Clamp(MathF.Abs(size.Z), 1f, 200000f));
                        var box = node.CreateNode(obj.ModelName + "_box");
                        box.Mesh = helperMeshes[kind];
                        try
                        {
                            box.LocalTransform = Matrix4x4.CreateScale(size) * Matrix4x4.CreateTranslation(center);
                        }
                        catch
                        {
                            box.LocalTransform = Matrix4x4.CreateScale(size);
                        }
                    }

                    resolveLog.Add(new
                    {
                        SourceFile = file,
                        obj.ModelName,
                        Kind = kind,
                        UsedRealMesh = usedRealMesh,
                        UsedNodeTm = usedNodeTm,
                        obj.HasBbox,
                        ResolvedModelPath = ResolveModelPath(mapRootPath, obj.ModelName),
                        obj.Position,
                        obj.Rotation,
                        Scale = obj.Scale,
                        SptOptions.Mode,
                        SptOptions.PivotFix,
                        SptOptions.RotationOrder,
                        SptOptions.ScaleMultiplier
                    });
                    count++;
                }
            }
            Console.WriteLine($"[SPT] Created markers: {count}");
            DiagnosticsOutput.WriteDiagnostic(mapName, "spt_resolve_log.json", JsonSerializer.Serialize(resolveLog, SafeJson));
        }

        /// <summary>
        /// Эмиттеры эффектов и звуков из .ebp (ExtBsp): фонтаны, лава, листопад,
        /// ambient-звуки. Сами частицы (.R3E) в GLB не конвертируются — ставим
        /// полупрозрачные маркеры с метаданными в extras (effect path, fade,
        /// shader, wave-файл и т.д.), чтобы эмиттеры были видны и адресуемы.
        /// </summary>
        private static void ProcessEbp(string mapRootPath, Scene gltfScene, bool mirrorY, string mapName, ModelRoot model)
        {
            var ebpFile = Directory.GetFiles(mapRootPath, "*.ebp", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (ebpFile == null) return;

            ExtBspFile ebp;
            try
            {
                ebp = ExtBspFile.Load(ebpFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FX] Failed to load {Path.GetFileName(ebpFile)}: {ex.Message}");
                return;
            }

            var fxMesh = CreateUnitCube(model, "FX_marker", "FX_marker_mat", new Vector4(1.0f, 0.2f, 1.0f, 0.35f), 0.5f);
            var sndMesh = CreateUnitCube(model, "SND_marker", "SND_marker_mat", new Vector4(0.45f, 0.3f, 1.0f, 0.35f), 0.5f);

            // Корень данных клиента (...\Maps) — для проверки наличия wav в Snd.
            var mapsRoot = GetClientDataRoot(mapRootPath);

            int fxCount = 0, sndCount = 0;
            var log = new List<object>();

            for (int i = 0; i < ebp.MapEntitiesList.Count; i++)
            {
                var inst = ebp.MapEntitiesList[i];
                var entry = inst.Id >= 0 && inst.Id < ebp.EntityList.Count ? ebp.EntityList[inst.Id] : null;
                var effectPath = entry?.Name ?? string.Empty;
                var baseName = SanitizeNodeName(Path.GetFileNameWithoutExtension(effectPath.Replace('\\', '/')));
                if (string.IsNullOrEmpty(baseName)) baseName = $"id{inst.Id}";

                var pos = new Vector3(inst.Pos.X, mirrorY ? -inst.Pos.Y : inst.Pos.Y, inst.Pos.Z);
                if (!IsFinite(pos)) pos = Vector3.Zero;

                var node = gltfScene.CreateNode($"FX_{i}_{baseName}");
                try { node.LocalTransform = Matrix4x4.CreateTranslation(pos); }
                catch { continue; }

                var size = new Vector3(
                    MathF.Abs(inst.BbMax.X - inst.BbMin.X),
                    MathF.Abs(inst.BbMax.Y - inst.BbMin.Y),
                    MathF.Abs(inst.BbMax.Z - inst.BbMin.Z));
                if (size.X < 1f && size.Y < 1f && size.Z < 1f)
                {
                    float s = IsFinite(inst.Scale) && inst.Scale > 0 ? 100f * inst.Scale : 100f;
                    size = new Vector3(Math.Clamp(s, 10f, 5000f));
                }
                size = new Vector3(
                    Math.Clamp(size.X, 1f, 200000f),
                    Math.Clamp(size.Y, 1f, 200000f),
                    Math.Clamp(size.Z, 1f, 200000f));

                var box = node.CreateNode($"FX_{i}_{baseName}_box");
                box.Mesh = fxMesh;
                try { box.LocalTransform = Matrix4x4.CreateScale(size); }
                catch { box.LocalTransform = Matrix4x4.CreateScale(new Vector3(100f)); }

                SetNodeExtras(node, new
                {
                    type = "fx_emitter",
                    effect = effectPath,
                    effectExists = ResolveEffectFile(mapsRoot, effectPath) != null,
                    effectFile = ResolveEffectFile(mapsRoot, effectPath),
                    isParticle = entry?.IsParticle ?? 0,
                    isFileExist = entry?.IsFileExist ?? 0,
                    fadeStart = entry?.FadeStart ?? 0f,
                    fadeEnd = entry?.FadeEnd ?? 0f,
                    shaderId = entry?.ShaderId ?? 0,
                    scale = inst.Scale,
                    rotX = inst.RotX,
                    rotY = inst.RotY
                });

                log.Add(new
                {
                    Index = i,
                    inst.Id,
                    Effect = effectPath,
                    IsParticle = entry?.IsParticle ?? 0,
                    Position = pos,
                    Size = size,
                    inst.Scale,
                    inst.RotX,
                    inst.RotY,
                    FadeStart = entry?.FadeStart ?? 0f,
                    FadeEnd = entry?.FadeEnd ?? 0f,
                    ShaderId = entry?.ShaderId ?? 0
                });
                fxCount++;
            }

            for (int i = 0; i < ebp.SoundEntitiesList.Count; i++)
            {
                var inst = ebp.SoundEntitiesList[i];
                var wave = inst.Id >= 0 && inst.Id < ebp.SoundEntityList.Count ? ebp.SoundEntityList[inst.Id].Name : string.Empty;
                var baseName = SanitizeNodeName(Path.GetFileNameWithoutExtension(wave.Replace('\\', '/')));
                if (string.IsNullOrEmpty(baseName)) baseName = $"id{inst.Id}";

                var pos = new Vector3(inst.Pos.X, mirrorY ? -inst.Pos.Y : inst.Pos.Y, inst.Pos.Z);
                if (!IsFinite(pos)) pos = Vector3.Zero;

                var node = gltfScene.CreateNode($"SND_{i}_{baseName}");
                try { node.LocalTransform = Matrix4x4.CreateTranslation(pos); }
                catch { continue; }

                var box = node.CreateNode($"SND_{i}_{baseName}_box");
                box.Mesh = sndMesh;
                try { box.LocalTransform = Matrix4x4.CreateScale(25f); }
                catch { }

                var waveRel = wave.TrimStart('\\', '/').Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                var waveBase = Path.GetFileName(waveRel);
                bool waveExists = false;
                string? waveResolved = null;
                if (!string.IsNullOrWhiteSpace(waveRel))
                {
                    foreach (var candidate in new[]
                    {
                        Path.Combine(mapsRoot, waveRel),
                        Path.Combine(mapsRoot, "Snd", waveRel),
                    })
                    {
                        if (File.Exists(candidate)) { waveExists = true; waveResolved = candidate; break; }
                    }
                    if (!waveExists)
                    {
                        var index = GetSoundIndex(mapsRoot);
                        if (waveBase.Length > 0 && index.TryGetValue(waveBase, out var found))
                        {
                            waveExists = true;
                            waveResolved = found;
                        }
                    }
                }

                SetNodeExtras(node, new
                {
                    type = "sound_emitter",
                    wave,
                    waveExists,
                    waveFile = waveResolved != null ? Path.GetRelativePath(mapsRoot, waveResolved).Replace('\\', '/') : null,
                    eventTime = inst.EventTime,
                    attn = inst.Attn,
                    flag = inst.Flag,
                    scale = inst.Scale
                });

                log.Add(new
                {
                    Index = i,
                    inst.Id,
                    Wave = wave,
                    WaveExists = waveExists,
                    Position = pos,
                    inst.EventTime,
                    inst.Attn,
                    inst.Flag,
                    inst.Scale
                });
                sndCount++;
            }

            Console.WriteLine($"[FX] emitters: {fxCount}, sounds: {sndCount}");
            if (fxCount + sndCount > 0)
                DiagnosticsOutput.WriteDiagnostic(mapName, "ebp_fx.json", JsonSerializer.Serialize(log, SafeJson));
        }

        private static string SanitizeNodeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var chars = name.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' ? c : '_').ToArray();
            return new string(chars);
        }

        private static void SetNodeExtras(Node node, object payload)
        {
            try
            {
                node.Extras = JsonSerializer.SerializeToNode(payload);
            }
            catch
            {
                // extras — опциональные метаданные; не валим экспорт, если API недоступен
            }
        }

        // Индекс звуковых файлов клиента: basename (lower) -> полный путь. Кэшируется на mapsRoot.
        private static readonly Dictionary<string, Dictionary<string, string>> _soundIndexCache = new(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, string> GetSoundIndex(string mapsRoot)
        {
            if (_soundIndexCache.TryGetValue(mapsRoot, out var cached)) return cached;
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var sndRoot = Path.Combine(mapsRoot, "Snd");
            if (Directory.Exists(sndRoot))
            {
                foreach (var f in Directory.EnumerateFiles(sndRoot, "*.*", SearchOption.AllDirectories))
                {
                    if (!f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)) continue;
                    var baseName = Path.GetFileName(f);
                    if (!index.ContainsKey(baseName)) index[baseName] = f;
                }
            }
            _soundIndexCache[mapsRoot] = index;
            return index;
        }

        // Индекс файлов эффектов (.r3e) по всему дереву данных клиента.
        private static readonly Dictionary<string, Dictionary<string, string>> _effectIndexCache = new(StringComparer.OrdinalIgnoreCase);

        private static string? ResolveEffectFile(string mapsRoot, string effectPath)
        {
            if (string.IsNullOrWhiteSpace(effectPath)) return null;
            var rel = effectPath.TrimStart('\\', '/').Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            foreach (var candidate in new[]
            {
                Path.Combine(mapsRoot, rel),
                Path.Combine(mapsRoot, "Effect", rel),
            })
            {
                if (File.Exists(candidate))
                    return Path.GetRelativePath(mapsRoot, candidate).Replace('\\', '/');
            }
            if (!_effectIndexCache.TryGetValue(mapsRoot, out var index))
            {
                index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (Directory.Exists(mapsRoot))
                {
                    foreach (var f in Directory.EnumerateFiles(mapsRoot, "*.r3e", SearchOption.AllDirectories))
                    {
                        var baseName = Path.GetFileName(f);
                        if (!index.ContainsKey(baseName))
                            index[baseName] = Path.GetRelativePath(mapsRoot, f).Replace('\\', '/');
                    }
                }
                _effectIndexCache[mapsRoot] = index;
            }
            var baseNameOnly = Path.GetFileName(rel);
            return baseNameOnly.Length > 0 && index.TryGetValue(baseNameOnly, out var found) ? found : null;
        }

        /// <summary>Корень данных клиента (...\Maps): mapRootPath = Maps\Map\X → Maps.</summary>
        private static string GetClientDataRoot(string mapRootPath)
        {
            var root = Directory.GetParent(mapRootPath)?.FullName ?? mapRootPath;
            if (string.Equals(Path.GetFileName(root), "Map", StringComparison.OrdinalIgnoreCase))
                root = Directory.GetParent(root)?.FullName ?? root;
            return root;
        }

        // Индекс файлов по расширению по всему дереву данных клиента (кэш на root+ext).
        private static readonly Dictionary<string, Dictionary<string, string>> _extIndexCache = new(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, string> GetExtIndex(string dataRoot, string ext)
        {
            var key = dataRoot + "|" + ext;
            if (_extIndexCache.TryGetValue(key, out var cached)) return cached;
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(dataRoot))
            {
                foreach (var f in Directory.EnumerateFiles(dataRoot, "*" + ext, SearchOption.AllDirectories))
                {
                    var baseName = Path.GetFileName(f);
                    if (!index.ContainsKey(baseName)) index[baseName] = f;
                }
            }
            _extIndexCache[key] = index;
            return index;
        }

        /// <summary>Резолв пути вида ".\Chef\RealTime_00\Mesh\x.msh" относительно корня данных клиента.</summary>
        private static string? ResolveClientPath(string dataRoot, string? relPath, string extForIndex)
        {
            if (string.IsNullOrWhiteSpace(relPath)) return null;
            var rel = relPath.TrimStart('.', '\\', '/').Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            var direct = Path.Combine(dataRoot, rel);
            if (File.Exists(direct)) return direct;
            var baseName = Path.GetFileName(rel);
            if (baseName.Length == 0) return null;
            var index = GetExtIndex(dataRoot, extForIndex);
            return index.TryGetValue(baseName, out var found) ? found : null;
        }

        /// <summary>
        /// Летающие/катсценовые объекты из .rvp (корабли, бабочки и т.п.):
        /// меши из Chef\RealTime_*, позиции/повороты — из треков DummyNN в .cam.
        /// Экспортируется как ноды RVP_* с анимацией (30 fps).
        /// </summary>
        private static void ProcessRvp(string mapRootPath, Scene gltfScene, bool mirrorY, string mapName, ModelRoot model)
        {
            var rvpFiles = Directory.GetFiles(mapRootPath, "*.rvp", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (rvpFiles.Length == 0) return;

            var dataRoot = GetClientDataRoot(mapRootPath);

            CamFile? cam = null;
            var camPath = Directory.GetFiles(mapRootPath, "*.cam", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (camPath != null)
            {
                try { cam = CamFile.Load(camPath); }
                catch (Exception ex) { Console.WriteLine($"[RVP] cam load failed: {ex.Message}"); }
            }

            var dummies = new Dictionary<string, CamFile.CamDummy>(StringComparer.OrdinalIgnoreCase);
            if (cam != null)
                foreach (var d in cam.Dummies)
                    if (!dummies.ContainsKey(d.Name))
                        dummies[d.Name] = d;

            var markerMesh = CreateUnitCube(model, "RVP_marker", "RVP_marker_mat", new Vector4(0.2f, 1f, 1f, 0.45f), 0.5f);
            Animation? anim = null;
            int created = 0, withMesh = 0, withAnim = 0, noDummy = 0;
            var log = new List<object>();

            foreach (var rvpPath in rvpFiles)
            {
                RvpFile rvp;
                try { rvp = RvpFile.Load(rvpPath); }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RVP] {Path.GetFileName(rvpPath)}: parse failed: {ex.Message}");
                    continue;
                }

                var bindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var b in rvp.PrepareBindings)
                    bindings[b.ObjectName] = b.DummyName.TrimStart('*');

                foreach (var obj in rvp.Objects)
                {
                    bindings.TryGetValue(obj.Name, out var dummyName);
                    CamFile.CamDummy? dummy = null;
                    if (dummyName != null) dummies.TryGetValue(dummyName, out dummy);

                    if (dummy == null)
                    {
                        noDummy++;
                        log.Add(new { Object = obj.Name, Rvp = Path.GetFileName(rvpPath), Dummy = dummyName, Error = "no dummy track" });
                        continue;
                    }

                    var node = gltfScene.CreateNode($"RVP_{obj.Name}");

                    string? resolvedMesh = ResolveClientPath(dataRoot, obj.MeshPath, ".msh");
                    bool meshOk = false;
                    string? meshErr = null;
                    if (resolvedMesh != null)
                    {
                        if (SptModelBridge.TryCreateMesh(model, resolvedMesh, out var mesh, out var reason) && mesh != null)
                        {
                            node.Mesh = mesh;
                            meshOk = true;
                            withMesh++;
                        }
                        else meshErr = reason;
                    }
                    if (!meshOk)
                    {
                        var box = node.CreateNode($"RVP_{obj.Name}_box");
                        box.Mesh = markerMesh;
                        try { box.LocalTransform = Matrix4x4.CreateScale(100f); } catch { }
                    }

                    bool animated = false;
                    var posKeys = dummy.PosKeys
                        .Where(k => k.Key >= 0 && IsFinite(k.Value))
                        .GroupBy(k => k.Key)
                        .Select(g => g.First())
                        .OrderBy(k => k.Key)
                        .ToList();

                    if (posKeys.Count > 0)
                    {
                        var trs = new Dictionary<float, Vector3>();
                        var rts = new Dictionary<float, Quaternion>();
                        Quaternion prevQ = default;
                        foreach (var k in posKeys)
                        {
                            float t = k.Key / 30f;
                            trs[t] = new Vector3(k.Value.X, mirrorY ? -k.Value.Y : k.Value.Y, k.Value.Z);

                            // поворот: ближайший ключ rot по frame, иначе baseQuat
                            var rq = dummy.BaseQuat;
                            if (dummy.RotKeys.Count > 0)
                            {
                                rq = dummy.RotKeys
                                    .OrderBy(rk => Math.Abs(rk.Key - k.Key))
                                    .First().Value;
                            }
                            var rm = Matrix4x4.CreateFromQuaternion(rq);
                            rm = MirrorSptMatrix(rm, mirrorY);
                            if (Matrix4x4.Decompose(rm, out _, out var q, out _))
                            {
                                if (rts.Count > 0 && Quaternion.Dot(q, prevQ) < 0f) q = Negate(q);
                                prevQ = q;
                                rts[t] = q;
                            }

                            if (posKeys.Count == 1) break;
                        }

                        if (trs.Count > 0)
                        {
                            anim ??= model.CreateAnimation($"{mapName}_RVP");
                            anim.CreateTranslationChannel(node, trs, true);
                            if (rts.Count > 0) anim.CreateRotationChannel(node, rts, true);
                            // начальный трансформ = первый ключ (для статичных просмотрщиков)
                            try
                            {
                                var firstT = trs.OrderBy(x => x.Key).First();
                                node.LocalTransform = Matrix4x4.CreateTranslation(firstT.Value);
                            }
                            catch { }
                            animated = true;
                            withAnim++;
                        }
                    }

                    if (!animated)
                    {
                        var pos = new Vector3(dummy.BasePos.X, mirrorY ? -dummy.BasePos.Y : dummy.BasePos.Y, dummy.BasePos.Z);
                        if (!IsFinite(pos)) pos = Vector3.Zero;
                        var rm = Matrix4x4.CreateFromQuaternion(dummy.BaseQuat);
                        rm = MirrorSptMatrix(rm, mirrorY);
                        var m = rm * Matrix4x4.CreateTranslation(pos);
                        if (obj.Scale is > 0 and < 1000)
                            m = Matrix4x4.CreateScale(obj.Scale.Value) * m;
                        try { node.LocalTransform = IsFinite(m) ? m : Matrix4x4.CreateTranslation(pos); }
                        catch { node.LocalTransform = Matrix4x4.CreateTranslation(pos); }
                    }

                    SetNodeExtras(node, new
                    {
                        type = "rvp_object",
                        rvp = Path.GetFileName(rvpPath),
                        dummy = dummyName,
                        mesh = obj.MeshPath,
                        meshResolved = resolvedMesh != null ? Path.GetRelativePath(dataRoot, resolvedMesh).Replace('\\', '/') : null,
                        meshOk,
                        meshError = meshErr,
                        animated,
                        collision = obj.Collision,
                        meshId = obj.MeshId,
                        scale = obj.Scale
                    });

                    log.Add(new
                    {
                        Object = obj.Name,
                        Rvp = Path.GetFileName(rvpPath),
                        Dummy = dummyName,
                        Mesh = obj.MeshPath,
                        MeshOk = meshOk,
                        MeshError = meshErr,
                        Animated = animated,
                        PosKeys = dummy.PosKeys.Count,
                        RotKeys = dummy.RotKeys.Count
                    });
                    created++;
                }
            }

            Console.WriteLine($"[RVP] objects: {created} (meshes: {withMesh}, animated: {withAnim}), no-dummy: {noDummy}");
            if (cam != null && cam.Warnings.Count > 0)
                Console.WriteLine($"[RVP] cam warnings: {cam.Warnings.Count}");
            DiagnosticsOutput.WriteDiagnostic(mapName, "rvp_objects.json", JsonSerializer.Serialize(new
            {
                CamFile = camPath != null ? Path.GetFileName(camPath) : null,
                CamWarnings = cam?.Warnings,
                Objects = log
            }, SafeJson));
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

        /// <summary>Подготовленный канал анимации ноды: либо покадровые TRS-словари, либо константный local.</summary>
        private sealed class PreparedChannel
        {
            public string Role { get; }
            public Dictionary<float, Vector3>? T { get; }
            public Dictionary<float, Quaternion>? R { get; }
            public Dictionary<float, Vector3>? S { get; }
            public SharpGLTF.Transforms.AffineTransform? ConstantLocal { get; }

            private PreparedChannel(string role, Dictionary<float, Vector3>? t, Dictionary<float, Quaternion>? r,
                Dictionary<float, Vector3>? s, SharpGLTF.Transforms.AffineTransform? constantLocal)
            {
                Role = role;
                T = t;
                R = r;
                S = s;
                ConstantLocal = constantLocal;
            }

            public static PreparedChannel Animated(string role, Dictionary<float, Vector3> t,
                Dictionary<float, Quaternion> r, Dictionary<float, Vector3> s) =>
                new(role, t, r, s, null);

            public static PreparedChannel Constant(string role, SharpGLTF.Transforms.AffineTransform local) =>
                new(role, null, null, null, local);
        }

        private static Quaternion Negate(Quaternion q) => new(-q.X, -q.Y, -q.Z, -q.W);

        /// <summary>Проверяет, что сэмплы анимации действительно меняются (иначе каналы бесполезны).</summary>
        private static bool SamplesVary(IReadOnlyList<Collision.BspFile.BspObjectAnimSample> samples)
        {
            const float eps = 1e-4f;
            var first = samples[0];
            foreach (var s in samples)
            {
                if (Vector3.DistanceSquared(s.Translation, first.Translation) > eps) return true;
                if (Vector3.DistanceSquared(s.Scale, first.Scale) > eps) return true;
                // кватернионы: q и -q — одна и та же ориентация
                float dot = Quaternion.Dot(s.Rotation, first.Rotation);
                if (1f - MathF.Abs(dot) > 1e-5f) return true;
            }
            return false;
        }

        private static float[] MatrixToFloats(Matrix4x4 m) => new[]
        {
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44
        };

        /// <summary>Матрица близка к identity (с учётом масштаба элементов).</summary>
        private static bool NearlyIdentity(Matrix4x4 m)
        {
            return NearlyEqual(m.M11, 1f) && NearlyEqual(m.M12, 0f) && NearlyEqual(m.M13, 0f) && NearlyEqual(m.M14, 0f)
                && NearlyEqual(m.M21, 0f) && NearlyEqual(m.M22, 1f) && NearlyEqual(m.M23, 0f) && NearlyEqual(m.M24, 0f)
                && NearlyEqual(m.M31, 0f) && NearlyEqual(m.M32, 0f) && NearlyEqual(m.M33, 1f) && NearlyEqual(m.M34, 0f)
                && NearlyEqual(m.M41, 0f) && NearlyEqual(m.M42, 0f) && NearlyEqual(m.M43, 0f) && NearlyEqual(m.M44, 1f);
        }

        /// <summary>Поэлементное сравнение матриц с допуском, масштабированным по величине элементов.</summary>
        private static bool NearlyEqual(Matrix4x4 a, Matrix4x4 b)
        {
            return NearlyEqual(a.M11, b.M11) && NearlyEqual(a.M12, b.M12) && NearlyEqual(a.M13, b.M13) && NearlyEqual(a.M14, b.M14)
                && NearlyEqual(a.M21, b.M21) && NearlyEqual(a.M22, b.M22) && NearlyEqual(a.M23, b.M23) && NearlyEqual(a.M24, b.M24)
                && NearlyEqual(a.M31, b.M31) && NearlyEqual(a.M32, b.M32) && NearlyEqual(a.M33, b.M33) && NearlyEqual(a.M34, b.M34)
                && NearlyEqual(a.M41, b.M41) && NearlyEqual(a.M42, b.M42) && NearlyEqual(a.M43, b.M43) && NearlyEqual(a.M44, b.M44);
        }

        private static bool NearlyEqual(float a, float b)
        {
            float tol = 1e-3f * MathF.Max(1f, MathF.Max(MathF.Abs(a), MathF.Abs(b)));
            return MathF.Abs(a - b) <= tol;
        }

        private static float MaxAbs(Matrix4x4 m)
        {
            float d = 0f;
            d = MathF.Max(d, MathF.Abs(m.M11)); d = MathF.Max(d, MathF.Abs(m.M12));
            d = MathF.Max(d, MathF.Abs(m.M13)); d = MathF.Max(d, MathF.Abs(m.M14));
            d = MathF.Max(d, MathF.Abs(m.M21)); d = MathF.Max(d, MathF.Abs(m.M22));
            d = MathF.Max(d, MathF.Abs(m.M23)); d = MathF.Max(d, MathF.Abs(m.M24));
            d = MathF.Max(d, MathF.Abs(m.M31)); d = MathF.Max(d, MathF.Abs(m.M32));
            d = MathF.Max(d, MathF.Abs(m.M33)); d = MathF.Max(d, MathF.Abs(m.M34));
            d = MathF.Max(d, MathF.Abs(m.M41)); d = MathF.Max(d, MathF.Abs(m.M42));
            d = MathF.Max(d, MathF.Abs(m.M43)); d = MathF.Max(d, MathF.Abs(m.M44));
            return d;
        }

        private static float MaxAbsDiff(Matrix4x4 a, Matrix4x4 b)
        {
            float d = 0f;
            d = MathF.Max(d, MathF.Abs(a.M11 - b.M11)); d = MathF.Max(d, MathF.Abs(a.M12 - b.M12));
            d = MathF.Max(d, MathF.Abs(a.M13 - b.M13)); d = MathF.Max(d, MathF.Abs(a.M14 - b.M14));
            d = MathF.Max(d, MathF.Abs(a.M21 - b.M21)); d = MathF.Max(d, MathF.Abs(a.M22 - b.M22));
            d = MathF.Max(d, MathF.Abs(a.M23 - b.M23)); d = MathF.Max(d, MathF.Abs(a.M24 - b.M24));
            d = MathF.Max(d, MathF.Abs(a.M31 - b.M31)); d = MathF.Max(d, MathF.Abs(a.M32 - b.M32));
            d = MathF.Max(d, MathF.Abs(a.M33 - b.M33)); d = MathF.Max(d, MathF.Abs(a.M34 - b.M34));
            d = MathF.Max(d, MathF.Abs(a.M41 - b.M41)); d = MathF.Max(d, MathF.Abs(a.M42 - b.M42));
            d = MathF.Max(d, MathF.Abs(a.M43 - b.M43)); d = MathF.Max(d, MathF.Abs(a.M44 - b.M44));
            return d;
        }

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
            return CreateUnitCube(model, "DebugCube", "RedDebug", new Vector4(1, 0, 0, 1), 50f);
        }

        /// <summary>
        /// Маркеры helper-объектов SPT: юнит-куб ±0.5, полупрозрачный,
        /// цвет по классу helper'а. Реальный размер задаётся нодой-_box
        /// (scale = bbox из SPT), поэтому куб единый на всех.
        /// </summary>
        private static Dictionary<string, Mesh> CreateHelperMeshes(ModelRoot model)
        {
            var defs = new (string Kind, Vector4 Color)[]
            {
                ("helper", new Vector4(1.0f, 0.85f, 0.2f, 0.35f)),  // жёлтый — прочие helper'ы
                ("music",  new Vector4(0.2f, 1.0f, 0.3f, 0.35f)),   // зелёный — музыкальные зоны
                ("portal", new Vector4(1.0f, 0.5f, 0.1f, 0.40f)),   // оранжевый — dpgoto/dpfrom
                ("spawn",  new Vector4(0.2f, 0.8f, 1.0f, 0.40f)),   // голубой — dsstart
            };
            var dict = new Dictionary<string, Mesh>(StringComparer.OrdinalIgnoreCase);
            foreach (var (kind, color) in defs)
                dict[kind] = CreateUnitCube(model, $"SPT_marker_{kind}", $"SPT_marker_{kind}_mat", color, 0.5f);
            return dict;
        }

        private static Mesh CreateUnitCube(ModelRoot model, string meshName, string matName, Vector4 color, float s)
        {
            var meshBuilder = new MeshBuilder<VPOS, VTEX, VEMPTY>(meshName);
            var mat = new SharpGLTF.Materials.MaterialBuilder(matName)
                .WithBaseColor(color)
                .WithDoubleSide(true);
            if (color.W < 1f)
                mat.WithAlpha(SharpGLTF.Materials.AlphaMode.BLEND);

            var prim = meshBuilder.UsePrimitive(mat);

            var p0 = new Vector3(-s, -s, -s); var p1 = new Vector3( s, -s, -s);
            var p2 = new Vector3( s,  s, -s); var p3 = new Vector3(-s,  s, -s);
            var p4 = new Vector3(-s, -s,  s); var p5 = new Vector3( s, -s,  s);
            var p6 = new Vector3(-s,  s,  s); var p7 = new Vector3(-s,  s,  s);

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
