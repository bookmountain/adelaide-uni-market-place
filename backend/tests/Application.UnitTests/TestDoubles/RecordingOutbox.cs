using System.Collections.Generic;
using Application.Common.Interfaces;

namespace Application.UnitTests.TestDoubles;

public sealed class RecordingOutbox : IOutbox
{
    public List<(string eventType, object payload)> Events { get; } = new();
    public void Enqueue<TPayload>(string eventType, TPayload payload) => Events.Add((eventType, payload!));
}
