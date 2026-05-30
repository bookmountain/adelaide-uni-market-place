namespace Domain.Entities.Threads;

public class ThreadCategory
{
    private ThreadCategory() { }

    public ThreadCategory(Guid id, string slug, string name, string description, string iconKey, int sortOrder)
    {
        Id = id;
        Slug = slug;
        Name = name;
        Description = description;
        IconKey = iconKey;
        SortOrder = sortOrder;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string IconKey { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void Update(string name, string description, string iconKey, int sortOrder, bool isActive)
    {
        Name = name;
        Description = description;
        IconKey = iconKey;
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
