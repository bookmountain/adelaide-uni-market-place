using Domain.Shared.Enums;

namespace Domain.Entities.Threads;

public class ThreadLike
{
    private ThreadLike() { }

    public ThreadLike(Guid userId, ThreadLikeTarget targetType, Guid targetId)
    {
        UserId = userId;
        TargetType = targetType;
        TargetId = targetId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; private set; }
    public ThreadLikeTarget TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
