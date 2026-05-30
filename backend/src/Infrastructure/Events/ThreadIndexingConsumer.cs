using Application.Common.Interfaces;
using Application.Threads.Indexing;
using Contracts.Events.Threads;
using MassTransit;

namespace Infrastructure.Events;

/// <summary>Plain, MassTransit-free indexing logic so it is unit-testable.</summary>
public sealed class ThreadIndexingService
{
    private readonly ThreadPostDocumentBuilder _builder;
    private readonly IThreadSearchIndex _index;
    private readonly IThreadFeedCache _cache;
    private readonly IIndexerIdempotencyStore _idempotency;

    public ThreadIndexingService(
        ThreadPostDocumentBuilder builder, IThreadSearchIndex index, IThreadFeedCache cache, IIndexerIdempotencyStore idempotency)
    {
        _builder = builder;
        _index = index;
        _cache = cache;
        _idempotency = idempotency;
    }

    public async Task HandlePostChangedAsync(Guid postId, string idempotencyKey, CancellationToken ct)
    {
        if (!await _idempotency.TryMarkAsync(idempotencyKey, ct)) return;

        var doc = await _builder.BuildAsync(postId, ct);
        if (doc is null)
        {
            // Post is gone/soft-deleted — ensure it is not in the index.
            await _index.DeleteAsync(postId, ct);
        }
        else
        {
            await _index.UpsertAsync(doc, ct);
        }

        await _cache.InvalidateAsync(doc?.CategorySlug, ct);
    }

    public async Task HandlePostDeletedAsync(Guid postId, string idempotencyKey, CancellationToken ct)
    {
        if (!await _idempotency.TryMarkAsync(idempotencyKey, ct)) return;
        await _index.DeleteAsync(postId, ct);
        await _cache.InvalidateAsync(null, ct);
    }
}

public sealed class ThreadIndexingConsumer :
    IConsumer<ThreadPostCreated>,
    IConsumer<ThreadPostUpdated>,
    IConsumer<ThreadPostLikeChanged>,
    IConsumer<ThreadPostDeleted>,
    IConsumer<ThreadCommentCreated>,
    IConsumer<ThreadCommentLikeChanged>,
    IConsumer<ThreadCommentDeleted>
{
    private readonly ThreadIndexingService _service;
    public ThreadIndexingConsumer(ThreadIndexingService service) => _service = service;

    public Task Consume(ConsumeContext<ThreadPostCreated> c)
        => _service.HandlePostChangedAsync(c.Message.PostId, Key(c), c.CancellationToken);
    public Task Consume(ConsumeContext<ThreadPostUpdated> c)
        => _service.HandlePostChangedAsync(c.Message.PostId, Key(c), c.CancellationToken);
    public Task Consume(ConsumeContext<ThreadPostLikeChanged> c)
        => _service.HandlePostChangedAsync(c.Message.PostId, Key(c), c.CancellationToken);
    public Task Consume(ConsumeContext<ThreadPostDeleted> c)
        => _service.HandlePostDeletedAsync(c.Message.PostId, Key(c), c.CancellationToken);
    public Task Consume(ConsumeContext<ThreadCommentCreated> c)
        => _service.HandlePostChangedAsync(c.Message.PostId, Key(c), c.CancellationToken);
    public Task Consume(ConsumeContext<ThreadCommentLikeChanged> c)
        => _service.HandlePostChangedAsync(c.Message.PostId, Key(c), c.CancellationToken);
    public Task Consume(ConsumeContext<ThreadCommentDeleted> c)
        => _service.HandlePostChangedAsync(c.Message.PostId, Key(c), c.CancellationToken);

    // MassTransit MessageId is stable across redeliveries — use it as the idempotency key.
    private static string Key<T>(ConsumeContext<T> c) where T : class
        => (c.MessageId ?? Guid.NewGuid()).ToString();
}
