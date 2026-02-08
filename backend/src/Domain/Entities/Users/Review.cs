using Domain.Entities.Orders;

namespace Domain.Entities.Users;

public class Review
{
    private Review()
    {
    }

    public Review(
        Guid id,
        Guid reviewerId,
        Guid targetUserId,
        string comment,
        int rating,
        DateTimeOffset createdAt,
        Guid? orderId = null)
    {
        Id = id;
        ReviewerId = reviewerId;
        TargetUserId = targetUserId;
        Comment = comment;
        Rating = rating;
        CreatedAt = createdAt;
        OrderId = orderId;
    }

    public Guid Id { get; private set; }
    public Guid ReviewerId { get; private set; }
    public Guid TargetUserId { get; private set; }
    public string Comment { get; private set; } = string.Empty;
    public int Rating { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? OrderId { get; private set; }

    public User? Reviewer { get; private set; }
    public User? TargetUser { get; private set; }
    public Order? Order { get; private set; }
}
