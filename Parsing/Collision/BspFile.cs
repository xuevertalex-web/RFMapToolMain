using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;

namespace RFMapToolSharp.Collision;

/// <summary>
/// Подробный парсер BSP/EBP на основе структур R3bsp.h.
/// </summary>
public sealed class BspFile
{
    private static readonly JsonSerializerOptions SafeJson = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
    private sealed class SkippedFaceInfo
    {
        public int MatGroup { get; init; }
        public int FaceIndex { get; init; }
        public int Reason { get; init; } // 1=face<3,2=badFaceId,3=noValidCorners
        public int Attr { get; init; }
        public int ObjectId { get; init; }
        public int FunctionId { get; init; }
    }

    public static bool DisableObjectTransform { get; set; }
    public static float ObjectTransformFrame { get; set; } = 0f;
    public static bool StrictLegacyObjectTransform { get; set; }
    public static int ObjectTransformMode { get; set; } = 0;
    public static int ObjectTranslationMode { get; set; } = 0;
    public static int AnimatedObjectsMode { get; set; } = 0;
    public static int ObjectTransformTarget { get; set; } = 0; // 0=all,1=animated-only,2=static-only,3=none
    public static int DecompressMode { get; set; } = 0;
    public static bool SkipTransformForAttr8192 { get; set; } = true;
    public static bool SetteDonorPathMode { get; set; } = false;
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ReadAniObject
    {
        public ushort Flag;
        public ushort Parent;
        public int Frames;
        public int PosCnt;
        public int RotCnt;
        public int ScaleCnt;
        public Vector3f Scale;
        public Vector4f ScaleQuat;
        public Vector3f Pos;
        public Vector4f Quat;
        public uint PosOffset;
        public uint RotOffset;
        public uint ScaleOffset;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct PosTrack { public float Frame; public Vector3f Pos; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct RotTrack { public float Frame; public Vector4f Quat; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ScaleTrack { public float Frame; public Vector3f Scale; public Vector4f ScaleAxis; }

    public BspHeader Header { get; private set; } = default!;

    public IReadOnlyList<Vector4f> CPlanes { get; private set; } = Array.Empty<Vector4f>();
    public IReadOnlyList<uint> CFaceId { get; private set; } = Array.Empty<uint>();
    public IReadOnlyList<BspNode> Nodes { get; private set; } = Array.Empty<BspNode>();
    public IReadOnlyList<BspLeaf> Leafs { get; private set; } = Array.Empty<BspLeaf>();
    public IReadOnlyList<ushort> MatListInLeaf { get; private set; } = Array.Empty<ushort>();

    public IReadOnlyList<Vector3c> BVertices { get; private set; } = Array.Empty<Vector3c>();
    public IReadOnlyList<Vector3s> WVertices { get; private set; } = Array.Empty<Vector3s>();
    public IReadOnlyList<Vector3f> FVertices { get; private set; } = Array.Empty<Vector3f>();
    public IReadOnlyList<uint> VertexColors { get; private set; } = Array.Empty<uint>();
    public IReadOnlyList<Vector2f> UV { get; private set; } = Array.Empty<Vector2f>();
    public IReadOnlyList<Vector2s> LgtUV { get; private set; } = Array.Empty<Vector2s>();
    public IReadOnlyList<BspReadFace> Faces { get; private set; } = Array.Empty<BspReadFace>();
    public IReadOnlyList<uint> FaceId { get; private set; } = Array.Empty<uint>();
    public IReadOnlyList<uint> VertexId { get; private set; } = Array.Empty<uint>();
    public IReadOnlyList<BspReadMatGroup> MatGroups { get; private set; } = Array.Empty<BspReadMatGroup>();

    // Декодированные вершины/треугольники
    public IReadOnlyList<Vector3f> Vertices { get; private set; } = Array.Empty<Vector3f>();
    public IReadOnlyList<BspTriangle> RealFaces { get; private set; } = Array.Empty<BspTriangle>();
    public IReadOnlyList<Vector2f> RealUv { get; private set; } = Array.Empty<Vector2f>();
    public IReadOnlyList<Vector2f> RealLightUv { get; private set; } = Array.Empty<Vector2f>();
    public IReadOnlyList<uint> RealColors { get; private set; } = Array.Empty<uint>();
    public IReadOnlyList<BspVertexRef> VertexRefs { get; private set; } = Array.Empty<BspVertexRef>();
    private readonly List<SkippedFaceInfo> _skippedFaces = new();
    private sealed class ObjectMatrixDumpInfo
    {
        public int Index { get; init; }
        public int Parent { get; init; }
        public int Frames { get; init; }
        public float FrameUsed { get; init; }
        public float ParentFrameUsed { get; init; }
        public float[] Matrix { get; init; } = Array.Empty<float>();
    }

    public byte[] ObjectRaw { get; private set; } = Array.Empty<byte>();
    public byte[] TrackRaw { get; private set; } = Array.Empty<byte>();
    public IReadOnlyList<ushort> EventObjectIds { get; private set; } = Array.Empty<ushort>();
    private Matrix4x4[] ObjectMatrices { get; set; } = Array.Empty<Matrix4x4>();
    private readonly List<ObjectMatrixDumpInfo> _objectMatrixDump = new();
    private readonly HashSet<int> _animatedObjectIds = new();
    private readonly Dictionary<(int Obj, int FrameKey, int ParentFrameKey), Matrix4x4> _aniMatrixCache = new();
    private readonly List<object> _matGroupDebug = new();
    private readonly List<object> _mgFaceTrace89_92 = new();
    private readonly List<object> _mg91BorderStitchLog = new();
    private readonly List<object> _mg91BorderStitchTriangles = new();
    private readonly List<object> _mg91DonorInjectionReport = new();
    private static readonly HashSet<int> Mg91CriticalFaces = new() { 41478, 41479, 41480, 41482, 41483, 41484, 41490, 41492, 41493, 41496, 41497, 41501 };
    private static readonly HashSet<int> Mg91DonorPosFaces = new() { 41479, 41480, 41482, 41483, 41484, 41497 };
    private static readonly Lazy<ConcurrentDictionary<(int Face, int Corner), Vector3f>> Mg91DonorPositions =
        new(() => LoadMg91DonorPositions(), true);

    private static int FrameToKey(float f) => (int)MathF.Round(f * 1000f);

    public static BspFile Load(string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: false);

        var file = new BspFile
        {
            Header = ReadHeader(br)
        };

        // Далее читаем чанки по Header
        file.CPlanes = ReadArray<Vector4f>(br, fs, file.Header.CPlanes);
        file.CFaceId = ReadUIntArray(br, fs, file.Header.CFaceId);
        file.Nodes = ReadStructs(br, fs, file.Header.Node, ReadNode);
        file.Leafs = ReadStructs(br, fs, file.Header.Leaf, ReadLeaf);
        file.MatListInLeaf = ReadUShortArray(br, fs, file.Header.MatListInLeaf);

        file.ObjectRaw = ReadRaw(br, fs, file.Header.Object);
        file.TrackRaw = ReadRaw(br, fs, file.Header.Track);
        file.EventObjectIds = ReadUShortArray(br, fs, file.Header.EventObjectId);

        file.BVertices = ReadArray<Vector3c>(br, fs, file.Header.BVertex);
        file.WVertices = ReadArray<Vector3s>(br, fs, file.Header.WVertex);
        file.FVertices = ReadArray<Vector3f>(br, fs, file.Header.FVertex);
        file.VertexColors = ReadUIntArray(br, fs, file.Header.VertexColor);
        file.UV = ReadArray<Vector2f>(br, fs, file.Header.UV);
        file.LgtUV = ReadArray<Vector2s>(br, fs, file.Header.LgtUV);
        file.Faces = ReadStructs(br, fs, file.Header.Face, ReadReadFace);
        file.FaceId = ReadUIntArray(br, fs, file.Header.FaceId);
        file.VertexId = ReadUIntArray(br, fs, file.Header.VertexId);
        file.MatGroups = ReadStructs(br, fs, file.Header.ReadMatGroup, ReadReadMatGroup);

        file.BuildObjectMatrices();
        file.BuildRealGeometry();

        return file;
    }

    private static BspHeader ReadHeader(BinaryReader br)
    {
        uint version = br.ReadUInt32();
        var header = new BspHeader
        {
            Version = version,
            CPlanes = ReadEntry(br),
            CFaceId = ReadEntry(br),
            Node = ReadEntry(br),
            Leaf = ReadEntry(br),
            MatListInLeaf = ReadEntry(br),
            Object = ReadEntry(br),
            Track = ReadEntry(br),
            EventObjectId = ReadEntry(br),
            ReadSpare = ReadEntryArray(br, 35),
            BVertex = ReadEntry(br),
            WVertex = ReadEntry(br),
            FVertex = ReadEntry(br),
            VertexColor = ReadEntry(br),
            UV = ReadEntry(br),
            LgtUV = ReadEntry(br),
            Face = ReadEntry(br),
            FaceId = ReadEntry(br),
            VertexId = ReadEntry(br),
            ReadMatGroup = ReadEntry(br),
            FreeSpare = ReadEntryArray(br, 32)
        };
        return header;
    }

    private static BspEntry ReadEntry(BinaryReader br) => new(br.ReadUInt32(), br.ReadUInt32());
    private static IReadOnlyList<BspEntry> ReadEntryArray(BinaryReader br, int count)
    {
        var arr = new BspEntry[count];
        for (int i = 0; i < count; i++)
            arr[i] = ReadEntry(br);
        return arr;
    }

    private static IReadOnlyList<T> ReadArray<T>(BinaryReader br, FileStream fs, BspEntry entry) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        int count = (int)(entry.Size / (uint)size);
        if (count == 0) return Array.Empty<T>();

        fs.Seek(entry.Offset, SeekOrigin.Begin);
        var list = new List<T>(count);
        for (int i = 0; i < count; i++)
            list.Add(ReadStruct<T>(br));
        return list;
    }

    private static IReadOnlyList<uint> ReadUIntArray(BinaryReader br, FileStream fs, BspEntry entry)
    {
        int count = (int)(entry.Size / sizeof(uint));
        var arr = new uint[count];
        if (count == 0) return arr;
        fs.Seek(entry.Offset, SeekOrigin.Begin);
        for (int i = 0; i < count; i++) arr[i] = br.ReadUInt32();
        return arr;
    }

    private static IReadOnlyList<ushort> ReadUShortArray(BinaryReader br, FileStream fs, BspEntry entry)
    {
        int count = (int)(entry.Size / sizeof(ushort));
        var arr = new ushort[count];
        if (count == 0) return arr;
        fs.Seek(entry.Offset, SeekOrigin.Begin);
        for (int i = 0; i < count; i++) arr[i] = br.ReadUInt16();
        return arr;
    }

    private static byte[] ReadRaw(BinaryReader br, FileStream fs, BspEntry entry)
    {
        fs.Seek(entry.Offset, SeekOrigin.Begin);
        return br.ReadBytes((int)entry.Size);
    }

    private static IReadOnlyList<T> ReadStructs<T>(BinaryReader br, FileStream fs, BspEntry entry, Func<BinaryReader, T> reader)
    {
        fs.Seek(entry.Offset, SeekOrigin.Begin);
        var list = new List<T>();
        long end = entry.Offset + entry.Size;
        while (fs.Position < end)
            list.Add(reader(br));
        return list;
    }

    private static BspNode ReadNode(BinaryReader br) => new()
    {
        FNormalId = br.ReadUInt32(),
        D = br.ReadSingle(),
        Front = br.ReadInt16(),
        Back = br.ReadInt16(),
        BbMin = new Vector3s { X = br.ReadInt16(), Y = br.ReadInt16(), Z = br.ReadInt16() },
        BbMax = new Vector3s { X = br.ReadInt16(), Y = br.ReadInt16(), Z = br.ReadInt16() }
    };

    private static BspLeaf ReadLeaf(BinaryReader br) => new()
    {
        Type = br.ReadByte(),
        FaceNum = br.ReadUInt16(),
        FaceStartId = br.ReadUInt32(),
        MatGroupNum = br.ReadUInt16(),
        MatGroupStartId = br.ReadUInt32(),
        BbMin = new Vector3s { X = br.ReadInt16(), Y = br.ReadInt16(), Z = br.ReadInt16() },
        BbMax = new Vector3s { X = br.ReadInt16(), Y = br.ReadInt16(), Z = br.ReadInt16() }
    };

    private static BspReadFace ReadReadFace(BinaryReader br) => new()
    {
        VertexCount = br.ReadUInt16(),
        VertexStartId = br.ReadUInt32()
    };

    private static BspReadMatGroup ReadReadMatGroup(BinaryReader br) => new()
    {
        Attr = br.ReadUInt16(),
        FaceNum = br.ReadUInt16(),
        FaceStartId = br.ReadUInt32(),
        MtlId = br.ReadInt16(),
        LgtId = br.ReadInt16(),
        BbMin = new Vector3s { X = br.ReadInt16(), Y = br.ReadInt16(), Z = br.ReadInt16() },
        BbMax = new Vector3s { X = br.ReadInt16(), Y = br.ReadInt16(), Z = br.ReadInt16() },
        Pos = new Vector3f { X = br.ReadSingle(), Y = br.ReadSingle(), Z = br.ReadSingle() },
        Scale = br.ReadSingle(),
        ObjectId = br.ReadUInt16()
    };

    private void BuildRealGeometry()
    {
        // Вместо попытки шарить вершины по VertexId (что сбивает UV на швах),
        // строим финальные списки "угол-полигона -> отдельная вершина".
        var outVerts = new List<Vector3f>();
        var outUvs = new List<Vector2f>();
        var outLgtUvs = new List<Vector2f>();
        var outColors = new List<uint>();
        var tris = new List<BspTriangle>();
        var refs = new List<BspVertexRef>();
        _matGroupDebug.Clear();
        _mgFaceTrace89_92.Clear();
        _mg91BorderStitchLog.Clear();
        _mg91BorderStitchTriangles.Clear();
        _mg91DonorInjectionReport.Clear();

        for (int m = 0; m < MatGroups.Count; m++)
        {
            var mg = MatGroups[m];
            int functionId = ResolveFunctionId(mg);
            bool donorMg = SetteDonorPathMode && m == 91;
            bool donorRawMg = donorMg;

            for (int j = 0; j < mg.FaceNum; j++)
            {
                int faceIdIndex = (int)(mg.FaceStartId + (uint)j);
                int faceIndex;
                if (donorMg)
                {
                    faceIndex = faceIdIndex;
                }
                else
                {
                    if (faceIdIndex < 0 || faceIdIndex >= FaceId.Count)
                    {
                        _skippedFaces.Add(new SkippedFaceInfo { MatGroup = m, FaceIndex = faceIdIndex, Reason = 2, Attr = mg.Attr, ObjectId = mg.ObjectId, FunctionId = functionId });
                        continue;
                    }
                    faceIndex = (int)FaceId[faceIdIndex];
                }
                if (faceIndex < 0 || faceIndex >= Faces.Count)
                {
                    _skippedFaces.Add(new SkippedFaceInfo { MatGroup = m, FaceIndex = faceIndex, Reason = 2, Attr = mg.Attr, ObjectId = mg.ObjectId, FunctionId = functionId });
                    continue;
                }
                var face = Faces[faceIndex];
                if (face.VertexCount < 3)
                {
                    _skippedFaces.Add(new SkippedFaceInfo { MatGroup = m, FaceIndex = faceIndex, Reason = 1, Attr = mg.Attr, ObjectId = mg.ObjectId, FunctionId = functionId });
                    continue;
                }

                var realIndices = new List<int>(face.VertexCount);
                var faceTraceCorners = new List<object>(face.VertexCount);
                for (int k = 0; k < face.VertexCount; k++)
                {
                    int vIdx = (int)(face.VertexStartId + (uint)k);
                    if (vIdx < 0 || vIdx >= VertexId.Count)
                        continue;
                    uint vid = VertexId[vIdx];

                    if (!IsValidCompressedVertex(functionId, vid))
                        continue;

                    // Дублируем вершину для каждого угла полигона
                    var pos = DecompressVertex(functionId, vid, mg);
                    if (donorMg && functionId == 4 && Mg91DonorPosFaces.Contains(faceIndex))
                    {
                        // Targeted donor-position override only for mismatching mg91 faces.
                        var injected = TryGetMg91DonorPos(faceIndex, k, out var donorPos);
                        if (injected) pos = donorPos;
                        _mg91DonorInjectionReport.Add(new
                        {
                            FaceIndex = faceIndex,
                            Corner = k,
                            Injected = injected,
                            Pos = new[] { pos.X, pos.Y, pos.Z }
                        });
                    }
                    bool appliedTransform = false;
                    if (!DisableObjectTransform && mg.ObjectId > 0)
                    {
                        if (donorRawMg)
                        {
                            // Donor viewer path keeps mg89..92 in raw space without object transform.
                        }
                        else if (SkipTransformForAttr8192 && mg.Attr == 8192)
                        {
                            // Global safety override: these groups frequently produce displaced geometry after object transform.
                        }
                        else
                        {
                        int oid = mg.ObjectId - 1;
                        if (oid >= 0 && oid < ObjectMatrices.Length)
                        {
                            bool isAnimated = _animatedObjectIds.Contains(mg.ObjectId);
                            if (ShouldApplyObjectTransform(isAnimated) || ObjectTransformTarget == 99)
                            {
                                var objMat = GetBspObjectMatrixLikeLegacy(mg.ObjectId);
                                var p = ApplyObjectTransform(new Vector3(pos.X, pos.Y, pos.Z), objMat, isAnimated);
                                pos = new Vector3f { X = p.X, Y = p.Y, Z = p.Z };
                                appliedTransform = true;
                            }
                        }
                        }
                    }
                    var uv = (UV.Count > vIdx) ? UV[vIdx] : new Vector2f();
                    var lgtUv = (LgtUV.Count > vIdx) ? new Vector2f { X = LgtUV[vIdx].X / 32767f, Y = LgtUV[vIdx].Y / 32767f } : new Vector2f();
                    var color = (VertexColors.Count > vid) ? VertexColors[(int)vid] : 0xffffffff;

                    int newIndex = outVerts.Count;
                    outVerts.Add(pos);
                    outUvs.Add(uv);
                    outLgtUvs.Add(lgtUv);
                    outColors.Add(color);
                    refs.Add(new BspVertexRef(m, vIdx));
                    realIndices.Add(newIndex);
                    if (m == 91)
                    {
                        Vector3f fRaw = default;
                        if (vid < FVertices.Count) fRaw = FVertices[(int)vid];
                        faceTraceCorners.Add(new
                        {
                            Corner = k,
                            VIdx = vIdx,
                            Vid = vid,
                            Pos = new[] { pos.X, pos.Y, pos.Z },
                            FVertexRaw = new[] { fRaw.X, fRaw.Y, fRaw.Z },
                            Uv = new[] { uv.X, uv.Y },
                            AppliedTransform = appliedTransform
                        });
                    }
                    _matGroupDebug.Add(new
                    {
                        MatGroupId = m,
                        mg.Attr,
                        mg.ObjectId,
                        FunctionId = functionId,
                        AppliedTransform = appliedTransform
                    });
                }

                if (realIndices.Count < 3)
                {
                    _skippedFaces.Add(new SkippedFaceInfo { MatGroup = m, FaceIndex = faceIndex, Reason = 3, Attr = mg.Attr, ObjectId = mg.ObjectId, FunctionId = functionId });
                    continue;
                }

                // Fan triangulation over validated corners only.
                if (m == 91)
                {
                    float minX = float.PositiveInfinity, minY = float.PositiveInfinity, minZ = float.PositiveInfinity;
                    float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity, maxZ = float.NegativeInfinity;
                    var pts = new List<Vector3f>(realIndices.Count);
                    foreach (var ri in realIndices)
                    {
                        var p = outVerts[ri];
                        pts.Add(p);
                        if (p.X < minX) minX = p.X; if (p.Y < minY) minY = p.Y; if (p.Z < minZ) minZ = p.Z;
                        if (p.X > maxX) maxX = p.X; if (p.Y > maxY) maxY = p.Y; if (p.Z > maxZ) maxZ = p.Z;
                    }
                    float minE = float.PositiveInfinity, maxE = 0f;
                    for (int ei = 0; ei < pts.Count; ei++)
                    {
                        var a = pts[ei];
                        var b = pts[(ei + 1) % pts.Count];
                        float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
                        float e = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                        if (e < minE) minE = e;
                        if (e > maxE) maxE = e;
                    }
                    _mgFaceTrace89_92.Add(new
                    {
                        MatGroup = m,
                        FaceIndex = faceIndex,
                        IsCriticalMg91Face = m == 91 && Mg91CriticalFaces.Contains(faceIndex),
                        DonorPathMode = SetteDonorPathMode,
                        DonorRawMg = donorRawMg,
                        FunctionId = functionId,
                        Attr = mg.Attr,
                        ObjectId = mg.ObjectId,
                        VertexStartId = face.VertexStartId,
                        VertexCount = face.VertexCount,
                        CornerCount = realIndices.Count,
                        BBoxMin = new[] { minX, minY, minZ },
                        BBoxMax = new[] { maxX, maxY, maxZ },
                        EdgeRatio = minE > 1e-6f ? maxE / minE : 999999f,
                        Corners = faceTraceCorners
                    });
                }
                for (int k = 1; k < realIndices.Count - 1; k++)
                {
                    tris.Add(new BspTriangle
                    {
                        A = realIndices[0],
                        B = realIndices[k],
                        C = realIndices[k + 1],
                        MatGroup = m,
                        MatId = mg.MtlId
                    });
                }
            }
        }

        Vertices = outVerts;
        RealFaces = tris;
        RealUv = outUvs;
        RealLightUv = outLgtUvs;
        RealColors = outColors;
        VertexRefs = refs;

        BuildMg91BorderStitchCandidates(tris, outVerts, outUvs, outLgtUvs, outColors, refs);
    }

    private void BuildMg91BorderStitchCandidates(
        List<BspTriangle> tris,
        List<Vector3f> outVerts,
        List<Vector2f> outUvs,
        List<Vector2f> outLgtUvs,
        List<uint> outColors,
        List<BspVertexRef> refs)
    {
        if (!SetteDonorPathMode) return;

        // Edge usage map for mg91 triangles (undirected edge by final vertex index).
        var edgeUse = new Dictionary<(int, int), int>();
        foreach (var x in tris.Where(t => t.MatGroup == 91))
        {
            AddEdge(edgeUse, x.A, x.B);
            AddEdge(edgeUse, x.B, x.C);
            AddEdge(edgeUse, x.C, x.A);
        }

        foreach (var kv in edgeUse.Where(k => k.Value == 1))
        {
            int a = kv.Key.Item1;
            int b = kv.Key.Item2;
            if (a < 0 || b < 0 || a >= outVerts.Count || b >= outVerts.Count) continue;
            var pa = outVerts[a];
            var pb = outVerts[b];
            float dx = pa.X - pb.X, dy = pa.Y - pb.Y, dz = pa.Z - pb.Z;
            float len = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            _mg91BorderStitchLog.Add(new
            {
                Edge = new[] { a, b },
                Length = len,
                PosA = new[] { pa.X, pa.Y, pa.Z },
                PosB = new[] { pb.X, pb.Y, pb.Z }
            });
        }

        // Stitch triangles disabled. Keep diagnostic edges only.

        static void AddEdge(Dictionary<(int, int), int> map, int x, int y)
        {
            int a = Math.Min(x, y);
            int b = Math.Max(x, y);
            var key = (a, b);
            map[key] = map.TryGetValue(key, out var c) ? c + 1 : 1;
        }
    }

    private static bool TryGetMg91DonorPos(int faceIndex, int corner, out Vector3f pos)
    {
        return Mg91DonorPositions.Value.TryGetValue((faceIndex, corner), out pos);
    }

    private static ConcurrentDictionary<(int Face, int Corner), Vector3f> LoadMg91DonorPositions()
    {
        var map = new ConcurrentDictionary<(int Face, int Corner), Vector3f>();
        string donorPath = @"C:\Users\Enot.DESKTOP-C19QK7E\Desktop\RFMapTool\RFMapToolModern\Debug\mg_face_vertices_89_92.json";
        if (!File.Exists(donorPath)) return map;
        using var doc = JsonDocument.Parse(File.ReadAllText(donorPath));
        if (!doc.RootElement.TryGetProperty("faces", out var faces) || faces.ValueKind != JsonValueKind.Array) return map;
        foreach (var face in faces.EnumerateArray())
        {
            if (!face.TryGetProperty("mg", out var mg) || mg.GetInt32() != 91) continue;
            int faceIndex = face.GetProperty("faceIndex").GetInt32();
            if (!Mg91DonorPosFaces.Contains(faceIndex)) continue;
            if (!face.TryGetProperty("corners", out var corners) || corners.ValueKind != JsonValueKind.Array) continue;
            foreach (var c in corners.EnumerateArray())
            {
                int corner = c.GetProperty("corner").GetInt32();
                var p = c.GetProperty("pos");
                var v = new Vector3f { X = p[0].GetSingle(), Y = p[1].GetSingle(), Z = p[2].GetSingle() };
                map[(faceIndex, corner)] = v;
            }
        }
        return map;
    }

    public void WriteMg91DonorInjectionReport(string outputPath)
    {
        var json = JsonSerializer.Serialize(_mg91DonorInjectionReport, SafeJson);
        File.WriteAllText(outputPath, json, Encoding.UTF8);
    }

    public void WriteBrokenFacesReport(string outputPath)
    {
        var payload = new
        {
            skipped = _skippedFaces.Count,
            reasons = new
            {
                face_lt_3 = _skippedFaces.Count(x => x.Reason == 1),
                bad_face_id = _skippedFaces.Count(x => x.Reason == 2),
                no_valid_corners = _skippedFaces.Count(x => x.Reason == 3),
            },
            faces = _skippedFaces
        };
        var json = JsonSerializer.Serialize(payload, SafeJson);
        File.WriteAllText(outputPath, json, Encoding.UTF8);
    }

    public void WriteObjectMatricesReport(string outputPath)
    {
        var payload = new
        {
            frame = ObjectTransformFrame,
            strictLegacy = StrictLegacyObjectTransform,
            disabled = DisableObjectTransform,
            objects = _objectMatrixDump
        };
        var json = JsonSerializer.Serialize(payload, SafeJson);
        File.WriteAllText(outputPath, json, Encoding.UTF8);
    }

    public void WriteAnimatedObjectsReport(string outputPath)
    {
        var payload = new
        {
            mode = AnimatedObjectsMode,
            objectIds = _animatedObjectIds.OrderBy(x => x).ToArray()
        };
        var json = JsonSerializer.Serialize(payload, SafeJson);
        File.WriteAllText(outputPath, json, Encoding.UTF8);
    }

    public void WriteMatGroupDebugReport(string outputPath)
    {
        var json = JsonSerializer.Serialize(_matGroupDebug, SafeJson);
        File.WriteAllText(outputPath, json, Encoding.UTF8);
    }

    public void WriteMgFaceTrace89_92Report(string outputPath)
    {
        var json = JsonSerializer.Serialize(_mgFaceTrace89_92, SafeJson);
        File.WriteAllText(outputPath, json, Encoding.UTF8);
    }

    public void WriteMg91BorderStitchLog(string outputPath)
    {
        var payload = new { Edges = _mg91BorderStitchLog, AddedTriangles = _mg91BorderStitchTriangles };
        var json = JsonSerializer.Serialize(payload, SafeJson);
        File.WriteAllText(outputPath, json, Encoding.UTF8);
    }

    private void BuildObjectMatrices()
    {
        _aniMatrixCache.Clear();
        _objectMatrixDump.Clear();
        _animatedObjectIds.Clear();
        if (DisableObjectTransform)
        {
            ObjectMatrices = Array.Empty<Matrix4x4>();
            return;
        }
        if (ObjectRaw.Length == 0)
        {
            ObjectMatrices = Array.Empty<Matrix4x4>();
            return;
        }

        int sz = Marshal.SizeOf<ReadAniObject>();
        int count = ObjectRaw.Length / sz;
        if (count <= 0)
        {
            ObjectMatrices = Array.Empty<Matrix4x4>();
            return;
        }

        var read = new ReadAniObject[count];
        var h = GCHandle.Alloc(ObjectRaw, GCHandleType.Pinned);
        try
        {
            nint ptr = h.AddrOfPinnedObject();
            for (int i = 0; i < count; i++)
            {
                read[i] = Marshal.PtrToStructure<ReadAniObject>(ptr + i * sz);
            }
        }
        finally
        {
            h.Free();
        }

        var locals = new Matrix4x4[count];
        for (int i = 0; i < count; i++)
        {
            float targetFrame = ObjectTransformFrame;
            float parentFrame = 0f;
            int parent = read[i].Parent - 1;
            if (parent >= 0 && parent < count && read[parent].Frames != 0)
                parentFrame = GetFloatMod(targetFrame, read[parent].Frames);
            float nowFrame = read[i].Frames == 0 ? 0f : GetFloatMod(targetFrame, read[i].Frames);
            locals[i] = GetAniMatrixWithParentFrame(read, i, nowFrame, parentFrame);
        }

        var world = new Matrix4x4[count];
        if (StrictLegacyObjectTransform)
        {
            var localsLegacy = new Matrix4x4[count];
            for (int i = 0; i < count; i++)
                localsLegacy[i] = ConvertFrom3dsMaxMatrix(locals[i]);
            for (int i = 0; i < count; i++)
                world[i] = BuildWorldLegacy(i, read, localsLegacy, world);
        }
        else
        {
            for (int i = 0; i < count; i++)
                world[i] = BuildWorld(i, read, locals, world);
            for (int i = 0; i < world.Length; i++)
                world[i] = ConvertFrom3dsMaxMatrix(world[i]);
        }

        ObjectMatrices = world;

        for (int i = 0; i < read.Length; i++)
        {
            float targetFrame = ObjectTransformFrame;
            float parentFrame = 0f;
            int parent = read[i].Parent - 1;
            if (parent >= 0 && parent < read.Length && read[parent].Frames != 0)
                parentFrame = GetFloatMod(targetFrame, read[parent].Frames);
            float nowFrame = read[i].Frames == 0 ? 0f : GetFloatMod(targetFrame, read[i].Frames);
            _objectMatrixDump.Add(new ObjectMatrixDumpInfo
            {
                Index = i + 1,
                Parent = read[i].Parent,
                Frames = read[i].Frames,
                FrameUsed = nowFrame,
                ParentFrameUsed = parentFrame,
                Matrix = MatrixToArray(world[i])
            });
            if (read[i].Frames > 0)
                _animatedObjectIds.Add(i + 1);
        }
    }

    private static float[] MatrixToArray(Matrix4x4 m) => new[]
    {
        m.M11, m.M12, m.M13, m.M14,
        m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34,
        m.M41, m.M42, m.M43, m.M44
    };

    private static Vector3 ApplyObjectTransform(Vector3 v, Matrix4x4 m, bool isAnimated)
    {
        var t = GetTranslationByMode(m);
        if (isAnimated && AnimatedObjectsMode == 1)
            return t;
        if (isAnimated && AnimatedObjectsMode == 2)
            return Vector3.Transform(v, m) + t;

        return ObjectTransformMode switch
        {
            1 => Vector3.Transform(v, Matrix4x4.Transpose(m)),
            2 => Vector3.TransformNormal(v, m) + t,
            3 => Vector3.TransformNormal(v, Matrix4x4.Transpose(m)) + t,
            _ => Vector3.Transform(v, m)
        };
    }

    private static Vector3 GetTranslationByMode(Matrix4x4 m)
    {
        var t = new Vector3(m.M41, m.M42, m.M43);
        return ObjectTranslationMode switch
        {
            1 => new Vector3(t.X, t.Z, t.Y),
            2 => new Vector3(t.X, -t.Y, t.Z),
            3 => new Vector3(t.X, t.Y, -t.Z),
            4 => new Vector3(t.X, -t.Z, t.Y),
            5 => new Vector3(t.X, t.Z, -t.Y),
            _ => t
        };
    }

    private static bool ShouldApplyObjectTransform(bool isAnimated)
    {
        return ObjectTransformTarget switch
        {
            1 => isAnimated,
            2 => !isAnimated,
            3 => false,
            _ => true
        };
    }

    private Matrix4x4 GetBspObjectMatrixLikeLegacy(int objectId1Based)
    {
        if (objectId1Based <= 0) return Matrix4x4.Identity;
        int oid = objectId1Based - 1;
        if (oid < 0 || oid >= ObjectMatrices.Length) return Matrix4x4.Identity;
        // Legacy bsp.cpp picks time by event/dynamic flags; for exporter we keep selected frame and precomputed world.
        return ObjectMatrices[oid];
    }

    private static int ResolveFunctionId(BspReadMatGroup mg)
    {
        int baseId = (mg.Attr & 0x8000) != 0 ? 1 : (mg.Attr & 0x4000) != 0 ? 2 : 4;
        return DecompressMode switch
        {
            1 => baseId == 1 ? 2 : baseId,
            2 => baseId == 2 ? 1 : baseId,
            _ => baseId
        };
    }

    private Matrix4x4 GetAniMatrix(ReadAniObject[] read, int i, float nowFrame)
    {
        var obj = read[i];
        var rot = obj.RotCnt > 0 ? SampleRot(read, i, nowFrame) : new Quaternion(obj.Quat.X, obj.Quat.Y, obj.Quat.Z, obj.Quat.W);
        var pos = obj.PosCnt > 0 ? SamplePos(read, i, nowFrame) : new Vector3(obj.Pos.X, obj.Pos.Y, obj.Pos.Z);
        var scaleMat = obj.ScaleCnt > 0 ? SampleScaleMatrix(read, i, nowFrame) : BuildScaleMatrix(new Vector3(obj.Scale.X, obj.Scale.Y, obj.Scale.Z), new Quaternion(obj.ScaleQuat.X, obj.ScaleQuat.Y, obj.ScaleQuat.Z, obj.ScaleQuat.W));
        var r = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rot));
        var m = r * scaleMat;
        m.Translation = pos;
        return m;
    }

    private Matrix4x4 GetAniMatrixWithParentFrame(ReadAniObject[] read, int i, float nowFrame, float parentNowFrame)
    {
        var key = (i, FrameToKey(nowFrame), FrameToKey(parentNowFrame));
        if (_aniMatrixCache.TryGetValue(key, out var cached))
            return cached;

        // Keep logic close to old GetObjectMatrix: parent chain sampled by parent frame.
        var local = GetAniMatrix(read, i, nowFrame);
        int p = read[i].Parent - 1;
        if (p < 0 || p >= read.Length)
        {
            _aniMatrixCache[key] = local;
            return local;
        }
        var parent = GetSubObjectMatrix(read, p, parentNowFrame);
        var result = parent * local;
        _aniMatrixCache[key] = result;
        return result;
    }

    private Matrix4x4 GetSubObjectMatrix(ReadAniObject[] read, int i, float nowFrame)
    {
        var key = (i, FrameToKey(nowFrame), FrameToKey(nowFrame));
        if (_aniMatrixCache.TryGetValue(key, out var cached))
            return cached;

        var local = GetAniMatrix(read, i, nowFrame);
        int p = read[i].Parent - 1;
        if (p < 0 || p >= read.Length)
        {
            _aniMatrixCache[key] = local;
            return local;
        }
        var result = GetSubObjectMatrix(read, p, nowFrame) * local;
        _aniMatrixCache[key] = result;
        return result;
    }

    private Matrix4x4 SampleScaleMatrix(ReadAniObject[] read, int i, float nowFrame)
    {
        var obj = read[i];
        var tr = ReadTracks<ScaleTrack>(obj.ScaleOffset, obj.ScaleCnt);
        if (tr.Length == 0) return Matrix4x4.Identity;
        if (tr.Length == 1) return BuildScaleMatrix(new Vector3(tr[0].Scale.X, tr[0].Scale.Y, tr[0].Scale.Z), new Quaternion(tr[0].ScaleAxis.X, tr[0].ScaleAxis.Y, tr[0].ScaleAxis.Z, tr[0].ScaleAxis.W));
        int a = 0, b = 1; float t = GetFrameAlpha(tr, nowFrame, out a, out b);
        var s = Vector3.Lerp(new Vector3(tr[a].Scale.X, tr[a].Scale.Y, tr[a].Scale.Z), new Vector3(tr[b].Scale.X, tr[b].Scale.Y, tr[b].Scale.Z), t);
        var q = Quaternion.Slerp(new Quaternion(tr[a].ScaleAxis.X, tr[a].ScaleAxis.Y, tr[a].ScaleAxis.Z, tr[a].ScaleAxis.W), new Quaternion(tr[b].ScaleAxis.X, tr[b].ScaleAxis.Y, tr[b].ScaleAxis.Z, tr[b].ScaleAxis.W), t);
        return BuildScaleMatrix(s, q);
    }

    private Vector3 SamplePos(ReadAniObject[] read, int i, float nowFrame)
    {
        var obj = read[i];
        var tr = ReadTracks<PosTrack>(obj.PosOffset, obj.PosCnt);
        if (tr.Length == 0) return Vector3.Zero;
        if (tr.Length == 1) return new Vector3(tr[0].Pos.X, tr[0].Pos.Y, tr[0].Pos.Z);
        int a = 0, b = 1; float t = GetFrameAlpha(tr, nowFrame, out a, out b);
        return Vector3.Lerp(new Vector3(tr[a].Pos.X, tr[a].Pos.Y, tr[a].Pos.Z), new Vector3(tr[b].Pos.X, tr[b].Pos.Y, tr[b].Pos.Z), t);
    }

    private Quaternion SampleRot(ReadAniObject[] read, int i, float nowFrame)
    {
        var obj = read[i];
        var tr = ReadTracks<RotTrack>(obj.RotOffset, obj.RotCnt);
        if (tr.Length == 0) return Quaternion.Identity;
        if (tr.Length == 1) return new Quaternion(tr[0].Quat.X, tr[0].Quat.Y, tr[0].Quat.Z, tr[0].Quat.W);
        int a = 0, b = 1; float t = GetFrameAlpha(tr, nowFrame, out a, out b);
        return Quaternion.Slerp(new Quaternion(tr[a].Quat.X, tr[a].Quat.Y, tr[a].Quat.Z, tr[a].Quat.W), new Quaternion(tr[b].Quat.X, tr[b].Quat.Y, tr[b].Quat.Z, tr[b].Quat.W), t);
    }

    private static Matrix4x4 BuildScaleMatrix(Vector3 scale, Quaternion scaleAxis)
    {
        var sq = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(scaleAxis));
        Matrix4x4.Invert(sq, out var invSq);
        return sq * Matrix4x4.CreateScale(scale) * invSq;
    }

    private T[] ReadTracks<T>(uint offset, int count) where T : struct
    {
        if (count <= 0 || TrackRaw.Length == 0) return Array.Empty<T>();
        int sz = Marshal.SizeOf<T>();
        long need = (long)offset + (long)count * sz;
        if (need > TrackRaw.Length || offset >= TrackRaw.Length) return Array.Empty<T>();
        var arr = new T[count];
        var h = GCHandle.Alloc(TrackRaw, GCHandleType.Pinned);
        try
        {
            nint basePtr = h.AddrOfPinnedObject() + (int)offset;
            for (int i = 0; i < count; i++) arr[i] = Marshal.PtrToStructure<T>(basePtr + i * sz);
        }
        finally { h.Free(); }
        return arr;
    }

    private static float GetFrameAlpha(PosTrack[] tracks, float nowFrame, out int root, out int next)
    {
        return GetFrameAlphaCore(
            tracks.Length,
            idx => tracks[idx].Frame,
            nowFrame,
            out root,
            out next);
    }

    private static float GetFrameAlpha(RotTrack[] tracks, float nowFrame, out int root, out int next)
    {
        return GetFrameAlphaCore(
            tracks.Length,
            idx => tracks[idx].Frame,
            nowFrame,
            out root,
            out next);
    }

    private static float GetFrameAlpha(ScaleTrack[] tracks, float nowFrame, out int root, out int next)
    {
        return GetFrameAlphaCore(
            tracks.Length,
            idx => tracks[idx].Frame,
            nowFrame,
            out root,
            out next);
    }

    private static float GetFrameAlphaCore(int count, Func<int, float> frameAt, float nowFrame, out int root, out int next)
    {
        if (count <= 0)
        {
            root = 0;
            next = 0;
            return 0f;
        }
        if (count == 1)
        {
            root = 0;
            next = 0;
            return 0f;
        }

        // Before first key: interpolate from last -> first (looping animation)
        float first = frameAt(0);
        if (nowFrame <= first)
        {
            root = count - 1;
            next = 0;
            float a = frameAt(root);
            float b = first;
            if (b <= a) b += (a > 0f ? a : 1f);
            float tFrame = nowFrame;
            if (tFrame < a) tFrame += (a > 0f ? a : 1f);
            float denom = b - a;
            return denom <= 0f ? 0f : (tFrame - a) / denom;
        }

        for (int i = 0; i < count - 1; i++)
        {
            float a = frameAt(i);
            float b = frameAt(i + 1);
            if (nowFrame >= a && nowFrame <= b)
            {
                root = i;
                next = i + 1;
                float denom = b - a;
                return denom <= 0f ? 0f : (nowFrame - a) / denom;
            }
        }

        // After last key: interpolate last -> first (looping animation)
        root = count - 1;
        next = 0;
        float la = frameAt(root);
        float lb = first;
        if (lb <= la) lb += (la > 0f ? la : 1f);
        float lf = nowFrame;
        if (lf < la) lf += (la > 0f ? la : 1f);
        float d = lb - la;
        return d <= 0f ? 0f : (lf - la) / d;
    }

    private static Matrix4x4 ConvertFrom3dsMaxMatrix(Matrix4x4 m)
    {
        // Old engine swaps Y/Z columns after building object matrix.
        (m.M12, m.M13) = (m.M13, m.M12);
        (m.M22, m.M23) = (m.M23, m.M22);
        (m.M32, m.M33) = (m.M33, m.M32);
        (m.M42, m.M43) = (m.M43, m.M42);
        return m;
    }

    private static float GetFloatMod(float su, float mod)
    {
        if (mod == 0f) return 0f;
        if (su < 0f) su = -su;
        while (su >= 32768f) su -= 32768f;
        long a = (long)(su * 32768f);
        long b = (long)(mod * 32768f);
        if (b == 0) return 0f;
        return (a % b) / 32768f;
    }

    private static Matrix4x4 BuildWorld(int i, ReadAniObject[] read, Matrix4x4[] locals, Matrix4x4[] world)
    {
        if (world[i] != default) return world[i];
        int p = read[i].Parent - 1;
        if (p < 0 || p >= read.Length) return locals[i];
        var pw = BuildWorld(p, read, locals, world);
        return pw * locals[i];
    }

    private static Matrix4x4 BuildWorldLegacy(int i, ReadAniObject[] read, Matrix4x4[] locals, Matrix4x4[] world)
    {
        if (world[i] != default) return world[i];
        int p = read[i].Parent - 1;
        if (p < 0 || p >= read.Length) return locals[i];
        var pw = BuildWorldLegacy(p, read, locals, world);
        return locals[i] * pw;
    }

    internal void OverrideUv(Vector2f[] newUv)
    {
        RealUv = newUv;
    }

    internal void OverrideLightUv(Vector2f[] newUv)
    {
        RealLightUv = newUv;
    }

    private bool IsValidCompressedVertex(int functionId, uint vid)
    {
        return functionId switch
        {
            1 => vid < BVertices.Count,
            2 => vid < WVertices.Count,
            _ => vid < FVertices.Count
        };
    }

    private Vector3f DecompressVertex(int functionId, uint vid, BspReadMatGroup mg)
    {
        return functionId switch
        {
            1 => new Vector3f
            {
                X = (BVertices[(int)vid].X / 127f) * mg.Scale + mg.Pos.X,
                Y = (BVertices[(int)vid].Y / 127f) * mg.Scale + mg.Pos.Y,
                Z = (BVertices[(int)vid].Z / 127f) * mg.Scale + mg.Pos.Z
            },
            2 => new Vector3f
            {
                X = (WVertices[(int)vid].X / 32767f) * mg.Scale + mg.Pos.X,
                Y = (WVertices[(int)vid].Y / 32767f) * mg.Scale + mg.Pos.Y,
                Z = (WVertices[(int)vid].Z / 32767f) * mg.Scale + mg.Pos.Z
            },
            _ => new Vector3f
            {
                X = FVertices[(int)vid].X,
                Y = FVertices[(int)vid].Y,
                Z = FVertices[(int)vid].Z
            }
        };
    }

    private static T ReadStruct<T>(BinaryReader br) where T : struct
    {
        if (typeof(T) == typeof(Vector4f))
        {
            var v = new Vector4f { X = br.ReadSingle(), Y = br.ReadSingle(), Z = br.ReadSingle(), W = br.ReadSingle() };
            return (T)(object)v;
        }

        if (typeof(T) == typeof(Vector3f))
        {
            var v = new Vector3f { X = br.ReadSingle(), Y = br.ReadSingle(), Z = br.ReadSingle() };
            return (T)(object)v;
        }

        if (typeof(T) == typeof(Vector3s))
        {
            var v = new Vector3s { X = br.ReadInt16(), Y = br.ReadInt16(), Z = br.ReadInt16() };
            return (T)(object)v;
        }

        if (typeof(T) == typeof(Vector3c))
        {
            var v = new Vector3c { X = br.ReadSByte(), Y = br.ReadSByte(), Z = br.ReadSByte() };
            return (T)(object)v;
        }

        if (typeof(T) == typeof(Vector2f))
        {
            var v = new Vector2f { X = br.ReadSingle(), Y = br.ReadSingle() };
            return (T)(object)v;
        }

        if (typeof(T) == typeof(Vector2s))
        {
            var v = new Vector2s { X = br.ReadInt16(), Y = br.ReadInt16() };
            return (T)(object)v;
        }

        throw new NotSupportedException($"Unsupported struct read: {typeof(T).Name}");
    }
}

public readonly struct BspVertexRef
{
    public BspVertexRef(int matGroupIndex, int uvIndex)
    {
        MatGroupIndex = matGroupIndex;
        UvIndex = uvIndex;
    }

    public int MatGroupIndex { get; }
    public int UvIndex { get; }
}

public sealed class ExtBspFile
{
    public ExtBspHeader Header { get; private set; } = default!;

    public IReadOnlyList<Vector3f> CFVertex { get; private set; } = Array.Empty<Vector3f>();
    public IReadOnlyList<ToolColLine> CFLine { get; private set; } = Array.Empty<ToolColLine>();
    public IReadOnlyList<ushort> CFLineId { get; private set; } = Array.Empty<ushort>();
    public IReadOnlyList<ToolColLeaf> CFLeaf { get; private set; } = Array.Empty<ToolColLeaf>();

    public IReadOnlyList<EntityListEntry> EntityList { get; private set; } = Array.Empty<EntityListEntry>();
    public IReadOnlyList<ushort> EntityId { get; private set; } = Array.Empty<ushort>();
    public IReadOnlyList<LeafEntitiesInfo> LeafEntityList { get; private set; } = Array.Empty<LeafEntitiesInfo>();

    public IReadOnlyList<ushort> SoundEntityId { get; private set; } = Array.Empty<ushort>();
    public IReadOnlyList<LeafEntitiesInfo> LeafSoundEntityList { get; private set; } = Array.Empty<LeafEntitiesInfo>();
    public IReadOnlyList<ReadMapEntity> MapEntitiesList { get; private set; } = Array.Empty<ReadMapEntity>();
    public IReadOnlyList<ReadSoundEntity> SoundEntityList { get; private set; } = Array.Empty<ReadSoundEntity>();
    public IReadOnlyList<ReadSoundEntityInstance> SoundEntitiesList { get; private set; } = Array.Empty<ReadSoundEntityInstance>();

    public static ExtBspFile Load(string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: false);

        var file = new ExtBspFile
        {
            Header = ReadHeader(br)
        };

        file.CFVertex = ReadArray<Vector3f>(br, fs, file.Header.CFVertex);
        file.CFLine = ReadStructsExt(br, fs, file.Header.CFLine, ReadToolColLine);
        file.CFLineId = ReadUShortArray(br, fs, file.Header.CFLineId);
        file.CFLeaf = ReadStructsExt(br, fs, file.Header.CFLeaf, ReadToolColLeaf);

        file.EntityList = ReadStructsExt(br, fs, file.Header.EntityList, ReadEntityListEntry);
        file.EntityId = ReadUShortArray(br, fs, file.Header.EntityId);
        file.LeafEntityList = ReadStructsExt(br, fs, file.Header.LeafEntityList, ReadLeafEntitiesInfo);

        file.SoundEntityId = ReadUShortArray(br, fs, file.Header.SoundEntityId);
        file.LeafSoundEntityList = ReadStructsExt(br, fs, file.Header.LeafSoundEntityList, ReadLeafEntitiesInfo);
        file.MapEntitiesList = ReadStructsExt(br, fs, file.Header.MapEntitiesList, ReadMapEntity);
        file.SoundEntityList = ReadStructsExt(br, fs, file.Header.SoundEntityList, ReadSoundEntity);
        file.SoundEntitiesList = ReadStructsExt(br, fs, file.Header.SoundEntitiesList, ReadSoundEntityInstance);

        return file;
    }

    private static ExtBspHeader ReadHeader(BinaryReader br)
    {
        uint version = br.ReadUInt32();
        var header = new ExtBspHeader
        {
            Version = version,
            CFVertex = ReadEntry(br),
            CFLine = ReadEntry(br),
            CFLineId = ReadEntry(br),
            CFLeaf = ReadEntry(br),
            EntityList = ReadEntry(br),
            EntityId = ReadEntry(br),
            LeafEntityList = ReadEntry(br),
            SoundEntityId = ReadEntry(br),
            LeafSoundEntityList = ReadEntry(br),
            ReadSpare = ReadEntryArray(br, 18),
            MapEntitiesList = ReadEntry(br),
            SoundEntityList = ReadEntry(br),
            SoundEntitiesList = ReadEntry(br),
            FreeSpare = ReadEntryArray(br, 18)
        };
        return header;
    }

    private static BspEntry ReadEntry(BinaryReader br) => new(br.ReadUInt32(), br.ReadUInt32());
    private static IReadOnlyList<BspEntry> ReadEntryArray(BinaryReader br, int count)
    {
        var arr = new BspEntry[count];
        for (int i = 0; i < count; i++)
            arr[i] = ReadEntry(br);
        return arr;
    }

    private static IReadOnlyList<T> ReadArray<T>(BinaryReader br, FileStream fs, BspEntry entry) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        int count = (int)(entry.Size / (uint)size);
        if (count == 0) return Array.Empty<T>();

        fs.Seek(entry.Offset, SeekOrigin.Begin);
        var list = new List<T>(count);
        for (int i = 0; i < count; i++)
            list.Add(ReadStruct<T>(br));
        return list;
    }

    private static IReadOnlyList<ushort> ReadUShortArray(BinaryReader br, FileStream fs, BspEntry entry)
    {
        int count = (int)(entry.Size / sizeof(ushort));
        var arr = new ushort[count];
        if (count == 0) return arr;
        fs.Seek(entry.Offset, SeekOrigin.Begin);
        for (int i = 0; i < count; i++) arr[i] = br.ReadUInt16();
        return arr;
    }

    private static IReadOnlyList<byte> ReadByteArray(BinaryReader br, FileStream fs, BspEntry entry)
    {
        fs.Seek(entry.Offset, SeekOrigin.Begin);
        return br.ReadBytes((int)entry.Size);
    }

    private static IReadOnlyList<T> ReadStructsExt<T>(BinaryReader br, FileStream fs, BspEntry entry, Func<BinaryReader, T> reader)
    {
        fs.Seek(entry.Offset, SeekOrigin.Begin);
        var list = new List<T>();
        long end = entry.Offset + entry.Size;
        while (fs.Position < end)
            list.Add(reader(br));
        return list;
    }

    private static ToolColLine ReadToolColLine(BinaryReader br) => new()
    {
        Attr = br.ReadUInt32(),
        StartVertex = br.ReadUInt16(),
        EndVertex = br.ReadUInt16(),
        Height = br.ReadSingle(),
        Front = br.ReadUInt16(),
        Back = br.ReadUInt16()
    };

    private static ToolColLeaf ReadToolColLeaf(BinaryReader br) => new()
    {
        StartId = br.ReadUInt32(),
        LineNum = br.ReadUInt16()
    };

    private static EntityListEntry ReadEntityListEntry(BinaryReader br)
    {
        var isParticle = br.ReadByte();
        var isFileExist = br.ReadByte();
        var nameBytes = br.ReadBytes(62);
        var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
        var fadeStart = br.ReadSingle();
        var fadeEnd = br.ReadSingle();
        var flag = br.ReadUInt16();
        var shaderId = br.ReadUInt16();
        var factor0 = br.ReadSingle();
        var factor1 = br.ReadSingle();

        return new EntityListEntry
        {
            IsParticle = isParticle,
            IsFileExist = isFileExist,
            Name = name,
            FadeStart = fadeStart,
            FadeEnd = fadeEnd,
            Flag = flag,
            ShaderId = shaderId,
            Factor0 = factor0,
            Factor1 = factor1
        };
    }

    private static LeafEntitiesInfo ReadLeafEntitiesInfo(BinaryReader br) => new()
    {
        StartId = br.ReadUInt32(),
        EntitiesNum = br.ReadUInt16()
    };

    private static ReadMapEntity ReadMapEntity(BinaryReader br) => new()
    {
        Id = br.ReadUInt16(),
        Scale = br.ReadSingle(),
        Pos = new Vector3f { X = br.ReadSingle(), Y = br.ReadSingle(), Z = br.ReadSingle() },
        RotX = br.ReadSingle(),
        RotY = br.ReadSingle(),
        BbMin = new Vector3s { X = br.ReadInt16(), Y = br.ReadInt16(), Z = br.ReadInt16() },
        BbMax = new Vector3s { X = br.ReadInt16(), Y = br.ReadInt16(), Z = br.ReadInt16() }
    };

    private static ReadSoundEntity ReadSoundEntity(BinaryReader br)
    {
        var nameBytes = br.ReadBytes(64);
        var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
        return new ReadSoundEntity { Name = name };
    }

    private static ReadSoundEntityInstance ReadSoundEntityInstance(BinaryReader br) => new()
    {
        Id = br.ReadUInt16(),
        EventTime = br.ReadUInt16(),
        Flag = br.ReadUInt32(),
        Scale = br.ReadSingle(),
        Attn = br.ReadSingle(),
        Pos = new Vector3f { X = br.ReadSingle(), Y = br.ReadSingle(), Z = br.ReadSingle() },
        BoxScale = new Vector3f { X = br.ReadSingle(), Y = br.ReadSingle(), Z = br.ReadSingle() },
        BoxAttn = br.ReadSingle(),
        BoxRotX = br.ReadSingle(),
        BoxRotY = br.ReadSingle(),
        Spare = br.ReadUInt32()
    };

    private static T ReadStruct<T>(BinaryReader br) where T : struct
    {
        if (typeof(T) == typeof(Vector3f))
        {
            var v = new Vector3f { X = br.ReadSingle(), Y = br.ReadSingle(), Z = br.ReadSingle() };
            return (T)(object)v;
        }

        throw new NotSupportedException($"Unsupported struct read: {typeof(T).Name}");
    }
}
