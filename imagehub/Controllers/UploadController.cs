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
    CacheService cacheService,
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
        return await cacheService.CacheRewriteRoutes();
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
