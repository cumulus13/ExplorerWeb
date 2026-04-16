using ExplorerWeb.Models;
using System.Security;

namespace ExplorerWeb.Services;

public class FileSystemService
{
    private readonly ILogger<FileSystemService> _log;

    public FileSystemService(ILogger<FileSystemService> log) => _log = log;

    // ── List directory ────────────────────────────────────────────────────────

    public DirectoryListing List(string path, bool showHidden = true, string sort = "name", string dir = "asc")
    {
        // Normalize path
        if (string.IsNullOrWhiteSpace(path) || path == "/" || path == "\\")
            path = ""; // root = drives list

        var listing = new DirectoryListing
        {
            Path   = path,
            Name   = string.IsNullOrEmpty(path) ? "This PC" : Path.GetFileName(path) is { Length: > 0 } n ? n : path,
            Drives = GetDrives(),
        };

        // Build breadcrumbs
        listing.Breadcrumbs = BuildBreadcrumbs(path);

        if (string.IsNullOrEmpty(path))
        {
            // Virtual root: return drives as entries
            foreach (var d in listing.Drives)
            {
                listing.Entries.Add(new FsEntry
                {
                    Name        = d.Label.Length > 0 ? $"{d.Label} ({d.Name})" : $"Local Disk ({d.Name})",
                    FullPath    = d.Name + "\\",
                    IsDirectory = true,
                    IconKey     = d.IconKey,
                    TypeLabel   = d.DriveType,
                    Modified    = DateTime.Now,
                    Created     = DateTime.Now,
                    SizeDisplay = d.FreeDisplay + " free",
                });
            }
            return listing;
        }

        if (!Directory.Exists(path))
        {
            listing.Name = "Not found";
            return listing;
        }

        var parent = Directory.GetParent(path);
        listing.ParentPath = parent?.FullName;

        // Folders
        try
        {
            var dirs = new DirectoryInfo(path).GetDirectories();
            foreach (var d in dirs)
            {
                try
                {
                    if (!showHidden && (d.Attributes.HasFlag(FileAttributes.Hidden) || d.Attributes.HasFlag(FileAttributes.System)))
                        continue;

                    listing.Entries.Add(new FsEntry
                    {
                        Name        = d.Name,
                        FullPath    = d.FullName,
                        IsDirectory = true,
                        Modified    = d.LastWriteTime,
                        Created     = d.CreationTime,
                        TypeLabel   = "Folder",
                        IconKey     = FolderIconKey(d.Name),
                        IsHidden    = d.Attributes.HasFlag(FileAttributes.Hidden),
                        IsSystem    = d.Attributes.HasFlag(FileAttributes.System),
                        IsReadOnly  = d.Attributes.HasFlag(FileAttributes.ReadOnly),
                        Attributes  = AttributeString(d.Attributes),
                    });
                    listing.FolderCount++;
                }
                catch { }
            }
        }
        catch (UnauthorizedAccessException) { }

        // Files
        try
        {
            var files = new DirectoryInfo(path).GetFiles();
            foreach (var f in files)
            {
                try
                {
                    if (!showHidden && (f.Attributes.HasFlag(FileAttributes.Hidden) || f.Attributes.HasFlag(FileAttributes.System)))
                        continue;

                    var ext = f.Extension.TrimStart('.').ToLower();
                    listing.Entries.Add(new FsEntry
                    {
                        Name        = f.Name,
                        FullPath    = f.FullName,
                        IsDirectory = false,
                        SizeBytes   = f.Length,
                        SizeDisplay = FormatSize(f.Length),
                        Modified    = f.LastWriteTime,
                        Created     = f.CreationTime,
                        Extension   = ext,
                        TypeLabel   = TypeLabel(ext),
                        IconKey     = FileIconKey(ext),
                        IsHidden    = f.Attributes.HasFlag(FileAttributes.Hidden),
                        IsSystem    = f.Attributes.HasFlag(FileAttributes.System),
                        IsReadOnly  = f.Attributes.HasFlag(FileAttributes.ReadOnly),
                        Attributes  = AttributeString(f.Attributes),
                    });
                    listing.FileCount++;
                    listing.TotalSize += f.Length;
                }
                catch { }
            }
        }
        catch (UnauthorizedAccessException) { }

        // Sort
        listing.Entries = SortEntries(listing.Entries, sort, dir);

        return listing;
    }

    // ── Search ────────────────────────────────────────────────────────────────

    public List<FsEntry> Search(string path, string query, bool recursive = true)
    {
        var results = new List<FsEntry>();
        if (string.IsNullOrWhiteSpace(query) || !Directory.Exists(path)) return results;

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        try
        {
            foreach (var f in Directory.EnumerateFiles(path, $"*{query}*", option))
            {
                try
                {
                    var fi  = new FileInfo(f);
                    var ext = fi.Extension.TrimStart('.').ToLower();
                    results.Add(new FsEntry
                    {
                        Name        = fi.Name,
                        FullPath    = fi.FullName,
                        SizeBytes   = fi.Length,
                        SizeDisplay = FormatSize(fi.Length),
                        Modified    = fi.LastWriteTime,
                        Extension   = ext,
                        TypeLabel   = TypeLabel(ext),
                        IconKey     = FileIconKey(ext),
                    });
                    if (results.Count >= 500) break;
                }
                catch { }
            }

            foreach (var d in Directory.EnumerateDirectories(path, $"*{query}*", option))
            {
                try
                {
                    var di = new DirectoryInfo(d);
                    results.Add(new FsEntry
                    {
                        Name        = di.Name,
                        FullPath    = di.FullName,
                        IsDirectory = true,
                        Modified    = di.LastWriteTime,
                        TypeLabel   = "Folder",
                        IconKey     = FolderIconKey(di.Name),
                    });
                    if (results.Count >= 500) break;
                }
                catch { }
            }
        }
        catch { }

        return results.OrderBy(e => e.IsDirectory ? 0 : 1).ThenBy(e => e.Name).ToList();
    }

    // ── File operations ───────────────────────────────────────────────────────

    public (bool ok, string error) CreateFolder(string parentPath, string name)
    {
        try
        {
            if (!IsValidName(name)) return (false, "Invalid folder name");
            var target = Path.Combine(parentPath, name);
            if (Directory.Exists(target)) return (false, "Folder already exists");
            Directory.CreateDirectory(target);
            return (true, "");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public (bool ok, string error) Rename(string fullPath, string newName)
    {
        try
        {
            if (!IsValidName(newName)) return (false, "Invalid name");
            var parent = Path.GetDirectoryName(fullPath)!;
            var target = Path.Combine(parent, newName);
            if (File.Exists(target) || Directory.Exists(target)) return (false, "Name already exists");

            if (Directory.Exists(fullPath))
                Directory.Move(fullPath, target);
            else if (File.Exists(fullPath))
                File.Move(fullPath, target);
            else
                return (false, "Item not found");

            return (true, "");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public (bool ok, string error) Delete(string fullPath, bool permanent = false)
    {
        try
        {
            if (Directory.Exists(fullPath))
            {
                if (permanent)
                    Directory.Delete(fullPath, recursive: true);
                else
                    SendToRecycleBin(fullPath, isDir: true);
            }
            else if (File.Exists(fullPath))
            {
                if (permanent)
                    File.Delete(fullPath);
                else
                    SendToRecycleBin(fullPath, isDir: false);
            }
            else return (false, "Item not found");
            return (true, "");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public (bool ok, string error) DeleteMany(IEnumerable<string> paths, bool permanent = false)
    {
        int ok = 0, fail = 0;
        foreach (var p in paths)
        {
            var (success, _) = Delete(p, permanent);
            if (success) ok++; else fail++;
        }
        return (true, fail > 0 ? $"{fail} item(s) failed" : "");
    }

    public (bool ok, string error) Copy(string sourcePath, string destDir)
    {
        try
        {
            var name   = Path.GetFileName(sourcePath);
            var target = Path.Combine(destDir, name);
            target = GetUniqueTarget(target);

            if (Directory.Exists(sourcePath))
                CopyDirectory(sourcePath, target);
            else if (File.Exists(sourcePath))
                File.Copy(sourcePath, target);
            else
                return (false, "Source not found");

            return (true, "");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public (bool ok, string error) Move(string sourcePath, string destDir)
    {
        try
        {
            var name   = Path.GetFileName(sourcePath);
            var target = Path.Combine(destDir, name);
            target = GetUniqueTarget(target);

            if (Directory.Exists(sourcePath))
                Directory.Move(sourcePath, target);
            else if (File.Exists(sourcePath))
                File.Move(sourcePath, target);
            else
                return (false, "Source not found");

            return (true, "");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public FileProperties? GetProperties(string fullPath)
    {
        try
        {
            if (Directory.Exists(fullPath))
            {
                var di = new DirectoryInfo(fullPath);
                long size = 0;
                int count = 0;
                try
                {
                    var files = di.EnumerateFiles("*", SearchOption.AllDirectories);
                    foreach (var f in files) { try { size += f.Length; count++; } catch { } }
                }
                catch { }

                return new FileProperties
                {
                    Name        = di.Name,
                    FullPath    = di.FullName,
                    IsDirectory = true,
                    SizeBytes   = size,
                    SizeDisplay = FormatSize(size),
                    Created     = di.CreationTime,
                    Modified    = di.LastWriteTime,
                    Accessed    = di.LastAccessTime,
                    TypeLabel   = "Folder",
                    Attributes  = AttributeString(di.Attributes),
                    ItemCount   = count,
                };
            }
            else if (File.Exists(fullPath))
            {
                var fi  = new FileInfo(fullPath);
                var ext = fi.Extension.TrimStart('.').ToLower();
                return new FileProperties
                {
                    Name        = fi.Name,
                    FullPath    = fi.FullName,
                    SizeBytes   = fi.Length,
                    SizeDisplay = FormatSize(fi.Length),
                    Created     = fi.CreationTime,
                    Modified    = fi.LastWriteTime,
                    Accessed    = fi.LastAccessTime,
                    TypeLabel   = TypeLabel(ext),
                    Attributes  = AttributeString(fi.Attributes),
                };
            }
            return null;
        }
        catch { return null; }
    }

    // ── Drives ────────────────────────────────────────────────────────────────

    private static List<DriveInfo2> GetDrives()
    {
        var result = new List<DriveInfo2>();
        foreach (var d in System.IO.DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady) continue;
                long used = d.TotalSize - d.AvailableFreeSpace;
                result.Add(new DriveInfo2
                {
                    Name        = d.Name.TrimEnd('\\'),
                    Label       = d.VolumeLabel,
                    DriveType   = d.DriveType.ToString(),
                    TotalBytes  = d.TotalSize,
                    FreeBytes   = d.AvailableFreeSpace,
                    TotalDisplay= FormatSize(d.TotalSize),
                    FreeDisplay = FormatSize(d.AvailableFreeSpace),
                    UsedPercent = d.TotalSize > 0 ? (int)(used * 100 / d.TotalSize) : 0,
                    IconKey     = d.DriveType == System.IO.DriveType.CDRom ? "cdrom"
                                : d.DriveType == System.IO.DriveType.Network ? "network"
                                : d.DriveType == System.IO.DriveType.Removable ? "usb"
                                : "harddisk",
                });
            }
            catch { }
        }
        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<FsEntry> SortEntries(List<FsEntry> entries, string sort, string dir)
    {
        // Always: dirs first
        var dirs  = entries.Where(e => e.IsDirectory).ToList();
        var files = entries.Where(e => !e.IsDirectory).ToList();

        IEnumerable<FsEntry> SortGroup(IEnumerable<FsEntry> g) => sort switch
        {
            "size"    => dir == "asc" ? g.OrderBy(e => e.SizeBytes)   : g.OrderByDescending(e => e.SizeBytes),
            "modified"=> dir == "asc" ? g.OrderBy(e => e.Modified)    : g.OrderByDescending(e => e.Modified),
            "type"    => dir == "asc" ? g.OrderBy(e => e.TypeLabel)   : g.OrderByDescending(e => e.TypeLabel),
            _         => dir == "asc" ? g.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                                      : g.OrderByDescending(e => e.Name, StringComparer.OrdinalIgnoreCase),
        };

        return SortGroup(dirs).Concat(SortGroup(files)).ToList();
    }

    private static List<Breadcrumb> BuildBreadcrumbs(string path)
    {
        var crumbs = new List<Breadcrumb>
        {
            new() { Name = "This PC", Path = "" }
        };
        if (string.IsNullOrEmpty(path)) return crumbs;

        // Windows: split by drive + folder segments
        var parts = path.Replace('/', '\\').TrimEnd('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        string accumulated = "";
        foreach (var p in parts)
        {
            accumulated = accumulated.Length == 0 ? p + "\\" : Path.Combine(accumulated, p);
            crumbs.Add(new() { Name = p, Path = accumulated });
        }
        return crumbs;
    }

    private static void SendToRecycleBin(string path, bool isDir)
    {
        // SHFileOperation — move to recycle bin
        var op = new Shell32.SHFILEOPSTRUCT
        {
            wFunc  = Shell32.FO_DELETE,
            pFrom  = path + "\0\0",
            fFlags = Shell32.FOF_ALLOWUNDO | Shell32.FOF_NOCONFIRMATION | Shell32.FOF_SILENT | Shell32.FOF_NOERRORUI,
        };
        Shell32.SHFileOperation(ref op);
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(src))
            CopyDirectory(d, Path.Combine(dest, Path.GetFileName(d)));
    }

    private static string GetUniqueTarget(string target)
    {
        if (!File.Exists(target) && !Directory.Exists(target)) return target;
        var dir  = Path.GetDirectoryName(target)!;
        var name = Path.GetFileNameWithoutExtension(target);
        var ext  = Path.GetExtension(target);
        for (int i = 2; i < 100; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
        return target;
    }

    private static bool IsValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var invalid = Path.GetInvalidFileNameChars();
        return !name.Any(c => invalid.Contains(c));
    }

    private static string AttributeString(FileAttributes attr)
    {
        var parts = new List<string>();
        if (attr.HasFlag(FileAttributes.ReadOnly)) parts.Add("Read-only");
        if (attr.HasFlag(FileAttributes.Hidden))   parts.Add("Hidden");
        if (attr.HasFlag(FileAttributes.System))   parts.Add("System");
        if (attr.HasFlag(FileAttributes.Archive))  parts.Add("Archive");
        return string.Join(", ", parts);
    }

    public static string FormatSize(long bytes)
    {
        if (bytes <= 0)      return "0 B";
        if (bytes < 1024)    return $"{bytes} B";
        if (bytes < 1024*1024)          return $"{bytes/1024.0:F1} KB";
        if (bytes < 1024L*1024*1024)    return $"{bytes/(1024.0*1024):F1} MB";
        return $"{bytes/(1024.0*1024*1024):F2} GB";
    }

    private static string FolderIconKey(string name)
    {
        var n = name.ToLower();
        if (n is "desktop")           return "folder-desktop";
        if (n is "documents" or "my documents") return "folder-docs";
        if (n is "downloads")         return "folder-downloads";
        if (n is "pictures" or "my pictures")   return "folder-pics";
        if (n is "music" or "my music")         return "folder-music";
        if (n is "videos" or "my videos")       return "folder-video";
        if (n.Contains("git") || n.Contains(".github")) return "folder-git";
        if (n.Contains("node_modules"))         return "folder-node";
        if (n is ".vs" or "bin" or "obj" or "build" or "dist" or "out") return "folder-build";
        return "folder";
    }

    private static string FileIconKey(string ext) => ext switch
    {
        "pdf"  => "pdf",
        "doc" or "docx" => "word",
        "xls" or "xlsx" => "excel",
        "ppt" or "pptx" => "ppt",
        "zip" or "rar" or "7z" or "gz" or "tar" or "bz2" => "archive",
        "png" or "jpg" or "jpeg" or "gif" or "bmp" or "webp" or "svg" or "ico" or "tiff" => "image",
        "mp4" or "mkv" or "avi" or "mov" or "wmv" or "flv" or "webm" or "m4v" => "video",
        "mp3" or "wav" or "flac" or "aac" or "ogg" or "wma" or "m4a" => "audio",
        "txt" or "md" or "log" or "ini" or "cfg" or "conf" or "toml" or "yaml" or "yml" => "text",
        "cs"  => "csharp",
        "js" or "mjs" or "cjs" => "js",
        "ts"  => "ts",
        "py"  => "python",
        "java"=> "java",
        "cpp" or "cc" or "cxx" => "cpp",
        "c"   => "c",
        "h" or "hpp" => "header",
        "html" or "htm" => "html",
        "css" => "css",
        "json"=> "json",
        "xml" => "xml",
        "sql" => "sql",
        "sh" or "bash" => "shell",
        "bat" or "cmd" => "bat",
        "ps1" => "powershell",
        "exe" or "msi" => "exe",
        "dll" => "dll",
        "iso" or "img" => "disk",
        "ttf" or "otf" or "woff" or "woff2" => "font",
        _  => "file",
    };

    private static string TypeLabel(string ext) => ext switch
    {
        "pdf"  => "PDF Document",
        "doc"  => "Word Document", "docx" => "Word Document",
        "xls"  => "Excel Spreadsheet", "xlsx" => "Excel Spreadsheet",
        "ppt"  => "PowerPoint", "pptx" => "PowerPoint",
        "zip"  => "ZIP Archive", "rar" => "RAR Archive", "7z" => "7-Zip Archive",
        "gz" or "tar" => "Archive",
        "png"  => "PNG Image", "jpg" or "jpeg" => "JPEG Image",
        "gif"  => "GIF Image", "bmp" => "Bitmap", "webp" => "WebP Image", "svg" => "SVG Image",
        "mp4"  => "MP4 Video", "mkv" => "MKV Video", "avi" => "AVI Video", "mov" => "MOV Video",
        "mp3"  => "MP3 Audio", "wav" => "WAV Audio", "flac" => "FLAC Audio",
        "txt"  => "Text File", "md" => "Markdown File", "log" => "Log File",
        "cs"   => "C# Source", "js" => "JavaScript", "ts" => "TypeScript",
        "py"   => "Python Script", "java" => "Java Source",
        "html" or "htm" => "HTML File", "css" => "CSS File",
        "json" => "JSON File", "xml" => "XML File", "sql" => "SQL File",
        "sh"   => "Shell Script", "bat" or "cmd" => "Batch File", "ps1" => "PowerShell Script",
        "exe"  => "Application", "msi" => "Installer", "dll" => "DLL Library",
        ""     => "File",
        _      => ext.ToUpper() + " File",
    };
}

// ── Shell32 P/Invoke (for recycle bin delete) ─────────────────────────────────
internal static class Shell32
{
    public const uint   FO_DELETE           = 0x0003;
    public const ushort FOF_ALLOWUNDO       = 0x0040;
    public const ushort FOF_NOCONFIRMATION  = 0x0010;
    public const ushort FOF_SILENT          = 0x0004;
    public const ushort FOF_NOERRORUI       = 0x0400;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    public struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint   wFunc;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] public string pFrom;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [System.Runtime.InteropServices.DllImport("Shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    public static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);
}
