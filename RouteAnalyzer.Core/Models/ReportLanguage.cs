namespace RouteAnalyzer.Models;

public static class ReportLanguage
{
    public const string English = "en";
    public const string TraditionalChinese = "zh-TW";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return English;
        }

        var trimmed = value.Trim();

        return trimmed.Equals("zh", StringComparison.OrdinalIgnoreCase)
               || trimmed.Equals("zh-TW", StringComparison.OrdinalIgnoreCase)
               || trimmed.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase)
            ? TraditionalChinese
            : English;
    }

    public static bool IsTraditionalChinese(string? value)
    {
        return string.Equals(Normalize(value), TraditionalChinese, StringComparison.OrdinalIgnoreCase);
    }
}
