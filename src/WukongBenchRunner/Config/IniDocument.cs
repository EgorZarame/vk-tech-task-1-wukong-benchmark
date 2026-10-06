namespace WukongBenchRunner.Config;

/// <summary>
/// Минимальный редактор ini, сохраняющий файл построчно: меняются только нужные ключи,
/// порядок, комментарии и чужие секции остаются как были.
/// </summary>
public sealed class IniDocument
{
    private readonly List<string> _lines;
    private readonly string _newLine;

    private IniDocument(List<string> lines, string newLine)
    {
        _lines = lines;
        _newLine = newLine;
    }

    public static IniDocument Parse(string text)
    {
        var newLine = text.Contains("\r\n") ? "\r\n" : "\n";
        return new IniDocument([.. text.Replace("\r\n", "\n").Split('\n')], newLine);
    }

    public string? Get(string section, string key)
    {
        var index = FindKey(section, key);
        return index < 0 ? null : _lines[index][(key.Length + 1)..];
    }

    public void Set(string section, string key, string value)
    {
        var index = FindKey(section, key);
        if (index >= 0)
        {
            _lines[index] = $"{key}={value}";
            return;
        }

        var header = FindSection(section);
        if (header < 0)
        {
            // Секции нет — добавляем в конец, отделив пустой строкой.
            if (_lines.Count > 0 && _lines[^1].Length > 0) _lines.Add("");
            _lines.Add($"[{section}]");
            _lines.Add($"{key}={value}");
            return;
        }

        _lines.Insert(SectionEnd(header), $"{key}={value}");
    }

    public bool HasSection(string section) => FindSection(section) >= 0;

    public override string ToString() => string.Join(_newLine, _lines);

    private int FindSection(string section) =>
        _lines.FindIndex(l => l.Trim().Equals($"[{section}]", StringComparison.OrdinalIgnoreCase));

    private int FindKey(string section, string key)
    {
        var header = FindSection(section);
        if (header < 0) return -1;

        for (var i = header + 1; i < _lines.Count && !IsSectionHeader(_lines[i]); i++)
        {
            if (_lines[i].StartsWith(key + "=", StringComparison.Ordinal)) return i;
        }
        return -1;
    }

    // Вставляем после последней непустой строки секции, чтобы не разрывать её пустой строкой.
    private int SectionEnd(int header)
    {
        var last = header;
        for (var i = header + 1; i < _lines.Count && !IsSectionHeader(_lines[i]); i++)
        {
            if (_lines[i].Trim().Length > 0) last = i;
        }
        return last + 1;
    }

    private static bool IsSectionHeader(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith('[') && trimmed.EndsWith(']');
    }
}
