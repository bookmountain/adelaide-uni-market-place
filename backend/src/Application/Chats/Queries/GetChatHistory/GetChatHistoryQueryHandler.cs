using System.Security.Cryptography;
using System.Text;
using Application.Common.Interfaces;
using Contracts.DTO.Chats;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Chats.Queries.GetChatHistory;

public sealed class GetChatHistoryQueryHandler : IRequestHandler<GetChatHistoryQuery, List<ChatMessageDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetChatHistoryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ChatMessageDto>> Handle(GetChatHistoryQuery request, CancellationToken cancellationToken)
    {
        var threadId = GetThreadId(request.CurrentUserId, request.OtherUserId);

        return await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(cm => cm.ThreadId == threadId)
            .OrderBy(cm => cm.SentAt)
            .ProjectToType<ChatMessageDto>()
            .ToListAsync(cancellationToken);
    }

    private static Guid GetThreadId(Guid userId1, Guid userId2)
    {
        var list = new[] { userId1, userId2 }.OrderBy(x => x).ToList();
        var input = $"{list[0]}-{list[1]}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}
