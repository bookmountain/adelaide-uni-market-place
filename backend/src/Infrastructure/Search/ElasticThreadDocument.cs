namespace Infrastructure.Search;

/// <summary>Flat ES document. Author identity fields are populated ONLY for non-anonymous posts.</summary>
public sealed class ElasticThreadDocument
{
    public Guid PostId { get; set; }
    public string CategorySlug { get; set; } = string.Empty;
    public bool AuthorIsAnonymous { get; set; }
    public string? AuthorHandle { get; set; }
    public Guid? AuthorUserId { get; set; }
    public string? AuthorDisplayName { get; set; }
    public string? AuthorAvatarUrl { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ThumbnailKey { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public double HotRank { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
}
