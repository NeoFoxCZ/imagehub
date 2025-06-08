#region

using imagehub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

#endregion

namespace imagehub.Controllers;

[ApiController]
[Route("img")]
public class ServingController(
    ILogger<UploadController> logger,
    IMemoryCache cache,
    CacheService cacheService)
    : ControllerBase
{
    // Catch-all: do parametru id se bude mapovat např. "scooter/250/babetta-classic-50"
    [HttpGet("{*id}")]
    public async Task<IActionResult> GetImageDisk(string id)
    {
        const string cacheKey = Const.CacheMapKey;
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest("Nebyl zadán žádný název obrázku.");

        // read the rewrites from the cache
        if (!cache.TryGetValue(cacheKey, out Dictionary<string, string>? imagePathMap))
        {
            // pokud není v cache, načteme znovu
            await cacheService.CacheRewriteRoutes();
            logger.LogWarning("Image path map not found in cache, reloading from file, call again please.");
        }

        // Pokud je v mapě, přepíšeme cestu
        if (imagePathMap != null && imagePathMap.TryGetValue(id, out var imagePath)) id = imagePath;


        // Pokud id neobsahuje příponu, přidáme ".jpg"
        var fileName = id;
        if (Path.GetExtension(fileName) == string.Empty)
        {
            // zjistime jestli obrazek už existuje jako .webp
            var existsWebp = System.IO.File.Exists(Path.Combine("images", $"{fileName}.webp"));

            // pokud existuje jako .webp, přepíšeme název souboru
            if (existsWebp)
                fileName += ".webp";
            else
                fileName += ".jpg";
        }

        // Složka wwwroot/images/...
        var wwwRoot = "images/";
        var filePath = Path.Combine(wwwRoot, fileName);

        if (!System.IO.File.Exists(filePath))
        {
            logger.LogWarning("Obrázek nenalezen: {FilePath}", filePath);
            // vratime obrazek nenalezeno.webp /images/nenalezeno.webp
            filePath = Path.Combine(wwwRoot, "nenalezeno.webp");
        }

        byte[] fileBytes;
        try
        {
            fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            if (fileBytes.Length == 0)
            {
                logger.LogError("Obrázek je prázdný: {FilePath}", filePath);
                return NotFound();
            }
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Chyba čtení souboru {FilePath}", filePath);
            return NotFound();
        }

        logger.LogDebug("Obrázek {FileName} úspěšně načten z disku.", fileName);
        var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = fileExtension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
        return File(fileBytes, contentType);
    }
}
