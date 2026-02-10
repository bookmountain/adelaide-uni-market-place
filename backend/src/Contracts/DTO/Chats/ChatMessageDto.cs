namespace Contracts.DTO.Chats;

public sealed record ChatMessageDto(
    Guid Id,
    Guid ThreadId,
    Guid FromUserId,
    Guid ToUserId,
    Guid? ItemId,
    string Body,
    string? AttachmentUrl,
    DateTimeOffset SentAt);
