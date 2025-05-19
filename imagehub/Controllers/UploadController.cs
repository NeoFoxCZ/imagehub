#region

using imagehub.Services;
using imagehub.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

#endregion

namespace imagehub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController(
    ILogger<UploadController> logger,
    ImageService imageService,
    IMemoryCache cache,
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
}
