namespace RecycleBinWeb.Models;

public class RecycleItem
{
    public string Id           { get; set; } = "";   // unique key (path hash)
    public string Name         { get; set; } = "";   // original filename
    public string OriginalPath { get; set; } = "";   // where it came from
    public string RecyclePath  { get; set; } = "";   // current $Recycle.Bin path
    public long   SizeBytes    { get; set; }
    public string SizeDisplay  { get; set; } = "";
    public DateTime DeletedAt  { get; set; }
    public string FileType     { get; set; } = "";   // extension or "Folder"
    public string TypeIcon     { get; set; } = "";   // category key for frontend
    public bool   IsDirectory  { get; set; }
    public string Drive        { get; set; } = "";   // e.g. "C:"
}

public class RecycleBinStats
{
    public int    TotalItems   { get; set; }
    public long   TotalBytes   { get; set; }
    public string TotalDisplay { get; set; } = "";
    public List<DriveStats> ByDrive { get; set; } = new();
}

public class DriveStats
{
    public string Drive        { get; set; } = "";
    public int    Items        { get; set; }
    public long   Bytes        { get; set; }
    public string Display      { get; set; } = "";
}
