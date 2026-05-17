using System.Collections.Generic;

namespace RFMapToolSharp.Editor
{
    public sealed class EditorTemplate
    {
        public string Name { get; set; } = "default";
        public int Seed { get; set; } = 1337;
        public int ObjectCount { get; set; } = 100;
        public float MinX { get; set; } = -1000f;
        public float MaxX { get; set; } = 1000f;
        public float MinZ { get; set; } = -1000f;
        public float MaxZ { get; set; } = 1000f;
        public float MinY { get; set; } = 0f;
        public float MaxY { get; set; } = 300f;
        public List<string> ModelPool { get; set; } = new();
    }

    public sealed class GeneratedObject
    {
        public string ModelName { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float RotY { get; set; }
        public float Scale { get; set; }
    }
}

