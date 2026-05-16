using System.Collections.Generic;

namespace RFMapToolSharp.Rvp;

public class RvpTrack
{
    public string Type { get; set; } = string.Empty; // camera, fade_in, magic, ani, etc.
    public float Frame { get; set; }
    public Dictionary<string, string> Args { get; set; } = new();

    public override string ToString()
    {
        return $"{Type}@{Frame:0.##}";
    }
}

public class RvpPrepareBinding
{
    public string ObjectName { get; set; } = string.Empty;
    public string DummyName { get; set; } = string.Empty;
}
