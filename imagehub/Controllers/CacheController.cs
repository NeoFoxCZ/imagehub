#region

using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

#endregion

namespace imagehub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CacheController(ILogger<CacheController> logger, IMemoryCache cache) : ControllerBase
{
    [HttpGet]
    public IActionResult GetCache()
    {
        // get all cache items and images
        var cacheData = cache;
        return Ok(cacheData);
    }

    [HttpDelete]
    public IActionResult ClearCache()
    {
        // clear all cache items and images
        var cacheEntriesCollection =
            cache.GetType()
                .GetProperty("EntriesCollection", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(cache) as IDictionary;

        if (cacheEntriesCollection != null)
        {
            var keysToRemove = new List<object>();
            foreach (DictionaryEntry entry in cacheEntriesCollection) keysToRemove.Add(entry.Key);

            foreach (var key in keysToRemove) cache.Remove(key);
        }

        logger.LogInformation("Cache cleared");
        return Ok("Cache cleared");
    }
}
