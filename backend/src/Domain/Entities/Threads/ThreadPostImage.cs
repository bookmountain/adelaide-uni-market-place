namespace Domain.Entities.Threads;

public class ThreadPostImage
{
    private ThreadPostImage() { }

    public ThreadPostImage(Guid id, Guid postId, string r2Key, int ordinal)
    {
        Id = id;
        PostId = postId;
        R2Key = r2Key;
        Ordinal = ordinal;
    }

    public Guid Id { get; private set; }
    public Guid PostId { get; private set; }
    public string R2Key { get; private set; } = string.Empty;
    public int Ordinal { get; private set; }
}
