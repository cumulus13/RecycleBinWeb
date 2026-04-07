using System.Runtime.InteropServices;
using System.Text;

namespace RecycleBinWeb.Services;

/// <summary>
/// P/Invoke declarations for Windows Shell32 and SHFileOperation APIs.
/// Used to enumerate, restore, and permanently delete Recycle Bin items.
/// </summary>
internal static class Shell32
{
    // ── SHEmptyRecycleBin ────────────────────────────────────────────────────
    public const uint SHERB_NOCONFIRMATION = 0x00000001;
    public const uint SHERB_NOPROGRESSUI   = 0x00000002;
    public const uint SHERB_NOSOUND        = 0x00000004;

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    // ── SHQueryRecycleBin ────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    public struct SHQUERYRBINFO
    {
        public int    cbSize;
        public long   i64Size;
        public long   i64NumItems;
    }

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    // ── SHFileOperation — used for Restore (MOVE) and Delete ────────────────
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint   wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool   fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszProgressTitle;
    }

    public const uint FO_MOVE   = 0x0001;
    public const uint FO_DELETE = 0x0003;

    public const ushort FOF_NOCONFIRMATION    = 0x0010;
    public const ushort FOF_NOERRORUI         = 0x0400;
    public const ushort FOF_SILENT            = 0x0004;
    public const ushort FOF_NOCONFIRMMKDIR    = 0x0200;
    public const ushort FOF_ALLOWUNDO         = 0x0040;

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    // ── IFileOperation (Vista+) — preferred for restore ──────────────────────
    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHGetKnownFolderPath(
        ref Guid rfid, uint dwFlags, IntPtr hToken,
        [MarshalAs(UnmanagedType.LPWStr)] out string ppszPath);
}
