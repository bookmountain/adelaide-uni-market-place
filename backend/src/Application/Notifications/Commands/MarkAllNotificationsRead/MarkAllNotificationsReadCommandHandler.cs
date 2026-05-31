using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Notifications.Commands.MarkAllNotificationsRead;

public sealed class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand>
{
    private readonly IApplicationDbContext _db;
    public MarkAllNotificationsReadCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(MarkAllNotificationsReadCommand request, CancellationToken ct)
    {
        var unread = await _db.Notifications
            .Where(n => n.RecipientUserId == request.RecipientUserId && !n.IsRead)
            .ToListAsync(ct);
        foreach (var n in unread) n.MarkRead();
        await _db.SaveChangesAsync(ct);
    }
}
