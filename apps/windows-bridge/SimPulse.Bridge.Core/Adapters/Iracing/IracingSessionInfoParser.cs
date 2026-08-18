using System.Globalization;
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
    public static IracingSessionInfo Parse(string yaml, int? driverCarIdx = null, int? sessionNum = null)
    {
        Dictionary<string, Dictionary<string, string>> sections = ParseSections(yaml);
        OptionalValue<string> vehicleId = First(sections, "DriverInfo", "CarPath");
        OptionalValue<string> vehicleDisplayName = First(sections, "DriverInfo", "CarScreenName", "CarPath");
        OptionalValue<string> sessionType = First(sections, "SessionInfo", "SessionType");

        if (driverCarIdx is int carIdx &&
            TryFindListEntry(yaml, "Drivers", "CarIdx", carIdx.ToString(CultureInfo.InvariantCulture), out Dictionary<string, string> driver))
        {
            vehicleId = First(driver, "CarPath");
            vehicleDisplayName = First(driver, "CarScreenName", "CarPath");
        }

        if (sessionNum is int num &&
            TryFindListEntry(yaml, "Sessions", "SessionNum", num.ToString(CultureInfo.InvariantCulture), out Dictionary<string, string> session))
        {
            sessionType = First(session, "SessionType");
        }

        return new IracingSessionInfo(
            First(sections, "WeekendInfo", "TrackID"),
            First(sections, "WeekendInfo", "TrackDisplayName", "TrackName"),
            vehicleId,
            vehicleDisplayName,
            sessionType);
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

        return First(values, keys);
    }

    private static OptionalValue<string> First(Dictionary<string, string> values, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                return OptionalValue<string>.Available(value);
            }
        }

        return OptionalValue<string>.Unavailable();
    }

    private static bool TryFindListEntry(
        string yaml,
        string listName,
        string matchKey,
        string matchValue,
        out Dictionary<string, string> entry)
    {
        foreach (Dictionary<string, string> item in ParseList(yaml, listName))
        {
            if (item.TryGetValue(matchKey, out string? value) &&
                string.Equals(value, matchValue, StringComparison.Ordinal))
            {
                entry = item;
                return true;
            }
        }

        entry = new Dictionary<string, string>(StringComparer.Ordinal);
        return false;
    }

    private static List<Dictionary<string, string>> ParseList(string yaml, string listName)
    {
        List<Dictionary<string, string>> items = [];
        bool inList = false;
        int listIndent = -1;
        Dictionary<string, string>? current = null;

        foreach (string raw in yaml.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (IsIgnorable(line))
            {
                continue;
            }

            if (TryReadTopLevelSection(line, out _))
            {
                inList = false;
                current = null;
                continue;
            }

            if (TryReadListHeader(line, listName, out int headerIndent))
            {
                inList = true;
                listIndent = headerIndent;
                current = null;
                continue;
            }

            if (!inList)
            {
                continue;
            }

            string trimmed = line.TrimStart(' ', '\t');
            bool dashItem = trimmed.StartsWith('-');
            if (!dashItem && Indent(line) <= listIndent)
            {
                inList = false;
                current = null;
                continue;
            }

            if (dashItem)
            {
                current = new Dictionary<string, string>(StringComparer.Ordinal);
                items.Add(current);
                TryCaptureFirstKey(current, line);
                continue;
            }

            if (current is not null)
            {
                TryCaptureFirstKey(current, line);
            }
        }

        return items;
    }

    private static bool TryReadListHeader(string line, string listName, out int indent)
    {
        indent = Indent(line);
        string trimmed = line.Trim();
        int colon = trimmed.IndexOf(':');
        if (colon <= 0 || !string.IsNullOrWhiteSpace(trimmed[(colon + 1)..]))
        {
            return false;
        }

        return string.Equals(trimmed[..colon].Trim(), listName, StringComparison.Ordinal);
    }

    private static int Indent(string line)
    {
        int count = 0;
        while (count < line.Length && line[count] is ' ' or '\t')
        {
            count++;
        }

        return count;
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
