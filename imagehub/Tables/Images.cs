#region

using System.ComponentModel.DataAnnotations;

#endregion

namespace imagehub.tables;

public class Images
{
    [Key] public Guid Id { get; set; }

    public string Name { get; set; }
    public string Alt { get; set; }
    public string Type { get; set; }
    public string Path { get; set; }
    public string Folder { get; set; }
    public string Extension { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Size { get; set; }
}
