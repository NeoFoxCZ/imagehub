#region

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

#endregion

namespace imagehub.Services;

public class CacheService(ILogger<CacheService> logger, IMemoryCache cache, IWebHostEnvironment env)
{
    private const string cacheMapKey = "ImagePathMap";

    public async Task<IActionResult> CacheRewriteRoutes()
    {
        logger.LogInformation("Cache rewrite routes");
        var rewritesPath = Path.Combine(env.ContentRootPath, "conf", "rewrites.conf");

        // Zkontrolujeme, zda soubor existuje
        if (!File.Exists(rewritesPath))
        {
            logger.LogError("Rewrites file not found at {Path}", rewritesPath);
            return new NotFoundObjectResult("Rewrites file not found.");
        }

        // Načteme soubor a vytvoříme mapu přepisů
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = await File.ReadAllLinesAsync(rewritesPath);
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
        if (cache.TryGetValue(cacheMapKey, out _))
        {
            logger.LogInformation("Přepisujeme existující cache pro klíč {CacheKey}.", cacheMapKey);
            cache.Remove(cacheMapKey);
            // Uvolníme paměť, pokud je potřeba
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        cache.Set(cacheMapKey, map, cacheOptions);
        logger.LogInformation("Mapa načtena ze souboru a uložena do cache ({Count} záznamů).", map.Count);
        return new OkObjectResult($"Mapa přepisů uložena. Celkem záznamů: {map.Count}");
    }
}
