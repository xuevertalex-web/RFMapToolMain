using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Numerics;

namespace RFMapToolSharp.Collision;

/// <summary>
/// Подробный парсер BSP/EBP на основе структур R3bsp.h.
/// </summary>
public sealed class BspFile
{
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

    public byte[] ObjectRaw { get; private set; } = Array.Empty<byte>();
    public byte[] TrackRaw { get; private set; } = Array.Empty<byte>();
    public IReadOnlyList<ushort> EventObjectIds { get; private set; } = Array.Empty<ushort>();
    private Matrix4x4[] ObjectMatrices { get; set; } = Array.Empty<Matrix4x4>();

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

        file.BuildRealGeometry();
        file.BuildObjectMatrices();

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

        for (int m = 0; m < MatGroups.Count; m++)
        {
            var mg = MatGroups[m];
            int functionId = (mg.Attr & 0x8000) != 0 ? 1 : (mg.Attr & 0x4000) != 0 ? 2 : 4;

            for (int j = 0; j < mg.FaceNum; j++)
            {
                int faceIndex = (int)FaceId[(int)(mg.FaceStartId + (uint)j)];
                var face = Faces[faceIndex];
                if (face.VertexCount < 3)
                    continue;

                var realIndices = new List<int>(face.VertexCount);
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
                    if (mg.ObjectId > 0)
                    {
                        int oid = mg.ObjectId - 1;
                        if (oid >= 0 && oid < ObjectMatrices.Length)
                        {
                            var p = Vector3.Transform(new Vector3(pos.X, pos.Y, pos.Z), ObjectMatrices[oid]);
                            pos = new Vector3f { X = p.X, Y = p.Y, Z = p.Z };
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
                }

                if (realIndices.Count < 3)
                    continue;

                // Fan triangulation over validated corners only.
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
    }

    private void BuildObjectMatrices()
    {
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
            var s = Quaternion.Normalize(new Quaternion(read[i].ScaleQuat.X, read[i].ScaleQuat.Y, read[i].ScaleQuat.Z, read[i].ScaleQuat.W));
            var r = Quaternion.Normalize(new Quaternion(read[i].Quat.X, read[i].Quat.Y, read[i].Quat.Z, read[i].Quat.W));
            var scale = Matrix4x4.CreateScale(read[i].Scale.X, read[i].Scale.Y, read[i].Scale.Z);
            var sq = Matrix4x4.CreateFromQuaternion(s);
            Matrix4x4.Invert(sq, out var invSq);
            var sMatrix = sq * scale * invSq;
            var rot = Matrix4x4.CreateFromQuaternion(r);
            rot.Translation = new Vector3(read[i].Pos.X, read[i].Pos.Y, read[i].Pos.Z);
            locals[i] = sMatrix * rot;
        }

        var world = new Matrix4x4[count];
        for (int i = 0; i < count; i++)
        {
            world[i] = BuildWorld(i, read, locals, world);
        }

        ObjectMatrices = world;
    }

    private static Matrix4x4 BuildWorld(int i, ReadAniObject[] read, Matrix4x4[] locals, Matrix4x4[] world)
    {
        if (world[i] != default) return world[i];
        int p = read[i].Parent - 1;
        if (p < 0 || p >= read.Length) return locals[i];
        var pw = BuildWorld(p, read, locals, world);
        return pw * locals[i];
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
