using System.Collections.Generic;

namespace RFMapToolSharp.Spt;

public enum SptPositionType
{
    Box,
    Sphere,
    SphereEdge
}

public record SptRange(float Min, float Max)
{
    public bool IsRandom => Min != Max;
}

public record SptVectorRange(SptRange X, SptRange Y, SptRange Z);

public record SptColorRange(SptRange R, SptRange G, SptRange B);

public class SptTrack
{
    public float Frame { get; set; }
    public SptVectorRange? Power { get; set; }
    public SptRange? Alpha { get; set; }
    public SptRange? ZRot { get; set; }
    public SptRange? YRot { get; set; }
    public SptColorRange? Color { get; set; }
    public SptRange? Scale { get; set; }
    public bool Flicker { get; set; }
}

/// <summary>
/// Полный снимок одного блока [Particle] из .spt.
/// </summary>
public class SptParticle
{
    public string? EntityFile { get; set; }
    public int? Num { get; set; }
    public float? StartTimeRange { get; set; }

    public SptPositionType? PositionType { get; set; }
    public SptVectorRange? StartPos { get; set; }

    public SptVectorRange? Gravity { get; set; }
    public SptVectorRange? StartPower { get; set; }
    public SptRange? StartScale { get; set; }
    public SptRange? StartZRot { get; set; }
    public SptRange? StartYRot { get; set; }

    public SptRange? LiveTime { get; set; }
    public int? AlphaType { get; set; }
    public SptRange? TimeSpeed { get; set; }

    public SptRange? StartAlpha { get; set; }
    public SptColorRange? StartColor { get; set; }

    public byte? FlickerAlpha { get; set; }
    public float? FlickerTime { get; set; }
    public bool Flicker { get; set; }

    public SptRange? EmitTime { get; set; }
    public SptRange? ZFront { get; set; }
    public SptRange? OnePerTimeEpsilon { get; set; }
    public SptRange? Elasticity { get; set; }
    public int? SpecialId { get; set; }
    public float? TexRepeat { get; set; }

    public bool AlwaysLive { get; set; }
    public bool NoBillboard { get; set; }
    public bool YBillboard { get; set; }
    public bool ZBillboard { get; set; }
    public bool Free { get; set; }
    public bool Collision { get; set; }
    public bool ZDisable { get; set; }

    public List<SptTrack> Tracks { get; } = new();
    public Dictionary<string, string> RawParams { get; } = new();

    public override string ToString()
    {
        var pos = PositionType.HasValue && StartPos != null
            ? $"{PositionType} [{StartPos.X.Min},{StartPos.X.Max}; {StartPos.Y.Min},{StartPos.Y.Max}; {StartPos.Z.Min},{StartPos.Z.Max}]"
            : "pos:?";
        var num = Num.HasValue ? $"num={Num}" : "num=?";
        return $"{EntityFile ?? "particle"} ({num}, {pos})";
    }
}
