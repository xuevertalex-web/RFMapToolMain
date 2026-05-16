using System.Collections.Generic;

namespace RFMapToolSharp.Collision;

public record BspEntry(uint Offset, uint Size);

public sealed class BspHeader
{
    public uint Version { get; init; }

    public BspEntry CPlanes { get; init; } = new(0, 0);
    public BspEntry CFaceId { get; init; } = new(0, 0);
    public BspEntry Node { get; init; } = new(0, 0);
    public BspEntry Leaf { get; init; } = new(0, 0);
    public BspEntry MatListInLeaf { get; init; } = new(0, 0);

    public BspEntry Object { get; init; } = new(0, 0);
    public BspEntry Track { get; init; } = new(0, 0);
    public BspEntry EventObjectId { get; init; } = new(0, 0);
    public IReadOnlyList<BspEntry> ReadSpare { get; init; } = new List<BspEntry>();

    public BspEntry BVertex { get; init; } = new(0, 0);
    public BspEntry WVertex { get; init; } = new(0, 0);
    public BspEntry FVertex { get; init; } = new(0, 0);
    public BspEntry VertexColor { get; init; } = new(0, 0);
    public BspEntry UV { get; init; } = new(0, 0);
    public BspEntry LgtUV { get; init; } = new(0, 0);
    public BspEntry Face { get; init; } = new(0, 0);
    public BspEntry FaceId { get; init; } = new(0, 0);
    public BspEntry VertexId { get; init; } = new(0, 0);
    public BspEntry ReadMatGroup { get; init; } = new(0, 0);
    public IReadOnlyList<BspEntry> FreeSpare { get; init; } = new List<BspEntry>();
}

public sealed class ExtBspHeader
{
    public uint Version { get; init; }
    public BspEntry CFVertex { get; init; } = new(0, 0);
    public BspEntry CFLine { get; init; } = new(0, 0);
    public BspEntry CFLineId { get; init; } = new(0, 0);
    public BspEntry CFLeaf { get; init; } = new(0, 0);
    public BspEntry EntityList { get; init; } = new(0, 0);
    public BspEntry EntityId { get; init; } = new(0, 0);
    public BspEntry LeafEntityList { get; init; } = new(0, 0);
    public BspEntry SoundEntityId { get; init; } = new(0, 0);
    public BspEntry LeafSoundEntityList { get; init; } = new(0, 0);
    public IReadOnlyList<BspEntry> ReadSpare { get; init; } = new List<BspEntry>();
    public BspEntry MapEntitiesList { get; init; } = new(0, 0);
    public BspEntry SoundEntityList { get; init; } = new(0, 0);
    public BspEntry SoundEntitiesList { get; init; } = new(0, 0);
    public IReadOnlyList<BspEntry> FreeSpare { get; init; } = new List<BspEntry>();
}

public sealed class BspNode
{
    public uint FNormalId { get; init; }
    public float D { get; init; }
    public short Front { get; init; }
    public short Back { get; init; }
    public Vector3s BbMin { get; init; }
    public Vector3s BbMax { get; init; }
}

public sealed class BspLeaf
{
    public byte Type { get; init; }
    public ushort FaceNum { get; init; }
    public uint FaceStartId { get; init; }
    public ushort MatGroupNum { get; init; }
    public uint MatGroupStartId { get; init; }
    public Vector3s BbMin { get; init; }
    public Vector3s BbMax { get; init; }
}

public sealed class BspReadFace
{
    public ushort VertexCount { get; init; }
    public uint VertexStartId { get; init; }
}

public sealed class BspReadMatGroup
{
    public ushort Attr { get; init; }
    public ushort FaceNum { get; init; }
    public uint FaceStartId { get; init; }
    public short MtlId { get; init; }
    public short LgtId { get; init; }
    public Vector3s BbMin { get; init; }
    public Vector3s BbMax { get; init; }
    public Vector3f Pos { get; init; }
    public float Scale { get; init; }
    public ushort ObjectId { get; init; }
}

public sealed class BspTriangle
{
    public int A { get; init; }
    public int B { get; init; }
    public int C { get; init; }
    public int MatGroup { get; init; }
    public int MatId { get; init; }
}

public sealed class BspCFace
{
    public byte Attr { get; init; }
    public byte VertexCount { get; init; }
    public uint VertexStartId { get; init; }
    public ushort MatGroupIndex { get; init; }
    public Vector4f Normal { get; init; }
}

public sealed class ToolColLine
{
    public uint Attr { get; init; }
    public ushort StartVertex { get; init; }
    public ushort EndVertex { get; init; }
    public float Height { get; init; }
    public ushort Front { get; init; }
    public ushort Back { get; init; }
}

public sealed class ToolColLeaf
{
    public uint StartId { get; init; }
    public ushort LineNum { get; init; }
}

public sealed class LeafEntitiesInfo
{
    public uint StartId { get; init; }
    public ushort EntitiesNum { get; init; }
}

public sealed class EntityListEntry
{
    public byte IsParticle { get; init; }
    public byte IsFileExist { get; init; }
    public string Name { get; init; } = string.Empty;
    public float FadeStart { get; init; }
    public float FadeEnd { get; init; }
    public ushort Flag { get; init; }
    public ushort ShaderId { get; init; }
    public float Factor0 { get; init; }
    public float Factor1 { get; init; }
}

public sealed class ReadMapEntity
{
    public ushort Id { get; init; }
    public float Scale { get; init; }
    public Vector3f Pos { get; init; }
    public float RotX { get; init; }
    public float RotY { get; init; }
    public Vector3s BbMin { get; init; }
    public Vector3s BbMax { get; init; }
}

public sealed class ReadSoundEntity
{
    public string Name { get; init; } = string.Empty;
}

public sealed class SoundEntityEntry
{
    public ushort Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class ReadSoundEntityInstance
{
    public ushort Id { get; init; }
    public ushort EventTime { get; init; }
    public uint Flag { get; init; }
    public float Scale { get; init; }
    public float Attn { get; init; }
    public Vector3f Pos { get; init; }
    public Vector3f BoxScale { get; init; }
    public float BoxAttn { get; init; }
    public float BoxRotX { get; init; }
    public float BoxRotY { get; init; }
    public uint Spare { get; init; }
}
