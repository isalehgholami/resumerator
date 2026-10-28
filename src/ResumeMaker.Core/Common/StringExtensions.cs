using System.Text;

namespace ResumeMaker.Core.Common;

public static class StringExtensions
{
    private const char PersianZero = '۰';
    private const char ArabicIndicZero = '٠';

    /// <summary>Maps ASCII digits and Arabic-Indic digits to Persian digits.</summary>
    public static string ToPersianNumerals(this string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? "";
        }
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            sb.Append(
                c is >= '0' and <= '9' ? (char)(PersianZero + (c - '0'))
                : c is >= '٠' and <= '٩' ? (char)(PersianZero + (c - ArabicIndicZero))
                : c
            );
        }
        return sb.ToString();
    }

    /// <summary>Maps Persian and Arabic-Indic digits to ASCII digits.</summary>
    public static string ToEnglishNumerals(this string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? "";
        }
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            sb.Append(
                c is >= '۰' and <= '۹' ? (char)('0' + (c - PersianZero))
                : c is >= '٠' and <= '٩' ? (char)('0' + (c - ArabicIndicZero))
                : c
            );
        }
        return sb.ToString();
    }
}
