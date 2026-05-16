using System.Collections.Generic;

namespace RFMapToolSharp.Rvp;

public record RvpColor(byte R, byte G, byte B);

public class RvpObject
{
    public string Name { get; set; } = string.Empty;
    public string? TexPath { get; set; }
    public string? Bone { get; set; }
    public List<string> Animations { get; } = new();
    public RvpColor? Color { get; set; }
    public bool Collision { get; set; }
    public bool Shadow { get; set; }
    public int? MeshId { get; set; }
    public float? Scale { get; set; }

    public override string ToString()
    {
        var flags = $"{(Collision ? "col" : "")}{(Shadow ? " sh" : "")}".Trim();
        return $"{Name} (mesh={MeshId?.ToString() ?? "?"}, ani={Animations.Count}, scale={Scale?.ToString("0.##") ?? "?"}{(flags.Length > 0 ? ", " + flags : "")})";
    }
}
