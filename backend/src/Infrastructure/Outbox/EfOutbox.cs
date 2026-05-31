using Application.Common.Interfaces;
using Domain.Entities.Outbox;
using Infrastructure.Data;

namespace Infrastructure.Outbox;

public sealed class EfOutbox : IOutbox
{
    private readonly MarketplaceDbContext _db;
    public EfOutbox(MarketplaceDbContext db) => _db = db;

    public void Enqueue<TPayload>(string eventType, TPayload payload)
        => _db.OutboxEvents.Add(OutboxEvent.Create(eventType, payload));
}
