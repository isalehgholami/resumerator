using System.Text;

namespace ResumeMaker.Core.Common;

public static class ProfileFileName
{
    private const int MaxLength = 100;

    /// <summary>Returns a file-system safe base name (no extension, no path separators).</summary>
    public static string Sanitize(string? name, string fallback = "resume")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name.Trim())
        {
            sb.Append(char.IsControl(c) || Array.IndexOf(invalid, c) >= 0 || c is '/' or '\\' ? '_' : c);
        }
        var result = sb.ToString().Trim(' ', '.', '_');
        if (result.Length > MaxLength)
        {
            result = result[..MaxLength].TrimEnd(' ', '.', '_');
        }
        return result.Length == 0 ? fallback : result;
    }
}
