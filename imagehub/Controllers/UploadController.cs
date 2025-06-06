#region

using imagehub.Services;
using imagehub.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Formats.Webp;

#endregion

namespace imagehub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController(
    ILogger<UploadController> logger,
    ImageService imageService,
    IMemoryCache cache,
    IWebHostEnvironment env,
    IOptions<CacheSettings> cacheSettings)
    : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> UploadImage(IFormFile file, string folder = "product")
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

        try
        {
            var imageUrl = await imageService.UploadImageAsync(file, folder);
            return Ok(new { Url = imageUrl });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading image");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("multiple")]
    public async Task<IActionResult> UploadImages(IFormFileCollection files, string folder = "product")
    {
        if (files == null || files.Count == 0) return BadRequest("No files uploaded.");

        try
        {
            var imageUrls = new List<string>();
            foreach (var file in files)
            {
                var imageUrl = await imageService.UploadImageAsync(file, folder);
                imageUrls.Add(imageUrl.ToString());
            }

            return Ok(new { Urls = imageUrls });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading images");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("cache-rewrites")]
    public async Task<IActionResult> CacheRewrites()
    {
        const string cacheKey = "ImagePathMap";
        // get folder path conf/rewrites.conf
        var rewritesPath = Path.Combine(env.ContentRootPath, "conf", "rewrites.conf");
        
        // reead the file and save it to the cache
        // rows are in this format: "/img/article/250/baotian-a-sada-zrcatek-p-p-zavit-m8 /img/article/250/LM0037.jpg;"
        // SAVE as dictionary to cache, splited by blank with key as the first part and value as the second part
        // /img/article/250/baotian-a-sada-zrcatek-p-p-zavit-m8  is key
        // /img/article/250/LM0037.jpg; is value
        
        if (!System.IO.File.Exists(rewritesPath))
        {
            logger.LogError("Rewrites file not found: {RewritesPath}", rewritesPath);
            return NotFound("Rewrites file not found.");
        }
        
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        var lines = await System.IO.File.ReadAllLinesAsync(rewritesPath);
        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            // Rozdělíme podle mezery (jen na první výskyt), aby hodnota mohla obsahovat teoreticky další mezery
            // Ale dle vašeho příkladu stačí split(' ', 2)
            // odebere všude "/img/"
            var parts = rawLine.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                // špatně formátovaný řádek, můžeme jen logovat a pokračovat
                logger.LogWarning("Řádek má neočekávaný formát: {Line}", rawLine);
                continue;
            }

            var key = parts[0].Replace("/img/", string.Empty).Trim();
            var value = parts[1].Replace("/img/", string.Empty).Trim();

            // Hodnota z vašeho příkladu končí středníkem – můžete ho remove, pokud nechcete, aby zůstal ve výsledku:
            if (value.EndsWith(";"))
                value = value.Substring(0, value.Length - 1);

            // Přidáme do dictionary (přepsat existující, pokud náhodou duplicitní klíč)
            map[key] = value;
        }

        // Uložíme do cache s vlastními parametry (např. expirační doba)
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromHours(2)); // cache se obnoví, když je položka přistupována

        // Pokud již existuje v cache, přepíšeme ji a uložíme novou verzi + dumpneme nepotřebnout paměť
        if (cache.TryGetValue(cacheKey, out _))
        {
            logger.LogInformation("Přepisujeme existující cache pro klíč {CacheKey}.", cacheKey);
            cache.Remove(cacheKey);
            // Uvolníme paměť, pokud je potřeba
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        cache.Set(cacheKey, map, cacheOptions);
        logger.LogInformation("Mapa načtena ze souboru a uložena do cache ({Count} záznamů).", map.Count);

        return Ok("Mapa přepisů cest k obrázkům byla úspěšně načtena a uložena do cache." +
                   $" Celkem záznamů: {map.Count}");
    }
    
    // Catch-all: do parametru id se bude mapovat např. "scooter/250/babetta-classic-50"
    [HttpGet("new/{*id}")]
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

        return File(fileBytes, "image/jpeg");
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetImage(string id, string? size, int? width, int? height,
        ResizeMode resizeMode = ResizeMode.Max, string folder = "product")
    {
        // add cache
        var cacheKey = $"image_{id}_{size}_{width}_{height}";
        if (cacheSettings.Value.EnableCache && cache.TryGetValue(cacheKey, out byte[] cachedImage))
            return File(cachedImage, "image/jpeg");

        try
        {
            var extension = Path.GetExtension(id);
            var image = await imageService.GetImageAsync(id, size, width, height, resizeMode);
            var contentType = extension switch
            {
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".avif" => "image/avif",
                _ => "application/octet-stream"
            };
            // Cache the image in memory
            if (image == null)
            {
                logger.LogError("Image not found");
                return NotFound();
            }

            if (image.Length == 0)
            {
                logger.LogError("Image is empty");
                return NotFound();
            }

            if (cacheSettings.Value.EnableCache) cache.Set(cacheKey, image, TimeSpan.FromMinutes(120));
            return File(image, contentType);
        }
        catch (FileNotFoundException)
        {
            logger.LogError("Error retrieving image {Id}", id);
            return NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError("Error retrieving image {Id}", id);
            //logger.LogError(ex, "Error retrieving image");
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteImage(Guid id)
    {
        try
        {
            var path = $"images/{id}.jpg";
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
                return NoContent();
            }

            return NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting image");
            return StatusCode(500, "Internal server error");
        }
    }
    
    [HttpGet("structure")]
    public async Task<IActionResult> GetImageStructure(string? folder)
    {
        // Get the image structure from the database
        var imageStructure = await imageService.GetImagesStructureAsync(folder);
        return Ok(imageStructure);
    }
}
