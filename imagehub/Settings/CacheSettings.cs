namespace imagehub.Settings;

/// <summary>
///     Cache settings for the application.
/// </summary>
public class CacheSettings
{
    /// <summary>
    ///     Duration in seconds for how long the cache should be valid.
    /// </summary>
    public int CacheDuration { get; set; } = 60;

    /// <summary>
    ///     Maximum size of the cache in MB.
    /// </summary>
    public int CacheSize { get; set; } = 100;

    /// <summary>
    ///     Enable or disable caching for the application.
    /// </summary>
    public bool EnableCache { get; set; } = true;

    /// <summary>
    ///     Cache type to be used in the application. <see cref="CacheType" />
    /// </summary>
    public CacheType CacheType { get; set; } = CacheType.Memory;

    /// <summary>
    ///     Save the cache on disk.
    /// </summary>
    public bool SaveOnDisk { get; set; } = false;
}

/// <summary>
///     Cache types for the application.
/// </summary>
public enum CacheType
{
    /// <summary>
    ///     Memory cache type.
    /// </summary>
    Memory,

    /// <summary>
    ///     Redis cache type.
    /// </summary>
    Redis
}
