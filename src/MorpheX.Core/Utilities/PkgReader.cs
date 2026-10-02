using System.Text;
using Serilog;

namespace MorpheX.Core.Utilities;

/// <summary>
/// Minimal reader for Wallpaper Engine .pkg archives (magic: PKGV0001-PKGV9999).
/// Format (little-endian):
///   - uint32   : file count
///   - bytes[8] : magic e.g. "PKGV0013"
///   - Entries  : for each entry:
///       - int32  : full-path byte length
///       - byte[] : UTF-8 path (NOT null-terminated)
///       - int32  : data offset relative to end-of-header
///       - int32  : data byte length
/// </summary>
public static class PkgReader
{
    public readonly record struct PkgEntry(string FullPath, int Offset, int Length);

    /// <summary>
    /// Reads the table of contents from a .pkg file.
    /// Returns an empty list if the file cannot be parsed.
    /// </summary>
    public static List<PkgEntry> ReadTableOfContents(string pkgPath)
    {
        var entries = new List<PkgEntry>();
        try
        {
            using var stream = File.OpenRead(pkgPath);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

            // Read magic (I32Size-prefixed string, no null terminator)
            var magicLen = reader.ReadInt32();
            var magic = Encoding.UTF8.GetString(reader.ReadBytes(magicLen));
            if (!magic.StartsWith("PKGV", StringComparison.Ordinal))
            {
                Log.Warning("PkgReader: unrecognised magic '{Magic}' in {Path}", magic, pkgPath);
                return entries;
            }

            var entryCount = reader.ReadInt32();
            for (var i = 0; i < entryCount; i++)
            {
                var pathLen = reader.ReadInt32();
                var fullPath = Encoding.UTF8.GetString(reader.ReadBytes(pathLen));
                var offset   = reader.ReadInt32();
                var length   = reader.ReadInt32();
                entries.Add(new PkgEntry(fullPath, offset, length));
            }

            // Record where the header ends so callers know the data base offset
            var dataBase = (int)stream.Position;
            // Patch offsets to be absolute within the stream
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                entries[i] = e with { Offset = e.Offset + dataBase };
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PkgReader: failed to read TOC from {Path}", pkgPath);
        }
        return entries;
    }

    /// <summary>
    /// Extracts a single file from the .pkg by its internal path (e.g. "sounds/music.mp3").
    /// Returns the raw bytes, or null if not found / on error.
    /// </summary>
    public static byte[]? ExtractEntry(string pkgPath, string entryPath)
    {
        try
        {
            var toc = ReadTableOfContents(pkgPath);
            var entry = toc.FirstOrDefault(e =>
                string.Equals(e.FullPath, entryPath, StringComparison.OrdinalIgnoreCase));

            if (entry.FullPath == null)
            {
                Log.Warning("PkgReader: entry '{Entry}' not found in {Pkg}", entryPath, pkgPath);
                return null;
            }

            using var stream = File.OpenRead(pkgPath);
            stream.Seek(entry.Offset, SeekOrigin.Begin);
            var buf = new byte[entry.Length];
            var read = stream.Read(buf, 0, buf.Length);
            return read == buf.Length ? buf : null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PkgReader: failed to extract '{Entry}' from {Pkg}", entryPath, pkgPath);
            return null;
        }
    }

    /// <summary>
    /// Returns all audio entries (mp3, ogg, wav, flac) found in the .pkg.
    /// </summary>
    public static IEnumerable<PkgEntry> GetAudioEntries(string pkgPath)
    {
        var audioExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".mp3", ".ogg", ".wav", ".flac", ".aac", ".m4a" };
        return ReadTableOfContents(pkgPath)
            .Where(e => audioExts.Contains(Path.GetExtension(e.FullPath)));
    }
}
