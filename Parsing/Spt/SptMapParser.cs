using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Globalization;
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
                    Console.WriteLine($"[SPT] {Path.GetFileName(filePath)}: text/script SPT detected, using text fallback parser.");
                    return ParseTextScriptSpt(filePath);
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

        private static List<SptMapObject> ParseTextScriptSpt(string filePath)
        {
            var result = new List<SptMapObject>();
            var lines = File.ReadAllLines(filePath);
            SptMapObject? current = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("*", StringComparison.Ordinal))
                {
                    if (current != null && !string.IsNullOrWhiteSpace(current.ModelName)) result.Add(current);
                    current = new SptMapObject
                    {
                        ModelName = line.Substring(1).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0],
                        Position = Vector3.Zero,
                        Rotation = Vector3.Zero,
                        Scale = Vector3.One
                    };
                    continue;
                }

                if (current == null) continue;
                if (!line.StartsWith("-node_tm", StringComparison.OrdinalIgnoreCase)) continue;
                if (i + 4 >= lines.Length) continue;

                var m1 = ParseRow(lines[i + 1]);
                var m2 = ParseRow(lines[i + 2]);
                var m3 = ParseRow(lines[i + 3]);
                var m4 = ParseRow(lines[i + 4]);
                if (m1 == null || m2 == null || m3 == null || m4 == null) continue;

                current.Position = new Vector3(m4.Value.X, m4.Value.Y, m4.Value.Z);
                var rowX = new Vector3(m1.Value.X, m1.Value.Y, m1.Value.Z);
                var rowY = new Vector3(m2.Value.X, m2.Value.Y, m2.Value.Z);
                var rowZ = new Vector3(m3.Value.X, m3.Value.Y, m3.Value.Z);
                current.Scale = new Vector3(rowX.Length(), rowY.Length(), rowZ.Length());
                if (current.Scale.X == 0 || current.Scale.Y == 0 || current.Scale.Z == 0) current.Scale = Vector3.One;

                // Build rotation matrix from normalized basis and convert to Euler XYZ (degrees).
                if (rowX.LengthSquared() > 1e-8f && rowY.LengthSquared() > 1e-8f && rowZ.LengthSquared() > 1e-8f)
                {
                    var nx = Vector3.Normalize(rowX);
                    var ny = Vector3.Normalize(rowY);
                    var nz = Vector3.Normalize(rowZ);
                    var rotM = new Matrix4x4(
                        nx.X, nx.Y, nx.Z, 0f,
                        ny.X, ny.Y, ny.Z, 0f,
                        nz.X, nz.Y, nz.Z, 0f,
                        0f,   0f,   0f,   1f);
                    current.Rotation = MatrixToEulerXyzDegrees(rotM);
                }
                else
                {
                    current.Rotation = Vector3.Zero;
                }
                i += 4;
            }

            if (current != null && !string.IsNullOrWhiteSpace(current.ModelName)) result.Add(current);
            return result;
        }

        private static Vector4? ParseRow(string line)
        {
            var t = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length < 4) return null;
            if (!float.TryParse(t[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a)) return null;
            if (!float.TryParse(t[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b)) return null;
            if (!float.TryParse(t[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var c)) return null;
            if (!float.TryParse(t[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return null;
            return new Vector4(a, b, c, d);
        }

        private static Vector3 MatrixToEulerXyzDegrees(Matrix4x4 m)
        {
            // XYZ decomposition with simple gimbal handling.
            float sy = m.M13;
            if (sy < -1f) sy = -1f;
            else if (sy > 1f) sy = 1f;
            float x, y, z;
            y = MathF.Asin(sy);
            if (MathF.Abs(sy) < 0.9999f)
            {
                x = MathF.Atan2(-m.M23, m.M33);
                z = MathF.Atan2(-m.M12, m.M11);
            }
            else
            {
                x = MathF.Atan2(m.M32, m.M22);
                z = 0f;
            }

            const float rad2deg = 57.2957795f;
            return new Vector3(x * rad2deg, y * rad2deg, z * rad2deg);
        }
    }
}
