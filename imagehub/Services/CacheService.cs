#region

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

#endregion

namespace imagehub.Services;

/// <summary>
///     Služba pro správu cache přepisů cest k obrázkům.
/// </summary>
/// <param name="logger">
///     Logovací služba pro záznam událostí.
/// </param>
/// <param name="cache">
///     Paměťová cache pro ukládání přepisů cest k obrázkům.
/// </param>
/// <param name="env">
///     Prostředí webového hostitele pro získání cesty k souborům.
/// </param>
public class CacheService(ILogger<CacheService> logger, IMemoryCache cache, IWebHostEnvironment env)
{
    /// <summary>
    ///     Vytvoří přepisování cest k obrázkům z konfiguračního souboru a uloží je do cache.
    /// </summary>
    public async Task<IActionResult> CacheRewriteRoutes()
    {
        # region Získání přepisů cest k obrázkům z konfiguračního souboru

        logger.LogInformation("Získávám přepisování cest k obrázkům z konfiguračního souboru.");
        var rewritesPath = Path.Combine(env.ContentRootPath, "conf", "rewrites.conf"); // Cesta k souboru s přepisy    

        // Zkontrolujeme, zda soubor existuje
        if (!File.Exists(rewritesPath))
        {
            logger.LogError("Přepisový soubor rewrites.conf nebyl nalezen: {Path}", rewritesPath);
            return new NotFoundObjectResult("Rewrites file not found.");
        }

        // Načteme soubor a vytvoříme mapu
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = await File.ReadAllLinesAsync(rewritesPath);

        # endregion

        # region Přepisování cest k obrázkům, protože formát byl původně pro nginx

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

        #endregion

        #region Uložení do cache

        // Uložíme do cache s vlastními parametry (např. expirační doba)
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromHours(2)); // cache se obnoví, když je položka přistupována

        // Pokud již existuje v cache, přepíšeme ji a uložíme novou verzi + dumpneme nepotřebnout paměť
        if (cache.TryGetValue(Const.CacheMapKey, out _))
        {
            logger.LogInformation("Přepisujeme existující cache pro klíč {CacheKey}.", Const.CacheMapKey);
            cache.Remove(Const.CacheMapKey);
            // Uvolníme paměť ručně, aby se uvolnily objekty, které již nejsou potřeba
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        cache.Set(Const.CacheMapKey, map, cacheOptions);
        cache.Set(Const.CacheMapStatisticsKey, map.Count);
        logger.LogInformation("Mapa načtena ze souboru a uložena do cache ({Count} záznamů).", map.Count);
        return new OkObjectResult($"Mapa přepisů uložena. Celkem záznamů: {map.Count}");

        #endregion
    }

    /// <summary>
    ///     Získá přepisování cest k obrázkům z cache.
    /// </summary>
    /// <param name="key">
    ///     Klíč, pro který chceme získat přepis cesty k obrázku.
    /// </param>
    public Dictionary<string, string> GetCacheRewrites(string key)
    {
        if (cache.TryGetValue(Const.CacheMapKey, out Dictionary<string, string>? imagePathMap))
        {
            // Pokud je v mapě, přepíšeme cestu
            if (imagePathMap != null && imagePathMap.TryGetValue(key, out var imagePath))
                return new Dictionary<string, string> { { key, imagePath } };

            logger.LogWarning("Klíč {Key} nenalezen v cache.", key);
            return new Dictionary<string, string>();
        }

        logger.LogCritical("Cache pro přepisování cest k obrázkům není inicializována.");
        return new Dictionary<string, string>();
    }
}
