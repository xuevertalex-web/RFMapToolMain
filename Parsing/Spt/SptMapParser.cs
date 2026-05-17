using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace RFMapToolSharp.Parsing
{
    public class SptMapObject
    {
        public string ModelName;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;
    }

    public static class SptMapParser
    {
        private const int RecordSize = 136; // 100 bytes name + 9 floats

        public static List<SptMapObject> Parse(string filePath)
        {
            var list = new List<SptMapObject>();
            if (!File.Exists(filePath)) return list;

            try
            {
                if (LooksLikeTextSpt(filePath))
                {
                    Console.WriteLine($"[SPT Skip] {Path.GetFileName(filePath)}: text/script SPT detected.");
                    return list;
                }

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    if (fs.Length < 4) return list;

                    int count = br.ReadInt32();
                    if (!IsPlausibleCount(count, fs.Length))
                    {
                        if (fs.Length < 8) return list;

                        // Some files may have an extra dword header before count
                        count = br.ReadInt32();
                        if (!IsPlausibleCount(count, fs.Length - 4))
                        {
                            Console.WriteLine($"[SPT Skip] {Path.GetFileName(filePath)}: unsupported/invalid binary layout.");
                            return list;
                        }
                    }

                    for (int i = 0; i < count; i++)
                    {
                        if (fs.Position + RecordSize > fs.Length) break;

                        var obj = new SptMapObject();
                        obj.ModelName = CleanString(br.ReadBytes(100));
                        obj.Position = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        obj.Rotation = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        obj.Scale = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

                        if (!string.IsNullOrEmpty(obj.ModelName))
                            list.Add(obj);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SPT Error] {Path.GetFileName(filePath)}: {ex.Message}");
            }

            return list;
        }

        private static string CleanString(byte[] bytes)
        {
            int nullIdx = Array.IndexOf(bytes, (byte)0);
            return nullIdx >= 0
                ? Encoding.ASCII.GetString(bytes, 0, nullIdx).Trim()
                : Encoding.ASCII.GetString(bytes).Trim();
        }

        private static bool IsPlausibleCount(int count, long payloadLength)
        {
            if (count <= 0 || count > 100000) return false;
            return payloadLength >= (long)count * RecordSize;
        }

        private static bool LooksLikeTextSpt(string filePath)
        {
            var probe = File.ReadAllBytes(filePath);
            int len = Math.Min(256, probe.Length);
            if (len == 0) return false;

            int printable = 0;
            int zeroes = 0;
            for (int i = 0; i < len; i++)
            {
                byte b = probe[i];
                if (b == 0) zeroes++;
                if ((b >= 9 && b <= 13) || (b >= 32 && b <= 126)) printable++;
            }

            string head = Encoding.ASCII.GetString(probe, 0, len).ToLowerInvariant();
            bool scriptMarker = head.Contains("script_begin") || head.Contains(";helper") || head.Contains("helper");
            bool mostlyText = printable >= (len * 80 / 100) && zeroes == 0;

            return scriptMarker || mostlyText;
        }
    }
}
