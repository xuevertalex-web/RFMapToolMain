using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using RFMapToolSharp.Collision;

namespace RFMapToolSharp.Materials;

/// <summary>
/// Полный парсер R3M (.r3m) по структурам _ONE_LAYER/_R3MATERIAL из R3Material.h.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct R3MMaterialLayer
{
    public short TileAniTexNum;       // m_iTileAniTexNum
    public int Surface;               // m_iSurface (texture index)
    public uint AlphaType;            // m_dwAlphaType
    public uint Argb;                 // m_ARGB
    public uint Flag;                 // m_dwFlag (UV/animation flags)
    public short UvLavaWave;          // m_sUVLavaWave
    public short UvLavaSpeed;         // m_sUVLavaSpeed
    public short UvScrollU;           // m_sUVScrollU
    public short UvScrollV;           // m_sUVScrollV
    public short UvRotate;            // m_sUVRotate
    public short UvScaleStart;        // m_sUVScaleStart
    public short UvScaleEnd;          // m_sUVScaleEnd
    public short UvScaleSpeed;        // m_sUVScaleSpeed
    public short UvMetal;             // m_sUVMetal
    public short AniAlphaFlicker;     // m_sANIAlphaFlicker
    public ushort AniAlphaFlickerAni; // m_sANIAlphaFlickerAni
    public short AniTexFrame;         // m_sANITexFrame
    public short AniTexSpeed;         // m_sANITexSpeed
    public short GradientAlpha;       // m_sGradientAlpha
}

public class R3MMaterial
{
    public string Name { get; set; } = string.Empty;
    public uint LayerCount { get; set; }
    public uint Flag { get; set; }
    public int DetailSurfaceId { get; set; }
    public float DetailScale { get; set; }
    public List<R3MMaterialLayer> Layers { get; } = new();
}

public class R3MMaterialFile
{
    public float Version { get; private set; }
    public List<R3MMaterial> Materials { get; } = new();

    /// <summary>
    /// Чтение R3M. baseTextureOffset позволяет сместить ссылки на текстуры (как AdjustIndependenceR3M в C++).
    /// </summary>
    public static R3MMaterialFile Load(string path, int baseTextureOffset = 0)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: false);

        var file = new R3MMaterialFile
        {
            Version = br.ReadSingle()
        };

        uint materialCount = br.ReadUInt32();
        for (int i = 0; i < materialCount; i++)
        {
            var mat = new R3MMaterial
            {
                LayerCount = br.ReadUInt32(),        // m_dwLayerNum
                Flag = br.ReadUInt32(),              // m_dwFlag (BOOL in C++, 4 байта)
                DetailSurfaceId = br.ReadInt32(),    // m_iDetailSurface
                DetailScale = br.ReadSingle()        // m_fDetailScale
            };

            var nameBytes = br.ReadBytes(128);       // m_name[_TEX_NAME_SIZE]
            mat.Name = ReadAsciiZ(nameBytes);

            for (int l = 0; l < mat.LayerCount; l++)
            {
                var layer = ReadLayer(br);
                if (layer.Surface != -1 && baseTextureOffset != 0)
                    layer.Surface += baseTextureOffset;
                mat.Layers.Add(layer);
            }

            if (mat.DetailSurfaceId != -1 && baseTextureOffset != 0)
                mat.DetailSurfaceId += baseTextureOffset;

            // Повторяем логику движка: если есть detail, флаг NO_LOD_TEXTURE включается.
            if (mat.DetailSurfaceId != -1)
                mat.Flag |= 0x00000002; // _MAT_FLAG_NO_LOD_TEXTURE

            file.Materials.Add(mat);
        }

        return file;
    }

    private static R3MMaterialLayer ReadLayer(BinaryReader br)
    {
        return new R3MMaterialLayer
        {
            TileAniTexNum = br.ReadInt16(),
            Surface = br.ReadInt32(),
            AlphaType = br.ReadUInt32(),
            Argb = br.ReadUInt32(),
            Flag = br.ReadUInt32(),
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
    }

    private static string ReadAsciiZ(byte[] bytes)
    {
        int zero = Array.IndexOf(bytes, (byte)0);
        if (zero < 0) zero = bytes.Length;
        return Encoding.ASCII.GetString(bytes, 0, zero);
    }

    internal static R3MMaterialFile? Load(object materialPath)
    {
        throw new NotImplementedException();
    }

}

/// <summary>
/// Парсер расширения материалов (.r3x) по структуре _EXT_MAT.
/// </summary>
public class R3XMaterialFile
{
    public float Version { get; private set; }
    public R3XMaterialExt Ext { get; private set; } = new();

    public static R3XMaterialFile Load(string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: false);

        var file = new R3XMaterialFile
        {
            Version = br.ReadSingle()
        };

        var ext = new R3XMaterialExt
        {
            Flag = br.ReadUInt32(),
            FogStart = br.ReadSingle(),
            FogEnd = br.ReadSingle(),
            FogColor = br.ReadUInt32(),
            FogStart2 = br.ReadSingle(),
            FogEnd2 = br.ReadSingle(),
            FogColor2 = br.ReadUInt32(),
            BbMin2 = new Vector3f { X = br.ReadSingle(), Y = br.ReadSingle(), Z = br.ReadSingle() },
            BbMax2 = new Vector3f { X = br.ReadSingle(), Y = br.ReadSingle(), Z = br.ReadSingle() },
            LensFlareScale = ReadFloats(br, 16),
            LensTexName = ReadAsciiZ(br.ReadBytes(128)),
            LensFlarePos = new Vector3f { X = br.ReadSingle(), Y = br.ReadSingle(), Z = br.ReadSingle() },
            EnvEntityName = ReadAsciiZ(br.ReadBytes(128)),
            EnvId = br.ReadUInt32()
        };

        // Остаток структуры (spare) 252+384 байт.
        ext.Spare = br.ReadBytes(252 + 384);
        file.Ext = ext;
        return file;
    }

    private static float[] ReadFloats(BinaryReader br, int count)
    {
        var arr = new float[count];
        for (int i = 0; i < count; i++)
            arr[i] = br.ReadSingle();
        return arr;
    }

    private static string ReadAsciiZ(byte[] bytes)
    {
        int zero = Array.IndexOf(bytes, (byte)0);
        if (zero < 0) zero = bytes.Length;
        return Encoding.ASCII.GetString(bytes, 0, zero);
    }
}

public class R3XMaterialExt
{
    public uint Flag { get; set; }
    public float FogStart { get; set; }
    public float FogEnd { get; set; }
    public uint FogColor { get; set; }
    public float FogStart2 { get; set; }
    public float FogEnd2 { get; set; }
    public uint FogColor2 { get; set; }
    public Vector3f BbMin2 { get; set; }
    public Vector3f BbMax2 { get; set; }
    public float[] LensFlareScale { get; set; } = Array.Empty<float>();
    public string LensTexName { get; set; } = string.Empty;
    public Vector3f LensFlarePos { get; set; }
    public string EnvEntityName { get; set; } = string.Empty;
    public uint EnvId { get; set; }
    public byte[] Spare { get; set; } = Array.Empty<byte>();
}

