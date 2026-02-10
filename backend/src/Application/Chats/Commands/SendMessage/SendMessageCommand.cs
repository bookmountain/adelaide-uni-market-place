using Contracts.DTO.Chats;
using MediatR;

namespace Application.Chats.Commands.SendMessage;

public sealed record SendMessageCommand(
    Guid SenderId,
    Guid ToUserId,
    string Body,
    Guid? ItemId,
    string? AttachmentUrl) : IRequest<ChatMessageDto>;
