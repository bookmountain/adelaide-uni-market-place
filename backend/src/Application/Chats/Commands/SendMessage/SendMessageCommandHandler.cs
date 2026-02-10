using System.Security.Cryptography;
using System.Text;
using Application.Common.Interfaces;
using Contracts.DTO.Chats;
using Domain.Entities.Chats;
using Mapster;
using MediatR;

namespace Application.Chats.Commands.SendMessage;

public sealed class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, ChatMessageDto>
{
    private readonly IApplicationDbContext _dbContext;

    public SendMessageCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ChatMessageDto> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var threadId = GetThreadId(request.SenderId, request.ToUserId);

        var message = new ChatMessage(
            id: Guid.NewGuid(),
            threadId: threadId,
            fromUserId: request.SenderId,
            toUserId: request.ToUserId,
            body: request.Body,
            sentAt: DateTimeOffset.UtcNow,
            itemId: request.ItemId,
            attachmentUrl: request.AttachmentUrl);

        _dbContext.ChatMessages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return message.Adapt<ChatMessageDto>();
    }

    private static Guid GetThreadId(Guid userId1, Guid userId2)
    {
        var list = new[] { userId1, userId2 }.OrderBy(x => x).ToList();
        var input = $"{list[0]}-{list[1]}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}
