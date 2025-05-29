namespace imagehub.Settings;

public class ImageSettings
{
    public int MaxSize { get; set; } = 10; // in MB
    public string[] AllowedExtensions { get; set; } = new[] { ".jpg", ".jpeg", ".png", ".gif" };
    public string[] AllowedMimeTypes { get; set; } = new[] { "image/jpeg", "image/png", "image/gif" };
    public int MaxDiskUsage { get; set; } = 100; // in MB
}
