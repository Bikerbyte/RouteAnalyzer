using System.Globalization;
using System.Text;

namespace RouteAnalyzer.Services;

public static class CommandOutputDecoder
{
    static CommandOutputDecoder()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static readonly char[] SuspiciousMojibakeChars =
    [
        'Ã', 'Â', 'Å', 'Æ', 'Ç', 'È', 'É', 'Ð', 'Ñ', 'Ò', 'Ó', 'Ô', 'Õ', 'Ö',
        'Ø', 'Ù', 'Ú', 'Û', 'Ü', 'Ý', 'Þ', 'ß', 'æ', 'ç', 'è', 'é', 'ê', 'ë',
        'ì', 'í', 'î', 'ï', 'ð', 'ñ', 'ò', 'ó', 'ô', 'õ', 'ö', 'ø', 'ù', 'ú',
        'û', 'ü', 'ý', 'þ'
    ];

    public static string Decode(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        var candidates = GetCandidateEncodings().DistinctBy(static encoding => encoding.CodePage);
        var best = string.Empty;
        var bestScore = int.MinValue;

        foreach (var encoding in candidates)
        {
            var decoded = encoding.GetString(bytes);
            var score = Score(decoded);
            if (score > bestScore)
            {
                best = decoded;
                bestScore = score;
            }
        }

        return best;
    }

    private static IEnumerable<Encoding> GetCandidateEncodings()
    {
        yield return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
        yield return Encoding.Default;

        if (OperatingSystem.IsWindows())
        {
            yield return Console.OutputEncoding;

            Encoding? oemEncoding = null;
            try
            {
                oemEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
            }
            catch
            {
                // 這裡先放寬一點，後面還有其他編碼可以試。
            }

            if (oemEncoding is not null)
            {
                yield return oemEncoding;
            }

            // 傳統中文 Windows 環境還是很常遇到 Big5，這裡補一手 fallback。
            Encoding? big5Encoding = null;
            try
            {
                big5Encoding = Encoding.GetEncoding(950);
            }
            catch
            {
                // runtime 沒提供 Big5 就算了，不要讓整個解碼流程卡死。
            }

            if (big5Encoding is not null)
            {
                yield return big5Encoding;
            }
        }
    }

    private static int Score(string value)
    {
        var score = 0;

        foreach (var ch in value)
        {
            if (ch == '\uFFFD')
            {
                score -= 20;
                continue;
            }

            if (char.IsControl(ch) && ch is not '\r' and not '\n' and not '\t')
            {
                score -= 8;
                continue;
            }

            if (IsCjk(ch))
            {
                score += 4;
                continue;
            }

            if (SuspiciousMojibakeChars.Contains(ch))
            {
                score -= 3;
                continue;
            }

            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch))
            {
                score += 1;
            }
        }

        return score;
    }

    private static bool IsCjk(char ch)
    {
        return ch is >= '\u3400' and <= '\u9FFF'
            or >= '\uF900' and <= '\uFAFF';
    }
}
