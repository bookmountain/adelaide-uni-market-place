using Application.Common.Interfaces;
using Contracts.DTO.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Notifications.Queries.GetUnreadCount;

public sealed class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, UnreadCountResponse>
{
    private readonly IApplicationDbContext _db;
    public GetUnreadCountQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<UnreadCountResponse> Handle(GetUnreadCountQuery request, CancellationToken ct)
    {
        var count = await _db.Notifications.CountAsync(n => n.RecipientUserId == request.RecipientUserId && !n.IsRead, ct);
        return new UnreadCountResponse(count);
    }
}
