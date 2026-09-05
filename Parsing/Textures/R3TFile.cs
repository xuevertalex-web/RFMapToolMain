using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RFMapToolSharp.Textures;

/// <summary>
/// Парсер .r3t по описанию из R3Engine/1stclass/r3d3dtex.cpp.
/// </summary>
public class R3TFile
{
    public float Version { get; private set; }
    public List<R3TTextureEntry> Textures { get; } = new();

    public static R3TFile Load(string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: false);

        var r3t = new R3TFile
        {
            Version = br.ReadSingle() // float version
        };

        uint texCount = br.ReadUInt32(); // DWORD textureCount

        // имена текстур
        var names = new string[texCount];
        for (int i = 0; i < texCount; i++)
        {
            var nameBytes = br.ReadBytes(128); // char name[128]
            int zero = Array.IndexOf(nameBytes, (byte)0);
            if (zero < 0) zero = nameBytes.Length;
            names[i] = Encoding.ASCII.GetString(nameBytes, 0, zero);
        }

        // DDS-данные
        for (int i = 0; i < texCount; i++)
        {
            uint ddsSize = br.ReadUInt32();   // DWORD ddsSize
            byte[] ddsData = br.ReadBytes((int)ddsSize);

            r3t.Textures.Add(new R3TTextureEntry
            {
                Name = names[i],
                DdsData = ddsData
            });
        }

        return r3t;
    }

    /// <summary>
    /// Запись .r3t в том же формате, что читает Load:
    /// float version; uint textureCount; textureCount имён по 128 байт; затем пары uint size + DDS bytes.
    /// </summary>
    public void Save(string path)
    {
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs, Encoding.ASCII, leaveOpen: false);

        bw.Write(Version);
        bw.Write((uint)Textures.Count);

        foreach (var tex in Textures)
        {
            var nameBytes = new byte[128];
            var raw = Encoding.ASCII.GetBytes(tex.Name ?? string.Empty);
            Array.Copy(raw, 0, nameBytes, 0, Math.Min(raw.Length, nameBytes.Length - 1));
            bw.Write(nameBytes);
        }

        foreach (var tex in Textures)
        {
            var data = tex.DdsData ?? Array.Empty<byte>();
            bw.Write((uint)data.Length);
            bw.Write(data);
        }
    }

    public void ExportAll(string outDir)
    {
        Directory.CreateDirectory(outDir);

        foreach (var tex in Textures)
        {
            var safeName = string.Join("_",
                tex.Name.Split(Path.GetInvalidFileNameChars(),
                StringSplitOptions.RemoveEmptyEntries));

            if (string.IsNullOrWhiteSpace(safeName))
                safeName = "tex";

            var outPath = Path.Combine(outDir, safeName + ".dds");
            File.WriteAllBytes(outPath, tex.DdsData);
        }
    }
}
