using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PosTpv.Domain.Common;
using PosTpv.Domain.Entities;

namespace PosTpv.Infrastructure.Persistence;

/// <summary>
/// Moves catalogue images stored inline as <c>data:</c> URIs (what the seeders generate) out of
/// the database into files under wwwroot/uploads, and points ImageUrl at them instead — the same
/// kind of path an uploaded image already gets (see ImageUpload.razor). Inline images were sent
/// in full inside every page and every Blazor render diff; as files the browser downloads each one
/// once and caches it.
/// Runs on every startup after the seeders, so anything they just seeded is converted too. Only
/// the ImageUrl column is updated and nothing is deleted; rows already holding a path are skipped,
/// so it's a no-op once everything has been converted.
/// </summary>
public class ImageUrlExternalizer
{
    private const string ImageColumn = "ImageUrl";

    // Only image types a browser can show from <img>/CSS; anything else is left untouched.
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/svg+xml"] = ".svg",
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
    };

    private readonly PosDbContext _db;
    private readonly ILogger<ImageUrlExternalizer> _log;

    public ImageUrlExternalizer(PosDbContext db, ILogger<ImageUrlExternalizer> log)
    {
        _db = db;
        _log = log;
    }

    public async Task ExternalizeAsync(string webRootPath, CancellationToken ct = default)
    {
        // Folder names match the ones the Products page's ImageUpload pickers already use.
        var moved = await ExternalizeSetAsync<Product>(webRootPath, "products", ct)
                    + await ExternalizeSetAsync<Category>(webRootPath, "categories", ct)
                    + await ExternalizeSetAsync<Extra>(webRootPath, "extras", ct)
                    + await ExternalizeSetAsync<Allergen>(webRootPath, "allergens", ct);
        if (moved > 0) _log.LogInformation("Moved {Count} inline images to wwwroot/uploads.", moved);
    }

    private async Task<int> ExternalizeSetAsync<T>(string webRootPath, string folder, CancellationToken ct)
        where T : BaseEntity
    {
        var rows = await _db.Set<T>().AsNoTracking()
            .Where(x => EF.Property<string?>(x, ImageColumn) != null && EF.Property<string>(x, ImageColumn).StartsWith("data:"))
            .Select(x => new { x.Id, Url = EF.Property<string>(x, ImageColumn) })
            .ToListAsync(ct);
        if (rows.Count == 0) return 0;

        var dir = Path.Combine(webRootPath, "uploads", folder);
        var byPath = new Dictionary<string, List<int>>();
        foreach (var row in rows)
        {
            var path = TryWriteFile(row.Url, dir, folder);
            if (path is null) continue;
            if (!byPath.TryGetValue(path, out var ids)) byPath[path] = ids = new List<int>();
            ids.Add(row.Id);
        }

        var updated = 0;
        foreach (var (path, ids) in byPath)
        {
            // ExecuteUpdate: a storage-format change, not an edit, so no UpdatedAt stamping.
            updated += await _db.Set<T>().Where(x => ids.Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => EF.Property<string?>(x, ImageColumn), path), ct);
        }
        return updated;
    }

    /// <summary>
    /// Decodes a base64 data URI and writes it as /uploads/{folder}/{content hash}.{ext}; identical
    /// images (e.g. many products sharing a category placeholder) share one file. Returns the
    /// public path, or null if the URI isn't a supported base64 image or the file can't be written
    /// — that row then simply keeps its inline image.
    /// </summary>
    private string? TryWriteFile(string dataUri, string dir, string folder)
    {
        var comma = dataUri.IndexOf(',');
        var meta = comma > 5 ? dataUri[5..comma] : "";               // e.g. "image/svg+xml;base64"
        var parts = meta.Split(';');
        if (!parts.Contains("base64", StringComparer.OrdinalIgnoreCase) || !Extensions.TryGetValue(parts[0], out var ext))
        {
            _log.LogWarning("Skipping unsupported inline image ({Meta}).", meta);
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(dataUri[(comma + 1)..]);
            var name = Convert.ToHexString(SHA256.HashData(bytes))[..32].ToLowerInvariant() + ext;
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, name);
            if (!File.Exists(file)) File.WriteAllBytes(file, bytes);
            return $"/uploads/{folder}/{name}";
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            _log.LogWarning(ex, "Could not move an inline image to {Dir}; keeping it in the database.", dir);
            return null;
        }
    }
}
