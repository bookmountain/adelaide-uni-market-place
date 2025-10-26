using Domain.Entities.Categories;
using Domain.Entities.Users;
using Domain.Shared.Enums;

namespace Domain.Entities.Items;

public class Item
{
    private readonly List<ListingImage> _images = new();

    private Item()
    {
    }

    public Item(
        Guid id,
        Guid sellerId,
        Guid categoryId,
        string title,
        string description,
        decimal price,
        ItemStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt = null)
    {
        Id = id;
        SellerId = sellerId;
        CategoryId = categoryId;
        Title = title;
        Description = description;
        Price = price;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; private set; }
    public Guid SellerId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public ItemStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public User? Seller { get; private set; }
    public Category? Category { get; private set; }
    public IReadOnlyCollection<ListingImage> Images => _images;

    public void UpdateDetails(string title, string description, decimal price, ItemStatus status)
    {
        Title = title;
        Description = description;
        Price = price;
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddImage(ListingImage image)
    {
        _images.Add(image);
    }

    public void ChangeCategory(Guid categoryId)
    {
        CategoryId = categoryId;
        Category = null;
    }
}
