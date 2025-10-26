using Domain.Entities.Items;
using Domain.Entities.Users;

namespace Domain.Entities.Chats;

public class ChatMessage
{
    private ChatMessage()
    {
    }

    public ChatMessage(
        Guid id,
        Guid threadId,
        Guid fromUserId,
        Guid toUserId,
        string body,
        DateTimeOffset sentAt,
        Guid? itemId = null)
    {
        Id = id;
        ThreadId = threadId;
        FromUserId = fromUserId;
        ToUserId = toUserId;
        Body = body;
        SentAt = sentAt;
        ItemId = itemId;
    }

    public Guid Id { get; private set; }
    public Guid ThreadId { get; private set; }
    public Guid FromUserId { get; private set; }
    public Guid ToUserId { get; private set; }
    public Guid? ItemId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset SentAt { get; private set; }

    public User? FromUser { get; private set; }
    public User? ToUser { get; private set; }
    public Item? Item { get; private set; }
}
