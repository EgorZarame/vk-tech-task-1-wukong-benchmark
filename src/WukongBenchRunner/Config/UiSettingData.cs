using System.Text.RegularExpressions;

namespace WukongBenchRunner.Config;

/// <summary>
/// Значение ключа <c>UISettingData</c> — то, что показывает меню настроек бенчмарка.
/// Формат: <c>(("Key", "Value"),("Key2", "Value2"),...)</c>.
/// </summary>
public static partial class UiSettingData
{
    public static IReadOnlyDictionary<string, string> Parse(string raw)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in PairRegex().Matches(raw))
        {
            result[match.Groups["key"].Value] = match.Groups["value"].Value;
        }
        return result;
    }

    /// <summary>Заменяет значения, сохраняя остальную строку байт в байт.</summary>
    /// <exception cref="ConfigFormatException">Ключа нет в строке — значит, конфиг другой версии.</exception>
    public static string Apply(string raw, IReadOnlyDictionary<string, string> values)
    {
        var missing = values.Keys.Except(Parse(raw).Keys).ToList();
        if (missing.Count > 0)
        {
            throw new ConfigFormatException(
                $"В UISettingData нет ключей: {string.Join(", ", missing)}. " +
                "Возможно, версия бенчмарка отличается — запустите его вручную и сохраните настройки в меню.");
        }

        return PairRegex().Replace(raw, match =>
        {
            var key = match.Groups["key"].Value;
            return values.TryGetValue(key, out var value)
                ? $"{match.Groups["prefix"].Value}{value}\")"
                : match.Value;
        });
    }

    [GeneratedRegex("""(?<prefix>\("(?<key>[^"]+)",\s*")(?<value>[^"]*)"\)""")]
    private static partial Regex PairRegex();
}

public sealed class ConfigFormatException(string message) : Exception(message);
