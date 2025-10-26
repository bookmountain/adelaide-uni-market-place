namespace Domain.Entities.Items;

public class ListingImage
{
    private ListingImage()
    {
    }

    public ListingImage(Guid id, Guid itemId, string url, int sortOrder)
    {
        Id = id;
        ItemId = itemId;
        Url = url;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }
    public Guid ItemId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    public Item? Item { get; private set; }
}
