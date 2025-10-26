using Domain.Entities.Items;

namespace Domain.Entities.Categories;

public class Category
{
    private Category()
    {
    }

    public Category(Guid id, string name, string slug)
    {
        Id = id;
        Name = name;
        Slug = slug;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;

    public ICollection<Item> Items { get; } = new List<Item>();
}
