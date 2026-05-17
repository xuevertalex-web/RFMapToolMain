using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace RFMapToolSharp.Export;

internal static class RfNativeMeshReader
{
    internal sealed class MeshData
    {
        public List<Vector3> Positions { get; } = new();
        public List<(int A, int B, int C)> Triangles { get; } = new();
    }

    // Minimal pragmatic reader:
    // tries to parse very common "raw triangle soup" layouts used by simple RF exports.
    // If format is different, caller falls back to marker/object bridge.
    public static bool TryRead(string path, out MeshData data, out string reason)
    {
        data = new MeshData();
        reason = "";
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".msh" && ext != ".mod")
        {
            reason = "not msh/mod";
            return false;
        }

        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception ex) { reason = $"read error: {ex.Message}"; return false; }
        if (bytes.Length < 64) { reason = "file too small"; return false; }

        // 1) Heuristic vertex scan.
        var verts = new List<Vector3>(4096);
        for (int i = 0; i + 12 <= bytes.Length; i += 4)
        {
            float x = BitConverter.ToSingle(bytes, i);
            float y = BitConverter.ToSingle(bytes, i + 4);
            float z = BitConverter.ToSingle(bytes, i + 8);
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) continue;
            if (MathF.Abs(x) > 200000f || MathF.Abs(y) > 200000f || MathF.Abs(z) > 200000f) continue;
            if (MathF.Abs(x) < 1e-9f && MathF.Abs(y) < 1e-9f && MathF.Abs(z) < 1e-9f) continue;
            verts.Add(new Vector3(x, y, z));
            if (verts.Count > 120000) break;
        }

        if (verts.Count < 300)
        {
            reason = $"not enough plausible vertices: {verts.Count.ToString(CultureInfo.InvariantCulture)}";
            return false;
        }

        data.Positions.AddRange(verts);

        // 2) Try detect index buffer nearby (better than sequential soup).
        var trisFrom16 = TryFindIndexTriangles16(bytes, verts.Count);
        var trisFrom32 = TryFindIndexTriangles32(bytes, verts.Count);
        if (trisFrom32.Count > trisFrom16.Count && trisFrom32.Count > 0)
        {
            data.Triangles.AddRange(trisFrom32);
        }
        else if (trisFrom16.Count > 0)
        {
            data.Triangles.AddRange(trisFrom16);
        }
        else
        {
            for (int i = 0; i + 2 < verts.Count; i += 3)
                data.Triangles.Add((i, i + 1, i + 2));
        }

        if (data.Triangles.Count < 100)
        {
            reason = "not enough triangles";
            return false;
        }

        return true;
    }

    private static List<(int A, int B, int C)> TryFindIndexTriangles16(byte[] bytes, int vcount)
    {
        var best = new List<(int A, int B, int C)>();
        if (vcount <= 0 || vcount > 65535) return best;
        for (int start = 0; start + 6 <= bytes.Length; start += 2)
        {
            var cur = new List<(int A, int B, int C)>(256);
            for (int i = start; i + 6 <= bytes.Length; i += 6)
            {
                int a = BitConverter.ToUInt16(bytes, i);
                int b = BitConverter.ToUInt16(bytes, i + 2);
                int c = BitConverter.ToUInt16(bytes, i + 4);
                if (a >= vcount || b >= vcount || c >= vcount) break;
                if (a == b || b == c || a == c) continue;
                cur.Add((a, b, c));
                if (cur.Count > 120000) break;
            }
            if (cur.Count > best.Count) best = cur;
        }
        return best;
    }

    private static List<(int A, int B, int C)> TryFindIndexTriangles32(byte[] bytes, int vcount)
    {
        var best = new List<(int A, int B, int C)>();
        if (vcount <= 0) return best;
        for (int start = 0; start + 12 <= bytes.Length; start += 4)
        {
            var cur = new List<(int A, int B, int C)>(256);
            for (int i = start; i + 12 <= bytes.Length; i += 12)
            {
                int a = (int)BitConverter.ToUInt32(bytes, i);
                int b = (int)BitConverter.ToUInt32(bytes, i + 4);
                int c = (int)BitConverter.ToUInt32(bytes, i + 8);
                if (a < 0 || b < 0 || c < 0 || a >= vcount || b >= vcount || c >= vcount) break;
                if (a == b || b == c || a == c) continue;
                cur.Add((a, b, c));
                if (cur.Count > 120000) break;
            }
            if (cur.Count > best.Count) best = cur;
        }
        return best;
    }
}
