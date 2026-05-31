using System.Text.Json;

namespace Domain.Entities.Outbox;

public class OutboxEvent
{
    private OutboxEvent() { }

    private OutboxEvent(Guid id, string eventType, string payloadJson, DateTimeOffset occurredAt)
    {
        Id = id;
        EventType = eventType;
        PayloadJson = payloadJson;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public static OutboxEvent Create<TPayload>(string eventType, TPayload payload)
        => new(Guid.NewGuid(), eventType, JsonSerializer.Serialize(payload), DateTimeOffset.UtcNow);

    public void MarkPublished(DateTimeOffset at) => PublishedAt = at;
}
