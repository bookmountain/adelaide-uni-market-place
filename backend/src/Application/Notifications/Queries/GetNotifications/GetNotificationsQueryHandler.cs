using Application.Common.Interfaces;
using Contracts.DTO.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Notifications.Queries.GetNotifications;

public sealed class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, NotificationListResponse>
{
    private readonly IApplicationDbContext _db;
    public GetNotificationsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<NotificationListResponse> Handle(GetNotificationsQuery request, CancellationToken ct)
    {
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, 50);
        var offset = int.TryParse(request.Cursor, out var n) && n >= 0 ? n : 0;

        // Load then sort client-side to avoid SQLite DateTimeOffset ORDER BY limitation in tests.
        var all = await _db.Notifications
            .AsNoTracking()
            .Where(x => x.RecipientUserId == request.RecipientUserId)
            .ToListAsync(ct);

        var rows = all
            .OrderByDescending(x => x.CreatedAt)
            .Skip(offset)
            .Take(pageSize + 1)
            .ToList();
        var hasMore = rows.Count > pageSize;
        var page = rows.Take(pageSize).ToList();

        // Resolve display names for non-anonymous actors in one round-trip.
        var actorIds = page.Where(x => x.ActorUserId is not null).Select(x => x.ActorUserId!.Value).Distinct().ToList();
        var names = await _db.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var items = page.Select(x =>
        {
            var actor = x.ActorUserId is { } uid
                ? new NotificationActor(false, null, uid, names.TryGetValue(uid, out var dn) ? dn : "[unknown]")
                : new NotificationActor(true, x.ActorAnonHandleSnapshot ?? "anonymous", null, null);
            return new NotificationResponse(x.Id, x.Type, x.SourcePostId, x.SourceCommentId, actor, x.IsRead, x.CreatedAt);
        }).ToList();

        var next = hasMore ? (offset + pageSize).ToString() : null;
        return new NotificationListResponse(items, next);
    }
}
