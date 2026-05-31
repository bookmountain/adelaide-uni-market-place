using System.Text.Json;
using Domain.Entities.Outbox;
using Xunit;

namespace Application.UnitTests.Outbox;

public sealed class OutboxEventTests
{
    private sealed record SamplePayload(Guid PostId, int N);

    [Fact]
    public void Create_serializes_payload_and_sets_metadata()
    {
        var payload = new SamplePayload(Guid.NewGuid(), 5);
        var ev = OutboxEvent.Create("thread.post.created", payload);

        Assert.Equal("thread.post.created", ev.EventType);
        Assert.Null(ev.PublishedAt);
        Assert.NotEqual(Guid.Empty, ev.Id);
        var round = JsonSerializer.Deserialize<SamplePayload>(ev.PayloadJson);
        Assert.Equal(payload, round);
    }

    [Fact]
    public void MarkPublished_sets_timestamp()
    {
        var ev = OutboxEvent.Create("x", new SamplePayload(Guid.NewGuid(), 1));
        ev.MarkPublished(DateTimeOffset.UtcNow);
        Assert.NotNull(ev.PublishedAt);
    }
}
