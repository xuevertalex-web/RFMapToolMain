using System.Runtime.InteropServices;

namespace RFMapToolSharp.Collision;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vector2f
{
    public float X;
    public float Y;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vector2s
{
    public short X;
    public short Y;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vector3f
{
    public float X;
    public float Y;
    public float Z;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vector3s
{
    public short X;
    public short Y;
    public short Z;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vector3c
{
    public sbyte X;
    public sbyte Y;
    public sbyte Z;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vector4f
{
    public float X;
    public float Y;
    public float Z;
    public float W;
}
