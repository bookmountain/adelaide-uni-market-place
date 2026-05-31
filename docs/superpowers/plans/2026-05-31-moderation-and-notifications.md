# Moderation & Notifications Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add community moderation (user reports → admin review queue → resolve with dismiss / remove-content / warn, all audited) and in-app reply notifications, completing the Threads platform.

**Architecture:** Reports and an audit log are EF aggregates with MediatR command/query handlers, mirroring the existing Threads vertical. The admin review queue is the ONE deliberate "anon-break": admins see the real author of anonymous content for moderation, behind a role-gated endpoint with a dedicated DTO (the public read paths the anon-leak contract test guards are untouched). "Remove content" soft-deletes the target and enqueues the existing delete event so the Elasticsearch index drops it. Notifications are written by a separate `ThreadNotificationConsumer` that reacts to the existing `ThreadCommentCreated` event (DB-existence idempotency, anonymity preserved via an actor snapshot). Reads come straight from Postgres.

**Tech Stack:** ASP.NET Core 8, EF Core (Npgsql), MediatR, FluentValidation, MassTransit, StackExchange.Redis, xUnit + SQLite.

**Spec:** `docs/superpowers/specs/2026-05-29-threads-and-identity-overhaul-design.md` (Section 7 notifications, Section 8 moderation).

**Prereqs (merged):** Plan 2 Threads (entities incl. `ThreadPost.SoftDelete`, `ThreadComment.SoftDelete`, `AuthorRefFactory`), Plan 3 outbox (`IOutbox.Enqueue`, `ThreadEventTypes.{PostDeleted,CommentDeleted}` + the dispatcher/indexer that already handle them). Plan 1 `IConnectionMultiplexer` + the Redis rate-limiter pattern (`RedisLoginRateLimiter`).

---

## Conventions (read once)

- **Build:** `dotnet build Marketplace.sln --no-restore /m:1 /p:BuildInParallel=false`
- **Test:** `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false` (filter: `--filter "FullyQualifiedName~<Class>"`)
- **Migrations:** `src/Infrastructure/Migrations/`; `ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations add <Name> --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj --output-dir Migrations`
- Tables snake_case, columns PascalCase. Interface in `Application.Common.Interfaces`, impl in `Infrastructure`, in-memory fake in `tests/.../TestDoubles/` (mirror Plan 1).
- Admin endpoints: `[Authorize(Roles = "Admin")]`. Controllers read the caller via the `TryGetUserId(out Guid)` helper (`ClaimTypes.NameIdentifier ?? "sub"`).
- Enqueue re-index events with `IOutbox.Enqueue(ThreadEventTypes.X, new EventRecord(...))` before `SaveChangesAsync`.

## File Structure

- **Domain:** `src/Domain/Shared/Enums/{ReportTargetType,ReportReason,ReportStatus,NotificationType}.cs`; `src/Domain/Entities/Moderation/{ThreadReport,ModerationAudit}.cs`; `src/Domain/Entities/Notifications/Notification.cs`
- **Infrastructure config:** `Data/Configurations/{ThreadReportConfiguration,ModerationAuditConfiguration,NotificationConfiguration}.cs`; `Caching/RedisReportRateLimiter.cs`; `Events/ThreadNotificationConsumer.cs`
- **Application interfaces:** `IReportRateLimiter.cs`
- **Application:** `Moderation/Commands/{CreateReport,ResolveReport}/*`, `Moderation/Queries/GetReportQueue/*`; `Notifications/{NotificationService.cs}` + `Notifications/Queries/{GetNotifications,GetUnreadCount}/*` + `Notifications/Commands/{MarkNotificationRead,MarkAllNotificationsRead}/*`
- **Contracts:** `DTO/Moderation/*`, `DTO/Notifications/*`
- **Api:** modify `ThreadsController` (report endpoint); new `ModerationController`, `NotificationsController`
- **Tests:** `tests/Application.UnitTests/Moderation/*`, `tests/Application.UnitTests/Notifications/*`, `TestDoubles/InMemoryReportRateLimiter.cs`

---

## Task 1: Enums + Moderation/Notification aggregates

**Files:** 4 enum files; `ThreadReport.cs`, `ModerationAudit.cs`, `Notification.cs`. Test: `tests/Application.UnitTests/Moderation/ModerationAggregatesTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using Domain.Entities.Moderation;
using Domain.Entities.Notifications;
using Domain.Shared.Enums;
using Xunit;

namespace Application.UnitTests.Moderation;

public sealed class ModerationAggregatesTests
{
    [Fact]
    public void New_report_is_open()
    {
        var r = new ThreadReport(Guid.NewGuid(), Guid.NewGuid(), ReportTargetType.Post, Guid.NewGuid(), ReportReason.Spam, "scammy");
        Assert.Equal(ReportStatus.Open, r.Status);
        Assert.Null(r.ReviewedByUserId);
    }

    [Fact]
    public void Resolve_sets_status_reviewer_and_time()
    {
        var r = new ThreadReport(Guid.NewGuid(), Guid.NewGuid(), ReportTargetType.Comment, Guid.NewGuid(), ReportReason.Harassment, null);
        var admin = Guid.NewGuid();
        r.Resolve(admin, ReportStatus.Reviewed);
        Assert.Equal(ReportStatus.Reviewed, r.Status);
        Assert.Equal(admin, r.ReviewedByUserId);
        Assert.NotNull(r.ReviewedAt);
    }

    [Fact]
    public void Audit_captures_admin_action()
    {
        var a = ModerationAudit.Record(Guid.NewGuid(), ReportTargetType.Post, Guid.NewGuid(), "remove-content", "spam");
        Assert.Equal("remove-content", a.Action);
        Assert.NotEqual(Guid.Empty, a.Id);
    }

    [Fact]
    public void Notification_can_be_marked_read()
    {
        var n = Notification.ForReply(Guid.NewGuid(), NotificationType.PostReplied, Guid.NewGuid(), null,
            actorUserId: Guid.NewGuid(), actorAnonHandle: null);
        Assert.False(n.IsRead);
        n.MarkRead();
        Assert.True(n.IsRead);
    }

    [Fact]
    public void Anonymous_actor_notification_stores_handle_not_user()
    {
        var n = Notification.ForReply(Guid.NewGuid(), NotificationType.CommentReplied, Guid.NewGuid(), Guid.NewGuid(),
            actorUserId: null, actorAnonHandle: "quiet-koala-4821");
        Assert.Null(n.ActorUserId);
        Assert.Equal("quiet-koala-4821", n.ActorAnonHandleSnapshot);
    }
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: Enums**

`src/Domain/Shared/Enums/ReportTargetType.cs`:
```csharp
namespace Domain.Shared.Enums;

public enum ReportTargetType { Post = 0, Comment = 1 }
```
`src/Domain/Shared/Enums/ReportReason.cs`:
```csharp
namespace Domain.Shared.Enums;

public enum ReportReason { Spam = 0, Harassment = 1, Nsfw = 2, Scam = 3, Other = 4 }
```
`src/Domain/Shared/Enums/ReportStatus.cs`:
```csharp
namespace Domain.Shared.Enums;

public enum ReportStatus { Open = 0, Reviewed = 1, Dismissed = 2 }
```
`src/Domain/Shared/Enums/NotificationType.cs`:
```csharp
namespace Domain.Shared.Enums;

public enum NotificationType { PostReplied = 0, CommentReplied = 1 }
```

- [ ] **Step 4: Aggregates**

`src/Domain/Entities/Moderation/ThreadReport.cs`:
```csharp
using Domain.Shared.Enums;

namespace Domain.Entities.Moderation;

public class ThreadReport
{
    private ThreadReport() { }

    public ThreadReport(Guid id, Guid reporterUserId, ReportTargetType targetType, Guid targetId, ReportReason reason, string? notes)
    {
        Id = id;
        ReporterUserId = reporterUserId;
        TargetType = targetType;
        TargetId = targetId;
        Reason = reason;
        Notes = notes;
        Status = ReportStatus.Open;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ReporterUserId { get; private set; }
    public ReportTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public ReportReason Reason { get; private set; }
    public string? Notes { get; private set; }
    public ReportStatus Status { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void Resolve(Guid adminUserId, ReportStatus status)
    {
        Status = status;
        ReviewedByUserId = adminUserId;
        ReviewedAt = DateTimeOffset.UtcNow;
    }
}
```

`src/Domain/Entities/Moderation/ModerationAudit.cs`:
```csharp
using Domain.Shared.Enums;

namespace Domain.Entities.Moderation;

public class ModerationAudit
{
    private ModerationAudit() { }

    private ModerationAudit(Guid id, Guid adminUserId, ReportTargetType targetType, Guid targetId, string action, string? reason)
    {
        Id = id;
        AdminUserId = adminUserId;
        TargetType = targetType;
        TargetId = targetId;
        Action = action;
        Reason = reason;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid AdminUserId { get; private set; }
    public ReportTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ModerationAudit Record(Guid adminUserId, ReportTargetType targetType, Guid targetId, string action, string? reason)
        => new(Guid.NewGuid(), adminUserId, targetType, targetId, action, reason);
}
```

`src/Domain/Entities/Notifications/Notification.cs`:
```csharp
using Domain.Shared.Enums;

namespace Domain.Entities.Notifications;

public class Notification
{
    private Notification() { }

    private Notification(Guid id, Guid recipientUserId, NotificationType type, Guid sourcePostId, Guid? sourceCommentId,
        Guid? actorUserId, string? actorAnonHandleSnapshot)
    {
        Id = id;
        RecipientUserId = recipientUserId;
        Type = type;
        SourcePostId = sourcePostId;
        SourceCommentId = sourceCommentId;
        ActorUserId = actorUserId;
        ActorAnonHandleSnapshot = actorAnonHandleSnapshot;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public NotificationType Type { get; private set; }
    public Guid SourcePostId { get; private set; }
    public Guid? SourceCommentId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string? ActorAnonHandleSnapshot { get; private set; }
    public bool IsRead { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Notification ForReply(Guid recipientUserId, NotificationType type, Guid sourcePostId, Guid? sourceCommentId,
        Guid? actorUserId, string? actorAnonHandle)
        => new(Guid.NewGuid(), recipientUserId, type, sourcePostId, sourceCommentId, actorUserId, actorAnonHandle);

    public void MarkRead() => IsRead = true;
}
```

- [ ] **Step 5: Run — PASS (5). Commit** — `git add src/Domain/Shared/Enums/ReportTargetType.cs src/Domain/Shared/Enums/ReportReason.cs src/Domain/Shared/Enums/ReportStatus.cs src/Domain/Shared/Enums/NotificationType.cs src/Domain/Entities/Moderation/ src/Domain/Entities/Notifications/ tests/Application.UnitTests/Moderation/ModerationAggregatesTests.cs && git commit -m "feat: moderation + notification aggregates"`

---

## Task 2: EF configs + DbSets + migration

**Files:** 3 config files; interface + context DbSets; migration `AddModerationAndNotifications`.

- [ ] **Step 1: Interface + context DbSets**

In `src/Application/Common/Interfaces/IApplicationDbContext.cs` add `using Domain.Entities.Moderation;` + `using Domain.Entities.Notifications;` and:
```csharp
    DbSet<ThreadReport> ThreadReports { get; }
    DbSet<ModerationAudit> ModerationAudits { get; }
    DbSet<Notification> Notifications { get; }
```
In `src/Infrastructure/Data/MarketplaceDbContext.cs` add the usings and:
```csharp
    public DbSet<ThreadReport> ThreadReports => Set<ThreadReport>();
    public DbSet<ModerationAudit> ModerationAudits => Set<ModerationAudit>();
    public DbSet<Notification> Notifications => Set<Notification>();
```

- [ ] **Step 2: Configs**

`src/Infrastructure/Data/Configurations/ThreadReportConfiguration.cs`:
```csharp
using Domain.Entities.Moderation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ThreadReportConfiguration : IEntityTypeConfiguration<ThreadReport>
{
    public void Configure(EntityTypeBuilder<ThreadReport> builder)
    {
        builder.ToTable("thread_reports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.TargetType).HasConversion<int>().IsRequired();
        builder.Property(r => r.Reason).HasConversion<int>().IsRequired();
        builder.Property(r => r.Status).HasConversion<int>().IsRequired();
        builder.Property(r => r.Notes).HasMaxLength(1000);
        builder.Property(r => r.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(r => r.ReviewedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(r => new { r.Status, r.CreatedAt });
        builder.HasIndex(r => new { r.TargetType, r.TargetId });
    }
}
```
`src/Infrastructure/Data/Configurations/ModerationAuditConfiguration.cs`:
```csharp
using Domain.Entities.Moderation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ModerationAuditConfiguration : IEntityTypeConfiguration<ModerationAudit>
{
    public void Configure(EntityTypeBuilder<ModerationAudit> builder)
    {
        builder.ToTable("moderation_audits");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.TargetType).HasConversion<int>().IsRequired();
        builder.Property(a => a.Action).IsRequired().HasMaxLength(64);
        builder.Property(a => a.Reason).HasMaxLength(256);
        builder.Property(a => a.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.HasIndex(a => a.AdminUserId);
    }
}
```
`src/Infrastructure/Data/Configurations/NotificationConfiguration.cs`:
```csharp
using Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Type).HasConversion<int>().IsRequired();
        builder.Property(n => n.ActorAnonHandleSnapshot).HasMaxLength(64);
        builder.Property(n => n.IsRead).IsRequired();
        builder.Property(n => n.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.HasIndex(n => new { n.RecipientUserId, n.IsRead, n.CreatedAt });
        // Idempotency: at most one reply-notification per source comment.
        builder.HasIndex(n => n.SourceCommentId);
    }
}
```

- [ ] **Step 3: Build, then migration** `AddModerationAndNotifications`. Inspect: creates exactly `thread_reports`, `moderation_audits`, `notifications`; touches NO existing tables. If it does, STOP/BLOCKED.

- [ ] **Step 4: Build + full suite green. Commit** — `git add src/Application/Common/Interfaces/IApplicationDbContext.cs src/Infrastructure/Data/MarketplaceDbContext.cs src/Infrastructure/Data/Configurations/ThreadReportConfiguration.cs src/Infrastructure/Data/Configurations/ModerationAuditConfiguration.cs src/Infrastructure/Data/Configurations/NotificationConfiguration.cs src/Infrastructure/Migrations/ && git commit -m "feat: EF mapping + migration for moderation + notifications"`

---

## Task 3: Report rate limiter (interface + fake + Redis impl)

**Files:** `IReportRateLimiter.cs`, `tests/.../TestDoubles/InMemoryReportRateLimiter.cs`, `src/Infrastructure/Caching/RedisReportRateLimiter.cs`. Test: `tests/Application.UnitTests/Moderation/InMemoryReportRateLimiterTests.cs`

- [ ] **Step 1: Interface**

`src/Application/Common/Interfaces/IReportRateLimiter.cs`:
```csharp
namespace Application.Common.Interfaces;

public interface IReportRateLimiter
{
    /// <summary>Returns true if the user may file another report now (and counts it); false if over the limit.</summary>
    Task<bool> TryConsumeAsync(Guid userId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Failing test**

```csharp
using Application.UnitTests.TestDoubles;
using Xunit;

namespace Application.UnitTests.Moderation;

public sealed class InMemoryReportRateLimiterTests
{
    [Fact]
    public async Task Allows_up_to_limit_then_blocks()
    {
        var limiter = new InMemoryReportRateLimiter(limit: 3);
        var user = Guid.NewGuid();

        Assert.True(await limiter.TryConsumeAsync(user));
        Assert.True(await limiter.TryConsumeAsync(user));
        Assert.True(await limiter.TryConsumeAsync(user));
        Assert.False(await limiter.TryConsumeAsync(user));
    }

    [Fact]
    public async Task Limits_are_per_user()
    {
        var limiter = new InMemoryReportRateLimiter(limit: 1);
        Assert.True(await limiter.TryConsumeAsync(Guid.NewGuid()));
        Assert.True(await limiter.TryConsumeAsync(Guid.NewGuid()));
    }
}
```

- [ ] **Step 3: Run — FAIL.**

- [ ] **Step 4: Fake + Redis impl**

`tests/Application.UnitTests/TestDoubles/InMemoryReportRateLimiter.cs`:
```csharp
using System.Collections.Concurrent;
using Application.Common.Interfaces;

namespace Application.UnitTests.TestDoubles;

public sealed class InMemoryReportRateLimiter : IReportRateLimiter
{
    private readonly ConcurrentDictionary<Guid, int> _counts = new();
    private readonly int _limit;
    public InMemoryReportRateLimiter(int limit = 10) => _limit = limit;

    public Task<bool> TryConsumeAsync(Guid userId, CancellationToken ct = default)
    {
        var count = _counts.AddOrUpdate(userId, 1, (_, c) => c + 1);
        return Task.FromResult(count <= _limit);
    }
}
```
`src/Infrastructure/Caching/RedisReportRateLimiter.cs`:
```csharp
using Application.Common.Interfaces;
using StackExchange.Redis;

namespace Infrastructure.Caching;

public sealed class RedisReportRateLimiter : IReportRateLimiter
{
    private const int Limit = 10;
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private readonly IConnectionMultiplexer _redis;
    public RedisReportRateLimiter(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<bool> TryConsumeAsync(Guid userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = $"ratelimit:report:{userId}";
        var count = await db.StringIncrementAsync(key);
        if (count == 1)
        {
            await db.KeyExpireAsync(key, Window);
        }
        return count <= Limit;
    }
}
```

- [ ] **Step 5: Run — PASS (2). Build + full green. Commit** — `git add src/Application/Common/Interfaces/IReportRateLimiter.cs src/Infrastructure/Caching/RedisReportRateLimiter.cs tests/Application.UnitTests/TestDoubles/InMemoryReportRateLimiter.cs tests/Application.UnitTests/Moderation/InMemoryReportRateLimiterTests.cs && git commit -m "feat: report rate limiter"`

---

## Task 4: CreateReport command

**Files:** `src/Contracts/DTO/Moderation/CreateReportRequest.cs`; `Moderation/Commands/CreateReport/{Command,Handler,Validator}.cs`. Test: `tests/Application.UnitTests/Moderation/CreateReportTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using Application.Moderation.Commands.CreateReport;
using Application.UnitTests.Common;
using Application.UnitTests.TestDoubles;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Moderation;

public sealed class CreateReportTests
{
    private static async Task<(MarketplaceDbContext db, Guid userId, ThreadPost post)> Seed(TestDb t)
    {
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(new User(userId, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, false, "T", "B");
        t.Context.ThreadPosts.Add(post);
        await t.Context.SaveChangesAsync();
        return (t.Context, userId, post);
    }

    [Fact]
    public async Task Creates_open_report_for_existing_post()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, post) = await Seed(t);
        var handler = new CreateReportCommandHandler(db, new InMemoryReportRateLimiter());

        var id = await handler.Handle(new CreateReportCommand(userId, ReportTargetType.Post, post.Id, ReportReason.Spam, "scam"), default);

        var report = await db.ThreadReports.SingleAsync();
        Assert.Equal(id, report.Id);
        Assert.Equal(ReportStatus.Open, report.Status);
        Assert.Equal(post.Id, report.TargetId);
    }

    [Fact]
    public async Task Rejects_report_for_missing_target()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, _) = await Seed(t);
        var handler = new CreateReportCommandHandler(db, new InMemoryReportRateLimiter());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateReportCommand(userId, ReportTargetType.Post, Guid.NewGuid(), ReportReason.Spam, null), default));
    }

    [Fact]
    public async Task Rate_limited_user_is_rejected()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, post) = await Seed(t);
        var handler = new CreateReportCommandHandler(db, new InMemoryReportRateLimiter(limit: 1));
        await handler.Handle(new CreateReportCommand(userId, ReportTargetType.Post, post.Id, ReportReason.Spam, null), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateReportCommand(userId, ReportTargetType.Post, post.Id, ReportReason.Other, null), default));
    }
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: Implement**

`src/Application/Moderation/Commands/CreateReport/CreateReportCommand.cs`:
```csharp
using Domain.Shared.Enums;
using MediatR;

namespace Application.Moderation.Commands.CreateReport;

public sealed record CreateReportCommand(Guid ReporterUserId, ReportTargetType TargetType, Guid TargetId, ReportReason Reason, string? Notes)
    : IRequest<Guid>;
```
`src/Application/Moderation/Commands/CreateReport/CreateReportCommandHandler.cs`:
```csharp
using Application.Common.Interfaces;
using Domain.Entities.Moderation;
using Domain.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Moderation.Commands.CreateReport;

public sealed class CreateReportCommandHandler : IRequestHandler<CreateReportCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly IReportRateLimiter _rateLimiter;

    public CreateReportCommandHandler(IApplicationDbContext db, IReportRateLimiter rateLimiter)
    {
        _db = db;
        _rateLimiter = rateLimiter;
    }

    public async Task<Guid> Handle(CreateReportCommand request, CancellationToken ct)
    {
        if (!await _rateLimiter.TryConsumeAsync(request.ReporterUserId, ct))
        {
            throw new InvalidOperationException("You have filed too many reports recently. Please try again later.");
        }

        var exists = request.TargetType == ReportTargetType.Post
            ? await _db.ThreadPosts.AnyAsync(p => p.Id == request.TargetId && !p.IsDeleted, ct)
            : await _db.ThreadComments.AnyAsync(c => c.Id == request.TargetId && !c.IsDeleted, ct);
        if (!exists)
        {
            throw new InvalidOperationException("The reported content no longer exists.");
        }

        var report = new ThreadReport(Guid.NewGuid(), request.ReporterUserId, request.TargetType, request.TargetId, request.Reason, request.Notes);
        _db.ThreadReports.Add(report);
        await _db.SaveChangesAsync(ct);
        return report.Id;
    }
}
```
`src/Application/Moderation/Commands/CreateReport/CreateReportCommandValidator.cs`:
```csharp
using FluentValidation;

namespace Application.Moderation.Commands.CreateReport;

public sealed class CreateReportCommandValidator : AbstractValidator<CreateReportCommand>
{
    public CreateReportCommandValidator()
    {
        RuleFor(c => c.Notes).MaximumLength(1000).When(c => c.Notes is not null);
    }
}
```
Also `src/Contracts/DTO/Moderation/CreateReportRequest.cs` (target type + id come from the route, not the body):
```csharp
using System.ComponentModel.DataAnnotations;
using Domain.Shared.Enums;

namespace Contracts.DTO.Moderation;

public sealed class CreateReportRequest
{
    [Required] public ReportReason Reason { get; init; }
    [MaxLength(1000)] public string? Notes { get; init; }
}
```

- [ ] **Step 4: Run — PASS (3). Build + full green. Commit** — `git add src/Application/Moderation/Commands/CreateReport/ src/Contracts/DTO/Moderation/CreateReportRequest.cs tests/Application.UnitTests/Moderation/CreateReportTests.cs && git commit -m "feat: create-report command (rate-limited)"`

---

## Task 5: Admin report-queue query (the anon-break)

**Files:** `src/Contracts/DTO/Moderation/{ModerationReportResponse,ModerationAuthor}.cs`; `Moderation/Queries/GetReportQueue/{Query,Handler}.cs`. Test: `tests/Application.UnitTests/Moderation/GetReportQueueTests.cs`

This query is admin-only and DELIBERATELY reveals the real author of anonymous content (the spec's moderation anon-break). It uses a dedicated `ModerationAuthor` shape — NOT the public `AuthorRef` — so there is no risk of this projection being reused on a public path.

- [ ] **Step 1: Failing test** (asserts the queue surfaces real author identity EVEN for an anonymous post)

```csharp
using Application.Moderation.Queries.GetReportQueue;
using Application.UnitTests.Common;
using Domain.Entities.Moderation;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Moderation;

public sealed class GetReportQueueTests
{
    [Fact]
    public async Task Open_queue_reveals_real_author_of_anonymous_post()
    {
        await using var t = await TestDb.CreateAsync();
        var authorId = Guid.NewGuid(); var reporterId = Guid.NewGuid(); var catId = Guid.NewGuid();
        var author = new User(authorId, "sarah@adelaide.edu.au", "Sarah Chen", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true);
        author.AssignAnonHandle("quiet-koala-4821");
        t.Context.Users.Add(author);
        t.Context.Users.Add(new User(reporterId, "r@adelaide.edu.au", "Reporter", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, authorId, isAnonymous: true, "Bad title", "Bad body");
        t.Context.ThreadPosts.Add(post);
        t.Context.ThreadReports.Add(new ThreadReport(Guid.NewGuid(), reporterId, ReportTargetType.Post, post.Id, ReportReason.Scam, "scam"));
        await t.Context.SaveChangesAsync();

        var result = await new GetReportQueueQueryHandler(t.Context).Handle(new GetReportQueueQuery(ReportStatus.Open), default);

        var item = Assert.Single(result);
        Assert.Equal(ReportReason.Scam, item.Reason);
        Assert.True(item.TargetIsAnonymousToPublic);          // the content is anonymous publicly
        Assert.Equal(authorId, item.Author.UserId);            // but moderators see the real author
        Assert.Equal("Sarah Chen", item.Author.DisplayName);
        Assert.Equal("Bad title", item.TargetExcerpt);
    }

    [Fact]
    public async Task Resolved_reports_excluded_when_querying_open()
    {
        await using var t = await TestDb.CreateAsync();
        var reporterId = Guid.NewGuid(); var authorId = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(new User(authorId, "a@adelaide.edu.au", "A", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, authorId, false, "T", "B");
        t.Context.ThreadPosts.Add(post);
        var report = new ThreadReport(Guid.NewGuid(), reporterId, ReportTargetType.Post, post.Id, ReportReason.Spam, null);
        report.Resolve(Guid.NewGuid(), ReportStatus.Dismissed);
        t.Context.ThreadReports.Add(report);
        await t.Context.SaveChangesAsync();

        var open = await new GetReportQueueQueryHandler(t.Context).Handle(new GetReportQueueQuery(ReportStatus.Open), default);
        Assert.Empty(open);
    }
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: DTOs**

`src/Contracts/DTO/Moderation/ModerationAuthor.cs`:
```csharp
namespace Contracts.DTO.Moderation;

/// <summary>Admin-only author projection. Unlike the public AuthorRef, this ALWAYS carries real identity (the moderation anon-break).</summary>
public sealed record ModerationAuthor(Guid UserId, string DisplayName);
```
`src/Contracts/DTO/Moderation/ModerationReportResponse.cs`:
```csharp
using Domain.Shared.Enums;

namespace Contracts.DTO.Moderation;

public sealed record ModerationReportResponse(
    Guid ReportId,
    ReportTargetType TargetType,
    Guid TargetId,
    ReportReason Reason,
    string? Notes,
    ReportStatus Status,
    bool TargetIsAnonymousToPublic,
    ModerationAuthor Author,
    string TargetExcerpt,
    DateTimeOffset CreatedAt);
```

- [ ] **Step 4: Query + handler**

`src/Application/Moderation/Queries/GetReportQueue/GetReportQueueQuery.cs`:
```csharp
using Contracts.DTO.Moderation;
using Domain.Shared.Enums;
using MediatR;

namespace Application.Moderation.Queries.GetReportQueue;

public sealed record GetReportQueueQuery(ReportStatus Status) : IRequest<IReadOnlyList<ModerationReportResponse>>;
```
`src/Application/Moderation/Queries/GetReportQueue/GetReportQueueQueryHandler.cs`:
```csharp
using Application.Common.Interfaces;
using Contracts.DTO.Moderation;
using Domain.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Moderation.Queries.GetReportQueue;

public sealed class GetReportQueueQueryHandler : IRequestHandler<GetReportQueueQuery, IReadOnlyList<ModerationReportResponse>>
{
    private readonly IApplicationDbContext _db;
    public GetReportQueueQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ModerationReportResponse>> Handle(GetReportQueueQuery request, CancellationToken ct)
    {
        var reports = await _db.ThreadReports
            .AsNoTracking()
            .Where(r => r.Status == request.Status)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        var result = new List<ModerationReportResponse>(reports.Count);
        foreach (var r in reports)
        {
            bool anon;
            string excerpt;
            Guid authorId;

            if (r.TargetType == ReportTargetType.Post)
            {
                var post = await _db.ThreadPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == r.TargetId, ct);
                anon = post?.IsAnonymous ?? false;
                excerpt = post is null ? "[deleted]" : Excerpt(post.Title);
                authorId = post?.AuthorUserId ?? Guid.Empty;
            }
            else
            {
                var comment = await _db.ThreadComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.TargetId, ct);
                anon = comment?.IsAnonymous ?? false;
                excerpt = comment is null ? "[deleted]" : Excerpt(comment.Body);
                authorId = comment?.AuthorUserId ?? Guid.Empty;
            }

            // The anon-break: resolve the REAL author identity for moderators.
            var author = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == authorId, ct);
            var moderationAuthor = new ModerationAuthor(authorId, author?.DisplayName ?? "[unknown]");

            result.Add(new ModerationReportResponse(
                r.Id, r.TargetType, r.TargetId, r.Reason, r.Notes, r.Status, anon, moderationAuthor, excerpt, r.CreatedAt));
        }

        return result;
    }

    private static string Excerpt(string text) => text.Length <= 200 ? text : text[..200];
}
```

- [ ] **Step 5: Run — PASS (2). Build + full green. Commit** — `git add src/Contracts/DTO/Moderation/ModerationAuthor.cs src/Contracts/DTO/Moderation/ModerationReportResponse.cs src/Application/Moderation/Queries/GetReportQueue/ tests/Application.UnitTests/Moderation/GetReportQueueTests.cs && git commit -m "feat: admin report-queue query (moderation anon-break)"`

---

## Task 6: ResolveReport command (dismiss / remove-content / warn + audit + re-index)

**Files:** `src/Contracts/DTO/Moderation/ResolveReportRequest.cs`; `Moderation/Commands/ResolveReport/{Command,Handler}.cs`. Test: `tests/Application.UnitTests/Moderation/ResolveReportTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using Application.Moderation.Commands.ResolveReport;
using Application.UnitTests.Common;
using Application.UnitTests.TestDoubles;
using Contracts.Events.Threads;
using Domain.Entities.Moderation;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Moderation;

public sealed class ResolveReportTests
{
    private static async Task<(MarketplaceDbContext db, ThreadPost post, ThreadReport report)> Seed(TestDb t)
    {
        var authorId = Guid.NewGuid(); var reporterId = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(new User(authorId, "a@adelaide.edu.au", "A", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, authorId, false, "T", "B");
        t.Context.ThreadPosts.Add(post);
        var report = new ThreadReport(Guid.NewGuid(), reporterId, ReportTargetType.Post, post.Id, ReportReason.Spam, null);
        t.Context.ThreadReports.Add(report);
        await t.Context.SaveChangesAsync();
        return (t.Context, post, report);
    }

    [Fact]
    public async Task Dismiss_marks_dismissed_and_audits()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, _, report) = await Seed(t);
        var admin = Guid.NewGuid();
        await new ResolveReportCommandHandler(db, new RecordingOutbox()).Handle(
            new ResolveReportCommand(report.Id, admin, "dismiss"), default);

        var saved = await db.ThreadReports.SingleAsync();
        Assert.Equal(ReportStatus.Dismissed, saved.Status);
        Assert.Equal(admin, saved.ReviewedByUserId);
        Assert.Equal(1, await db.ModerationAudits.CountAsync());
    }

    [Fact]
    public async Task Remove_content_soft_deletes_post_enqueues_delete_event_and_audits()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, post, report) = await Seed(t);
        var outbox = new RecordingOutbox();
        await new ResolveReportCommandHandler(db, outbox).Handle(
            new ResolveReportCommand(report.Id, Guid.NewGuid(), "remove-content"), default);

        Assert.True((await db.ThreadPosts.SingleAsync()).IsDeleted);
        Assert.Equal(ReportStatus.Reviewed, (await db.ThreadReports.SingleAsync()).Status);
        Assert.Equal(1, await db.ModerationAudits.CountAsync());
        Assert.Contains(outbox.Events, e => e.eventType == ThreadEventTypes.PostDeleted);
    }

    [Fact]
    public async Task Warn_user_marks_reviewed_and_audits_without_deleting()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, post, report) = await Seed(t);
        await new ResolveReportCommandHandler(db, new RecordingOutbox()).Handle(
            new ResolveReportCommand(report.Id, Guid.NewGuid(), "warn-user"), default);

        Assert.False((await db.ThreadPosts.SingleAsync()).IsDeleted);
        Assert.Equal(ReportStatus.Reviewed, (await db.ThreadReports.SingleAsync()).Status);
        Assert.Equal(1, await db.ModerationAudits.CountAsync());
    }

    [Fact]
    public async Task Unknown_action_throws()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, _, report) = await Seed(t);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ResolveReportCommandHandler(db, new RecordingOutbox()).Handle(
                new ResolveReportCommand(report.Id, Guid.NewGuid(), "explode"), default));
    }
}
```
> `RecordingOutbox` is a test double for `IOutbox` that records `(eventType, payload)` pairs. Create it at `tests/Application.UnitTests/TestDoubles/RecordingOutbox.cs`:
> ```csharp
> using System.Collections.Generic;
> using Application.Common.Interfaces;
> namespace Application.UnitTests.TestDoubles;
> public sealed class RecordingOutbox : IOutbox
> {
>     public List<(string eventType, object payload)> Events { get; } = new();
>     public void Enqueue<TPayload>(string eventType, TPayload payload) => Events.Add((eventType, payload!));
> }
> ```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: Implement**

`src/Application/Moderation/Commands/ResolveReport/ResolveReportCommand.cs`:
```csharp
using MediatR;

namespace Application.Moderation.Commands.ResolveReport;

public sealed record ResolveReportCommand(Guid ReportId, Guid AdminUserId, string Action) : IRequest;
```
`src/Application/Moderation/Commands/ResolveReport/ResolveReportCommandHandler.cs`:
```csharp
using Application.Common.Interfaces;
using Contracts.Events.Threads;
using Domain.Entities.Moderation;
using Domain.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Moderation.Commands.ResolveReport;

public sealed class ResolveReportCommandHandler : IRequestHandler<ResolveReportCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IOutbox _outbox;

    public ResolveReportCommandHandler(IApplicationDbContext db, IOutbox outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task Handle(ResolveReportCommand request, CancellationToken ct)
    {
        var report = await _db.ThreadReports.FirstOrDefaultAsync(r => r.Id == request.ReportId, ct)
            ?? throw new InvalidOperationException("Report not found.");

        var action = request.Action.Trim().ToLowerInvariant();
        switch (action)
        {
            case "dismiss":
                report.Resolve(request.AdminUserId, ReportStatus.Dismissed);
                break;

            case "warn-user":
                report.Resolve(request.AdminUserId, ReportStatus.Reviewed);
                break;

            case "remove-content":
                await RemoveContentAsync(report, ct);
                report.Resolve(request.AdminUserId, ReportStatus.Reviewed);
                break;

            default:
                throw new InvalidOperationException($"Unknown moderation action '{request.Action}'.");
        }

        _db.ModerationAudits.Add(ModerationAudit.Record(request.AdminUserId, report.TargetType, report.TargetId, action, report.Reason.ToString()));
        await _db.SaveChangesAsync(ct);
    }

    private async Task RemoveContentAsync(ThreadReport report, CancellationToken ct)
    {
        if (report.TargetType == ReportTargetType.Post)
        {
            var post = await _db.ThreadPosts.FirstOrDefaultAsync(p => p.Id == report.TargetId, ct);
            if (post is not null && !post.IsDeleted)
            {
                post.SoftDelete();
                _outbox.Enqueue(ThreadEventTypes.PostDeleted, new ThreadPostDeleted(post.Id));
            }
        }
        else
        {
            var comment = await _db.ThreadComments.FirstOrDefaultAsync(c => c.Id == report.TargetId, ct);
            if (comment is not null && !comment.IsDeleted)
            {
                comment.SoftDelete();
                _outbox.Enqueue(ThreadEventTypes.CommentDeleted, new ThreadCommentDeleted(comment.PostId, comment.Id));
            }
        }
    }
}
```
Also `src/Contracts/DTO/Moderation/ResolveReportRequest.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Moderation;

public sealed class ResolveReportRequest
{
    /// <summary>One of: dismiss, remove-content, warn-user.</summary>
    [Required] public string Action { get; init; } = string.Empty;
}
```

- [ ] **Step 4: Run — PASS (4). Build + full green. Commit** — `git add src/Application/Moderation/Commands/ResolveReport/ src/Contracts/DTO/Moderation/ResolveReportRequest.cs tests/Application.UnitTests/TestDoubles/RecordingOutbox.cs tests/Application.UnitTests/Moderation/ResolveReportTests.cs && git commit -m "feat: resolve-report command with audit + re-index"`

---

## Task 7: Moderation controllers (report + admin queue/resolve)

**Files:** modify `src/Api/Controllers/ThreadsController.cs` (add report endpoint); create `src/Api/Controllers/ModerationController.cs`.

- [ ] **Step 1: Add two report endpoints to `ThreadsController`** (any authenticated user)

Add `using Application.Moderation.Commands.CreateReport;`, `using Contracts.DTO.Moderation;`, and `using Domain.Shared.Enums;` (if not present), then add two thin actions that share a private helper. The target type is fixed by the route (no body/route duplication):
```csharp
    [HttpPost("posts/{postId:guid}/report")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public Task<IActionResult> ReportPost(Guid postId, [FromBody] CreateReportRequest request, CancellationToken ct)
        => FileReport(ReportTargetType.Post, postId, request, ct);

    [HttpPost("comments/{commentId:guid}/report")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public Task<IActionResult> ReportComment(Guid commentId, [FromBody] CreateReportRequest request, CancellationToken ct)
        => FileReport(ReportTargetType.Comment, commentId, request, ct);

    private async Task<IActionResult> FileReport(ReportTargetType targetType, Guid targetId, CreateReportRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var id = await _sender.Send(new CreateReportCommand(userId, targetType, targetId, request.Reason, request.Notes), ct);
            return StatusCode(StatusCodes.Status201Created, new { reportId = id });
        }
        catch (InvalidOperationException ex)
        {
            // Rate-limit and missing-target both surface as InvalidOperationException; map the rate-limit message to 429.
            return ex.Message.Contains("too many", StringComparison.OrdinalIgnoreCase)
                ? StatusCode(StatusCodes.Status429TooManyRequests, new { error = ex.Message })
                : BadRequest(new { error = ex.Message });
        }
    }
```

- [ ] **Step 2: Create `ModerationController`** (admin-only)

`src/Api/Controllers/ModerationController.cs`:
```csharp
using Application.Moderation.Commands.ResolveReport;
using Application.Moderation.Queries.GetReportQueue;
using Contracts.DTO.Moderation;
using Domain.Shared.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/threads/reports")]
public class ModerationController : ControllerBase
{
    private readonly ISender _sender;
    public ModerationController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ModerationReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Queue([FromQuery] ReportStatus status = ReportStatus.Open, CancellationToken ct = default)
        => Ok(await _sender.Send(new GetReportQueueQuery(status), ct));

    [HttpPost("{reportId:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Resolve(Guid reportId, [FromBody] ResolveReportRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();
        try
        {
            await _sender.Send(new ResolveReportCommand(reportId, adminId, request.Action), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out userId);
    }
}
```

- [ ] **Step 3: Build + full suite green. Commit** — `git add src/Api/Controllers/ThreadsController.cs src/Api/Controllers/ModerationController.cs && git commit -m "feat: report endpoint + admin moderation controller"`

---

## Task 8: Notification service + consumer

**Files:** `src/Application/Notifications/NotificationService.cs`; `src/Infrastructure/Events/ThreadNotificationConsumer.cs`. Test: `tests/Application.UnitTests/Notifications/NotificationServiceTests.cs`

The plain `NotificationService` holds the logic (DB-existence idempotency, recipient resolution, anonymity snapshot); the MassTransit consumer is thin. On `ThreadCommentCreated`: top-level comment → notify post author (`PostReplied`); reply → notify parent comment author (`CommentReplied`); skip self-notification; skip if a notification for that comment already exists.

- [ ] **Step 1: Failing test**

```csharp
using Application.Notifications;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Notifications;

public sealed class NotificationServiceTests
{
    private static User NewUser(Guid id, string? anon = null)
    {
        var u = new User(id, $"{id:N}@adelaide.edu.au", "Name", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true);
        if (anon is not null) u.AssignAnonHandle(anon);
        return u;
    }

    private static async Task<(MarketplaceDbContext db, Guid postAuthor, ThreadPost post)> SeedPost(TestDb t)
    {
        var postAuthor = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(NewUser(postAuthor));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, postAuthor, false, "T", "B");
        t.Context.ThreadPosts.Add(post);
        await t.Context.SaveChangesAsync();
        return (t.Context, postAuthor, post);
    }

    [Fact]
    public async Task Top_level_comment_notifies_post_author()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, postAuthor, post) = await SeedPost(t);
        var commenter = Guid.NewGuid();
        db.Users.Add(NewUser(commenter));
        var comment = new ThreadComment(Guid.NewGuid(), post.Id, null, commenter, false, "hi");
        db.ThreadComments.Add(comment);
        await db.SaveChangesAsync();

        await new NotificationService(db).OnCommentCreatedAsync(post.Id, comment.Id, default);

        var notif = await db.Notifications.SingleAsync();
        Assert.Equal(postAuthor, notif.RecipientUserId);
        Assert.Equal(NotificationType.PostReplied, notif.Type);
        Assert.Equal(commenter, notif.ActorUserId);
    }

    [Fact]
    public async Task Anonymous_commenter_stores_handle_snapshot_not_user()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, postAuthor, post) = await SeedPost(t);
        var commenter = Guid.NewGuid();
        db.Users.Add(NewUser(commenter, anon: "quiet-koala-4821"));
        var comment = new ThreadComment(Guid.NewGuid(), post.Id, null, commenter, isAnonymous: true, "hi");
        db.ThreadComments.Add(comment);
        await db.SaveChangesAsync();

        await new NotificationService(db).OnCommentCreatedAsync(post.Id, comment.Id, default);

        var notif = await db.Notifications.SingleAsync();
        Assert.Null(notif.ActorUserId);
        Assert.Equal("quiet-koala-4821", notif.ActorAnonHandleSnapshot);
    }

    [Fact]
    public async Task Reply_notifies_parent_comment_author()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, _, post) = await SeedPost(t);
        var parentAuthor = Guid.NewGuid(); var replier = Guid.NewGuid();
        db.Users.Add(NewUser(parentAuthor)); db.Users.Add(NewUser(replier));
        var parent = new ThreadComment(Guid.NewGuid(), post.Id, null, parentAuthor, false, "parent");
        db.ThreadComments.Add(parent);
        var reply = new ThreadComment(Guid.NewGuid(), post.Id, parent.Id, replier, false, "reply");
        db.ThreadComments.Add(reply);
        await db.SaveChangesAsync();

        await new NotificationService(db).OnCommentCreatedAsync(post.Id, reply.Id, default);

        var notif = await db.Notifications.SingleAsync(n => n.SourceCommentId == reply.Id);
        Assert.Equal(parentAuthor, notif.RecipientUserId);
        Assert.Equal(NotificationType.CommentReplied, notif.Type);
    }

    [Fact]
    public async Task Self_reply_creates_no_notification()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, postAuthor, post) = await SeedPost(t);
        var comment = new ThreadComment(Guid.NewGuid(), post.Id, null, postAuthor, false, "my own");
        db.ThreadComments.Add(comment);
        await db.SaveChangesAsync();

        await new NotificationService(db).OnCommentCreatedAsync(post.Id, comment.Id, default);
        Assert.Equal(0, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Duplicate_delivery_is_idempotent()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, _, post) = await SeedPost(t);
        var commenter = Guid.NewGuid();
        db.Users.Add(NewUser(commenter));
        var comment = new ThreadComment(Guid.NewGuid(), post.Id, null, commenter, false, "hi");
        db.ThreadComments.Add(comment);
        await db.SaveChangesAsync();

        var svc = new NotificationService(db);
        await svc.OnCommentCreatedAsync(post.Id, comment.Id, default);
        await svc.OnCommentCreatedAsync(post.Id, comment.Id, default); // redelivery
        Assert.Equal(1, await db.Notifications.CountAsync());
    }
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: Implement the service**

`src/Application/Notifications/NotificationService.cs`:
```csharp
using Application.Common.Interfaces;
using Domain.Entities.Notifications;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Notifications;

public sealed class NotificationService
{
    private readonly IApplicationDbContext _db;
    public NotificationService(IApplicationDbContext db) => _db = db;

    public async Task OnCommentCreatedAsync(Guid postId, Guid commentId, CancellationToken ct)
    {
        // DB-existence idempotency: at most one reply-notification per source comment.
        if (await _db.Notifications.AnyAsync(n => n.SourceCommentId == commentId, ct))
        {
            return;
        }

        var comment = await _db.ThreadComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == commentId, ct);
        if (comment is null)
        {
            return;
        }

        Guid recipientId;
        NotificationType type;

        if (comment.ParentCommentId is { } parentId)
        {
            var parent = await _db.ThreadComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == parentId, ct);
            if (parent is null) return;
            recipientId = parent.AuthorUserId;
            type = NotificationType.CommentReplied;
        }
        else
        {
            var post = await _db.ThreadPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId, ct);
            if (post is null) return;
            recipientId = post.AuthorUserId;
            type = NotificationType.PostReplied;
        }

        if (recipientId == comment.AuthorUserId)
        {
            return; // no self-notifications
        }

        // Preserve anonymity: snapshot the actor's anon handle instead of their identity.
        Guid? actorUserId = null;
        string? actorHandle = null;
        if (comment.IsAnonymous)
        {
            var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == comment.AuthorUserId, ct);
            actorHandle = actor?.AnonHandle ?? "anonymous";
        }
        else
        {
            actorUserId = comment.AuthorUserId;
        }

        _db.Notifications.Add(Notification.ForReply(recipientId, type, postId, comment.Id, actorUserId, actorHandle));
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Run — PASS (5).**

- [ ] **Step 5: Consumer**

`src/Infrastructure/Events/ThreadNotificationConsumer.cs`:
```csharp
using Application.Notifications;
using Contracts.Events.Threads;
using MassTransit;

namespace Infrastructure.Events;

public sealed class ThreadNotificationConsumer : IConsumer<ThreadCommentCreated>
{
    private readonly NotificationService _service;
    public ThreadNotificationConsumer(NotificationService service) => _service = service;

    public Task Consume(ConsumeContext<ThreadCommentCreated> context)
        => _service.OnCommentCreatedAsync(context.Message.PostId, context.Message.CommentId, context.CancellationToken);
}
```
> Both `ThreadIndexingConsumer` and `ThreadNotificationConsumer` consume `ThreadCommentCreated`; MassTransit's `ConfigureEndpoints` gives each its own queue, so both run independently.

- [ ] **Step 6: Build + full green. Commit** — `git add src/Application/Notifications/NotificationService.cs src/Infrastructure/Events/ThreadNotificationConsumer.cs tests/Application.UnitTests/Notifications/NotificationServiceTests.cs && git commit -m "feat: reply-notification service + consumer"`

---

## Task 9: Notification read APIs (list / unread-count / mark-read / mark-all)

**Files:** `src/Contracts/DTO/Notifications/{NotificationResponse,NotificationActor,UnreadCountResponse}.cs`; `Notifications/Queries/{GetNotifications,GetUnreadCount}/*`; `Notifications/Commands/{MarkNotificationRead,MarkAllNotificationsRead}/*`. Test: `tests/Application.UnitTests/Notifications/NotificationReadTests.cs`

The notification list must NOT leak identity for anonymous actors — it carries a `NotificationActor` that exposes only the handle for anon actors and real identity for named actors (same guarantee as `AuthorRef`).

- [ ] **Step 1: Failing test**

```csharp
using Application.Notifications.Commands.MarkAllNotificationsRead;
using Application.Notifications.Commands.MarkNotificationRead;
using Application.Notifications.Queries.GetNotifications;
using Application.Notifications.Queries.GetUnreadCount;
using Application.UnitTests.Common;
using Domain.Entities.Notifications;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Notifications;

public sealed class NotificationReadTests
{
    private static User NewUser(Guid id) => new(id, $"{id:N}@adelaide.edu.au", "Name", DateTimeOffset.UtcNow, "Student",
        "hash", AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true);

    [Fact]
    public async Task List_returns_recipient_notifications_with_anon_actor_hidden()
    {
        await using var t = await TestDb.CreateAsync();
        var recipient = Guid.NewGuid(); var postId = Guid.NewGuid();
        t.Context.Notifications.Add(Notification.ForReply(recipient, NotificationType.PostReplied, postId, Guid.NewGuid(),
            actorUserId: null, actorAnonHandle: "quiet-koala-4821"));
        await t.Context.SaveChangesAsync();

        var list = await new GetNotificationsQueryHandler(t.Context).Handle(new GetNotificationsQuery(recipient, null, 20), default);

        var item = Assert.Single(list.Items);
        Assert.True(item.Actor.IsAnonymous);
        Assert.Equal("quiet-koala-4821", item.Actor.Handle);
        Assert.Null(item.Actor.UserId);
    }

    [Fact]
    public async Task Unread_count_counts_only_unread_for_recipient()
    {
        await using var t = await TestDb.CreateAsync();
        var recipient = Guid.NewGuid();
        var read = Notification.ForReply(recipient, NotificationType.PostReplied, Guid.NewGuid(), null, Guid.NewGuid(), null);
        read.MarkRead();
        t.Context.Notifications.Add(read);
        t.Context.Notifications.Add(Notification.ForReply(recipient, NotificationType.PostReplied, Guid.NewGuid(), null, Guid.NewGuid(), null));
        t.Context.Notifications.Add(Notification.ForReply(Guid.NewGuid(), NotificationType.PostReplied, Guid.NewGuid(), null, Guid.NewGuid(), null));
        await t.Context.SaveChangesAsync();

        var count = await new GetUnreadCountQueryHandler(t.Context).Handle(new GetUnreadCountQuery(recipient), default);
        Assert.Equal(1, count.UnreadCount);
    }

    [Fact]
    public async Task Mark_read_only_affects_own_notification()
    {
        await using var t = await TestDb.CreateAsync();
        var recipient = Guid.NewGuid();
        var n = Notification.ForReply(recipient, NotificationType.PostReplied, Guid.NewGuid(), null, Guid.NewGuid(), null);
        t.Context.Notifications.Add(n);
        await t.Context.SaveChangesAsync();

        await new MarkNotificationReadCommandHandler(t.Context).Handle(new MarkNotificationReadCommand(recipient, n.Id), default);
        Assert.True((await t.Context.Notifications.SingleAsync()).IsRead);

        // A different user cannot mark it (no-op, no throw)
        await new MarkNotificationReadCommandHandler(t.Context).Handle(new MarkNotificationReadCommand(Guid.NewGuid(), n.Id), default);
    }

    [Fact]
    public async Task Mark_all_marks_every_unread_for_recipient()
    {
        await using var t = await TestDb.CreateAsync();
        var recipient = Guid.NewGuid();
        t.Context.Notifications.Add(Notification.ForReply(recipient, NotificationType.PostReplied, Guid.NewGuid(), null, Guid.NewGuid(), null));
        t.Context.Notifications.Add(Notification.ForReply(recipient, NotificationType.CommentReplied, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null));
        await t.Context.SaveChangesAsync();

        await new MarkAllNotificationsReadCommandHandler(t.Context).Handle(new MarkAllNotificationsReadCommand(recipient), default);
        Assert.Equal(0, await t.Context.Notifications.CountAsync(n => !n.IsRead));
    }
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: DTOs**

`src/Contracts/DTO/Notifications/NotificationActor.cs`:
```csharp
namespace Contracts.DTO.Notifications;

public sealed record NotificationActor(bool IsAnonymous, string? Handle, Guid? UserId, string? DisplayName);
```
`src/Contracts/DTO/Notifications/NotificationResponse.cs`:
```csharp
using Domain.Shared.Enums;

namespace Contracts.DTO.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    NotificationType Type,
    Guid SourcePostId,
    Guid? SourceCommentId,
    NotificationActor Actor,
    bool IsRead,
    DateTimeOffset CreatedAt);

public sealed record NotificationListResponse(IReadOnlyList<NotificationResponse> Items, string? NextCursor);
```
`src/Contracts/DTO/Notifications/UnreadCountResponse.cs`:
```csharp
namespace Contracts.DTO.Notifications;

public sealed record UnreadCountResponse(int UnreadCount);
```

- [ ] **Step 4: Queries**

`src/Application/Notifications/Queries/GetNotifications/GetNotificationsQuery.cs`:
```csharp
using Contracts.DTO.Notifications;
using MediatR;

namespace Application.Notifications.Queries.GetNotifications;

public sealed record GetNotificationsQuery(Guid RecipientUserId, string? Cursor, int PageSize)
    : IRequest<NotificationListResponse>;
```
`src/Application/Notifications/Queries/GetNotifications/GetNotificationsQueryHandler.cs`:
```csharp
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

        var query = _db.Notifications
            .AsNoTracking()
            .Where(x => x.RecipientUserId == request.RecipientUserId)
            .OrderByDescending(x => x.CreatedAt);

        var rows = await query.Skip(offset).Take(pageSize + 1).ToListAsync(ct);
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
```
`src/Application/Notifications/Queries/GetUnreadCount/GetUnreadCountQuery.cs`:
```csharp
using Contracts.DTO.Notifications;
using MediatR;

namespace Application.Notifications.Queries.GetUnreadCount;

public sealed record GetUnreadCountQuery(Guid RecipientUserId) : IRequest<UnreadCountResponse>;
```
`src/Application/Notifications/Queries/GetUnreadCount/GetUnreadCountQueryHandler.cs`:
```csharp
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
```

- [ ] **Step 5: Commands**

`src/Application/Notifications/Commands/MarkNotificationRead/MarkNotificationReadCommand.cs`:
```csharp
using MediatR;

namespace Application.Notifications.Commands.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(Guid RecipientUserId, Guid NotificationId) : IRequest;
```
`src/Application/Notifications/Commands/MarkNotificationRead/MarkNotificationReadCommandHandler.cs`:
```csharp
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
```
`src/Application/Notifications/Commands/MarkAllNotificationsRead/MarkAllNotificationsReadCommand.cs`:
```csharp
using MediatR;

namespace Application.Notifications.Commands.MarkAllNotificationsRead;

public sealed record MarkAllNotificationsReadCommand(Guid RecipientUserId) : IRequest;
```
`src/Application/Notifications/Commands/MarkAllNotificationsRead/MarkAllNotificationsReadCommandHandler.cs`:
```csharp
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
```

- [ ] **Step 6: Run — PASS (4). Build + full green. Commit** — `git add src/Contracts/DTO/Notifications/ src/Application/Notifications/Queries/ src/Application/Notifications/Commands/ tests/Application.UnitTests/Notifications/NotificationReadTests.cs && git commit -m "feat: notification read APIs (list, unread count, mark read/all)"`

---

## Task 10: NotificationsController

**Files:** `src/Api/Controllers/NotificationsController.cs`.

- [ ] **Step 1: Implement**

```csharp
using Application.Notifications.Commands.MarkAllNotificationsRead;
using Application.Notifications.Commands.MarkNotificationRead;
using Application.Notifications.Queries.GetNotifications;
using Application.Notifications.Queries.GetUnreadCount;
using Contracts.DTO.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly ISender _sender;
    public NotificationsController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType(typeof(NotificationListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? cursor = null, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await _sender.Send(new GetNotificationsQuery(userId, cursor, pageSize), ct));
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await _sender.Send(new GetUnreadCountQuery(userId), ct));
    }

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await _sender.Send(new MarkNotificationReadCommand(userId, id), ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await _sender.Send(new MarkAllNotificationsReadCommand(userId), ct);
        return NoContent();
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out userId);
    }
}
```

- [ ] **Step 2: Build + full green. Commit** — `git add src/Api/Controllers/NotificationsController.cs && git commit -m "feat: notifications controller"`

---

## Task 11: DI wiring + consumer registration

**Files:** `src/Api/Program.cs`.

- [ ] **Step 1: Register services + the notification consumer**

After the existing read-path registrations, add:
```csharp
builder.Services.AddScoped<Application.Common.Interfaces.IReportRateLimiter, Infrastructure.Caching.RedisReportRateLimiter>();
builder.Services.AddScoped<Application.Notifications.NotificationService>();
```
In the `AddMassTransit(x => { ... })` block, add next to the existing consumers:
```csharp
    x.AddConsumer<Infrastructure.Events.ThreadNotificationConsumer>();
```

- [ ] **Step 2: Build — must succeed. Full unit suite green (uses fakes, not these registrations).**

- [ ] **Step 3: Commit** — `git add src/Api/Program.cs && git commit -m "chore: wire moderation + notification services"`

---

## Task 12: Docs + final verification

**Files:** `README.md`, `AGENTS.md`.

- [ ] **Step 1: Build + FULL unit suite green.** Report the exact count.

- [ ] **Step 2: Docs** — in `README.md` and `AGENTS.md`, add a "Moderation & Notifications" section:
```
POST   /api/threads/posts/{id}/report            file a report on a post (rate-limited 10/hr)
POST   /api/threads/comments/{id}/report         file a report on a comment (rate-limited 10/hr)
GET    /api/threads/reports?status=open          [admin] review queue (reveals real author of anon content)
POST   /api/threads/reports/{id}/resolve         [admin] dismiss | remove-content | warn-user (audited)
GET    /api/notifications                         your reply notifications (paginated)
GET    /api/notifications/unread-count            unread badge count
POST   /api/notifications/{id}/read               mark one read
POST   /api/notifications/read-all                mark all read
```
Note: the admin review queue is the single deliberate "anon-break" (moderators see the real author of anonymous content); it is role-gated and every resolve action is written to `moderation_audits`. Reply notifications are created asynchronously by `ThreadNotificationConsumer` reacting to `ThreadCommentCreated`; anonymous repliers appear only by their stable handle (identity is never stored in the notification).

- [ ] **Step 3: (Best-effort) docker end-to-end** — if docker + `backend/.env` available: bring up the stack, comment on a post as user B, confirm user A gets a notification and the unread count increments; report a post, resolve it as admin, confirm it disappears from the feed. Else skip + note.

- [ ] **Step 4: Commit** — `git add README.md AGENTS.md && git commit -m "docs: document moderation + notifications"`

---

## Done criteria

- Build green; full unit suite green (moderation aggregates, rate limiter, create/queue/resolve report, notification service idempotency + anon snapshot, notification read APIs).
- Users can report posts/comments (rate-limited); admins see a review queue that reveals the real author of anonymous content; admins resolve with dismiss / remove-content (soft-deletes + enqueues the delete event so Elasticsearch drops it) / warn-user; every resolution writes a `moderation_audits` row.
- Replies generate in-app notifications (post author for top-level, parent author for replies; no self-notify; idempotent on redelivery); anonymous repliers never leak identity into notifications; users can list, count-unread, and mark read/all.
- The public anon-leak contract test (Plan 2/3) is unaffected — the moderation anon-break lives only behind the admin role-gated queue with its own `ModerationAuthor` DTO.

## Out of scope (future)
- Email/push delivery of notifications (APNs/FCM) — only in-app here.
- Auto-moderation / spam scoring, user bans/suspensions (warn-user is a logged no-op for now).
- Report deduplication (multiple users reporting the same target create multiple rows — fine for a queue; could group later).
