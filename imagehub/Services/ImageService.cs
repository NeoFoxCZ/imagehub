#region

using imagehub.Models;
using imagehub.tables;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

#endregion

namespace imagehub.Services;

public class ImageService(ILogger<ImageService> logger, MyContext db)
{
    public async Task<Guid> UploadImageAsync(IFormFile file, string folder)
    {
        var id = Guid.NewGuid();
        var ex = Path.GetExtension(file.FileName);
        var path = $"images/{folder}/{id}{ex}";

        // Create directory if it doesn't exist
        if (!Directory.Exists("images"))
            Directory.CreateDirectory("images");

        // Create folder if it doesn't exist
        if (!Directory.Exists($"images/{folder}"))
            Directory.CreateDirectory($"images/{folder}");


        // Save the file
        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);

        // does exist
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.FileName);
        var exists = await db.Images.FirstOrDefaultAsync(x => x.Name == fileNameWithoutExtension);
        if (exists != null)
        {
            // remove previous image if exists on disk
            if (File.Exists(exists.Path))
            {
                File.Delete(exists.Path);
                logger.LogInformation("Image {Id} deleted from disk", exists.Id);
            }

            logger.LogWarning("Image {Id} already exists in database", id);
            exists.Path = path;
            exists.Folder = folder;
            exists.UpdatedAt = DateTime.UtcNow;
            db.Images.Update(exists);
            await db.SaveChangesAsync();
            logger.LogInformation("Image {Id} updated in database", id);
            return id;
        }

        var image = new Images
        {
            Id = id,
            Name = fileNameWithoutExtension,
            Alt = file.FileName,
            Type = file.ContentType,
            Path = path,
            Extension = ex,
            Folder = folder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Images.Add(image);
        await db.SaveChangesAsync();
        logger.LogInformation("Image {Id} uploaded to {Path}", id, path);
        return id;
    }

    public async Task<byte[]> GetImageAsync(string id, string? size = null, int? w = null, int? h = null,
        ResizeMode resizeMode = ResizeMode.Max, string folder = "product")
    {
        // Get from db by Name
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(id);
        var extension = Path.GetExtension(id);
        var imageDb = await db.Images.FirstOrDefaultAsync(x => x.Name == nameWithoutExtension);

        if (imageDb == null)
            throw new FileNotFoundException($"Image {id} not found in database");

        using var image = await Image.LoadAsync(imageDb.Path);

        // Pokud je zadána předdefinovaná velikost
        if (!string.IsNullOrEmpty(size))
        {
            if (size == "small")
                image.Mutate(x => x.Resize(200, 0));
            else if (size == "medium")
                image.Mutate(x => x.Resize(800, 0));
            else if (size == "clean")
                image.Metadata.ExifProfile = null;
        }
        // Pokud je zadána vlastní velikost
        else if (w.HasValue || h.HasValue)
        {
            var resizeOptions = new ResizeOptions
            {
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center,
                Size = new Size(w ?? 0, h ?? 0)
            };
            image.Mutate(x => x.Resize(resizeOptions));
        }

        using var ms = new MemoryStream();
        switch (extension)
        {
            case ".webp":
                await image.SaveAsWebpAsync(ms, new WebpEncoder { Quality = 80 });
                break;
            case ".png":
                await image.SaveAsPngAsync(ms,
                    new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression });
                break;
            case ".jpg":
            case ".jpeg":
                await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 80 });
                break;
            case ".gif":
                await image.SaveAsGifAsync(ms);
                break;
            default:
                await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 80 });
                break;
        }

        return ms.ToArray();
    }
    
    /// <summary>
    ///     Method will return structured all images in folder and folders
    /// </summary>
    public async Task<List<StructureModel>> GetImagesStructureAsync(string? folder)
    {
        var folderPath = Path.Combine("images", folder ?? "");
        var structure = new List<StructureModel>();

        // 1. Přidej složky jako StructureModelFolder
        if (Directory.Exists(folderPath))
        {
            var directories = Directory.GetDirectories(folderPath);
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                //var children = await GetImagesStructureAsync(
                //    string.IsNullOrEmpty(folder) ? dirName : Path.Combine(folder!, dirName)
                //);

                structure.Add(new StructureModel
                {
                    Name = dirName,
                    Type = "folder",
                    //Children = children
                });
            }
        }

        // 2. Načti obrázky z DB
        var images = await db.Images
            .Where(x => x.Folder == folder)
            .ToListAsync();

        foreach (var image in images)
        {
            structure.Add(new StructureModel
            {
                Id = image.Id,
                Name = image.Name,
                Type= "image",
                Path = image.Path,
                Alt = image.Alt,
                Extension = image.Extension,
                //Size = image.Size
            });
        }

        return structure;
    }
    
}
