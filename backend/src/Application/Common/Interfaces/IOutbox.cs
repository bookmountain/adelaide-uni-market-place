namespace Application.Common.Interfaces;

public interface IOutbox
{
    /// <summary>Adds an outbox row to the current unit of work. Does NOT save — the caller's SaveChangesAsync commits it atomically.</summary>
    void Enqueue<TPayload>(string eventType, TPayload payload);
}
