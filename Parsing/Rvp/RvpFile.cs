using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RFMapToolSharp.Rvp;

/// <summary>
/// Парсер .rvp по RTMovieCreate: объекты, preparetrack и track-события.
/// </summary>
public class RvpFile
{
    public List<RvpObject> Objects { get; } = new();
    public List<RvpPrepareBinding> PrepareBindings { get; } = new();
    public List<RvpTrack> Tracks { get; } = new();

    public bool NoCamera { get; set; }
    public bool Wide { get; set; }
    public bool Logo { get; set; }
    public float? TotalFrame { get; set; }

    public static RvpFile Load(string path)
    {
        var rvp = new RvpFile();
        var tokens = Tokenize(path);

        int mode = 0; // 0 - none, 1 - object, 2 - prepare, 3 - track
        float currentFrame = 0f;
        RvpObject? currentObject = null;

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var lower = token.ToLowerInvariant();

            switch (lower)
            {
                case "[object]":
                    mode = 1;
                    currentObject = null;
                    continue;
                case "[preparetrack]":
                    mode = 2;
                    currentObject = null;
                    continue;
                case "[track]":
                    mode = 3;
                    currentObject = null;
                    continue;
            }

            if (mode == 1) // object block
            {
                if (token.StartsWith("*", StringComparison.Ordinal))
                {
                    currentObject = new RvpObject { Name = token.TrimStart('*') };
                    rvp.Objects.Add(currentObject);
                    continue;
                }

                if (currentObject == null)
                    continue;

                switch (lower)
                {
                    case "texpath":
                        currentObject.TexPath = ReadNext(tokens, ref i);
                        break;
                    case "bone":
                        currentObject.Bone = ReadNext(tokens, ref i);
                        break;
                    case "ani":
                        var ani = ReadNext(tokens, ref i);
                        if (!string.IsNullOrEmpty(ani))
                            currentObject.Animations.Add(ani);
                        break;
                    case "color":
                        var r = ReadNextInt(tokens, ref i) ?? 0;
                        var g = ReadNextInt(tokens, ref i) ?? 0;
                        var b = ReadNextInt(tokens, ref i) ?? 0;
                        currentObject.Color = new RvpColor((byte)(r / 2), (byte)(g / 2), (byte)(b / 2));
                        break;
                    case "collision":
                        currentObject.Collision = true;
                        break;
                    case "shadow":
                        currentObject.Shadow = true;
                        break;
                    case "mesh_id":
                        currentObject.MeshId = ReadNextInt(tokens, ref i);
                        break;
                    case "scale":
                        currentObject.Scale = ReadNextFloat(tokens, ref i);
                        break;
                }

                continue;
            }

            if (mode == 2) // preparetrack block
            {
                if (token.StartsWith("*", StringComparison.Ordinal))
                {
                    var objName = token.TrimStart('*');
                    var dummy = ReadNext(tokens, ref i);
                    rvp.PrepareBindings.Add(new RvpPrepareBinding
                    {
                        ObjectName = objName,
                        DummyName = dummy
                    });
                    continue;
                }

                switch (lower)
                {
                    case "no_camera":
                        rvp.NoCamera = true;
                        break;
                    case "wide":
                        rvp.Wide = true;
                        break;
                    case "logo":
                        rvp.Logo = true;
                        break;
                    case "total_frame":
                        rvp.TotalFrame = ReadNextFloat(tokens, ref i);
                        break;
                }
                continue;
            }

            // track block (mode == 3) or generic (mode != 1)
            if (mode != 1)
            {
                if (lower == "*frame")
                {
                    currentFrame = ReadNextFloat(tokens, ref i);
                    continue;
                }

                switch (lower)
                {
                    case "camera":
                        AddTrack(rvp, "camera", currentFrame, new() { { "camera", ReadNext(tokens, ref i) } });
                        break;
                    case "fade_in":
                        AddTrack(rvp, "fade_in", currentFrame, new() { { "duration", ReadNext(tokens, ref i) } });
                        break;
                    case "fade_out":
                        AddTrack(rvp, "fade_out", currentFrame, new() { { "duration", ReadNext(tokens, ref i) } });
                        break;
                    case "magic":
                        {
                            var dummy = ReadNext(tokens, ref i);
                            var effectId = ReadNext(tokens, ref i);
                            AddTrack(rvp, "magic", currentFrame, new()
                            {
                                { "dummy", dummy },
                                { "effect_id", effectId }
                            });
                        }
                        break;
                    case "ani":
                        {
                            var obj = ReadNext(tokens, ref i);
                            var aniIdx = ReadNext(tokens, ref i);
                            AddTrack(rvp, "ani", currentFrame, new()
                            {
                                { "object", obj },
                                { "ani", aniIdx }
                            });
                        }
                        break;
                    case "char_fade_in":
                        {
                            var obj = ReadNext(tokens, ref i);
                            var dur = ReadNext(tokens, ref i);
                            AddTrack(rvp, "char_fade_in", currentFrame, new()
                            {
                                { "object", obj },
                                { "duration", dur }
                            });
                        }
                        break;
                    case "char_fade_out":
                        {
                            var obj = ReadNext(tokens, ref i);
                            var dur = ReadNext(tokens, ref i);
                            AddTrack(rvp, "char_fade_out", currentFrame, new()
                            {
                                { "object", obj },
                                { "duration", dur }
                            });
                        }
                        break;
                    case "quake":
                        {
                            var time = ReadNext(tokens, ref i);
                            var density = ReadNext(tokens, ref i);
                            AddTrack(rvp, "quake", currentFrame, new()
                            {
                                { "time", time },
                                { "density", density }
                            });
                        }
                        break;
                    case "wav":
                        {
                            var dummy = ReadNext(tokens, ref i);
                            var wavePath = ReadNext(tokens, ref i);
                            AddTrack(rvp, "wav", currentFrame, new()
                            {
                                { "dummy", dummy },
                                { "wave", wavePath }
                            });
                        }
                        break;
                }
            }
        }

        return rvp;
    }

    private static void AddTrack(RvpFile file, string type, float frame, Dictionary<string, string> args)
    {
        file.Tracks.Add(new RvpTrack
        {
            Type = type,
            Frame = frame,
            Args = args
        });
    }

    private static List<string> Tokenize(string path)
    {
        var tokens = new List<string>();
        var enc = Encoding.GetEncoding(1251);
        foreach (var rawLine in File.ReadAllLines(path, enc))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("//"))
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

    private static int? ReadNextInt(IList<string> tokens, ref int index)
    {
        var txt = ReadNext(tokens, ref index);
        return int.TryParse(txt, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static float ReadNextFloat(IList<string> tokens, ref int index)
    {
        var txt = ReadNext(tokens, ref index);
        return float.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
    }
}

