using RecycleBinWeb.Models;
using System.Security.Cryptography;
using System.Text;

namespace RecycleBinWeb.Services;

/// <summary>
/// Reads the real Windows Recycle Bin by scanning $Recycle.Bin folders on
/// all fixed drives. Supports restore (move back to original path) and
/// permanent delete via SHFileOperation.
/// </summary>
public class RecycleBinService
{
    private readonly ILogger<RecycleBinService> _log;

    public RecycleBinService(ILogger<RecycleBinService> log) => _log = log;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Enumerate all items in the Recycle Bin across all drives.</summary>
    public List<RecycleItem> GetItems()
    {
        var items = new List<RecycleItem>();
        foreach (var drive in GetFixedDrives())
        {
            try { items.AddRange(ReadDrive(drive)); }
            catch (Exception ex) { _log.LogWarning(ex, "Cannot read recycle bin on {Drive}", drive); }
        }
        // Sort newest-deleted first
        items.Sort((a, b) => b.DeletedAt.CompareTo(a.DeletedAt));
        return items;
    }

    /// <summary>Overall stats (size + count).</summary>
    public RecycleBinStats GetStats()
    {
        var stats = new RecycleBinStats();
        long total = 0;

        foreach (var drive in GetFixedDrives())
        {
            var info = new Shell32.SHQUERYRBINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Shell32.SHQUERYRBINFO>() };
            int hr   = Shell32.SHQueryRecycleBin(drive + "\\", ref info);
            if (hr == 0)
            {
                stats.ByDrive.Add(new DriveStats
                {
                    Drive   = drive,
                    Items   = (int)info.i64NumItems,
                    Bytes   = info.i64Size,
                    Display = FormatSize(info.i64Size),
                });
                stats.TotalItems += (int)info.i64NumItems;
                total            += info.i64Size;
            }
        }
        stats.TotalBytes   = total;
        stats.TotalDisplay = FormatSize(total);
        return stats;
    }

    /// <summary>Restore one item back to its original location.</summary>
    public (bool ok, string error) Restore(string id)
    {
        var item = GetItems().FirstOrDefault(i => i.Id == id);
        if (item == null) return (false, "Item not found");
        return MoveItem(item.RecyclePath, item.OriginalPath);
    }

    /// <summary>Permanently delete one item.</summary>
    public (bool ok, string error) Delete(string id)
    {
        var item = GetItems().FirstOrDefault(i => i.Id == id);
        if (item == null) return (false, "Item not found");
        return DeleteItem(item.RecyclePath, item.IsDirectory);
    }

    /// <summary>Restore multiple items.</summary>
    public (int ok, int fail) RestoreMany(IEnumerable<string> ids)
    {
        int ok = 0, fail = 0;
        foreach (var id in ids)
        {
            var (success, _) = Restore(id);
            if (success) ok++; else fail++;
        }
        return (ok, fail);
    }

    /// <summary>Delete multiple items permanently.</summary>
    public (int ok, int fail) DeleteMany(IEnumerable<string> ids)
    {
        int ok = 0, fail = 0;
        foreach (var id in ids)
        {
            var (success, _) = Delete(id);
            if (success) ok++; else fail++;
        }
        return (ok, fail);
    }

    /// <summary>Empty the entire recycle bin (all drives).</summary>
    public (bool ok, string error) EmptyAll()
    {
        int hr = Shell32.SHEmptyRecycleBin(
            IntPtr.Zero, null,
            Shell32.SHERB_NOCONFIRMATION | Shell32.SHERB_NOPROGRESSUI | Shell32.SHERB_NOSOUND);
        return hr == 0 || hr == unchecked((int)0x80070002)  // "not found" = already empty
            ? (true, "")
            : (false, $"SHEmptyRecycleBin failed: 0x{hr:X8}");
    }

    // ── Drive enumeration ─────────────────────────────────────────────────────

    private static IEnumerable<string> GetFixedDrives()
        => DriveInfo.GetDrives()
                    .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                    .Select(d => d.Name.TrimEnd('\\'));

    // ── $Recycle.Bin reader ───────────────────────────────────────────────────

    private List<RecycleItem> ReadDrive(string drive)
    {
        var result  = new List<RecycleItem>();
        var binRoot = Path.Combine(drive + "\\", "$Recycle.Bin");
        if (!Directory.Exists(binRoot)) return result;

        // Each subfolder under $Recycle.Bin is a SID (one per user)
        foreach (var sidDir in Directory.GetDirectories(binRoot))
        {
            // $I files contain metadata; $R files contain the actual data
            string[] iFiles;
            try { iFiles = Directory.GetFiles(sidDir, "$I*", SearchOption.TopDirectoryOnly); }
            catch { continue; }

            foreach (var iFile in iFiles)
            {
                try
                {
                    var item = ParseInfoFile(iFile, drive);
                    if (item != null) result.Add(item);
                }
                catch (Exception ex) { _log.LogDebug(ex, "Skipping {File}", iFile); }
            }
        }
        return result;
    }

    /// <summary>
    /// Parse a $I metadata file.
    /// Format (Vista+):
    ///   Offset 0:  8 bytes — magic header (0x01 or 0x02)
    ///   Offset 8:  8 bytes — original file size (little-endian int64)
    ///   Offset 16: 8 bytes — deletion timestamp (FILETIME)
    ///   Offset 24: 4 bytes — original path length (chars, int32)  [version 2 only]
    ///   Offset 28: variable — original path (UTF-16LE)
    /// </summary>
    private RecycleItem? ParseInfoFile(string iFilePath, string drive)
    {
        byte[] data = File.ReadAllBytes(iFilePath);
        if (data.Length < 24) return null;

        long version = BitConverter.ToInt64(data, 0);
        long size    = BitConverter.ToInt64(data, 8);
        long ft      = BitConverter.ToInt64(data, 16);

        DateTime deletedAt = ft > 0
            ? DateTime.FromFileTimeUtc(ft).ToLocalTime()
            : File.GetLastWriteTime(iFilePath);

        // Decode original path
        string originalPath;
        if (version == 2 && data.Length >= 28)
        {
            int pathLen = BitConverter.ToInt32(data, 24);
            int byteLen = pathLen * 2;
            if (data.Length >= 28 + byteLen)
                originalPath = Encoding.Unicode.GetString(data, 28, byteLen).TrimEnd('\0');
            else
                originalPath = Encoding.Unicode.GetString(data, 28, data.Length - 28).TrimEnd('\0');
        }
        else if (data.Length > 24)
        {
            originalPath = Encoding.Unicode.GetString(data, 24, data.Length - 24).TrimEnd('\0');
        }
        else return null;

        if (string.IsNullOrWhiteSpace(originalPath)) return null;

        // Corresponding $R file
        var rFile = Path.Combine(
            Path.GetDirectoryName(iFilePath)!,
            "$R" + Path.GetFileName(iFilePath)[2..]);

        bool isDir   = Directory.Exists(rFile);
        bool isFile  = File.Exists(rFile);
        if (!isDir && !isFile) return null;  // orphaned $I without $R

        // Use actual disk size for dirs
        long actualSize = isDir ? GetDirSize(rFile) : (isFile ? new FileInfo(rFile).Length : size);
        if (actualSize == 0 && size > 0) actualSize = size;

        string name = Path.GetFileName(originalPath);
        string ext  = isDir ? "Folder" : Path.GetExtension(originalPath).TrimStart('.').ToUpper();

        var item = new RecycleItem
        {
            Id           = MakeId(iFilePath),
            Name         = name,
            OriginalPath = originalPath,
            RecyclePath  = rFile,
            SizeBytes    = actualSize,
            SizeDisplay  = FormatSize(actualSize),
            DeletedAt    = deletedAt,
            FileType     = string.IsNullOrEmpty(ext) ? "File" : ext,
            TypeIcon     = GetTypeIcon(ext, isDir),
            IsDirectory  = isDir,
            Drive        = drive,
        };
        return item;
    }

    // ── File operations ───────────────────────────────────────────────────────

    private (bool ok, string error) MoveItem(string from, string to)
    {
        try
        {
            // Ensure destination directory exists
            string? dir = Path.GetDirectoryName(to);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (Directory.Exists(from))
                Directory.Move(from, to);
            else if (File.Exists(from))
                File.Move(from, to, overwrite: false);
            else
                return (false, "Source no longer exists");

            return (true, "");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Restore failed {From} -> {To}", from, to);
            return (false, ex.Message);
        }
    }

    private (bool ok, string error) DeleteItem(string path, bool isDir)
    {
        try
        {
            if (isDir && Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            else if (File.Exists(path))
                File.Delete(path);
            else
                return (false, "Item no longer exists");

            // Also delete the matching $I metadata file
            var iFile = GetIFileForRFile(path);
            if (iFile != null && File.Exists(iFile))
                File.Delete(iFile);

            return (true, "");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Delete failed {Path}", path);
            return (false, ex.Message);
        }
    }

    private static string? GetIFileForRFile(string rPath)
    {
        var dir  = Path.GetDirectoryName(rPath);
        var name = Path.GetFileName(rPath);
        if (dir == null || !name.StartsWith("$R")) return null;
        return Path.Combine(dir, "$I" + name[2..]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static long GetDirSize(string path)
    {
        try
        {
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => { try { return f.Length; } catch { return 0L; } });
        }
        catch { return 0; }
    }

    private static string MakeId(string path)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant()));
        return Convert.ToHexString(hash)[..16];
    }

    public static string FormatSize(long bytes)
    {
        if (bytes <= 0)    return "0 B";
        if (bytes < 1024)  return $"{bytes} B";
        if (bytes < 1024 * 1024)       return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    private static string GetTypeIcon(string ext, bool isDir)
    {
        if (isDir) return "folder";
        return ext.ToUpper() switch
        {
            "PDF"                          => "pdf",
            "DOC" or "DOCX"               => "word",
            "XLS" or "XLSX"               => "excel",
            "PPT" or "PPTX"               => "ppt",
            "ZIP" or "RAR" or "7Z" or "GZ" or "TAR" => "archive",
            "PNG" or "JPG" or "JPEG" or "GIF" or "BMP" or "WEBP" or "SVG" or "ICO" => "image",
            "MP4" or "MKV" or "AVI" or "MOV" or "WMV" or "FLV" or "WEBM" => "video",
            "MP3" or "WAV" or "FLAC" or "AAC" or "OGG" or "WMA" => "audio",
            "TXT" or "MD" or "LOG" or "CSV" or "INI" or "CFG" or "CONF" => "text",
            "CS" or "JS" or "TS" or "PY" or "JAVA" or "CPP" or "C" or "H"
                or "HTML" or "CSS" or "JSON" or "XML" or "YAML" or "YML"
                or "SH" or "BAT" or "PS1" or "SQL" => "code",
            "EXE" or "MSI" or "DLL"       => "binary",
            _                             => "file",
        };
    }
}
