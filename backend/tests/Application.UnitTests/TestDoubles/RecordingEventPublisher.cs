using Application.Common.Interfaces;

namespace Application.UnitTests.TestDoubles;

public sealed class RecordingEventPublisher : IEventPublisher
{
    public List<object> Published { get; } = new();
    public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
    {
        Published.Add(message);
        return Task.CompletedTask;
    }
}
