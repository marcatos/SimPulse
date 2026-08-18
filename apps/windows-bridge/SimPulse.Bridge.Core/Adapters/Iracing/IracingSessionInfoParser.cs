using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Adapters.Iracing;

public sealed record IracingSessionInfo(
    OptionalValue<string> TrackId,
    OptionalValue<string> TrackDisplayName,
    OptionalValue<string> VehicleId,
    OptionalValue<string> VehicleDisplayName,
    OptionalValue<string> SessionType);

public static class IracingSessionInfoParser
{
    public static IracingSessionInfo Parse(string yaml)
    {
        Dictionary<string, Dictionary<string, string>> sections = ParseSections(yaml);
        return new IracingSessionInfo(
            First(sections, "WeekendInfo", "TrackID"),
            First(sections, "WeekendInfo", "TrackDisplayName", "TrackName"),
            First(sections, "DriverInfo", "CarPath"),
            First(sections, "DriverInfo", "CarScreenName", "CarPath"),
            First(sections, "SessionInfo", "SessionType"));
    }

    private static OptionalValue<string> First(
        Dictionary<string, Dictionary<string, string>> sections,
        string section,
        params string[] keys)
    {
        if (!sections.TryGetValue(section, out Dictionary<string, string>? values))
        {
            return OptionalValue<string>.Unavailable();
        }

        foreach (string key in keys)
        {
            if (values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                return OptionalValue<string>.Available(value);
            }
        }

        return OptionalValue<string>.Unavailable();
    }

    private static Dictionary<string, Dictionary<string, string>> ParseSections(string yaml)
    {
        Dictionary<string, Dictionary<string, string>> sections = new(StringComparer.Ordinal);
        string? section = null;
        foreach (string raw in yaml.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (IsIgnorable(line))
            {
                continue;
            }

            if (TryReadTopLevelSection(line, out string name))
            {
                section = name;
                if (!sections.ContainsKey(section))
                {
                    sections[section] = new Dictionary<string, string>(StringComparer.Ordinal);
                }

                continue;
            }

            if (section is not null)
            {
                TryCaptureFirstKey(sections[section], line);
            }
        }

        return sections;
    }

    private static bool IsIgnorable(string line)
    {
        return string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#');
    }

    private static bool TryReadTopLevelSection(string line, out string name)
    {
        name = "";
        if (line.Length == 0 || line[0] is ' ' or '\t' or '-')
        {
            return false;
        }

        int colon = line.IndexOf(':');
        if (colon <= 0 || !string.IsNullOrWhiteSpace(line[(colon + 1)..]))
        {
            return false;
        }

        name = line[..colon].Trim();
        return name.Length > 0;
    }

    private static void TryCaptureFirstKey(Dictionary<string, string> values, string line)
    {
        string trimmed = line.TrimStart(' ', '\t', '-');
        int colon = trimmed.IndexOf(':');
        if (colon <= 0)
        {
            return;
        }

        string key = trimmed[..colon].Trim();
        string value = Unquote(trimmed[(colon + 1)..].Trim());
        if (key.Length == 0 || value.Length == 0 || values.ContainsKey(key))
        {
            return;
        }

        values[key] = value;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }
}
