using System.Text;

namespace RFMapToolSharp.Parsing.R3;

/// <summary>
/// Reader for RF Online texture bundles (.r3t).
/// File layout (per r3d3dtex.cpp):
/// float version;
/// uint textureCount;
/// textureCount entries of 128-byte names;
/// then for each texture: uint size; followed by DDS bytes.
/// </summary>
public static class R3TFile
{
    public static R3TData Load(string path, bool skipTextureData = true)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: false);

        float version = br.ReadSingle();
        uint textureCount = br.ReadUInt32();

        var names = new List<string>((int)textureCount);
        for (int i = 0; i < textureCount; i++)
            names.Add(ReadCString(br.ReadBytes(128)));

        var textures = new List<R3TTexture>((int)textureCount);
        for (int i = 0; i < textureCount; i++)
        {
            uint size = br.ReadUInt32();
            long dataOffset = fs.Position;

            byte[]? data = null;
            if (!skipTextureData)
                data = br.ReadBytes((int)size);
            else
                fs.Position += size;

            textures.Add(new R3TTexture
            {
                Name = names[i],
                Size = size,
                DataOffset = dataOffset,
                Data = data
            });
        }

        return new R3TData
        {
            Version = version,
            Textures = textures
        };
    }

    private static string ReadCString(byte[] buffer)
    {
        int len = Array.IndexOf(buffer, (byte)0);
        if (len < 0) len = buffer.Length;
        return Encoding.ASCII.GetString(buffer, 0, len);
    }
}

public sealed class R3TData
{
    public float Version { get; init; }
    public List<R3TTexture> Textures { get; init; } = new();
}

public sealed class R3TTexture
{
    public string Name { get; init; } = string.Empty;
    public uint Size { get; init; }
    public long DataOffset { get; init; }
    public byte[]? Data { get; init; }
}
