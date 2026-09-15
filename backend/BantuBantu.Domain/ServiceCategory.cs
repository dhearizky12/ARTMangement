namespace BantuBantu.Domain;

public class ServiceCategory
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconKey { get; set; } = "briefcase";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
}
