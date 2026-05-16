using System;
using System.IO;
using System.Runtime.InteropServices;
using Pfim;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Advanced;

namespace RFMapToolSharp.Export;

public static class TextureConverter
{
    public static void SaveDdsAsPng(byte[] ddsData, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var buffer = (byte[])ddsData.Clone();
        XorHeader(buffer);

        using var ms = new MemoryStream(buffer);
        using var image = Pfim.Dds.Create(ms, new PfimConfig());
        if (image.Compressed)
            image.Decompress();

        using var sharp = ToImageSharp(image);
        sharp.Save(outputPath, new PngEncoder());
    }

    public static byte[] ToPngBytes(byte[] ddsData)
    {
        var buffer = (byte[])ddsData.Clone();
        XorHeader(buffer);

        using var ms = new MemoryStream(buffer);
        using var image = Pfim.Dds.Create(ms, new PfimConfig());
        if (image.Compressed)
            image.Decompress();

        using var sharp = ToImageSharp(image);
        using var outMs = new MemoryStream();
        sharp.Save(outMs, new PngEncoder());
        return outMs.ToArray();
    }

    private static Image<Rgba32> ToImageSharp(IImage image)
    {
        var data = image.Data;
        var width = image.Width;
        var height = image.Height;
        int bytesPerPixel = image.BitsPerPixel / 8;

        var result = new Image<Rgba32>(width, height);
        
        // Используем ProcessPixelRows для быстрого доступа
        result.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * image.Stride;
                var rowSpan = accessor.GetRowSpan(y);
                
                for (int x = 0; x < width; x++)
                {
                    int idx = rowOffset + x * bytesPerPixel;
                    byte r = 0, g = 0, b = 0, a = 255;

                    switch (image.Format)
                    {
                        case Pfim.ImageFormat.Rgba32:
                            // DDS обычно хранит BGRA
                            b = data[idx + 0]; // Было r
                            g = data[idx + 1];
                            r = data[idx + 2]; // Было b
                            a = data[idx + 3];
                            break;

                        case Pfim.ImageFormat.Rgb24:
                            // DDS обычно хранит BGR
                            b = data[idx + 0]; // Было r
                            g = data[idx + 1];
                            r = data[idx + 2]; // Было b
                            break;

                        default:
                            if (bytesPerPixel == 2) // 16-bit (555 or 565)
                            {
                                ushort val = MemoryMarshal.Read<ushort>(data.AsSpan(idx));
                                // Читаем как обычно
                                r = (byte)((val >> 11) & 0x1F);
                                g = (byte)((val >> 5) & 0x3F);
                                b = (byte)(val & 0x1F);
                                
                                // Расширяем до 8 бит
                                r = (byte)((r * 255) / 31);
                                g = (byte)((g * 255) / 63);
                                b = (byte)((b * 255) / 31);

                                // СВАП для 16 бит тоже нужен, если цвета перепутаны
                                // (сохраняем во временную переменную)
                                byte temp = r;
                                r = b;
                                b = temp;
                            }
                            break;
                    }
                    // ImageSharp ожидает R, G, B, A
                    rowSpan[x] = new Rgba32(r, g, b, a);
                }
            }
        });

        return result;
    }

    // XOR первого блока, как UnLockDDS в R3d3dtex.cpp
    private static void XorHeader(byte[] data)
    {
        Span<byte> pwd = stackalloc byte[]
        {
            0x2E,0x80,0x4D,0x76,0x2E,0xF8,0xD1,0xF0,0xBD,0x3F,0x86,0x81,0x58,0x2C,0x3F,0x3F,
            0x2E,0x2E,0x67,0x6F,0x3F,0x40,0x3F,0x78,0x3C,0x3F,0xF1,0xC0,0xA5,0xF6,0x3B,0x9F,
            0xC1,0x20,0x3F,0xD7,0xC8,0xC1,0xE9,0x85,0x86,0xBD,0xEF,0x56,0x3F,0xA1,0xFB,0x2E,
            0x87,0x86,0x61,0x4C,0x21,0x3B,0x4E,0xB4,0x78,0x57,0xAE,0x97,0x3F,0x2E,0x4A,0x2E,
            0x3F,0x4C,0x2E,0x44,0xCD,0xC5,0x5F,0xE8,0xE9,0xEC,0xEB,0xBD,0xBE,0xBB,0xF7,0x6C,
            0x2E,0xF2,0xE4,0x2E,0x3F,0x3F,0x97,0x9F,0x9D,0xB3,0x21,0xB9,0x76,0x65,0x54,0x3F,
            0xE6,0xF6,0xC6,0xF0,0x79,0xDB,0xE2,0xB2,0x4B,0x2E,0x2E,0xEB,0xD3,0xD3,0xCA,0xAB,
            0xEA,0xC7,0xED,0x9C,0xC7,0xD9,0xD0,0x65,0x48,0xB4,0xFA,0x35,0x2E,0x2E,0x6A,0x9B
        };

        int len = Math.Min(pwd.Length, data.Length);
        for (int i = 0; i < len; i++)
            data[i] ^= pwd[i];
    }
}