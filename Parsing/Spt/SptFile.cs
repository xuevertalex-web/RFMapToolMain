using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RFMapToolSharp.Spt;

/// <summary>
/// Текстовый парсер .spt по логике CParticle::LoadParticleSPT.
/// Парсит флаги, диапазоны (rand/min~max) и треки.
/// </summary>
public class SptFile
{
    public List<SptParticle> Particles { get; } = new();

    public static SptFile Load(string path)
    {
        var spt = new SptFile();
        var tokens = Tokenize(path);

        SptParticle? current = null;
        SptTrack? currentTrack = null;

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var lower = token.ToLowerInvariant();

            if (lower == "[particle]")
            {
                current = new SptParticle { AlphaType = 3 };
                currentTrack = null;
                spt.Particles.Add(current);
                continue;
            }

            if (current == null)
                continue;

            switch (lower)
            {
                case "entity_file":
                    current.EntityFile = ReadNext(tokens, ref i);
                    break;
                case "num":
                    current.Num = ReadNextInt(tokens, ref i);
                    break;
                case "start_time_range":
                    current.StartTimeRange = ReadNextFloat(tokens, ref i);
                    break;
                case "pos":
                    current.PositionType = ParsePosType(ReadNext(tokens, ref i));
                    current.StartPos = new SptVectorRange(
                        ConsumeRange(tokens, ref i),
                        ConsumeRange(tokens, ref i),
                        ConsumeRange(tokens, ref i));
                    break;
                case "no_billboard":
                    current.NoBillboard = true;
                    break;
                case "z_disable":
                    current.ZDisable = true;
                    break;
                case "always_live":
                    current.AlwaysLive = true;
                    break;
                case "time_speed":
                    current.TimeSpeed = ConsumeRange(tokens, ref i);
                    break;
                case "gravity":
                    current.Gravity = new SptVectorRange(
                        ConsumeRange(tokens, ref i),
                        ConsumeRange(tokens, ref i),
                        ConsumeRange(tokens, ref i));
                    break;
                case "live_time":
                    current.LiveTime = ConsumeRange(tokens, ref i);
                    break;
                case "alpha_type":
                    current.AlphaType = ReadNextInt(tokens, ref i);
                    break;
                case "start_power":
                    current.StartPower = new SptVectorRange(
                        ConsumeRange(tokens, ref i),
                        ConsumeRange(tokens, ref i),
                        ConsumeRange(tokens, ref i));
                    break;
                case "start_scale":
                    current.StartScale = ConsumeRange(tokens, ref i);
                    break;
                case "start_zrot":
                    current.StartZRot = ConsumeRange(tokens, ref i);
                    break;
                case "start_yrot":
                    current.StartYRot = ConsumeRange(tokens, ref i);
                    break;
                case "y_billboard":
                    current.YBillboard = true;
                    break;
                case "z_billboard":
                    current.ZBillboard = true;
                    break;
                case "free":
                    current.Free = true;
                    break;
                case "check_collision":
                    current.Collision = true;
                    break;
                case "z_front":
                    current.ZFront = ConsumeRange(tokens, ref i);
                    break;
                case "emit_time":
                    current.EmitTime = ConsumeRange(tokens, ref i);
                    break;
                case "start_alpha":
                    current.StartAlpha = ConsumeRange(tokens, ref i);
                    break;
                case "start_color":
                    current.StartColor = new SptColorRange(
                        ConsumeRange(tokens, ref i),
                        ConsumeRange(tokens, ref i),
                        ConsumeRange(tokens, ref i));
                    break;
                case "color":
                    if (currentTrack != null)
                    {
                        currentTrack.Color = new SptColorRange(
                            ConsumeRange(tokens, ref i),
                            ConsumeRange(tokens, ref i),
                            ConsumeRange(tokens, ref i));
                    }
                    else
                    {
                        current.StartColor = new SptColorRange(
                            ConsumeRange(tokens, ref i),
                            ConsumeRange(tokens, ref i),
                            ConsumeRange(tokens, ref i));
                    }
                    break;
                case "flicker_alpha":
                    current.FlickerAlpha = (byte?)ReadNextInt(tokens, ref i);
                    break;
                case "flicker_time":
                    current.FlickerTime = ReadNextFloat(tokens, ref i);
                    break;
                case "flicker":
                    if (currentTrack != null)
                        currentTrack.Flicker = true;
                    else
                        current.Flicker = true;
                    break;
                case "create_time_epsilon":
                    current.OnePerTimeEpsilon = ConsumeRange(tokens, ref i);
                    break;
                case "elasticity":
                    current.Elasticity = ConsumeRange(tokens, ref i);
                    break;
                case "special_id":
                    current.SpecialId = ReadNextInt(tokens, ref i);
                    break;
                case "tex_repeat":
                    current.TexRepeat = ReadNextFloat(tokens, ref i);
                    break;
                case "time":
                    currentTrack = new SptTrack
                    {
                        Frame = ReadNextFloat(tokens, ref i)
                    };
                    current.Tracks.Add(currentTrack);
                    break;
                case "power":
                    if (currentTrack != null)
                    {
                        currentTrack.Power = new SptVectorRange(
                            ConsumeRange(tokens, ref i),
                            ConsumeRange(tokens, ref i),
                            ConsumeRange(tokens, ref i));
                    }
                    break;
                case "alpha":
                    if (currentTrack != null)
                        currentTrack.Alpha = ConsumeRange(tokens, ref i);
                    break;
                case "zrot":
                    if (currentTrack != null)
                        currentTrack.ZRot = ConsumeRange(tokens, ref i);
                    break;
                case "yrot":
                    if (currentTrack != null)
                        currentTrack.YRot = ConsumeRange(tokens, ref i);
                    break;
                case "scale":
                    if (currentTrack != null)
                        currentTrack.Scale = ConsumeRange(tokens, ref i);
                    else
                        current.StartScale = ConsumeRange(tokens, ref i);
                    break;
                default:
                    // Сохраняем нераспознанное значение для отладки
                    var next = PeekNext(tokens, i);
                    if (!string.IsNullOrEmpty(next))
                        current.RawParams[lower] = next;
                    break;
            }
        }

        return spt;
    }

    private static List<string> Tokenize(string path)
    {
        var tokens = new List<string>();
        var enc = Encoding.GetEncoding(1251);
        foreach (var rawLine in File.ReadAllLines(path, enc))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;
            if (line.StartsWith("//"))
                continue;

            var commentIndex = line.IndexOf("//", StringComparison.Ordinal);
            if (commentIndex >= 0)
                line = line[..commentIndex].Trim();

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
                tokens.Add(part.Trim());
        }
        return tokens;
    }

    private static string ReadNext(IList<string> tokens, ref int index)
    {
        if (index + 1 < tokens.Count)
        {
            index++;
            return tokens[index];
        }
        return string.Empty;
    }

    private static string PeekNext(IList<string> tokens, int index) =>
        index + 1 < tokens.Count ? tokens[index + 1] : string.Empty;

    private static int? ReadNextInt(IList<string> tokens, ref int index)
    {
        var txt = ReadNext(tokens, ref index);
        return int.TryParse(txt, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static float ReadNextFloat(IList<string> tokens, ref int index)
    {
        var txt = ReadNext(tokens, ref index);
        return float.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static SptRange ConsumeRange(IList<string> tokens, ref int index)
    {
        var sb = new StringBuilder();
        if (index + 1 < tokens.Count)
        {
            index++;
            sb.Append(tokens[index]);
            while (sb.ToString().Contains("rand", StringComparison.OrdinalIgnoreCase) &&
                   !sb.ToString().Contains(')') &&
                   index + 1 < tokens.Count)
            {
                index++;
                sb.Append(tokens[index]);
                if (tokens[index].Contains(')'))
                    break;
            }
        }
        var text = sb.ToString();
        return ParseRange(text);
    }

    private static SptRange ParseRange(string token)
    {
        token = token.Trim();
        if (token.StartsWith("rand", StringComparison.OrdinalIgnoreCase))
        {
            int open = token.IndexOf('(');
            int close = token.LastIndexOf(')');
            if (open >= 0 && close > open)
                token = token.Substring(open + 1, close - open - 1);
        }
        token = token.Replace("~", ",");

        var parts = token.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        float min = parts.Length > 0 ? ParseFloat(parts[0]) : 0;
        float max = parts.Length > 1 ? ParseFloat(parts[1]) : min;

        return new SptRange(min, max);
    }

    private static float ParseFloat(string text) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static SptPositionType? ParsePosType(string token)
    {
        var lower = token.ToLowerInvariant();
        return lower switch
        {
            "box" => SptPositionType.Box,
            "sphere" => SptPositionType.Sphere,
            "sphere_edge" => SptPositionType.SphereEdge,
            _ => null
        };
    }
}
