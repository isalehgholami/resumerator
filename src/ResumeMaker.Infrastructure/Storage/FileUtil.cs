using System.Text;

namespace ResumeMaker.Infrastructure.Storage;

internal static class FileUtil
{
    public static readonly UTF8Encoding Utf8NoBom = new(false);

    /// <summary>Writes via temp file + move so readers never observe a partial file. 0600 on Unix.</summary>
    public static async Task WriteAtomicAsync(string path, byte[] content, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(tmp, content, ct).ConfigureAwait(false);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp))
            {
                try { File.Delete(tmp); } catch (IOException) { }
            }
        }
    }

    public static Task WriteAtomicTextAsync(string path, string text, CancellationToken ct = default) =>
        WriteAtomicAsync(path, Utf8NoBom.GetBytes(text), ct);

    public static void EnsurePrivateDirectory(string dir)
    {
        Directory.CreateDirectory(dir);
        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
            catch (Exception) { /* best effort */ }
        }
    }
}
