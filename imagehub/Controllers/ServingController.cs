using imagehub.Services;
using imagehub.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace imagehub.Controllers;

[ApiController]
[Route("img")]
public class ServingController(
    ILogger<UploadController> logger,
    ImageService imageService,
    IMemoryCache cache,
    IWebHostEnvironment env,
    IOptions<CacheSettings> cacheSettings)
    : ControllerBase
{
    // Catch-all: do parametru id se bude mapovat např. "scooter/250/babetta-classic-50"
    [HttpGet("{*id}")]
    public async Task<IActionResult> GetImageDisk(string id)
    {
        const string cacheKey = "ImagePathMap";
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest("Nebyl zadán žádný název obrázku.");
        
        // read the rewrites from the cache
        if (!cache.TryGetValue(cacheKey, out Dictionary<string, string> imagePathMap))
        {
            // pokud není v cache, načteme znovu
            // imagePathMap = await CacheRewrites();
            logger.LogWarning("Image path map not found in cache, reloading from file.");
            return NotFound("Neni incializovana cache pro přepisování cest k obrázkům.");
        }
        // Pokud je v mapě, přepíšeme cestu
        if (imagePathMap.TryGetValue(id, out var imagePath))
        {
            id = imagePath;
        }
        

        // Pokud id neobsahuje příponu, přidáme ".jpg"
        var fileName = id;
        if (Path.GetExtension(fileName) == string.Empty)
        {
            fileName += ".jpg";
        }

        // Složka wwwroot/images/...
        var wwwRoot = $"images/";
        var filePath = Path.Combine(wwwRoot, fileName);

        if (!System.IO.File.Exists(filePath))
        {
            logger.LogWarning("Obrázek nenalezen: {FilePath}", filePath);
            return NotFound();
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

        logger.LogInformation("Obrázek {FileName} úspěšně načten z disku.", fileName);
        return File(fileBytes, "image/jpeg");
    }
}
