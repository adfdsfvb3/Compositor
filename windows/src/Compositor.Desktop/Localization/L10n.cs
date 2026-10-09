using System.Text.Json;
using System.Text.RegularExpressions;

namespace Compositor.Desktop;

/// <summary>Small, file-backed UI catalogue. Chinese is the default for the Windows build.</summary>
internal static class L10n
{
    private static readonly Lazy<Catalogue> Current = new(Load, true);

    public static string T(string text)
    {
        if (string.IsNullOrEmpty(text) || IsEnglishRequested()) return text;
        return Current.Value.Translate(text);
    }

    private static bool IsEnglishRequested()
        => string.Equals(Environment.GetEnvironmentVariable("COMPOSITOR_LANGUAGE"), "en", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("COMPOSITOR_LANGUAGE"), "en-US", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("COMPOSITOR_LANGUAGE"), "en-GB", StringComparison.OrdinalIgnoreCase);

    private static Catalogue Load()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(AppContext.BaseDirectory, "Localization", "zh-CN.json");
        try
        {
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                var source = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
                if (source is not null)
                    foreach (var (key, value) in source) map[Normalize(key)] = value;
            }
        }
        catch
        {
            // A missing or damaged optional catalogue should never prevent the editor from opening.
        }
        return new Catalogue(map);
    }

    private static string Normalize(string value)
    {
        // Menu mnemonics are useful in English but are not displayed in the Chinese catalogue.
        var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace('…', ' ')
            .Replace("...", " ", StringComparison.Ordinal);
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private sealed class Catalogue
    {
        private readonly IReadOnlyDictionary<string, string> _map;
        private readonly (Regex Pattern, string Translation)[] _templates;

        public Catalogue(IReadOnlyDictionary<string, string> map)
        {
            _map = map;
            _templates = map
                .Where(pair => pair.Key.Contains('{'))
                .Select(pair => (BuildPattern(pair.Key), pair.Value))
                .ToArray();
        }

        public string Translate(string source)
        {
            var key = Normalize(source);
            if (_map.TryGetValue(key, out var exact)) return exact;

            foreach (var (pattern, translation) in _templates)
            {
                var match = pattern.Match(key);
                if (!match.Success) continue;
                var result = translation;
                for (var i = 1; i < match.Groups.Count; i++)
                    result = result.Replace("{" + (i - 1) + "}", match.Groups[i].Value, StringComparison.Ordinal);
                return result;
            }
            return source;
        }

        private static Regex BuildPattern(string key)
        {
            var builder = new System.Text.StringBuilder("^");
            var position = 0;
            while (position < key.Length)
            {
                var open = key.IndexOf('{', position);
                if (open < 0) { builder.Append(Regex.Escape(key[position..])); break; }
                var close = key.IndexOf('}', open + 1);
                if (close < 0) { builder.Append(Regex.Escape(key[position..])); break; }
                builder.Append(Regex.Escape(key[position..open]));
                builder.Append("(.+?)");
                position = close + 1;
            }
            builder.Append('$');
            return new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        }
    }
}
