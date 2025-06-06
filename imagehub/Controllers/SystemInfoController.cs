#region

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

#endregion

namespace imagehub.Controllers;

[ApiController]
[Route("[controller]")]
public class SystemInfoController : ControllerBase
{
    /// <summary>
    ///     Vrátí JSON s informacemi o paměti procesu a průměrném CPU vytížení od startu.
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        // Získáme informace o aktuálním procesu
        var process = Process.GetCurrentProcess();

        // Uptime procesu (jak dlouho běží)
        var startTimeUtc = process.StartTime.ToUniversalTime();
        var uptime = DateTime.UtcNow.ToUniversalTime() - startTimeUtc;

        // Celkový čas strávený procesorem
        var totalCpuTime = process.TotalProcessorTime;

        // Výpočet průměrného CPU vytížení od startu (v %)
        // vzorec: (celkový CPU čas / upTime) / počet jader * 100
        double cpuUsage = 0;
        if (uptime.TotalMilliseconds > 0)
            cpuUsage = totalCpuTime.TotalMilliseconds / uptime.TotalMilliseconds
                                                      / Environment.ProcessorCount
                       * 100.0;

        // Paměťové statistiky procesu
        var workingSet = process.WorkingSet64; // fyzická paměť (RSS) v bajtech
        var privateBytes = process.PrivateMemorySize64; // soukromé bajty (Private Bytes)
        var gcTotalMemory = GC.GetTotalMemory(false); // GC heap

        // Sestavíme anonymní objekt pro serializaci do JSON
        var info = new
        {
            Process = new
            {
                WorkingSetBytes = workingSet,
                WorkingSetMegabytes = workingSet / (1024 * 1024),
                PrivateMemoryBytes = privateBytes,
                PrivateMemoryMegabytes = privateBytes / (1024 * 1024),
                GCTotalMemoryBytes = gcTotalMemory,
                GCTotalMemoryMegabytes = gcTotalMemory / (1024 * 1024),
                process.StartTime, // lokální čas startu procesu
                UpTime = new
                {
                    uptime.Days,
                    uptime.Hours,
                    uptime.Minutes,
                    uptime.Seconds
                }
            },
            CPU = new
            {
                TotalCpuTime = totalCpuTime, // TimeSpan
                Environment.ProcessorCount,
                AverageCpuUsagePercent = Math.Round(cpuUsage, 2)
            },
            Timestamp = DateTime.UtcNow
        };

        return Ok(info);
    }
}
