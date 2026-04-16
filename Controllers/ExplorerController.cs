using ExplorerWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace ExplorerWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExplorerController : ControllerBase
{
    private readonly FileSystemService _fs;
    public ExplorerController(FileSystemService fs) => _fs = fs;

    // GET /api/explorer/list?path=C:\Users&sort=name&dir=asc&hidden=true
    [HttpGet("list")]
    public IActionResult List(
        [FromQuery] string  path   = "",
        [FromQuery] string  sort   = "name",
        [FromQuery] string  dir    = "asc",
        [FromQuery] bool    hidden = true)
    {
        var listing = _fs.List(path, hidden, sort, dir);
        return Ok(listing);
    }

    // GET /api/explorer/search?path=C:\Users&q=report&recursive=true
    [HttpGet("search")]
    public IActionResult Search(
        [FromQuery] string path,
        [FromQuery] string q,
        [FromQuery] bool   recursive = true)
    {
        var results = _fs.Search(path, q, recursive);
        return Ok(results);
    }

    // GET /api/explorer/properties?path=C:\Users\file.txt
    [HttpGet("properties")]
    public IActionResult Properties([FromQuery] string path)
    {
        var props = _fs.GetProperties(path);
        return props == null ? NotFound() : Ok(props);
    }

    // POST /api/explorer/newfolder   { "parentPath": "C:\\Users", "name": "New Folder" }
    [HttpPost("newfolder")]
    public IActionResult NewFolder([FromBody] NewFolderRequest req)
    {
        var (ok, err) = _fs.CreateFolder(req.ParentPath, req.Name);
        return ok ? Ok(new { message = $"Folder '{req.Name}' created" })
                  : BadRequest(new { error = err });
    }

    // POST /api/explorer/rename   { "fullPath": "...", "newName": "..." }
    [HttpPost("rename")]
    public IActionResult Rename([FromBody] RenameRequest req)
    {
        var (ok, err) = _fs.Rename(req.FullPath, req.NewName);
        return ok ? Ok(new { message = "Renamed" }) : BadRequest(new { error = err });
    }

    // POST /api/explorer/delete   { "paths": ["..."], "permanent": false }
    [HttpPost("delete")]
    public IActionResult Delete([FromBody] DeleteRequest req)
    {
        var (ok, err) = _fs.DeleteMany(req.Paths, req.Permanent);
        return ok ? Ok(new { message = "Deleted", warning = err })
                  : BadRequest(new { error = err });
    }

    // POST /api/explorer/copy   { "sourcePaths": ["..."], "destDir": "..." }
    [HttpPost("copy")]
    public IActionResult Copy([FromBody] CopyMoveRequest req)
    {
        int ok = 0, fail = 0;
        foreach (var src in req.SourcePaths)
        {
            var (success, _) = _fs.Copy(src, req.DestDir);
            if (success) ok++; else fail++;
        }
        return Ok(new { copied = ok, failed = fail });
    }

    // POST /api/explorer/move   { "sourcePaths": ["..."], "destDir": "..." }
    [HttpPost("move")]
    public IActionResult Move([FromBody] CopyMoveRequest req)
    {
        int ok = 0, fail = 0;
        foreach (var src in req.SourcePaths)
        {
            var (success, _) = _fs.Move(src, req.DestDir);
            if (success) ok++; else fail++;
        }
        return Ok(new { moved = ok, failed = fail });
    }

    // GET /api/explorer/download?path=C:\file.txt
    [HttpGet("download")]
    public IActionResult Download([FromQuery] string path)
    {
        if (!System.IO.File.Exists(path)) return NotFound();
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(path, out var contentType))
            contentType = "application/octet-stream";
        var bytes = System.IO.File.ReadAllBytes(path);
        return File(bytes, contentType, System.IO.Path.GetFileName(path));
    }

    // GET /api/explorer/preview?path=C:\image.png  (images/text only)
    [HttpGet("preview")]
    public IActionResult Preview([FromQuery] string path)
    {
        if (!System.IO.File.Exists(path)) return NotFound();
        var ext = System.IO.Path.GetExtension(path).TrimStart('.').ToLower();
        var textExts = new[]{"txt","md","log","json","xml","csv","html","htm","css","js","ts","cs","py","sh","bat","ps1","yaml","yml","toml","ini","cfg","conf","sql"};
        var imageExts = new[]{"png","jpg","jpeg","gif","bmp","webp","svg","ico"};

        if (textExts.Contains(ext))
        {
            try
            {
                var content = System.IO.File.ReadAllText(path);
                if (content.Length > 50000) content = content[..50000] + "\n... [truncated]";
                return Ok(new { type = "text", content });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        if (imageExts.Contains(ext))
        {
            var provider = new FileExtensionContentTypeProvider();
            provider.TryGetContentType(path, out var ct);
            ct ??= "image/png";
            try
            {
                var bytes = System.IO.File.ReadAllBytes(path);
                var b64 = Convert.ToBase64String(bytes);
                return Ok(new { type = "image", content = $"data:{ct};base64,{b64}" });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        return Ok(new { type = "unsupported" });
    }
}

public record NewFolderRequest(string ParentPath, string Name);
public record RenameRequest(string FullPath, string NewName);
public record DeleteRequest(List<string> Paths, bool Permanent);
public record CopyMoveRequest(List<string> SourcePaths, string DestDir);
