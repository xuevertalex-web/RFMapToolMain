using System.Text;

namespace RFMapToolSharp.Parsing.R3;

public record BspEntry(uint Offset, uint Size);

/// <summary>
/// Minimal reader for BSP header (geometry container).
/// Use this to inspect chunk offsets/sizes without unpacking every blob.
/// </summary>
public sealed class BspFile
{
    public uint Version { get; init; }

    // Core chunks
    public BspEntry CPlanes { get; init; } = default!;
    public BspEntry CFaceId { get; init; } = default!;
    public BspEntry Node { get; init; } = default!;
    public BspEntry Leaf { get; init; } = default!;
    public BspEntry MatListInLeaf { get; init; } = default!;

    public BspEntry Object { get; init; } = default!;
    public BspEntry Track { get; init; } = default!;
    public BspEntry EventObjectId { get; init; } = default!;
    public IReadOnlyList<BspEntry> ReadSpare { get; init; } = Array.Empty<BspEntry>();

    // Free chunks (actual geometry/UV/material data)
    public BspEntry BVertex { get; init; } = default!;
    public BspEntry WVertex { get; init; } = default!;
    public BspEntry FVertex { get; init; } = default!;
    public BspEntry VertexColor { get; init; } = default!;
    public BspEntry UV { get; init; } = default!;
    public BspEntry LgtUV { get; init; } = default!;
    public BspEntry Face { get; init; } = default!;
    public BspEntry FaceId { get; init; } = default!;
    public BspEntry VertexId { get; init; } = default!;
    public BspEntry ReadMatGroup { get; init; } = default!;
    public BspEntry MapEntitiesList { get; init; } = default!;
    public BspEntry SoundEntityList { get; init; } = default!;
    public BspEntry SoundEntitiesList { get; init; } = default!;
    public IReadOnlyList<BspEntry> FreeSpare { get; init; } = Array.Empty<BspEntry>();

    public static BspFile Load(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: false);

        var file = new BspFile
        {
            Version = br.ReadUInt32(),
            CPlanes = ReadEntry(br),
            CFaceId = ReadEntry(br),
            Node = ReadEntry(br),
            Leaf = ReadEntry(br),
            MatListInLeaf = ReadEntry(br),
            Object = ReadEntry(br),
            Track = ReadEntry(br),
            EventObjectId = ReadEntry(br),
            ReadSpare = ReadEntryArray(br, count: 35),

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
            MapEntitiesList = ReadEntry(br),
            SoundEntityList = ReadEntry(br),
            SoundEntitiesList = ReadEntry(br),
            FreeSpare = ReadEntryArray(br, count: 32)
        };

        return file;
    }

    private static BspEntry ReadEntry(BinaryReader br) => new(br.ReadUInt32(), br.ReadUInt32());

    private static IReadOnlyList<BspEntry> ReadEntryArray(BinaryReader br, int count)
    {
        var entries = new BspEntry[count];
        for (int i = 0; i < count; i++)
            entries[i] = ReadEntry(br);
        return entries;
    }
}

/// <summary>
/// Extended BSP (.ebp) header (collision/entities layer).
/// </summary>
public sealed class ExtBspFile
{
    public uint Version { get; init; }
    public BspEntry CFVertex { get; init; } = default!;
    public BspEntry CFLine { get; init; } = default!;
    public BspEntry CFLineId { get; init; } = default!;
    public BspEntry CFLeaf { get; init; } = default!;
    public BspEntry EntityList { get; init; } = default!;
    public BspEntry EntityId { get; init; } = default!;
    public BspEntry LeafEntityList { get; init; } = default!;
    public BspEntry SoundEntityId { get; init; } = default!;
    public BspEntry LeafSoundEntityList { get; init; } = default!;
    public IReadOnlyList<BspEntry> ReadSpare { get; init; } = Array.Empty<BspEntry>();
    public BspEntry MapEntitiesList { get; init; } = default!;
    public BspEntry SoundEntityList { get; init; } = default!;
    public BspEntry SoundEntitiesList { get; init; } = default!;
    public IReadOnlyList<BspEntry> FreeSpare { get; init; } = Array.Empty<BspEntry>();

    public static ExtBspFile Load(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: false);

        var file = new ExtBspFile
        {
            Version = br.ReadUInt32(),
            CFVertex = ReadEntry(br),
            CFLine = ReadEntry(br),
            CFLineId = ReadEntry(br),
            CFLeaf = ReadEntry(br),
            EntityList = ReadEntry(br),
            EntityId = ReadEntry(br),
            LeafEntityList = ReadEntry(br),
            SoundEntityId = ReadEntry(br),
            LeafSoundEntityList = ReadEntry(br),
            ReadSpare = ReadEntryArray(br, count: 18),
            MapEntitiesList = ReadEntry(br),
            SoundEntityList = ReadEntry(br),
            SoundEntitiesList = ReadEntry(br),
            FreeSpare = ReadEntryArray(br, count: 18)
        };

        return file;
    }

    private static BspEntry ReadEntry(BinaryReader br) => new(br.ReadUInt32(), br.ReadUInt32());

    private static IReadOnlyList<BspEntry> ReadEntryArray(BinaryReader br, int count)
    {
        var entries = new BspEntry[count];
        for (int i = 0; i < count; i++)
            entries[i] = ReadEntry(br);
        return entries;
    }
}
