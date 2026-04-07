using Microsoft.AspNetCore.Mvc;
using RecycleBinWeb.Services;

namespace RecycleBinWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecycleBinController : ControllerBase
{
    private readonly RecycleBinService _svc;
    public RecycleBinController(RecycleBinService svc) => _svc = svc;

    // GET /api/recyclebin
    // Optional query: ?sort=name|size|date|type&dir=asc|desc&q=searchterm&drive=C:
    [HttpGet]
    public IActionResult GetAll(
        [FromQuery] string? sort  = "date",
        [FromQuery] string? dir   = "desc",
        [FromQuery] string? q     = null,
        [FromQuery] string? drive = null,
        [FromQuery] string? type  = null)
    {
        var items = _svc.GetItems();

        // Filter
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLowerInvariant();
            items = items.Where(i =>
                i.Name.Contains(ql, StringComparison.OrdinalIgnoreCase) ||
                i.OriginalPath.Contains(ql, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }
        if (!string.IsNullOrWhiteSpace(drive))
            items = items.Where(i => i.Drive.Equals(drive, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(type))
            items = items.Where(i => i.TypeIcon == type).ToList();

        // Sort
        bool asc = dir?.ToLower() == "asc";
        items = (sort?.ToLower()) switch
        {
            "name" => asc ? items.OrderBy(i => i.Name).ToList()        : items.OrderByDescending(i => i.Name).ToList(),
            "size" => asc ? items.OrderBy(i => i.SizeBytes).ToList()   : items.OrderByDescending(i => i.SizeBytes).ToList(),
            "type" => asc ? items.OrderBy(i => i.FileType).ToList()    : items.OrderByDescending(i => i.FileType).ToList(),
            _      => asc ? items.OrderBy(i => i.DeletedAt).ToList()   : items.OrderByDescending(i => i.DeletedAt).ToList(),
        };

        return Ok(items);
    }

    // GET /api/recyclebin/stats
    [HttpGet("stats")]
    public IActionResult GetStats() => Ok(_svc.GetStats());

    // POST /api/recyclebin/{id}/restore
    [HttpPost("{id}/restore")]
    public IActionResult Restore(string id)
    {
        var (ok, err) = _svc.Restore(id);
        return ok ? Ok(new { message = "Restored successfully" })
                  : BadRequest(new { error = err });
    }

    // DELETE /api/recyclebin/{id}
    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        var (ok, err) = _svc.Delete(id);
        return ok ? Ok(new { message = "Permanently deleted" })
                  : BadRequest(new { error = err });
    }

    // POST /api/recyclebin/bulk-restore   body: { "ids": ["a","b"] }
    [HttpPost("bulk-restore")]
    public IActionResult BulkRestore([FromBody] BulkRequest req)
    {
        var (ok, fail) = _svc.RestoreMany(req.Ids);
        return Ok(new { restored = ok, failed = fail });
    }

    // POST /api/recyclebin/bulk-delete    body: { "ids": ["a","b"] }
    [HttpPost("bulk-delete")]
    public IActionResult BulkDelete([FromBody] BulkRequest req)
    {
        var (ok, fail) = _svc.DeleteMany(req.Ids);
        return Ok(new { deleted = ok, failed = fail });
    }

    // DELETE /api/recyclebin   — empty entire bin
    [HttpDelete]
    public IActionResult EmptyAll()
    {
        var (ok, err) = _svc.EmptyAll();
        return ok ? Ok(new { message = "Recycle Bin emptied" })
                  : BadRequest(new { error = err });
    }
}

public record BulkRequest(List<string> Ids);
