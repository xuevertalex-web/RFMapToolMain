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

        // Heuristic scan for float triples that look like bounded vertex coordinates.
        // Then build sequential triangles.
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
        for (int i = 0; i + 2 < verts.Count; i += 3)
            data.Triangles.Add((i, i + 1, i + 2));

        if (data.Triangles.Count < 100)
        {
            reason = "not enough triangles";
            return false;
        }

        return true;
    }
}

