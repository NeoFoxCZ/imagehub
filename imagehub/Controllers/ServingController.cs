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
    CacheService cacheService,
    IWebHostEnvironment env,
    IOptions<CacheSettings> cacheSettings)
    : ControllerBase
{
    // Catch-all: do parametru id se bude mapovat např. "scooter/250/babetta-classic-50"
    [HttpGet("old/{*id}")]
    public async Task<IActionResult> GetImageDisk(string id)
    {
        const string cacheKey = "ImagePathMap";
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest("Nebyl zadán žádný název obrázku.");
        
        // read the rewrites from the cache
        if (!cache.TryGetValue(cacheKey, out Dictionary<string, string> imagePathMap))
        {
            // pokud není v cache, načteme znovu
            await cacheService.CacheRewriteRoutes();
            logger.LogWarning("Image path map not found in cache, reloading from file, call again please.");
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
            // Pokud jde o /schemas/ pak rovnou .webp contains
            if (fileName.StartsWith("schemas/"))
            {
                fileName += ".webp";
            }
            else
            {
                fileName += ".jpg";
            }
        }

        // Složka wwwroot/images/...
        var wwwRoot = $"images/";
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

        logger.LogInformation("Obrázek {FileName} úspěšně načten z disku.", fileName);
        return File(fileBytes, "image/jpeg");
    }
    
    [HttpGet("{*id}")]
    public async Task<IActionResult> GetImageDiskV2(string id)
    {
        const string cacheKey = "ImagePathMap";
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest("Nebyl zadán žádný název obrázku.");
        
        // read the rewrites from the cache
        if (!cache.TryGetValue(cacheKey, out Dictionary<string, string> imagePathMap))
        {
            // pokud není v cache, načteme znovu
            await cacheService.CacheRewriteRoutes();
            logger.LogWarning("Image path map not found in cache, reloading from file, call again please.");
        }
        // Pokud je v mapě, přepíšeme cestu
        if (imagePathMap.TryGetValue(id, out var imagePath))
        {
            id = imagePath;
        }
        
        var fileName = id;
        // Pokud fileName obahuje .jpg, pak ho odebereme
        if (fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName.Substring(0, fileName.Length - 4);
        }
        
        // zjistime jestli obrazek už existuje jako .webp
        var existsWebp = System.IO.File.Exists(Path.Combine("images", $"{fileName}.webp"));
        if (existsWebp)
        {
            // pokud existuje jako .webp, přepíšeme název souboru
            fileName += ".webp";
        }
        
        // Pokud id neobsahuje příponu, přidáme ".jpg"
        if (Path.GetExtension(fileName) == string.Empty)
        {
            // Pokud jde o /schemas/ pak rovnou .webp contains
            if (fileName.StartsWith("schemas/"))
            {
                fileName += ".webp";
            }
            else
            {
                fileName += ".jpg";
            }
        }

        // Složka wwwroot/images/...
        var wwwRoot = $"images/";
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

        logger.LogInformation("Obrázek {FileName} úspěšně načten z disku.", fileName);
        
        // pokud je obrázek načten jako webp, tak jej převedeme na image/webp
        //  await image.SaveAsWebpAsync(ms, new WebpEncoder { Quality = 80 });
        // convert image to .webp if is .jpg
        
        var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
        if (fileExtension == ".jpg" || fileExtension == ".jpeg")
        {
            // Convert to webp if jpg
            using var ms = new MemoryStream(fileBytes);
            using var image = await Image.LoadAsync(ms);
            ms.SetLength(0); // Clear the stream
            await image.SaveAsWebpAsync(ms, new SixLabors.ImageSharp.Formats.Webp.WebpEncoder { Quality = 80 });
            fileBytes = ms.ToArray();
            fileName = Path.ChangeExtension(fileName, ".webp");
            
            // save file to disk as webp
            var webpFilePath = Path.ChangeExtension(filePath, ".webp");
            await System.IO.File.WriteAllBytesAsync(webpFilePath, fileBytes);
            logger.LogInformation("Obrázek {FileName} byl převeden na WebP a uložen.", fileName);
        }
        else
        {
            logger.LogInformation("Obrázek {FileName} je již ve správném formátu: {FileExtension}.", fileName, fileExtension);
        }
        
        
        var contentType = "image/jpeg";
        if (fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            contentType = "image/webp";
        }
        else if (fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            contentType = "image/png";
        }
        else if (fileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            contentType = "image/gif";
        }
        
        return File(fileBytes, contentType);
    }
}
