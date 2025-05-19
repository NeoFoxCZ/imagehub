namespace imagehub.Models;

public class StructureModel
{
    public string Name { get; set; }
    public string? Type { get; set; }
    
    public List<StructureModel>? Children { get; set; }
    
    public Guid? Id { get; set; }
    public string? Path { get; set; }
    public string? Alt { get; set; }
    public string? Extension { get; set; }
    public long? Size { get; set; }
}
