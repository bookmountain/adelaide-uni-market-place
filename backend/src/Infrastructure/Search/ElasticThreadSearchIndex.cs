using Application.Common.Interfaces;
using Application.Threads.Indexing;
using Contracts.DTO.Threads;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace Infrastructure.Search;

public sealed class ElasticThreadSearchIndex : IThreadSearchIndex
{
    public const string IndexName = "threads";
    private readonly ElasticsearchClient _client;

    public ElasticThreadSearchIndex(ElasticsearchClient client) => _client = client;

    public async Task UpsertAsync(ThreadPostDocument d, CancellationToken ct = default)
    {
        var doc = new ElasticThreadDocument
        {
            PostId = d.PostId,
            CategorySlug = d.CategorySlug,
            AuthorIsAnonymous = d.Author.IsAnonymous,
            AuthorHandle = d.Author.Handle,
            AuthorUserId = d.Author.IsAnonymous ? null : d.Author.UserId,
            AuthorDisplayName = d.Author.IsAnonymous ? null : d.Author.DisplayName,
            AuthorAvatarUrl = d.Author.IsAnonymous ? null : d.Author.AvatarUrl,
            Title = d.Title,
            Body = d.Body,
            ThumbnailKey = d.ThumbnailKey,
            LikeCount = d.LikeCount,
            CommentCount = d.CommentCount,
            HotRank = d.HotRank,
            CreatedAt = d.CreatedAt,
            LastActivityAt = d.LastActivityAt
        };
        await _client.IndexAsync(doc, i => i.Index(IndexName).Id(d.PostId.ToString()), ct);
    }

    public async Task DeleteAsync(Guid postId, CancellationToken ct = default)
        => await _client.DeleteAsync<ElasticThreadDocument>(postId.ToString(), d => d.Index(IndexName), ct);

    public async Task<ThreadSearchPage> SearchAsync(ThreadSearchQuery query, CancellationToken ct = default)
    {
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 50);
        var from = int.TryParse(query.Cursor, out var n) && n >= 0 ? n : 0;

        var must = new List<Query>();
        if (!string.IsNullOrWhiteSpace(query.CategorySlug))
            must.Add(new TermQuery { Field = "categorySlug", Value = query.CategorySlug });
        if (!string.IsNullOrWhiteSpace(query.Query))
            must.Add(new MultiMatchQuery { Query = query.Query, Fields = new[] { "title", "body" } });

        var response = await _client.SearchAsync<ElasticThreadDocument>(s => s
            .Indices(IndexName)
            .From(from)
            .Size(pageSize)
            .Query(q => q.Bool(b => b.Must(must.ToArray())))
            .Sort(SortFor(query.Sort)), ct);

        var docs = response.Documents.ToList();
        var items = docs.Select(d => new ThreadPostSummary(
            d.PostId,
            d.CategorySlug,
            new AuthorRef(d.AuthorIsAnonymous, d.AuthorHandle, d.AuthorUserId, d.AuthorDisplayName, d.AuthorAvatarUrl),
            d.Title,
            d.Body,
            d.ThumbnailKey,
            d.LikeCount,
            d.CommentCount,
            d.CreatedAt,
            d.LastActivityAt)).ToList();

        var total = response.Total;
        var next = from + pageSize < total ? (from + pageSize).ToString() : null;
        return new ThreadSearchPage(items, next);
    }

    private static Action<SortOptionsDescriptor<ElasticThreadDocument>> SortFor(string? sort)
        => sort?.ToLowerInvariant() switch
        {
            "top" => s => s.Field(f => f.LikeCount, fd => fd.Order(SortOrder.Desc)),
            "hot" => s => s.Field(f => f.HotRank, fd => fd.Order(SortOrder.Desc)),
            _ => s => s.Field(f => f.CreatedAt, fd => fd.Order(SortOrder.Desc))
        };
}
