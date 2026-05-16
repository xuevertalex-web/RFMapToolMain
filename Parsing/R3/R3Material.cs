using System.Text;

namespace RFMapToolSharp.Parsing.R3;

/// <summary>
/// Reader for RF Online material files (.r3m / .r3x).
/// Based on the original R3Material.h/R3Material.cpp structures.
/// </summary>
public static class R3Material
{
    public const float ExpectedVersion = 1.1f;

    public static R3MaterialFile Load(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: false);

        var version = br.ReadSingle();
        if (Math.Abs(version - ExpectedVersion) > 0.001f)
            throw new InvalidDataException($"Unexpected R3M version {version} in {path}");

        uint materialCount = br.ReadUInt32();
        var materials = new List<Material>((int)materialCount);

        for (int i = 0; i < materialCount; i++)
        {
            uint layerCount = br.ReadUInt32();
            uint flags = br.ReadUInt32(); // m_dwFlag
            int detailSurface = br.ReadInt32();
            float detailScale = br.ReadSingle();

            var nameBytes = br.ReadBytes(128);
            string name = ReadCString(nameBytes);

            var layers = new List<Layer>((int)layerCount);
            for (int l = 0; l < layerCount; l++)
                layers.Add(ReadLayer(br));

            materials.Add(new Material
            {
                Name = name,
                Flags = flags,
                DetailSurface = detailSurface,
                DetailScale = detailScale,
                Layers = layers
            });
        }

        return new R3MaterialFile
        {
            Version = version,
            Materials = materials
        };
    }

    private static Layer ReadLayer(BinaryReader br)
    {
        // _ONE_LAYER layout from R3Material.h
        var layer = new Layer
        {
            TileAniTexNum = br.ReadInt16(),
            Surface = br.ReadInt32(),
            AlphaType = br.ReadUInt32(),
            Argb = br.ReadUInt32(),
            Flags = br.ReadUInt32(),
            UvLavaWave = br.ReadInt16(),
            UvLavaSpeed = br.ReadInt16(),
            UvScrollU = br.ReadInt16(),
            UvScrollV = br.ReadInt16(),
            UvRotate = br.ReadInt16(),
            UvScaleStart = br.ReadInt16(),
            UvScaleEnd = br.ReadInt16(),
            UvScaleSpeed = br.ReadInt16(),
            UvMetal = br.ReadInt16(),
            AniAlphaFlicker = br.ReadInt16(),
            AniAlphaFlickerAni = br.ReadUInt16(),
            AniTexFrame = br.ReadInt16(),
            AniTexSpeed = br.ReadInt16(),
            GradientAlpha = br.ReadInt16()
        };

        return layer;
    }

    private static string ReadCString(byte[] buffer)
    {
        int len = Array.IndexOf(buffer, (byte)0);
        if (len < 0) len = buffer.Length;
        return Encoding.ASCII.GetString(buffer, 0, len);
    }
}

public sealed class R3MaterialFile
{
    public float Version { get; init; }
    public List<Material> Materials { get; init; } = new();
}

public sealed class Material
{
    public string Name { get; init; } = string.Empty;
    public uint Flags { get; init; }
    public int DetailSurface { get; init; }
    public float DetailScale { get; init; }
    public List<Layer> Layers { get; init; } = new();
}

public sealed class Layer
{
    public short TileAniTexNum { get; init; }
    public int Surface { get; init; }
    public uint AlphaType { get; init; }
    public uint Argb { get; init; }
    public uint Flags { get; init; }
    public short UvLavaWave { get; init; }
    public short UvLavaSpeed { get; init; }
    public short UvScrollU { get; init; }
    public short UvScrollV { get; init; }
    public short UvRotate { get; init; }
    public short UvScaleStart { get; init; }
    public short UvScaleEnd { get; init; }
    public short UvScaleSpeed { get; init; }
    public short UvMetal { get; init; }
    public short AniAlphaFlicker { get; init; }
    public ushort AniAlphaFlickerAni { get; init; }
    public short AniTexFrame { get; init; }
    public short AniTexSpeed { get; init; }
    public short GradientAlpha { get; init; }
}
