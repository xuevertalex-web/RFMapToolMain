using System.Text;

namespace RFMapToolSharp.Parsing.R3;

/// <summary>
/// Lightweight parser for cutscene scripts (.rvp).
/// The format is text with sections [object], [preparetrack], [track].
/// We capture commands in order with their arguments for easy inspection.
/// </summary>
public static class RvpFile
{
    public static RvpData Load(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.Default)
                        .Select(l => l.Trim())
                        .Where(l => l.Length > 0 && !l.StartsWith("//"))
                        .ToList();

        var data = new RvpData();
        Section section = Section.None;
        RvpObject? currentObject = null;

        foreach (var raw in lines)
        {
            var line = raw;
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                section = line.ToLowerInvariant() switch
                {
                    "[object]" => Section.Object,
                    "[preparetrack]" => Section.PrepareTrack,
                    "[track]" => Section.Track,
                    _ => Section.None
                };
                continue;
            }

            if (section == Section.Object)
            {
                if (line.StartsWith("*"))
                {
                    currentObject = new RvpObject { Name = line };
                    data.Objects.Add(currentObject);
                    continue;
                }
                if (currentObject == null)
                    continue;

                var parts = SplitParts(line);
                if (parts.Length == 0) continue;
                currentObject.Commands.Add(new RvpCommand(parts[0], parts.Skip(1).ToArray()));
            }
            else if (section == Section.PrepareTrack || section == Section.Track)
            {
                var parts = SplitParts(line);
                if (parts.Length == 0) continue;
                data.Tracks.Add(new RvpCommand(parts[0], parts.Skip(1).ToArray()));
            }
        }

        return data;
    }

    private static string[] SplitParts(string line) =>
        line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

    private enum Section { None, Object, PrepareTrack, Track }
}

public sealed class RvpData
{
    public List<RvpObject> Objects { get; } = new();
    public List<RvpCommand> Tracks { get; } = new();
}

public sealed class RvpObject
{
    public string Name { get; init; } = string.Empty;
    public List<RvpCommand> Commands { get; } = new();
}

public sealed record RvpCommand(string Keyword, string[] Args);
