namespace Domain.Entities.Items;

public class ListingImage
{
    private ListingImage()
    {
    }

    public ListingImage(Guid id, Guid itemId, string url, int sortOrder, string storageKey)
    {
        Id = id;
        ItemId = itemId;
        Url = url;
        SortOrder = sortOrder;
        StorageKey = storageKey;
    }

    public Guid Id { get; private set; }
    public Guid ItemId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;

    public Item? Item { get; private set; }

    public void UpdateSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }
}
