namespace Application.Threads.Indexing;

public sealed record ThreadSearchQuery(string? CategorySlug, string Sort, string? Query, string? Cursor, int PageSize);
