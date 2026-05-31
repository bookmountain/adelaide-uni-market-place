using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Notifications.Commands.MarkNotificationRead;

public sealed class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand>
{
    private readonly IApplicationDbContext _db;
    public MarkNotificationReadCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId && n.RecipientUserId == request.RecipientUserId, ct);
        if (notification is null) return; // not found or not owned — no-op
        notification.MarkRead();
        await _db.SaveChangesAsync(ct);
    }
}
