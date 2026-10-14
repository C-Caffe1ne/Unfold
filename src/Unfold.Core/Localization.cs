using System.Globalization;

namespace Unfold.Core;

// Persisted values stay stable even when a language's display name changes.
public enum AppLanguage { Korean = 0, English = 1, Japanese = 2, ChineseSimplified = 3 }

/// <summary>
/// UI text in the selected language. The Korean source text is the lookup key, so call sites stay
/// readable and a missing translation falls back to Korean instead of an empty label.
/// </summary>
public static partial class L
{
    public static IReadOnlyList<AppLanguage> Languages { get; } =
        [AppLanguage.Korean, AppLanguage.English, AppLanguage.Japanese, AppLanguage.ChineseSimplified];
    public static AppLanguage Current { get; private set; } = AppLanguage.Korean;
    public static CultureInfo Culture => CultureOf(Current);
    private static readonly Dictionary<AppLanguage, Dictionary<string, string>> sources = [];

    public static void Use(AppLanguage language)
    {
        if (!Enum.IsDefined(language)) throw new ArgumentOutOfRangeException(nameof(language));
        Current = language;
    }

    public static string T(string korean) => T(korean, Current);
    public static string T(string korean, AppLanguage language)
    {
        if (language == AppLanguage.Korean || !Table.TryGetValue(korean, out var row)) return korean;
        return language switch
        {
            AppLanguage.English => row.English,
            AppLanguage.Japanese => row.Japanese,
            AppLanguage.ChineseSimplified => row.Chinese,
            _ => korean
        };
    }
    public static string F(string korean, params object?[] args) => string.Format(Culture, T(korean), args);
    /// <summary>Korean text that means something else in <paramref name="context"/>; the table keeps it as "context|text".</summary>
    public static string Tx(string context, string korean) =>
        Current != AppLanguage.Korean && Table.ContainsKey(context + "|" + korean) ? T(context + "|" + korean) : T(korean);
    /// <summary>Marks Korean text that is stored and translated later with <see cref="T(string)"/>.</summary>
    public static string Key(string korean) => korean;

    /// <summary>Korean source of a text shown in the current language, or the text itself.</summary>
    public static string Source(string text)
    {
        if (Current == AppLanguage.Korean || Table.ContainsKey(text)) return text;
        lock (sources)
        {
            if (!sources.TryGetValue(Current, out var reverse))
            {
                reverse = new(StringComparer.Ordinal);
                foreach (var key in Table.Keys) reverse.TryAdd(T(key), key);
                sources[Current] = reverse;
            }
            return reverse.TryGetValue(text, out var korean) ? korean : text;
        }
    }

    public static string NativeName(AppLanguage language) => language switch
    {
        AppLanguage.Korean => "한국어",
        AppLanguage.English => "English",
        AppLanguage.Japanese => "日本語",
        AppLanguage.ChineseSimplified => "简体中文",
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };

    public static CultureInfo CultureOf(AppLanguage language) => CultureInfo.GetCultureInfo(language switch
    {
        AppLanguage.English => "en-US",
        AppLanguage.Japanese => "ja-JP",
        AppLanguage.ChineseSimplified => "zh-CN",
        _ => "ko-KR"
    });

    /// <summary>First-run choice: the first OS preferred language the app has, otherwise English.</summary>
    public static AppLanguage Preferred(IEnumerable<string> languages)
    {
        foreach (var name in languages)
            switch (name.Split('-', '_')[0].ToLowerInvariant())
            {
                case "ko": return AppLanguage.Korean;
                case "ja": return AppLanguage.Japanese;
                case "zh": return AppLanguage.ChineseSimplified;
                case "en": return AppLanguage.English;
            }
        return AppLanguage.English;
    }

    private sealed record Translation(string English, string Japanese, string Chinese);
}
