namespace ExplorerWeb.Models;

public class FsEntry
{
    public string   Name        { get; set; } = "";
    public string   FullPath    { get; set; } = "";
    public bool     IsDirectory { get; set; }
    public long     SizeBytes   { get; set; }
    public string   SizeDisplay { get; set; } = "";
    public DateTime Modified    { get; set; }
    public DateTime Created     { get; set; }
    public string   Extension   { get; set; } = "";   // lowercase, no dot
    public string   TypeLabel   { get; set; } = "";
    public string   IconKey     { get; set; } = "";
    public bool     IsHidden    { get; set; }
    public bool     IsReadOnly  { get; set; }
    public bool     IsSystem    { get; set; }
    public string   Attributes  { get; set; } = "";
}

public class DirectoryListing
{
    public string          Path       { get; set; } = "";
    public string          Name       { get; set; } = "";
    public List<FsEntry>   Entries    { get; set; } = new();
    public List<Breadcrumb>Breadcrumbs{ get; set; } = new();
    public long            TotalSize  { get; set; }
    public int             FileCount  { get; set; }
    public int             FolderCount{ get; set; }
    public string?         ParentPath { get; set; }
    public List<DriveInfo2>Drives     { get; set; } = new();
}

public class Breadcrumb
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
}

public class DriveInfo2
{
    public string  Name        { get; set; } = "";
    public string  Label       { get; set; } = "";
    public string  DriveType   { get; set; } = "";
    public long    TotalBytes  { get; set; }
    public long    FreeBytes   { get; set; }
    public string  TotalDisplay{ get; set; } = "";
    public string  FreeDisplay { get; set; } = "";
    public int     UsedPercent { get; set; }
    public string  IconKey     { get; set; } = "";
}

public class FileProperties
{
    public string   Name        { get; set; } = "";
    public string   FullPath    { get; set; } = "";
    public bool     IsDirectory { get; set; }
    public string   SizeDisplay { get; set; } = "";
    public long     SizeBytes   { get; set; }
    public DateTime Created     { get; set; }
    public DateTime Modified    { get; set; }
    public DateTime Accessed    { get; set; }
    public string   Attributes  { get; set; } = "";
    public string   TypeLabel   { get; set; } = "";
    public int?     ItemCount   { get; set; }  // for folders
}
