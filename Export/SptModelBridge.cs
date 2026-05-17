using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Schema2;
using SharpGLTF.Materials;

namespace RFMapToolSharp.Export;

internal static class SptModelBridge
{
    private struct ObjTri
    {
        public int A;
        public int B;
        public int C;
    }

    public static bool TryCreateMesh(ModelRoot model, string modelPath, out Mesh? mesh, out string reason)
    {
        mesh = null;
        reason = "";
        var ext = Path.GetExtension(modelPath).ToLowerInvariant();
        if (ext == ".msh" || ext == ".mod")
        {
            if (RfNativeMeshReader.TryRead(modelPath, out var rf, out var rr))
            {
                return TryCreateFromRaw(model, rf.Positions, rf.Triangles, out mesh, out reason);
            }
            reason = $"rf reader failed: {rr}";
            return false;
        }
        if (ext == ".obj")
        {
            return TryCreateObjMesh(model, modelPath, out mesh, out reason);
        }

        reason = $"unsupported extension: {ext}";
        return false;
    }

    private static bool TryCreateObjMesh(ModelRoot model, string path, out Mesh? mesh, out string reason)
    {
        mesh = null;
        reason = "";
        var pos = new List<Vector3>();
        var tris = new List<ObjTri>();

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            if (line.StartsWith("v "))
            {
                var t = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (t.Length < 4) continue;
                if (float.TryParse(t[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                    float.TryParse(t[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                    float.TryParse(t[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                {
                    pos.Add(new Vector3(x, y, z));
                }
            }
            else if (line.StartsWith("f "))
            {
                var t = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (t.Length < 4) continue;
                var idx = new List<int>();
                for (int i = 1; i < t.Length; i++)
                {
                    var p = t[i].Split('/')[0];
                    if (int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                    {
                        if (v < 0) v = pos.Count + v + 1;
                        v -= 1;
                        if (v >= 0 && v < pos.Count) idx.Add(v);
                    }
                }
                if (idx.Count >= 3)
                {
                    for (int i = 1; i < idx.Count - 1; i++)
                    {
                        tris.Add(new ObjTri { A = idx[0], B = idx[i], C = idx[i + 1] });
                    }
                }
            }
        }

        if (pos.Count == 0 || tris.Count == 0)
        {
            reason = "obj has no triangles";
            return false;
        }

        var mb = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>("SPT_OBJ");
        var mat = new MaterialBuilder("SPT_OBJ_MAT").WithDoubleSide(true).WithMetallicRoughnessShader();
        var prim = mb.UsePrimitive(mat);

        foreach (var t in tris)
        {
            var p1 = pos[t.A];
            var p2 = pos[t.B];
            var p3 = pos[t.C];
            var n = Vector3.Normalize(Vector3.Cross(p2 - p1, p3 - p1));
            var v1 = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(new VertexPositionNormal(p1, n), new VertexTexture1(Vector2.Zero), new VertexEmpty());
            var v2 = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(new VertexPositionNormal(p2, n), new VertexTexture1(Vector2.Zero), new VertexEmpty());
            var v3 = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(new VertexPositionNormal(p3, n), new VertexTexture1(Vector2.Zero), new VertexEmpty());
            prim.AddTriangle(v1, v2, v3);
        }

        mesh = model.CreateMesh(mb);
        return true;
    }

    private static bool TryCreateFromRaw(ModelRoot model, List<Vector3> pos, List<(int A, int B, int C)> tris, out Mesh? mesh, out string reason)
    {
        mesh = null;
        reason = "";
        if (pos.Count == 0 || tris.Count == 0)
        {
            reason = "empty raw mesh";
            return false;
        }

        var mb = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>("SPT_RF");
        var mat = new MaterialBuilder("SPT_RF_MAT").WithDoubleSide(true).WithMetallicRoughnessShader();
        var prim = mb.UsePrimitive(mat);

        foreach (var t in tris)
        {
            if (t.A < 0 || t.B < 0 || t.C < 0 || t.A >= pos.Count || t.B >= pos.Count || t.C >= pos.Count) continue;
            var p1 = pos[t.A];
            var p2 = pos[t.B];
            var p3 = pos[t.C];
            var n = Vector3.Normalize(Vector3.Cross(p2 - p1, p3 - p1));
            var v1 = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(new VertexPositionNormal(p1, n), new VertexTexture1(Vector2.Zero), new VertexEmpty());
            var v2 = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(new VertexPositionNormal(p2, n), new VertexTexture1(Vector2.Zero), new VertexEmpty());
            var v3 = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(new VertexPositionNormal(p3, n), new VertexTexture1(Vector2.Zero), new VertexEmpty());
            prim.AddTriangle(v1, v2, v3);
        }

        mesh = model.CreateMesh(mb);
        return true;
    }
}
