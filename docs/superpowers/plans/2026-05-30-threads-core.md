# Threads Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Reddit-style Threads subsystem on Postgres — admin-curated categories, posts (with images + per-post real/anonymous identity), 2-level nested comments, likes, and a feed — with the API enforcing the anonymity trust model so anonymous content never leaks its author.

**Architecture:** A new `Threads` bounded context inside the existing clean-architecture backend, mirroring the `Items` vertical (private-ctor aggregates with mutation methods, MediatR CQRS, `IEntityTypeConfiguration` EF mapping, controllers using the shared `TryGetUserId` helper). Postgres is the source of truth. The feed is served from Postgres in this plan (Hot/New/Top with cursor pagination) and is **provisional** — Plan 3 (Read Path) swaps it to an Elasticsearch read model via a RabbitMQ outbox. Reports/notifications are Plan 4.

**Tech Stack:** ASP.NET Core 8, EF Core (Npgsql), MediatR, FluentValidation, Mapster, Cloudflare R2 (existing `IObjectStorageService`), xUnit + SQLite in-memory.

**Spec:** `docs/superpowers/specs/2026-05-29-threads-and-identity-overhaul-design.md` (Sections 4 data model, 5 API + author-resolution rule, 8 moderation soft-delete, 10 anon-leak contract test).

**Prereqs (already merged in Plan 1):** `User.AnonHandle` + `AssignAnonHandle`, `GetOrCreateAnonHandleCommand` (DB collision retry), shared `Application.UnitTests.Common.TestDb`, `IApplicationDbContext`, `IObjectStorageService` (R2).

---

## Conventions (read once)

- **Build:** `dotnet build Marketplace.sln --no-restore /m:1 /p:BuildInParallel=false`
- **Test (all):** `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false`
- **Test (filter):** append `--filter "FullyQualifiedName~<ClassName>"`
- **Migrations live in** `src/Infrastructure/Migrations/` (NOT `Data/Migrations`). Generate with:
  `ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations add <Name> --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj --output-dir Migrations`
- **Table names are snake_case** (`.ToTable("thread_posts")`); **column names are PascalCase** (EF default) — match the existing `ItemConfiguration`/`UserConfiguration`.
- **Entities:** private parameterless ctor + public ctor + private setters + mutation methods (see `Domain/Entities/Items/Item.cs`).
- **All new `DbSet<>`s** go on `IApplicationDbContext` AND `MarketplaceDbContext`.
- Register nothing new in DI unless a task says so (MediatR/validators auto-scan).

## File Structure

**Domain** (`src/Domain/Entities/Threads/`): `ThreadCategory.cs`, `ThreadPost.cs`, `ThreadPostImage.cs`, `ThreadComment.cs`, `ThreadLike.cs`, plus `src/Domain/Shared/Enums/ThreadLikeTarget.cs`.

**Infrastructure** (`src/Infrastructure/Data/Configurations/`): `ThreadCategoryConfiguration.cs`, `ThreadPostConfiguration.cs`, `ThreadPostImageConfiguration.cs`, `ThreadCommentConfiguration.cs`, `ThreadLikeConfiguration.cs`. Plus one migration `AddThreads`.

**Contracts** (`src/Contracts/DTO/Threads/`): `ThreadCategoryResponse.cs`, `AuthorRef.cs`, `ThreadPostSummary.cs`, `ThreadPostDetailResponse.cs`, `ThreadCommentResponse.cs`, `CreateThreadPostRequest.cs`, `UpdateThreadPostRequest.cs`, `CreateThreadCommentRequest.cs`, `ThreadFeedResponse.cs`, `LikeResponse.cs`, `Create/Update category requests`.

**Application** (`src/Application/Threads/`): `AuthorRefFactory.cs` (the anonymity guard) + Commands/Queries folders per use case.

**Api** (`src/Api/Controllers/`): `ThreadsController.cs`, `ThreadCategoriesController.cs`.

**Tests** (`tests/Application.UnitTests/Threads/`): one test file per behavior + `tests/Application.UnitTests/Threads/AnonLeakContractTests.cs`.

---

## Task 1: ThreadCategory aggregate

**Files:**
- Create: `src/Domain/Entities/Threads/ThreadCategory.cs`
- Test: `tests/Application.UnitTests/Threads/ThreadCategoryTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Domain.Entities.Threads;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class ThreadCategoryTests
{
    [Fact]
    public void New_category_is_active_by_default()
    {
        var c = new ThreadCategory(Guid.NewGuid(), "housemate", "Housemate", "Find a room or flatmate", "home", 10);

        Assert.Equal("housemate", c.Slug);
        Assert.Equal("Housemate", c.Name);
        Assert.True(c.IsActive);
        Assert.Equal(10, c.SortOrder);
    }

    [Fact]
    public void Update_changes_display_fields_but_not_slug()
    {
        var c = new ThreadCategory(Guid.NewGuid(), "housemate", "Housemate", "old", "home", 10);

        c.Update("Housemates", "Find a room or flatmate", "house", 5, isActive: false);

        Assert.Equal("housemate", c.Slug);
        Assert.Equal("Housemates", c.Name);
        Assert.Equal("house", c.IconKey);
        Assert.Equal(5, c.SortOrder);
        Assert.False(c.IsActive);
    }
}
```

- [ ] **Step 2: Run it — FAIL (type missing).** `... --filter "FullyQualifiedName~ThreadCategoryTests"`

- [ ] **Step 3: Implement**

```csharp
namespace Domain.Entities.Threads;

public class ThreadCategory
{
    private ThreadCategory() { }

    public ThreadCategory(Guid id, string slug, string name, string description, string iconKey, int sortOrder)
    {
        Id = id;
        Slug = slug;
        Name = name;
        Description = description;
        IconKey = iconKey;
        SortOrder = sortOrder;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string IconKey { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void Update(string name, string description, string iconKey, int sortOrder, bool isActive)
    {
        Name = name;
        Description = description;
        IconKey = iconKey;
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 4: Run — PASS.**
- [ ] **Step 5: Commit** — `git add src/Domain/Entities/Threads/ThreadCategory.cs tests/Application.UnitTests/Threads/ThreadCategoryTests.cs && git commit -m "feat: add ThreadCategory aggregate"`

---

## Task 2: ThreadPost + ThreadPostImage aggregates

**Files:**
- Create: `src/Domain/Entities/Threads/ThreadPost.cs`, `src/Domain/Entities/Threads/ThreadPostImage.cs`
- Test: `tests/Application.UnitTests/Threads/ThreadPostTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Domain.Entities.Threads;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class ThreadPostTests
{
    private static ThreadPost NewPost(bool anon = false) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), anon, "Title", "Body");

    [Fact]
    public void New_post_defaults()
    {
        var p = NewPost();
        Assert.Equal(0, p.LikeCount);
        Assert.Equal(0, p.CommentCount);
        Assert.False(p.IsDeleted);
        Assert.False(p.IsPinned);
        Assert.False(p.IsLocked);
        Assert.Equal(p.CreatedAt, p.LastActivityAt);
    }

    [Fact]
    public void UpdateContent_changes_title_body_only()
    {
        var p = NewPost();
        var before = p.IsAnonymous;
        p.UpdateContent("New", "NewBody");
        Assert.Equal("New", p.Title);
        Assert.Equal("NewBody", p.Body);
        Assert.Equal(before, p.IsAnonymous); // anonymity is immutable
    }

    [Fact]
    public void Like_count_adjusts_and_never_negative()
    {
        var p = NewPost();
        p.AdjustLikeCount(+1);
        p.AdjustLikeCount(+1);
        p.AdjustLikeCount(-1);
        Assert.Equal(1, p.LikeCount);
        p.AdjustLikeCount(-5);
        Assert.Equal(0, p.LikeCount);
    }

    [Fact]
    public void Adding_comment_bumps_count_and_activity()
    {
        var p = NewPost();
        var t0 = p.LastActivityAt;
        p.RegisterCommentAdded(DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(1, p.CommentCount);
        Assert.True(p.LastActivityAt > t0);
    }

    [Fact]
    public void SoftDelete_marks_deleted()
    {
        var p = NewPost();
        p.SoftDelete();
        Assert.True(p.IsDeleted);
    }
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: Implement `ThreadPostImage` then `ThreadPost`**

`ThreadPostImage.cs`:
```csharp
namespace Domain.Entities.Threads;

public class ThreadPostImage
{
    private ThreadPostImage() { }

    public ThreadPostImage(Guid id, Guid postId, string r2Key, int ordinal)
    {
        Id = id;
        PostId = postId;
        R2Key = r2Key;
        Ordinal = ordinal;
    }

    public Guid Id { get; private set; }
    public Guid PostId { get; private set; }
    public string R2Key { get; private set; } = string.Empty;
    public int Ordinal { get; private set; }
}
```

`ThreadPost.cs`:
```csharp
using Domain.Entities.Users;

namespace Domain.Entities.Threads;

public class ThreadPost
{
    private readonly List<ThreadPostImage> _images = new();

    private ThreadPost() { }

    public ThreadPost(Guid id, Guid categoryId, Guid authorUserId, bool isAnonymous, string title, string body)
    {
        Id = id;
        CategoryId = categoryId;
        AuthorUserId = authorUserId;
        IsAnonymous = isAnonymous;
        Title = title;
        Body = body;
        CreatedAt = DateTimeOffset.UtcNow;
        LastActivityAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public bool IsAnonymous { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public int LikeCount { get; private set; }
    public int CommentCount { get; private set; }
    public DateTimeOffset LastActivityAt { get; private set; }
    public bool IsPinned { get; private set; }
    public bool IsLocked { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public ThreadCategory? Category { get; private set; }
    public User? Author { get; private set; }
    public IReadOnlyCollection<ThreadPostImage> Images => _images;

    public void UpdateContent(string title, string body)
    {
        Title = title;
        Body = body;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddImage(ThreadPostImage image) => _images.Add(image);

    public void AdjustLikeCount(int delta)
    {
        LikeCount += delta;
        if (LikeCount < 0) LikeCount = 0;
    }

    public void RegisterCommentAdded(DateTimeOffset at)
    {
        CommentCount += 1;
        LastActivityAt = at;
    }

    public void RegisterCommentRemoved()
    {
        CommentCount -= 1;
        if (CommentCount < 0) CommentCount = 0;
    }

    public void SoftDelete() => IsDeleted = true;
    public void SetLocked(bool locked) => IsLocked = locked;
    public void SetPinned(bool pinned) => IsPinned = pinned;
}
```

- [ ] **Step 4: Run — PASS (5 tests).**
- [ ] **Step 5: Commit** — `git add src/Domain/Entities/Threads/ThreadPost.cs src/Domain/Entities/Threads/ThreadPostImage.cs tests/Application.UnitTests/Threads/ThreadPostTests.cs && git commit -m "feat: add ThreadPost and ThreadPostImage aggregates"`

---

## Task 3: ThreadComment aggregate (2-level)

**Files:**
- Create: `src/Domain/Entities/Threads/ThreadComment.cs`
- Test: `tests/Application.UnitTests/Threads/ThreadCommentTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Domain.Entities.Threads;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class ThreadCommentTests
{
    [Fact]
    public void Top_level_comment_has_no_parent()
    {
        var c = new ThreadComment(Guid.NewGuid(), Guid.NewGuid(), parentCommentId: null, Guid.NewGuid(), isAnonymous: false, "hi");
        Assert.Null(c.ParentCommentId);
        Assert.False(c.IsDeleted);
        Assert.Equal(0, c.LikeCount);
    }

    [Fact]
    public void Reply_carries_parent()
    {
        var parentId = Guid.NewGuid();
        var c = new ThreadComment(Guid.NewGuid(), Guid.NewGuid(), parentId, Guid.NewGuid(), true, "reply");
        Assert.Equal(parentId, c.ParentCommentId);
    }

    [Fact]
    public void Like_count_never_negative()
    {
        var c = new ThreadComment(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), false, "x");
        c.AdjustLikeCount(+1);
        c.AdjustLikeCount(-3);
        Assert.Equal(0, c.LikeCount);
    }
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: Implement**

```csharp
using Domain.Entities.Users;

namespace Domain.Entities.Threads;

public class ThreadComment
{
    private ThreadComment() { }

    public ThreadComment(Guid id, Guid postId, Guid? parentCommentId, Guid authorUserId, bool isAnonymous, string body)
    {
        Id = id;
        PostId = postId;
        ParentCommentId = parentCommentId;
        AuthorUserId = authorUserId;
        IsAnonymous = isAnonymous;
        Body = body;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid PostId { get; private set; }
    public Guid? ParentCommentId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public bool IsAnonymous { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public int LikeCount { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public User? Author { get; private set; }

    public void UpdateBody(string body)
    {
        Body = body;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AdjustLikeCount(int delta)
    {
        LikeCount += delta;
        if (LikeCount < 0) LikeCount = 0;
    }

    public void SoftDelete() => IsDeleted = true;
}
```

- [ ] **Step 4: Run — PASS.**
- [ ] **Step 5: Commit** — `git add src/Domain/Entities/Threads/ThreadComment.cs tests/Application.UnitTests/Threads/ThreadCommentTests.cs && git commit -m "feat: add ThreadComment aggregate"`

---

## Task 4: ThreadLike aggregate + target enum

**Files:**
- Create: `src/Domain/Shared/Enums/ThreadLikeTarget.cs`, `src/Domain/Entities/Threads/ThreadLike.cs`
- Test: `tests/Application.UnitTests/Threads/ThreadLikeTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Domain.Entities.Threads;
using Domain.Shared.Enums;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class ThreadLikeTests
{
    [Fact]
    public void Like_captures_user_target_and_type()
    {
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var like = new ThreadLike(userId, ThreadLikeTarget.Post, targetId);

        Assert.Equal(userId, like.UserId);
        Assert.Equal(ThreadLikeTarget.Post, like.TargetType);
        Assert.Equal(targetId, like.TargetId);
        Assert.True(like.CreatedAt <= DateTimeOffset.UtcNow);
    }
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: Implement**

`ThreadLikeTarget.cs`:
```csharp
namespace Domain.Shared.Enums;

public enum ThreadLikeTarget
{
    Post = 0,
    Comment = 1
}
```

`ThreadLike.cs`:
```csharp
using Domain.Shared.Enums;

namespace Domain.Entities.Threads;

public class ThreadLike
{
    private ThreadLike() { }

    public ThreadLike(Guid userId, ThreadLikeTarget targetType, Guid targetId)
    {
        UserId = userId;
        TargetType = targetType;
        TargetId = targetId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; private set; }
    public ThreadLikeTarget TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
```

- [ ] **Step 4: Run — PASS.**
- [ ] **Step 5: Commit** — `git add src/Domain/Shared/Enums/ThreadLikeTarget.cs src/Domain/Entities/Threads/ThreadLike.cs tests/Application.UnitTests/Threads/ThreadLikeTests.cs && git commit -m "feat: add ThreadLike aggregate"`

---

## Task 5: EF configurations + DbSets + migration

**Files:**
- Create: 5 configuration files under `src/Infrastructure/Data/Configurations/`
- Modify: `src/Application/Common/Interfaces/IApplicationDbContext.cs`, `src/Infrastructure/Data/MarketplaceDbContext.cs`
- Create: migration `AddThreads`

- [ ] **Step 1: Add DbSets to the interface**

In `src/Application/Common/Interfaces/IApplicationDbContext.cs` add `using Domain.Entities.Threads;` and these properties to the interface:
```csharp
    DbSet<ThreadCategory> ThreadCategories { get; }
    DbSet<ThreadPost> ThreadPosts { get; }
    DbSet<ThreadPostImage> ThreadPostImages { get; }
    DbSet<ThreadComment> ThreadComments { get; }
    DbSet<ThreadLike> ThreadLikes { get; }
```

- [ ] **Step 2: Add DbSets to the context**

In `src/Infrastructure/Data/MarketplaceDbContext.cs` add `using Domain.Entities.Threads;` and matching `public DbSet<...> ... => Set<...>();` (match the existing style in that file — check whether it uses `{ get; set; }` auto-props or `Set<T>()` expression members and mirror it). Configurations are applied via `ApplyConfigurationsFromAssembly` (confirm the context already calls it in `OnModelCreating`; the existing Item/User configs are picked up that way, so new `IEntityTypeConfiguration` classes are auto-registered — no manual wiring needed).

- [ ] **Step 3: Create the 5 configuration files**

`ThreadCategoryConfiguration.cs`:
```csharp
using Domain.Entities.Threads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ThreadCategoryConfiguration : IEntityTypeConfiguration<ThreadCategory>
{
    public void Configure(EntityTypeBuilder<ThreadCategory> builder)
    {
        builder.ToTable("thread_categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(64);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(128);
        builder.Property(c => c.Description).IsRequired().HasMaxLength(512);
        builder.Property(c => c.IconKey).IsRequired().HasMaxLength(64);
        builder.Property(c => c.SortOrder).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(c => c.UpdatedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(c => c.Slug).IsUnique();
    }
}
```

`ThreadPostConfiguration.cs`:
```csharp
using Domain.Entities.Threads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ThreadPostConfiguration : IEntityTypeConfiguration<ThreadPost>
{
    public void Configure(EntityTypeBuilder<ThreadPost> builder)
    {
        builder.ToTable("thread_posts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.IsAnonymous).IsRequired();
        builder.Property(p => p.Title).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Body).IsRequired();
        builder.Property(p => p.LikeCount).IsRequired();
        builder.Property(p => p.CommentCount).IsRequired();
        builder.Property(p => p.LastActivityAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(p => p.IsPinned).IsRequired();
        builder.Property(p => p.IsLocked).IsRequired();
        builder.Property(p => p.IsDeleted).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(p => p.UpdatedAt).HasColumnType("timestamp with time zone");

        builder.HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Images)
            .WithOne()
            .HasForeignKey(i => i.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // Feed fallback ordering + "my posts"
        builder.HasIndex(p => new { p.CategoryId, p.LastActivityAt });
        builder.HasIndex(p => new { p.AuthorUserId, p.CreatedAt });
    }
}
```

`ThreadPostImageConfiguration.cs`:
```csharp
using Domain.Entities.Threads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ThreadPostImageConfiguration : IEntityTypeConfiguration<ThreadPostImage>
{
    public void Configure(EntityTypeBuilder<ThreadPostImage> builder)
    {
        builder.ToTable("thread_post_images");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.R2Key).IsRequired().HasMaxLength(512);
        builder.Property(i => i.Ordinal).IsRequired();
        builder.HasIndex(i => i.PostId);
    }
}
```

`ThreadCommentConfiguration.cs`:
```csharp
using Domain.Entities.Threads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ThreadCommentConfiguration : IEntityTypeConfiguration<ThreadComment>
{
    public void Configure(EntityTypeBuilder<ThreadComment> builder)
    {
        builder.ToTable("thread_comments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.IsAnonymous).IsRequired();
        builder.Property(c => c.Body).IsRequired();
        builder.Property(c => c.LikeCount).IsRequired();
        builder.Property(c => c.IsDeleted).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(c => c.UpdatedAt).HasColumnType("timestamp with time zone");

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ThreadPost>()
            .WithMany()
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ThreadComment>()
            .WithMany()
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.PostId, c.CreatedAt });
        builder.HasIndex(c => c.ParentCommentId);
    }
}
```

`ThreadLikeConfiguration.cs`:
```csharp
using Domain.Entities.Threads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

internal sealed class ThreadLikeConfiguration : IEntityTypeConfiguration<ThreadLike>
{
    public void Configure(EntityTypeBuilder<ThreadLike> builder)
    {
        builder.ToTable("thread_likes");
        builder.HasKey(l => new { l.UserId, l.TargetType, l.TargetId });
        builder.Property(l => l.TargetType).HasConversion<int>().IsRequired();
        builder.Property(l => l.CreatedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.HasIndex(l => new { l.TargetType, l.TargetId });
    }
}
```

- [ ] **Step 4: Build** — must succeed.

- [ ] **Step 5: Generate the migration**

`ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations add AddThreads --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj --output-dir Migrations`

Inspect the generated migration: confirm `Up()` creates exactly `thread_categories`, `thread_posts`, `thread_post_images`, `thread_comments`, `thread_likes` with the FKs/indexes above and NO changes to existing tables. If it tries to alter unrelated tables, STOP and report BLOCKED (snapshot drift).

- [ ] **Step 6: Build again + run full test suite (existing tests stay green; SQLite `EnsureCreatedAsync` builds the new tables from the model).**

- [ ] **Step 7: Commit** — `git add src/Application/Common/Interfaces/IApplicationDbContext.cs src/Infrastructure/Data/MarketplaceDbContext.cs src/Infrastructure/Data/Configurations/Thread*.cs src/Infrastructure/Migrations/ && git commit -m "feat: EF mapping + migration for threads schema"`

---

## Task 6: Category seed + admin/read use cases + controller

**Files:**
- Create: `src/Contracts/DTO/Threads/ThreadCategoryResponse.cs`, `CreateThreadCategoryRequest.cs`, `UpdateThreadCategoryRequest.cs`
- Create: `src/Application/Threads/Queries/GetThreadCategories/{Query,Handler}.cs`
- Create: `src/Application/Threads/Commands/CreateThreadCategory/{Command,Handler,Validator}.cs`
- Create: `src/Application/Threads/Commands/UpdateThreadCategory/{Command,Handler,Validator}.cs`
- Create: `src/Api/Controllers/ThreadCategoriesController.cs`
- Modify: `db/seed.sql`
- Test: `tests/Application.UnitTests/Threads/ThreadCategoryUseCaseTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Application.Threads.Commands.CreateThreadCategory;
using Application.Threads.Commands.UpdateThreadCategory;
using Application.Threads.Queries.GetThreadCategories;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class ThreadCategoryUseCaseTests
{
    [Fact]
    public async Task Create_then_list_returns_active_ordered_by_sort()
    {
        await using var db = await TestDb.CreateAsync();
        var create = new CreateThreadCategoryCommandHandler(db.Context);
        await create.Handle(new CreateThreadCategoryCommand("rides", "Rides", "Share a ride", "car", 20), default);
        await create.Handle(new CreateThreadCategoryCommand("housemate", "Housemate", "Find a room", "home", 10), default);

        var list = await new GetThreadCategoriesQueryHandler(db.Context).Handle(new GetThreadCategoriesQuery(), default);

        Assert.Equal(2, list.Count);
        Assert.Equal("housemate", list[0].Slug); // sort order 10 before 20
    }

    [Fact]
    public async Task Inactive_category_is_excluded_from_list()
    {
        await using var db = await TestDb.CreateAsync();
        var cat = new ThreadCategory(Guid.NewGuid(), "events", "Events", "Campus events", "calendar", 30);
        db.Context.ThreadCategories.Add(cat);
        await db.Context.SaveChangesAsync();

        await new UpdateThreadCategoryCommandHandler(db.Context)
            .Handle(new UpdateThreadCategoryCommand(cat.Id, "Events", "Campus events", "calendar", 30, IsActive: false), default);

        var list = await new GetThreadCategoriesQueryHandler(db.Context).Handle(new GetThreadCategoriesQuery(), default);
        Assert.Empty(list);
    }

    [Fact]
    public async Task Create_rejects_duplicate_slug()
    {
        await using var db = await TestDb.CreateAsync();
        var create = new CreateThreadCategoryCommandHandler(db.Context);
        await create.Handle(new CreateThreadCategoryCommand("general", "General", "Anything", "chat", 99), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            create.Handle(new CreateThreadCategoryCommand("general", "General 2", "Dup", "chat", 98), default));
    }
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: DTOs**

`ThreadCategoryResponse.cs`:
```csharp
namespace Contracts.DTO.Threads;

public sealed record ThreadCategoryResponse(
    Guid Id, string Slug, string Name, string Description, string IconKey, int SortOrder, bool IsActive);
```
`CreateThreadCategoryRequest.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Threads;

public sealed class CreateThreadCategoryRequest
{
    [Required, MaxLength(64)] public string Slug { get; init; } = string.Empty;
    [Required, MaxLength(128)] public string Name { get; init; } = string.Empty;
    [Required, MaxLength(512)] public string Description { get; init; } = string.Empty;
    [Required, MaxLength(64)] public string IconKey { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}
```
`UpdateThreadCategoryRequest.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Threads;

public sealed class UpdateThreadCategoryRequest
{
    [Required, MaxLength(128)] public string Name { get; init; } = string.Empty;
    [Required, MaxLength(512)] public string Description { get; init; } = string.Empty;
    [Required, MaxLength(64)] public string IconKey { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}
```

- [ ] **Step 4: Query (read) — `GetThreadCategories`**

`GetThreadCategoriesQuery.cs`:
```csharp
using Contracts.DTO.Threads;
using MediatR;

namespace Application.Threads.Queries.GetThreadCategories;

public sealed record GetThreadCategoriesQuery() : IRequest<IReadOnlyList<ThreadCategoryResponse>>;
```
`GetThreadCategoriesQueryHandler.cs`:
```csharp
using Application.Common.Interfaces;
using Contracts.DTO.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Queries.GetThreadCategories;

public sealed class GetThreadCategoriesQueryHandler
    : IRequestHandler<GetThreadCategoriesQuery, IReadOnlyList<ThreadCategoryResponse>>
{
    private readonly IApplicationDbContext _db;
    public GetThreadCategoriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ThreadCategoryResponse>> Handle(GetThreadCategoriesQuery request, CancellationToken ct)
    {
        return await _db.ThreadCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new ThreadCategoryResponse(c.Id, c.Slug, c.Name, c.Description, c.IconKey, c.SortOrder, c.IsActive))
            .ToListAsync(ct);
    }
}
```

- [ ] **Step 5: Create command**

`CreateThreadCategoryCommand.cs`:
```csharp
using MediatR;

namespace Application.Threads.Commands.CreateThreadCategory;

public sealed record CreateThreadCategoryCommand(
    string Slug, string Name, string Description, string IconKey, int SortOrder) : IRequest<Guid>;
```
`CreateThreadCategoryCommandHandler.cs`:
```csharp
using Application.Common.Interfaces;
using Domain.Entities.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.CreateThreadCategory;

public sealed class CreateThreadCategoryCommandHandler : IRequestHandler<CreateThreadCategoryCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    public CreateThreadCategoryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateThreadCategoryCommand request, CancellationToken ct)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await _db.ThreadCategories.AnyAsync(c => c.Slug == slug, ct))
        {
            throw new InvalidOperationException($"A category with slug '{slug}' already exists.");
        }

        var category = new ThreadCategory(Guid.NewGuid(), slug, request.Name, request.Description, request.IconKey, request.SortOrder);
        _db.ThreadCategories.Add(category);
        await _db.SaveChangesAsync(ct);
        return category.Id;
    }
}
```
`CreateThreadCategoryCommandValidator.cs`:
```csharp
using FluentValidation;

namespace Application.Threads.Commands.CreateThreadCategory;

public sealed class CreateThreadCategoryCommandValidator : AbstractValidator<CreateThreadCategoryCommand>
{
    public CreateThreadCategoryCommandValidator()
    {
        RuleFor(c => c.Slug).NotEmpty().MaximumLength(64).Matches("^[a-z0-9-]+$");
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(512);
        RuleFor(c => c.IconKey).NotEmpty().MaximumLength(64);
    }
}
```

- [ ] **Step 6: Update command**

`UpdateThreadCategoryCommand.cs`:
```csharp
using MediatR;

namespace Application.Threads.Commands.UpdateThreadCategory;

public sealed record UpdateThreadCategoryCommand(
    Guid Id, string Name, string Description, string IconKey, int SortOrder, bool IsActive) : IRequest;
```
`UpdateThreadCategoryCommandHandler.cs`:
```csharp
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.UpdateThreadCategory;

public sealed class UpdateThreadCategoryCommandHandler : IRequestHandler<UpdateThreadCategoryCommand>
{
    private readonly IApplicationDbContext _db;
    public UpdateThreadCategoryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(UpdateThreadCategoryCommand request, CancellationToken ct)
    {
        var category = await _db.ThreadCategories.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new InvalidOperationException("Category not found.");
        category.Update(request.Name, request.Description, request.IconKey, request.SortOrder, request.IsActive);
        await _db.SaveChangesAsync(ct);
    }
}
```
`UpdateThreadCategoryCommandValidator.cs`:
```csharp
using FluentValidation;

namespace Application.Threads.Commands.UpdateThreadCategory;

public sealed class UpdateThreadCategoryCommandValidator : AbstractValidator<UpdateThreadCategoryCommand>
{
    public UpdateThreadCategoryCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(512);
        RuleFor(c => c.IconKey).NotEmpty().MaximumLength(64);
    }
}
```

- [ ] **Step 7: Run the use-case tests — PASS (3).**

- [ ] **Step 8: Controller**

`src/Api/Controllers/ThreadCategoriesController.cs`:
```csharp
using Application.Threads.Commands.CreateThreadCategory;
using Application.Threads.Commands.UpdateThreadCategory;
using Application.Threads.Queries.GetThreadCategories;
using Contracts.DTO.Threads;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/threads/categories")]
public class ThreadCategoriesController : ControllerBase
{
    private readonly ISender _sender;
    public ThreadCategoriesController(ISender sender) => _sender = sender;

    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ThreadCategoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _sender.Send(new GetThreadCategoriesQuery(), ct));

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateThreadCategoryRequest request, CancellationToken ct)
    {
        try
        {
            var id = await _sender.Send(new CreateThreadCategoryCommand(
                request.Slug, request.Name, request.Description, request.IconKey, request.SortOrder), ct);
            return CreatedAtAction(nameof(List), new { id }, new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateThreadCategoryRequest request, CancellationToken ct)
    {
        try
        {
            await _sender.Send(new UpdateThreadCategoryCommand(
                id, request.Name, request.Description, request.IconKey, request.SortOrder, request.IsActive), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
```

> **Admin authorization note:** Plan 1 added `User.IsAdmin` but the JWT only carries `role` (e.g. "Student"). For `[Authorize(Roles = "Admin")]` to work, the access token must emit an `Admin` role claim for admins. In Task 14 you will update `AuthUserDtoFactory`/token issuance is NOT changed, but `AppJwtTokenService.IssueAccessToken` already takes the `role` string. The cleanest fix lives in this plan's Task 14 (see "Admin role claim"). For now the endpoints compile; the role wiring is verified in Task 14.

- [ ] **Step 9: Seed initial categories**

In `db/seed.sql`, after the existing inserts, add idempotent inserts for the canonical categories (use fixed UUIDs so re-runs are stable; `ON CONFLICT ("Slug") DO NOTHING` — the unique index from Task 5 backs this):
```sql
INSERT INTO thread_categories ("Id","Slug","Name","Description","IconKey","SortOrder","IsActive","CreatedAt")
VALUES
 ('a0000000-0000-0000-0000-000000000001','housemate','Housemate','Find a room or a flatmate','home',10,TRUE,NOW()),
 ('a0000000-0000-0000-0000-000000000002','share-memberships','Share Memberships','Split Spotify, Netflix, and more','share',20,TRUE,NOW()),
 ('a0000000-0000-0000-0000-000000000003','textbooks','Textbooks','Buy, sell, or borrow course textbooks','book',30,TRUE,NOW()),
 ('a0000000-0000-0000-0000-000000000004','rides','Rides','Share a ride or carpool','car',40,TRUE,NOW()),
 ('a0000000-0000-0000-0000-000000000005','lost-and-found','Lost & Found','Lost or found something on campus','search',50,TRUE,NOW()),
 ('a0000000-0000-0000-0000-000000000006','events','Events','Campus events and meetups','calendar',60,TRUE,NOW()),
 ('a0000000-0000-0000-0000-000000000007','general','General','Anything else','chat',99,TRUE,NOW())
ON CONFLICT ("Slug") DO NOTHING;
```

- [ ] **Step 10: Build + full test suite green. Commit** — `git add src/Contracts/DTO/Threads/ src/Application/Threads/ src/Api/Controllers/ThreadCategoriesController.cs db/seed.sql tests/Application.UnitTests/Threads/ThreadCategoryUseCaseTests.cs && git commit -m "feat: thread categories (seed, admin CRUD, public read)"`

---

## Task 7: Author-resolution guard (`AuthorRef` + factory) — the trust model

**Files:**
- Create: `src/Contracts/DTO/Threads/AuthorRef.cs`
- Create: `src/Application/Threads/AuthorRefFactory.cs`
- Test: `tests/Application.UnitTests/Threads/AuthorRefFactoryTests.cs`

This is the single enforcement point: anonymous content yields an `AuthorRef` carrying ONLY the anon handle; real content carries identity. No read DTO ever exposes `AuthorUserId` for anon content.

- [ ] **Step 1: Write the failing test**

```csharp
using Application.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class AuthorRefFactoryTests
{
    private static User NewUser(string display, string? anon) 
    {
        var u = new User(Guid.NewGuid(), "s@adelaide.edu.au", display, DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other,
            avatarUrl: "https://x/y.png", isActive: true);
        if (anon is not null) u.AssignAnonHandle(anon);
        return u;
    }

    [Fact]
    public void Anonymous_exposes_only_handle()
    {
        var user = NewUser("Sarah Chen", "quiet-koala-4821");
        var aref = AuthorRefFactory.Create(isAnonymous: true, user);

        Assert.True(aref.IsAnonymous);
        Assert.Equal("quiet-koala-4821", aref.Handle);
        Assert.Null(aref.UserId);
        Assert.Null(aref.DisplayName);
        Assert.Null(aref.AvatarUrl);
    }

    [Fact]
    public void Real_exposes_identity_not_handle()
    {
        var user = NewUser("Sarah Chen", "quiet-koala-4821");
        var aref = AuthorRefFactory.Create(isAnonymous: false, user);

        Assert.False(aref.IsAnonymous);
        Assert.Equal(user.Id, aref.UserId);
        Assert.Equal("Sarah Chen", aref.DisplayName);
        Assert.Equal("https://x/y.png", aref.AvatarUrl);
        Assert.Null(aref.Handle);
    }

    [Fact]
    public void Anonymous_without_handle_falls_back_to_placeholder()
    {
        var user = NewUser("Sarah Chen", anon: null);
        var aref = AuthorRefFactory.Create(isAnonymous: true, user);

        Assert.True(aref.IsAnonymous);
        Assert.Equal("anonymous", aref.Handle);
        Assert.Null(aref.UserId);
    }
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: Implement**

`AuthorRef.cs`:
```csharp
namespace Contracts.DTO.Threads;

/// <summary>
/// Author projection that enforces anonymity at the API boundary.
/// Anonymous content carries ONLY <see cref="Handle"/>; real content carries identity.
/// </summary>
public sealed record AuthorRef(
    bool IsAnonymous,
    string? Handle,
    Guid? UserId,
    string? DisplayName,
    string? AvatarUrl);
```

`AuthorRefFactory.cs`:
```csharp
using Contracts.DTO.Threads;
using Domain.Entities.Users;

namespace Application.Threads;

public static class AuthorRefFactory
{
    public static AuthorRef Create(bool isAnonymous, User author)
    {
        if (isAnonymous)
        {
            // Never leak any identifying field for anonymous content.
            return new AuthorRef(
                IsAnonymous: true,
                Handle: string.IsNullOrWhiteSpace(author.AnonHandle) ? "anonymous" : author.AnonHandle,
                UserId: null,
                DisplayName: null,
                AvatarUrl: null);
        }

        return new AuthorRef(
            IsAnonymous: false,
            Handle: null,
            UserId: author.Id,
            DisplayName: author.DisplayName,
            AvatarUrl: author.AvatarUrl);
    }
}
```

- [ ] **Step 4: Run — PASS (3).**
- [ ] **Step 5: Commit** — `git add src/Contracts/DTO/Threads/AuthorRef.cs src/Application/Threads/AuthorRefFactory.cs tests/Application.UnitTests/Threads/AuthorRefFactoryTests.cs && git commit -m "feat: AuthorRef anonymity guard for threads"`

---

## Task 8: Create-post command (anon handle integration + R2 images)

**Files:**
- Create: `src/Contracts/DTO/Threads/CreateThreadPostRequest.cs`
- Create: `src/Application/Threads/Commands/CreateThreadPost/{Command,Handler,Validator}.cs`
- Test: `tests/Application.UnitTests/Threads/CreateThreadPostTests.cs`

The handler: validates the category exists+active; if `isAnonymous`, ensures the author has an anon handle (reuse the SAME generation rule as Plan 1 — call into `GetOrCreateAnonHandleCommand` via `ISender`, OR resolve inline). To avoid a handler-calls-handler dependency, inject `ISender` and send `GetOrCreateAnonHandleCommand`. Image bytes are uploaded via the existing `IObjectStorageService`; in unit tests a fake is injected.

- [ ] **Step 1: Inspect `IObjectStorageService`**

Read `src/Application/Common/Interfaces/IObjectStorageService.cs` (or wherever it lives — `grep -rn "interface IObjectStorageService" src`). Note the exact upload method signature (name, params: stream/bytes, key, content-type, return). The handler and the test fake below must match it. **If the real signature differs from `Task<string> UploadAsync(Stream content, string key, string contentType, CancellationToken ct)`, adapt the handler + fake accordingly and keep the test meaningful.**

- [ ] **Step 2: Write the failing test** (uses a fake storage + real SQLite)

```csharp
using Application.Common.Interfaces;
using Application.Threads.Commands.CreateThreadPost;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class CreateThreadPostTests
{
    private sealed class FakeStorage : IObjectStorageService
    {
        public List<string> Keys { get; } = new();
        public Task<string> UploadAsync(Stream content, string key, string contentType, CancellationToken ct = default)
        { Keys.Add(key); return Task.FromResult(key); }
        // NOTE: implement any other interface members as no-ops to satisfy the compiler.
    }

    // Minimal ISender that resolves GetOrCreateAnonHandle by assigning a fixed handle.
    private sealed class FakeSender : ISender
    {
        private readonly MarketplaceDbContext _db;
        public FakeSender(MarketplaceDbContext db) => _db = db;
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            // Only GetOrCreateAnonHandleCommand is expected here.
            dynamic r = request;
            Guid userId = r.UserId;
            var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);
            if (string.IsNullOrWhiteSpace(user.AnonHandle)) user.AssignAnonHandle("quiet-koala-4821");
            await _db.SaveChangesAsync(ct);
            return (TResponse)(object)user.AnonHandle!;
        }
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotImplementedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private static User NewUser(Guid id) => new(id, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student",
        "hash", AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true);

    private static ThreadCategory NewCategory(Guid id) => new(id, "general", "General", "x", "chat", 1);

    [Fact]
    public async Task Creates_real_post_in_category()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId));
        db.Context.ThreadCategories.Add(NewCategory(catId));
        await db.Context.SaveChangesAsync();

        var handler = new CreateThreadPostCommandHandler(db.Context, new FakeStorage(), new FakeSender(db.Context));
        var postId = await handler.Handle(
            new CreateThreadPostCommand(userId, catId, "Title", "Body", IsAnonymous: false, Images: new List<ThreadPostImageUpload>()), default);

        var post = await db.Context.ThreadPosts.FirstAsync(p => p.Id == postId);
        Assert.Equal("Title", post.Title);
        Assert.False(post.IsAnonymous);
        Assert.Equal(catId, post.CategoryId);
    }

    [Fact]
    public async Task Anonymous_post_triggers_handle_assignment()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId));
        db.Context.ThreadCategories.Add(NewCategory(catId));
        await db.Context.SaveChangesAsync();

        var handler = new CreateThreadPostCommandHandler(db.Context, new FakeStorage(), new FakeSender(db.Context));
        await handler.Handle(new CreateThreadPostCommand(userId, catId, "T", "B", true, new List<ThreadPostImageUpload>()), default);

        var user = await db.Context.Users.FirstAsync(u => u.Id == userId);
        Assert.False(string.IsNullOrWhiteSpace(user.AnonHandle));
    }

    [Fact]
    public async Task Rejects_unknown_or_inactive_category()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId));
        await db.Context.SaveChangesAsync();

        var handler = new CreateThreadPostCommandHandler(db.Context, new FakeStorage(), new FakeSender(db.Context));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateThreadPostCommand(userId, Guid.NewGuid(), "T", "B", false, new List<ThreadPostImageUpload>()), default));
    }
}
```

> If the real `IObjectStorageService` has more members, add no-op implementations to `FakeStorage`. If `ISender.Send` signature differs by MediatR version, adjust `FakeSender` to match the installed MediatR's `ISender` interface.

- [ ] **Step 3: Run — FAIL.**

- [ ] **Step 4: Implement command + handler + validator**

`CreateThreadPostCommand.cs`:
```csharp
using MediatR;

namespace Application.Threads.Commands.CreateThreadPost;

public sealed record ThreadPostImageUpload(byte[] Content, string ContentType, string FileName);

public sealed record CreateThreadPostCommand(
    Guid AuthorUserId,
    Guid CategoryId,
    string Title,
    string Body,
    bool IsAnonymous,
    IReadOnlyList<ThreadPostImageUpload> Images) : IRequest<Guid>;
```

`CreateThreadPostCommandHandler.cs`:
```csharp
using Application.Common.Interfaces;
using Application.Users.Commands.GetOrCreateAnonHandle;
using Domain.Entities.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.CreateThreadPost;

public sealed class CreateThreadPostCommandHandler : IRequestHandler<CreateThreadPostCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly IObjectStorageService _storage;
    private readonly ISender _sender;

    public CreateThreadPostCommandHandler(IApplicationDbContext db, IObjectStorageService storage, ISender sender)
    {
        _db = db;
        _storage = storage;
        _sender = sender;
    }

    public async Task<Guid> Handle(CreateThreadPostCommand request, CancellationToken ct)
    {
        var categoryExists = await _db.ThreadCategories.AnyAsync(c => c.Id == request.CategoryId && c.IsActive, ct);
        if (!categoryExists)
        {
            throw new InvalidOperationException("Category not found or inactive.");
        }

        // Ensure the author has a stable anon handle before publishing anonymously (reuses Plan 1's rule).
        if (request.IsAnonymous)
        {
            await _sender.Send(new GetOrCreateAnonHandleCommand(request.AuthorUserId), ct);
        }

        var post = new ThreadPost(Guid.NewGuid(), request.CategoryId, request.AuthorUserId,
            request.IsAnonymous, request.Title.Trim(), request.Body);

        var ordinal = 0;
        foreach (var image in request.Images)
        {
            var key = $"threads/{post.Id}/{Guid.NewGuid():N}";
            await using var stream = new MemoryStream(image.Content);
            await _storage.UploadAsync(stream, key, image.ContentType, ct);
            post.AddImage(new ThreadPostImage(Guid.NewGuid(), post.Id, key, ordinal++));
        }

        _db.ThreadPosts.Add(post);
        await _db.SaveChangesAsync(ct);
        return post.Id;
    }
}
```

`CreateThreadPostCommandValidator.cs`:
```csharp
using FluentValidation;

namespace Application.Threads.Commands.CreateThreadPost;

public sealed class CreateThreadPostCommandValidator : AbstractValidator<CreateThreadPostCommand>
{
    public CreateThreadPostCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(20000);
        RuleFor(c => c.Images).Must(i => i.Count <= 8).WithMessage("At most 8 images per post.");
    }
}
```

- [ ] **Step 5: Run the create-post tests — PASS (3). Run full suite green.**
- [ ] **Step 6: Commit** — `git add src/Contracts/DTO/Threads/CreateThreadPostRequest.cs src/Application/Threads/Commands/CreateThreadPost/ tests/Application.UnitTests/Threads/CreateThreadPostTests.cs && git commit -m "feat: create-thread-post command with anon handle + images"`

> Note: also create `src/Contracts/DTO/Threads/CreateThreadPostRequest.cs` now (used by the controller in Task 14):
> ```csharp
> using System.ComponentModel.DataAnnotations;
> namespace Contracts.DTO.Threads;
> public sealed class CreateThreadPostRequest
> {
>     [Required] public Guid CategoryId { get; init; }
>     [Required, MaxLength(200)] public string Title { get; init; } = string.Empty;
>     [Required] public string Body { get; init; } = string.Empty;
>     public bool IsAnonymous { get; init; }
>     public List<IFormFile>? Images { get; init; }
> }
> ```
> (`IFormFile` requires `using Microsoft.AspNetCore.Http;` — `Contracts` already targets the web SDK via the Items `CreateItemWithImagesRequest`; confirm and mirror that file's approach. If `Contracts` does NOT reference ASP.NET types, put `CreateThreadPostRequest` in `src/Api/Models/` like `CreateItemWithImagesRequest` and reference it from the controller instead.)

---

## Task 9: Post detail + comments read (author resolution applied)

**Files:**
- Create: `src/Contracts/DTO/Threads/ThreadPostDetailResponse.cs`, `ThreadCommentResponse.cs`
- Create: `src/Application/Threads/Queries/GetThreadPost/{Query,Handler}.cs`
- Create: `src/Application/Threads/Queries/GetThreadComments/{Query,Handler}.cs`
- Test: `tests/Application.UnitTests/Threads/GetThreadPostTests.cs`

- [ ] **Step 1: Write the failing test** (asserts anon detail hides identity, real detail shows it, deleted post → null)

```csharp
using Application.Threads.Commands.CreateThreadPost;
using Application.Threads.Queries.GetThreadPost;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class GetThreadPostTests
{
    private static User NewUser(Guid id, string? anon)
    {
        var u = new User(id, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true);
        if (anon is not null) u.AssignAnonHandle(anon);
        return u;
    }

    [Fact]
    public async Task Anonymous_post_detail_hides_author_identity()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId, "quiet-koala-4821"));
        db.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, isAnonymous: true, "T", "B");
        db.Context.ThreadPosts.Add(post);
        await db.Context.SaveChangesAsync();

        var result = await new GetThreadPostQueryHandler(db.Context).Handle(new GetThreadPostQuery(post.Id), default);

        Assert.NotNull(result);
        Assert.True(result!.Author.IsAnonymous);
        Assert.Equal("quiet-koala-4821", result.Author.Handle);
        Assert.Null(result.Author.UserId);
        Assert.Null(result.Author.DisplayName);
    }

    [Fact]
    public async Task Real_post_detail_shows_identity()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId, null));
        db.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, false, "T", "B");
        db.Context.ThreadPosts.Add(post);
        await db.Context.SaveChangesAsync();

        var result = await new GetThreadPostQueryHandler(db.Context).Handle(new GetThreadPostQuery(post.Id), default);

        Assert.False(result!.Author.IsAnonymous);
        Assert.Equal(userId, result.Author.UserId);
        Assert.Equal("Sarah", result.Author.DisplayName);
    }

    [Fact]
    public async Task Deleted_post_returns_null()
    {
        await using var db = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        db.Context.Users.Add(NewUser(userId, null));
        db.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, false, "T", "B");
        post.SoftDelete();
        db.Context.ThreadPosts.Add(post);
        await db.Context.SaveChangesAsync();

        var result = await new GetThreadPostQueryHandler(db.Context).Handle(new GetThreadPostQuery(post.Id), default);
        Assert.Null(result);
    }
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: DTOs**

`ThreadCommentResponse.cs`:
```csharp
namespace Contracts.DTO.Threads;

public sealed record ThreadCommentResponse(
    Guid Id,
    Guid? ParentCommentId,
    AuthorRef Author,
    string Body,
    int LikeCount,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ThreadCommentResponse> Replies);
```

`ThreadPostDetailResponse.cs`:
```csharp
namespace Contracts.DTO.Threads;

public sealed record ThreadPostDetailResponse(
    Guid Id,
    string CategorySlug,
    AuthorRef Author,
    string Title,
    string Body,
    IReadOnlyList<string> ImageKeys,
    int LikeCount,
    int CommentCount,
    bool IsLocked,
    bool IsPinned,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt);
```

- [ ] **Step 4: Post detail query**

`GetThreadPostQuery.cs`:
```csharp
using Contracts.DTO.Threads;
using MediatR;

namespace Application.Threads.Queries.GetThreadPost;

public sealed record GetThreadPostQuery(Guid PostId) : IRequest<ThreadPostDetailResponse?>;
```
`GetThreadPostQueryHandler.cs`:
```csharp
using Application.Common.Interfaces;
using Application.Threads;
using Contracts.DTO.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Queries.GetThreadPost;

public sealed class GetThreadPostQueryHandler : IRequestHandler<GetThreadPostQuery, ThreadPostDetailResponse?>
{
    private readonly IApplicationDbContext _db;
    public GetThreadPostQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ThreadPostDetailResponse?> Handle(GetThreadPostQuery request, CancellationToken ct)
    {
        var post = await _db.ThreadPosts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == request.PostId && !p.IsDeleted, ct);

        if (post is null || post.Author is null)
        {
            return null;
        }

        return new ThreadPostDetailResponse(
            post.Id,
            post.Category?.Slug ?? string.Empty,
            AuthorRefFactory.Create(post.IsAnonymous, post.Author),
            post.Title,
            post.Body,
            post.Images.OrderBy(i => i.Ordinal).Select(i => i.R2Key).ToList(),
            post.LikeCount,
            post.CommentCount,
            post.IsLocked,
            post.IsPinned,
            post.CreatedAt,
            post.LastActivityAt);
    }
}
```

- [ ] **Step 5: Comments query (2-level tree, author-resolved)**

`GetThreadCommentsQuery.cs`:
```csharp
using Contracts.DTO.Threads;
using MediatR;

namespace Application.Threads.Queries.GetThreadComments;

public sealed record GetThreadCommentsQuery(Guid PostId) : IRequest<IReadOnlyList<ThreadCommentResponse>>;
```
`GetThreadCommentsQueryHandler.cs`:
```csharp
using Application.Common.Interfaces;
using Application.Threads;
using Contracts.DTO.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Queries.GetThreadComments;

public sealed class GetThreadCommentsQueryHandler
    : IRequestHandler<GetThreadCommentsQuery, IReadOnlyList<ThreadCommentResponse>>
{
    private readonly IApplicationDbContext _db;
    public GetThreadCommentsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ThreadCommentResponse>> Handle(GetThreadCommentsQuery request, CancellationToken ct)
    {
        var comments = await _db.ThreadComments
            .AsNoTracking()
            .Include(c => c.Author)
            .Where(c => c.PostId == request.PostId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        // Build a 2-level tree. A deleted comment is shown as a "[removed]" placeholder
        // only if it has surviving replies; otherwise it is omitted.
        ThreadCommentResponse Map(Domain.Entities.Threads.ThreadComment c, IReadOnlyList<ThreadCommentResponse> replies)
        {
            var author = AuthorRefFactory.Create(c.IsAnonymous, c.Author!);
            var body = c.IsDeleted ? "[removed]" : c.Body;
            return new ThreadCommentResponse(c.Id, c.ParentCommentId, author, body, c.LikeCount, c.IsDeleted, c.CreatedAt, replies);
        }

        var byParent = comments.Where(c => c.ParentCommentId is not null)
            .GroupBy(c => c.ParentCommentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ThreadCommentResponse>();
        foreach (var top in comments.Where(c => c.ParentCommentId is null))
        {
            var replies = byParent.TryGetValue(top.Id, out var kids)
                ? kids.Where(k => !k.IsDeleted).Select(k => Map(k, Array.Empty<ThreadCommentResponse>())).ToList()
                : new List<ThreadCommentResponse>();

            if (top.IsDeleted && replies.Count == 0)
            {
                continue; // drop fully-dead top-level comments
            }

            result.Add(Map(top, replies));
        }

        return result;
    }
}
```

- [ ] **Step 6: Run post-detail tests — PASS (3). Build + full suite green.**
- [ ] **Step 7: Commit** — `git add src/Contracts/DTO/Threads/ThreadPostDetailResponse.cs src/Contracts/DTO/Threads/ThreadCommentResponse.cs src/Application/Threads/Queries/GetThreadPost/ src/Application/Threads/Queries/GetThreadComments/ tests/Application.UnitTests/Threads/GetThreadPostTests.cs && git commit -m "feat: thread post detail + comments read with author resolution"`

---

## Task 10: Create-comment command (2-level enforcement)

**Files:**
- Create: `src/Contracts/DTO/Threads/CreateThreadCommentRequest.cs`
- Create: `src/Application/Threads/Commands/CreateThreadComment/{Command,Handler,Validator}.cs`
- Test: `tests/Application.UnitTests/Threads/CreateThreadCommentTests.cs`

- [ ] **Step 1: Write the failing test** (top-level ok; reply-to-top ok; reply-to-reply rejected; bumps post comment count; anon assigns handle; locked post rejected)

```csharp
using Application.Threads.Commands.CreateThreadComment;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class CreateThreadCommentTests
{
    private sealed class FakeSender : ISender
    {
        private readonly MarketplaceDbContext _db;
        public FakeSender(MarketplaceDbContext db) => _db = db;
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            dynamic r = request; Guid userId = r.UserId;
            var u = await _db.Users.FirstAsync(x => x.Id == userId, ct);
            if (string.IsNullOrWhiteSpace(u.AnonHandle)) u.AssignAnonHandle("quiet-koala-4821");
            await _db.SaveChangesAsync(ct);
            return (TResponse)(object)u.AnonHandle!;
        }
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotImplementedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private static async Task<(MarketplaceDbContext db, Guid userId, ThreadPost post)> Seed(TestDb t, bool locked = false)
    {
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(new User(userId, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, false, "T", "B");
        if (locked) post.SetLocked(true);
        t.Context.ThreadPosts.Add(post);
        await t.Context.SaveChangesAsync();
        return (t.Context, userId, post);
    }

    [Fact]
    public async Task Top_level_comment_bumps_post_count()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, post) = await Seed(t);
        var handler = new CreateThreadCommentCommandHandler(db, new FakeSender(db));

        await handler.Handle(new CreateThreadCommentCommand(post.Id, null, userId, false, "hello"), default);

        var saved = await db.ThreadPosts.FirstAsync(p => p.Id == post.Id);
        Assert.Equal(1, saved.CommentCount);
    }

    [Fact]
    public async Task Reply_to_top_level_is_allowed()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, post) = await Seed(t);
        var handler = new CreateThreadCommentCommandHandler(db, new FakeSender(db));
        var topId = await handler.Handle(new CreateThreadCommentCommand(post.Id, null, userId, false, "top"), default);

        var replyId = await handler.Handle(new CreateThreadCommentCommand(post.Id, topId, userId, false, "reply"), default);
        Assert.NotEqual(Guid.Empty, replyId);
    }

    [Fact]
    public async Task Reply_to_a_reply_is_rejected()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, post) = await Seed(t);
        var handler = new CreateThreadCommentCommandHandler(db, new FakeSender(db));
        var topId = await handler.Handle(new CreateThreadCommentCommand(post.Id, null, userId, false, "top"), default);
        var replyId = await handler.Handle(new CreateThreadCommentCommand(post.Id, topId, userId, false, "reply"), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateThreadCommentCommand(post.Id, replyId, userId, false, "deep"), default));
    }

    [Fact]
    public async Task Comment_on_locked_post_is_rejected()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, post) = await Seed(t, locked: true);
        var handler = new CreateThreadCommentCommandHandler(db, new FakeSender(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateThreadCommentCommand(post.Id, null, userId, false, "x"), default));
    }
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: Implement**

`CreateThreadCommentCommand.cs`:
```csharp
using MediatR;

namespace Application.Threads.Commands.CreateThreadComment;

public sealed record CreateThreadCommentCommand(
    Guid PostId, Guid? ParentCommentId, Guid AuthorUserId, bool IsAnonymous, string Body) : IRequest<Guid>;
```
`CreateThreadCommentCommandHandler.cs`:
```csharp
using Application.Common.Interfaces;
using Application.Users.Commands.GetOrCreateAnonHandle;
using Domain.Entities.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.CreateThreadComment;

public sealed class CreateThreadCommentCommandHandler : IRequestHandler<CreateThreadCommentCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ISender _sender;

    public CreateThreadCommentCommandHandler(IApplicationDbContext db, ISender sender)
    {
        _db = db;
        _sender = sender;
    }

    public async Task<Guid> Handle(CreateThreadCommentCommand request, CancellationToken ct)
    {
        var post = await _db.ThreadPosts.FirstOrDefaultAsync(p => p.Id == request.PostId && !p.IsDeleted, ct)
            ?? throw new InvalidOperationException("Post not found.");
        if (post.IsLocked)
        {
            throw new InvalidOperationException("This post is locked.");
        }

        if (request.ParentCommentId is { } parentId)
        {
            var parent = await _db.ThreadComments
                .FirstOrDefaultAsync(c => c.Id == parentId && c.PostId == request.PostId, ct)
                ?? throw new InvalidOperationException("Parent comment not found.");
            if (parent.ParentCommentId is not null)
            {
                throw new InvalidOperationException("Replies can only be one level deep.");
            }
        }

        if (request.IsAnonymous)
        {
            await _sender.Send(new GetOrCreateAnonHandleCommand(request.AuthorUserId), ct);
        }

        var comment = new ThreadComment(Guid.NewGuid(), request.PostId, request.ParentCommentId,
            request.AuthorUserId, request.IsAnonymous, request.Body);
        _db.ThreadComments.Add(comment);
        post.RegisterCommentAdded(DateTimeOffset.UtcNow);

        await _db.SaveChangesAsync(ct);
        return comment.Id;
    }
}
```
`CreateThreadCommentCommandValidator.cs`:
```csharp
using FluentValidation;

namespace Application.Threads.Commands.CreateThreadComment;

public sealed class CreateThreadCommentCommandValidator : AbstractValidator<CreateThreadCommentCommand>
{
    public CreateThreadCommentCommandValidator()
    {
        RuleFor(c => c.Body).NotEmpty().MaximumLength(10000);
    }
}
```

- [ ] **Step 4: Run comment tests — PASS (4). Full suite green.**
- [ ] **Step 5: Commit** — `git add src/Application/Threads/Commands/CreateThreadComment/ tests/Application.UnitTests/Threads/CreateThreadCommentTests.cs && git commit -m "feat: create-thread-comment with 2-level enforcement"`

> Also create `src/Contracts/DTO/Threads/CreateThreadCommentRequest.cs` (used by controller Task 14):
> ```csharp
> using System.ComponentModel.DataAnnotations;
> namespace Contracts.DTO.Threads;
> public sealed class CreateThreadCommentRequest
> {
>     public Guid? ParentCommentId { get; init; }
>     public bool IsAnonymous { get; init; }
>     [Required] public string Body { get; init; } = string.Empty;
> }
> ```

---

## Task 11: Like-toggle command (post + comment, idempotent)

**Files:**
- Create: `src/Contracts/DTO/Threads/LikeResponse.cs`
- Create: `src/Application/Threads/Commands/ToggleThreadLike/{Command,Handler}.cs`
- Test: `tests/Application.UnitTests/Threads/ToggleThreadLikeTests.cs`

- [ ] **Step 1: Write the failing test** (first toggle adds + increments; second removes + decrements; comment target works; count never negative)

```csharp
using Application.Threads.Commands.ToggleThreadLike;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class ToggleThreadLikeTests
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
    public async Task First_like_adds_and_increments_second_removes()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, post) = await Seed(t);
        var handler = new ToggleThreadLikeCommandHandler(db);

        var r1 = await handler.Handle(new ToggleThreadLikeCommand(userId, ThreadLikeTarget.Post, post.Id), default);
        Assert.True(r1.Liked);
        Assert.Equal(1, r1.LikeCount);

        var r2 = await handler.Handle(new ToggleThreadLikeCommand(userId, ThreadLikeTarget.Post, post.Id), default);
        Assert.False(r2.Liked);
        Assert.Equal(0, r2.LikeCount);

        Assert.False(await db.ThreadLikes.AnyAsync(l => l.UserId == userId && l.TargetId == post.Id));
    }

    [Fact]
    public async Task Like_on_missing_target_throws()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, userId, _) = await Seed(t);
        var handler = new ToggleThreadLikeCommandHandler(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ToggleThreadLikeCommand(userId, ThreadLikeTarget.Post, Guid.NewGuid()), default));
    }
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: Implement**

`LikeResponse.cs`:
```csharp
namespace Contracts.DTO.Threads;

public sealed record LikeResponse(bool Liked, int LikeCount);
```
`ToggleThreadLikeCommand.cs`:
```csharp
using Contracts.DTO.Threads;
using Domain.Shared.Enums;
using MediatR;

namespace Application.Threads.Commands.ToggleThreadLike;

public sealed record ToggleThreadLikeCommand(Guid UserId, ThreadLikeTarget Target, Guid TargetId)
    : IRequest<LikeResponse>;
```
`ToggleThreadLikeCommandHandler.cs`:
```csharp
using Application.Common.Interfaces;
using Contracts.DTO.Threads;
using Domain.Entities.Threads;
using Domain.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.ToggleThreadLike;

public sealed class ToggleThreadLikeCommandHandler : IRequestHandler<ToggleThreadLikeCommand, LikeResponse>
{
    private readonly IApplicationDbContext _db;
    public ToggleThreadLikeCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<LikeResponse> Handle(ToggleThreadLikeCommand request, CancellationToken ct)
    {
        var existing = await _db.ThreadLikes.FirstOrDefaultAsync(
            l => l.UserId == request.UserId && l.TargetType == request.Target && l.TargetId == request.TargetId, ct);

        int newCount;
        bool liked;

        if (request.Target == ThreadLikeTarget.Post)
        {
            var post = await _db.ThreadPosts.FirstOrDefaultAsync(p => p.Id == request.TargetId && !p.IsDeleted, ct)
                ?? throw new InvalidOperationException("Post not found.");
            if (existing is null)
            {
                _db.ThreadLikes.Add(new ThreadLike(request.UserId, request.Target, request.TargetId));
                post.AdjustLikeCount(+1); liked = true;
            }
            else
            {
                _db.ThreadLikes.Remove(existing);
                post.AdjustLikeCount(-1); liked = false;
            }
            newCount = post.LikeCount;
        }
        else
        {
            var comment = await _db.ThreadComments.FirstOrDefaultAsync(c => c.Id == request.TargetId && !c.IsDeleted, ct)
                ?? throw new InvalidOperationException("Comment not found.");
            if (existing is null)
            {
                _db.ThreadLikes.Add(new ThreadLike(request.UserId, request.Target, request.TargetId));
                comment.AdjustLikeCount(+1); liked = true;
            }
            else
            {
                _db.ThreadLikes.Remove(existing);
                comment.AdjustLikeCount(-1); liked = false;
            }
            newCount = comment.LikeCount;
        }

        await _db.SaveChangesAsync(ct);
        return new LikeResponse(liked, newCount);
    }
}
```

- [ ] **Step 4: Run like tests — PASS (2). Full suite green.**
- [ ] **Step 5: Commit** — `git add src/Contracts/DTO/Threads/LikeResponse.cs src/Application/Threads/Commands/ToggleThreadLike/ tests/Application.UnitTests/Threads/ToggleThreadLikeTests.cs && git commit -m "feat: idempotent like-toggle for posts and comments"`

---

## Task 12: Update + soft-delete post (owner / admin)

**Files:**
- Create: `src/Contracts/DTO/Threads/UpdateThreadPostRequest.cs`
- Create: `src/Application/Threads/Commands/UpdateThreadPost/{Command,Handler,Validator}.cs`
- Create: `src/Application/Threads/Commands/DeleteThreadPost/{Command,Handler}.cs`
- Test: `tests/Application.UnitTests/Threads/UpdateDeleteThreadPostTests.cs`

Authorization rule: only the author may update; the author OR an admin may soft-delete. The commands carry the acting user's id + whether they are an admin (the controller supplies these from claims).

- [ ] **Step 1: Write the failing test**

```csharp
using Application.Threads.Commands.DeleteThreadPost;
using Application.Threads.Commands.UpdateThreadPost;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class UpdateDeleteThreadPostTests
{
    private static async Task<(MarketplaceDbContext db, Guid ownerId, ThreadPost post)> Seed(TestDb t)
    {
        var ownerId = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(new User(ownerId, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, ownerId, false, "T", "B");
        t.Context.ThreadPosts.Add(post);
        await t.Context.SaveChangesAsync();
        return (t.Context, ownerId, post);
    }

    [Fact]
    public async Task Owner_can_update_body()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, ownerId, post) = await Seed(t);
        await new UpdateThreadPostCommandHandler(db).Handle(
            new UpdateThreadPostCommand(post.Id, ownerId, "New T", "New B"), default);
        var saved = await db.ThreadPosts.FirstAsync(p => p.Id == post.Id);
        Assert.Equal("New T", saved.Title);
    }

    [Fact]
    public async Task Non_owner_cannot_update()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, _, post) = await Seed(t);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new UpdateThreadPostCommandHandler(db).Handle(
                new UpdateThreadPostCommand(post.Id, Guid.NewGuid(), "X", "Y"), default));
    }

    [Fact]
    public async Task Admin_can_soft_delete_others_post()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, _, post) = await Seed(t);
        await new DeleteThreadPostCommandHandler(db).Handle(
            new DeleteThreadPostCommand(post.Id, Guid.NewGuid(), IsAdmin: true), default);
        var saved = await db.ThreadPosts.FirstAsync(p => p.Id == post.Id);
        Assert.True(saved.IsDeleted);
    }

    [Fact]
    public async Task Non_owner_non_admin_cannot_delete()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, _, post) = await Seed(t);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new DeleteThreadPostCommandHandler(db).Handle(
                new DeleteThreadPostCommand(post.Id, Guid.NewGuid(), IsAdmin: false), default));
    }
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: Implement update**

`UpdateThreadPostCommand.cs`:
```csharp
using MediatR;

namespace Application.Threads.Commands.UpdateThreadPost;

public sealed record UpdateThreadPostCommand(Guid PostId, Guid ActingUserId, string Title, string Body) : IRequest;
```
`UpdateThreadPostCommandHandler.cs`:
```csharp
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.UpdateThreadPost;

public sealed class UpdateThreadPostCommandHandler : IRequestHandler<UpdateThreadPostCommand>
{
    private readonly IApplicationDbContext _db;
    public UpdateThreadPostCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(UpdateThreadPostCommand request, CancellationToken ct)
    {
        var post = await _db.ThreadPosts.FirstOrDefaultAsync(p => p.Id == request.PostId && !p.IsDeleted, ct)
            ?? throw new InvalidOperationException("Post not found.");
        if (post.AuthorUserId != request.ActingUserId)
        {
            throw new UnauthorizedAccessException("Only the author can edit this post.");
        }
        post.UpdateContent(request.Title.Trim(), request.Body);
        await _db.SaveChangesAsync(ct);
    }
}
```
`UpdateThreadPostCommandValidator.cs`:
```csharp
using FluentValidation;

namespace Application.Threads.Commands.UpdateThreadPost;

public sealed class UpdateThreadPostCommandValidator : AbstractValidator<UpdateThreadPostCommand>
{
    public UpdateThreadPostCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(20000);
    }
}
```

- [ ] **Step 4: Implement delete**

`DeleteThreadPostCommand.cs`:
```csharp
using MediatR;

namespace Application.Threads.Commands.DeleteThreadPost;

public sealed record DeleteThreadPostCommand(Guid PostId, Guid ActingUserId, bool IsAdmin) : IRequest;
```
`DeleteThreadPostCommandHandler.cs`:
```csharp
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Commands.DeleteThreadPost;

public sealed class DeleteThreadPostCommandHandler : IRequestHandler<DeleteThreadPostCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteThreadPostCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteThreadPostCommand request, CancellationToken ct)
    {
        var post = await _db.ThreadPosts.FirstOrDefaultAsync(p => p.Id == request.PostId && !p.IsDeleted, ct)
            ?? throw new InvalidOperationException("Post not found.");
        if (post.AuthorUserId != request.ActingUserId && !request.IsAdmin)
        {
            throw new UnauthorizedAccessException("Not allowed to delete this post.");
        }
        post.SoftDelete();
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 5: Run — PASS (4). Full suite green.**
- [ ] **Step 6: Commit** — `git add src/Contracts/DTO/Threads/UpdateThreadPostRequest.cs src/Application/Threads/Commands/UpdateThreadPost/ src/Application/Threads/Commands/DeleteThreadPost/ tests/Application.UnitTests/Threads/UpdateDeleteThreadPostTests.cs && git commit -m "feat: update + soft-delete thread post with owner/admin rules"`

> Also create `src/Contracts/DTO/Threads/UpdateThreadPostRequest.cs`:
> ```csharp
> using System.ComponentModel.DataAnnotations;
> namespace Contracts.DTO.Threads;
> public sealed class UpdateThreadPostRequest
> {
>     [Required, MaxLength(200)] public string Title { get; init; } = string.Empty;
>     [Required] public string Body { get; init; } = string.Empty;
> }
> ```

---

## Task 13: Feed query (Postgres — Hot / New / Top, cursor pagination)

**Files:**
- Create: `src/Contracts/DTO/Threads/ThreadPostSummary.cs`, `ThreadFeedResponse.cs`
- Create: `src/Application/Threads/Queries/GetThreadFeed/{Query,Handler}.cs`
- Test: `tests/Application.UnitTests/Threads/GetThreadFeedTests.cs`

**Provisional:** this Postgres feed is replaced by an Elasticsearch read model in Plan 3. Keep the handler isolated so it can be swapped. Sort modes:
- `New` — `CreatedAt DESC`
- `Top` — `LikeCount DESC, CreatedAt DESC`
- `Hot` — score `(LikeCount + 2*CommentCount) / pow(hoursSince + 2, 1.8)` computed in memory over a bounded candidate window (last 500 non-deleted posts), then take the page. Cursor pagination is offset-based for this provisional version (`cursor` = integer offset as string); Plan 3 introduces ES `search_after`.

- [ ] **Step 1: Write the failing test**

```csharp
using Application.Threads.Queries.GetThreadFeed;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class GetThreadFeedTests
{
    private static async Task<(MarketplaceDbContext db, Guid catId, Guid userId)> Seed(TestDb t)
    {
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        t.Context.Users.Add(new User(userId, "s@adelaide.edu.au", "Sarah", DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other, isActive: true));
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        await t.Context.SaveChangesAsync();
        return (t.Context, catId, userId);
    }

    private static ThreadPost Post(Guid catId, Guid userId, string title, bool anon = false) =>
        new(Guid.NewGuid(), catId, userId, anon, title, "B");

    [Fact]
    public async Task New_sort_returns_most_recent_first()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, catId, userId) = await Seed(t);
        var older = Post(catId, userId, "older"); db.ThreadPosts.Add(older); await db.SaveChangesAsync();
        await Task.Delay(5);
        var newer = Post(catId, userId, "newer"); db.ThreadPosts.Add(newer); await db.SaveChangesAsync();

        var feed = await new GetThreadFeedQueryHandler(db).Handle(
            new GetThreadFeedQuery(CategorySlug: null, Sort: "new", Cursor: null, PageSize: 10), default);

        Assert.Equal("newer", feed.Items[0].Title);
    }

    [Fact]
    public async Task Top_sort_orders_by_like_count()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, catId, userId) = await Seed(t);
        var a = Post(catId, userId, "a"); a.AdjustLikeCount(1);
        var b = Post(catId, userId, "b"); b.AdjustLikeCount(5);
        db.ThreadPosts.AddRange(a, b); await db.SaveChangesAsync();

        var feed = await new GetThreadFeedQueryHandler(db).Handle(
            new GetThreadFeedQuery(null, "top", null, 10), default);

        Assert.Equal("b", feed.Items[0].Title);
    }

    [Fact]
    public async Task Deleted_posts_excluded_and_anon_authors_hidden()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, catId, userId) = await Seed(t);
        var user = await db.Users.FirstAsync(); user.AssignAnonHandle("quiet-koala-4821"); await db.SaveChangesAsync();
        var visible = Post(catId, userId, "visible", anon: true);
        var gone = Post(catId, userId, "gone"); gone.SoftDelete();
        db.ThreadPosts.AddRange(visible, gone); await db.SaveChangesAsync();

        var feed = await new GetThreadFeedQueryHandler(db).Handle(new GetThreadFeedQuery(null, "new", null, 10), default);

        Assert.Single(feed.Items);
        Assert.Equal("visible", feed.Items[0].Title);
        Assert.True(feed.Items[0].Author.IsAnonymous);
        Assert.Equal("quiet-koala-4821", feed.Items[0].Author.Handle);
        Assert.Null(feed.Items[0].Author.UserId);
    }

    [Fact]
    public async Task Cursor_paginates()
    {
        await using var t = await TestDb.CreateAsync();
        var (db, catId, userId) = await Seed(t);
        for (var i = 0; i < 3; i++) { db.ThreadPosts.Add(Post(catId, userId, $"p{i}")); await db.SaveChangesAsync(); await Task.Delay(2); }

        var page1 = await new GetThreadFeedQueryHandler(db).Handle(new GetThreadFeedQuery(null, "new", null, 2), default);
        Assert.Equal(2, page1.Items.Count);
        Assert.NotNull(page1.NextCursor);

        var page2 = await new GetThreadFeedQueryHandler(db).Handle(new GetThreadFeedQuery(null, "new", page1.NextCursor, 2), default);
        Assert.Single(page2.Items);
        Assert.Null(page2.NextCursor);
    }
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: DTOs**

`ThreadPostSummary.cs`:
```csharp
namespace Contracts.DTO.Threads;

public sealed record ThreadPostSummary(
    Guid Id,
    string CategorySlug,
    AuthorRef Author,
    string Title,
    string Excerpt,
    string? ThumbnailKey,
    int LikeCount,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt);
```
`ThreadFeedResponse.cs`:
```csharp
namespace Contracts.DTO.Threads;

public sealed record ThreadFeedResponse(IReadOnlyList<ThreadPostSummary> Items, string? NextCursor);
```

- [ ] **Step 4: Query + handler**

`GetThreadFeedQuery.cs`:
```csharp
using Contracts.DTO.Threads;
using MediatR;

namespace Application.Threads.Queries.GetThreadFeed;

public sealed record GetThreadFeedQuery(string? CategorySlug, string Sort, string? Cursor, int PageSize)
    : IRequest<ThreadFeedResponse>;
```
`GetThreadFeedQueryHandler.cs`:
```csharp
using Application.Common.Interfaces;
using Application.Threads;
using Contracts.DTO.Threads;
using Domain.Entities.Threads;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Threads.Queries.GetThreadFeed;

/// <summary>
/// PROVISIONAL Postgres feed. Plan 3 replaces this with an Elasticsearch read model
/// (search_after cursor + precomputed hot_rank). Keep the handler self-contained.
/// </summary>
public sealed class GetThreadFeedQueryHandler : IRequestHandler<GetThreadFeedQuery, ThreadFeedResponse>
{
    private const int CandidateWindow = 500;
    private readonly IApplicationDbContext _db;
    public GetThreadFeedQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ThreadFeedResponse> Handle(GetThreadFeedQuery request, CancellationToken ct)
    {
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, 50);
        var offset = ParseCursor(request.Cursor);

        var query = _db.ThreadPosts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.CategorySlug))
        {
            var slug = request.CategorySlug.ToLowerInvariant();
            query = query.Where(p => p.Category!.Slug == slug);
        }

        // Bounded candidate set ordered by recency, then sorted in memory for the requested mode.
        var candidates = await query
            .OrderByDescending(p => p.LastActivityAt)
            .Take(CandidateWindow)
            .ToListAsync(ct);

        IEnumerable<ThreadPost> ordered = request.Sort?.ToLowerInvariant() switch
        {
            "top" => candidates.OrderByDescending(p => p.LikeCount).ThenByDescending(p => p.CreatedAt),
            "hot" => candidates.OrderByDescending(HotScore),
            _ => candidates.OrderByDescending(p => p.CreatedAt) // "new"
        };

        var orderedList = ordered.ToList();
        var page = orderedList.Skip(offset).Take(pageSize).ToList();
        var next = offset + pageSize < orderedList.Count ? (offset + pageSize).ToString() : null;

        var items = page.Select(p => new ThreadPostSummary(
            p.Id,
            p.Category?.Slug ?? string.Empty,
            AuthorRefFactory.Create(p.IsAnonymous, p.Author!),
            p.Title,
            Excerpt(p.Body),
            p.Images.OrderBy(i => i.Ordinal).Select(i => i.R2Key).FirstOrDefault(),
            p.LikeCount,
            p.CommentCount,
            p.CreatedAt,
            p.LastActivityAt)).ToList();

        return new ThreadFeedResponse(items, next);
    }

    private static double HotScore(ThreadPost p)
    {
        var hours = (DateTimeOffset.UtcNow - p.CreatedAt).TotalHours;
        return (p.LikeCount + 2 * p.CommentCount) / Math.Pow(hours + 2, 1.8);
    }

    private static string Excerpt(string body) => body.Length <= 200 ? body : body[..200];

    private static int ParseCursor(string? cursor)
        => int.TryParse(cursor, out var n) && n >= 0 ? n : 0;
}
```

- [ ] **Step 5: Run feed tests — PASS (4). Full suite green.**
- [ ] **Step 6: Commit** — `git add src/Contracts/DTO/Threads/ThreadPostSummary.cs src/Contracts/DTO/Threads/ThreadFeedResponse.cs src/Application/Threads/Queries/GetThreadFeed/ tests/Application.UnitTests/Threads/GetThreadFeedTests.cs && git commit -m "feat: provisional Postgres thread feed (hot/new/top, cursor)"`

---

## Task 14: ThreadsController + admin role claim wiring

**Files:**
- Create: `src/Api/Controllers/ThreadsController.cs`
- Modify: `src/Application/Auth/Commands/AuthenticateUser/AuthenticateUserCommandHandler.cs`, `src/Application/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs`, and `AuthUserDtoFactory` usage — to emit an `Admin` role for admins (see below)
- Modify: `src/Api/Controllers/AuthController.cs` — issue role from `IsAdmin`

### Admin role claim
`ThreadCategoriesController` uses `[Authorize(Roles = "Admin")]`. Today `ITokenService.IssueAccessToken(userId, email, role)` is given `user.Role` (e.g. `"Student"`). To make admins actually admin, the controller should pass `"Admin"` when `user.IsAdmin`.

- [ ] **Step 1: Write a failing test for role selection**

Role selection (admin vs base role) is the one new piece of token logic; extract it into a small pure helper `RoleResolver` so it is unit-testable independent of the controller. Create the failing test first:

`tests/Application.UnitTests/Auth/RoleResolverTests.cs`:
```csharp
using Application.Auth;
using Xunit;

namespace Application.UnitTests.Auth;

public sealed class RoleResolverTests
{
    [Fact]
    public void Admin_user_resolves_admin_role()
        => Assert.Equal("Admin", RoleResolver.Resolve(baseRole: "Student", isAdmin: true));

    [Fact]
    public void Non_admin_keeps_base_role()
        => Assert.Equal("Student", RoleResolver.Resolve(baseRole: "Student", isAdmin: false));
}
```

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: Implement `RoleResolver`**

`src/Application/Auth/RoleResolver.cs`:
```csharp
namespace Application.Auth;

public static class RoleResolver
{
    public const string AdminRole = "Admin";

    public static string Resolve(string baseRole, bool isAdmin) => isAdmin ? AdminRole : baseRole;
}
```

- [ ] **Step 4: Use it in the controller's token issuance**

In `src/Api/Controllers/AuthController.cs`, in `IssueAuthResponse`, change the access-token line to emit the admin role:
```csharp
        var role = Application.Auth.RoleResolver.Resolve(user.Role, user.IsAdmin);
        var accessToken = _tokenService.IssueAccessToken(user.UserId, user.Email, role);
```
(`user` is an `AuthUserDto`, which already carries `Role` and `IsAdmin` from Plan 1.)

- [ ] **Step 5: Run — PASS. Build green.**

- [ ] **Step 6: Implement `ThreadsController`**

`src/Api/Controllers/ThreadsController.cs`:
```csharp
using Application.Threads.Commands.CreateThreadComment;
using Application.Threads.Commands.CreateThreadPost;
using Application.Threads.Commands.DeleteThreadPost;
using Application.Threads.Commands.ToggleThreadLike;
using Application.Threads.Commands.UpdateThreadPost;
using Application.Threads.Queries.GetThreadComments;
using Application.Threads.Queries.GetThreadFeed;
using Application.Threads.Queries.GetThreadPost;
using Contracts.DTO.Threads;
using Domain.Shared.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[Authorize]
[ApiController]
[Route("api/threads")]
public class ThreadsController : ControllerBase
{
    private readonly ISender _sender;
    public ThreadsController(ISender sender) => _sender = sender;

    [HttpGet("feed")]
    [ProducesResponseType(typeof(ThreadFeedResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Feed(
        [FromQuery] string? category, [FromQuery] string sort = "hot",
        [FromQuery] string? cursor = null, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _sender.Send(new GetThreadFeedQuery(category, sort, cursor, pageSize), ct));

    [HttpGet("posts/{postId:guid}")]
    [ProducesResponseType(typeof(ThreadPostDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPost(Guid postId, CancellationToken ct)
    {
        var post = await _sender.Send(new GetThreadPostQuery(postId), ct);
        return post is null ? NotFound() : Ok(post);
    }

    [HttpGet("posts/{postId:guid}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<ThreadCommentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComments(Guid postId, CancellationToken ct)
        => Ok(await _sender.Send(new GetThreadCommentsQuery(postId), ct));

    [HttpPost("posts")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePost([FromForm] CreateThreadPostRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var images = new List<ThreadPostImageUpload>();
        foreach (var file in request.Images ?? new List<Microsoft.AspNetCore.Http.IFormFile>())
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            images.Add(new ThreadPostImageUpload(ms.ToArray(), file.ContentType, file.FileName));
        }

        try
        {
            var id = await _sender.Send(new CreateThreadPostCommand(
                userId, request.CategoryId, request.Title, request.Body, request.IsAnonymous, images), ct);
            return CreatedAtAction(nameof(GetPost), new { postId = id }, new { id });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPatch("posts/{postId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdatePost(Guid postId, [FromBody] UpdateThreadPostRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            await _sender.Send(new UpdateThreadPostCommand(postId, userId, request.Title, request.Body), ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("posts/{postId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeletePost(Guid postId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var isAdmin = User.IsInRole("Admin");
        try
        {
            await _sender.Send(new DeleteThreadPostCommand(postId, userId, isAdmin), ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("posts/{postId:guid}/like")]
    [ProducesResponseType(typeof(LikeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> LikePost(Guid postId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { return Ok(await _sender.Send(new ToggleThreadLikeCommand(userId, ThreadLikeTarget.Post, postId), ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("posts/{postId:guid}/comments")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateComment(Guid postId, [FromBody] CreateThreadCommentRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var id = await _sender.Send(new CreateThreadCommentCommand(
                postId, request.ParentCommentId, userId, request.IsAnonymous, request.Body), ct);
            return CreatedAtAction(nameof(GetComments), new { postId }, new { id });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("comments/{commentId:guid}/like")]
    [ProducesResponseType(typeof(LikeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> LikeComment(Guid commentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try { return Ok(await _sender.Send(new ToggleThreadLikeCommand(userId, ThreadLikeTarget.Comment, commentId), ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out userId);
    }
}
```

> If `CreateThreadPostRequest`/`CreateThreadCommentRequest`/`UpdateThreadPostRequest` were placed in `src/Api/Models/` instead of `Contracts` (see notes in Tasks 8/10/12), adjust the usings accordingly.

- [ ] **Step 7: Build + full test suite green. Commit** — `git add src/Application/Auth/RoleResolver.cs src/Api/Controllers/ThreadsController.cs src/Api/Controllers/AuthController.cs tests/Application.UnitTests/Auth/RoleResolverTests.cs && git commit -m "feat: ThreadsController + admin role claim"`

---

## Task 15: Anon-leak contract test (the trust-model guard)

**Files:**
- Test: `tests/Application.UnitTests/Threads/AnonLeakContractTests.cs`

This test instantiates the read query handlers with seeded ANON content and asserts — by serializing the response DTOs to JSON — that NO author-identifying field (`authorUserId`, `userId`, `displayName`, `avatarUrl`, `email`) is present anywhere for anonymous content. This guards every read path against a future leaky field.

- [ ] **Step 1: Write the test**

```csharp
using System.Text.Json;
using Application.Threads.Queries.GetThreadComments;
using Application.Threads.Queries.GetThreadFeed;
using Application.Threads.Queries.GetThreadPost;
using Application.UnitTests.Common;
using Domain.Entities.Threads;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class AnonLeakContractTests
{
    private static readonly string[] ForbiddenKeys = { "userId", "displayName", "avatarUrl", "email", "authorUserId" };

    private static void AssertNoIdentityLeak(string json, string realDisplayName, Guid realUserId)
    {
        using var doc = JsonDocument.Parse(json);
        var leaks = new List<string>();
        Walk(doc.RootElement, leaks);

        // No identifying property names should carry non-null values anywhere in anon responses.
        Assert.True(leaks.Count == 0, $"Identity leak via keys: {string.Join(", ", leaks)}");
        // And the real values must not appear anywhere in the payload.
        Assert.DoesNotContain(realDisplayName, json);
        Assert.DoesNotContain(realUserId.ToString(), json);

        static void Walk(JsonElement el, List<string> leaks)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in el.EnumerateObject())
                    {
                        if (ForbiddenKeys.Contains(prop.Name, StringComparer.OrdinalIgnoreCase)
                            && prop.Value.ValueKind is not JsonValueKind.Null)
                        {
                            leaks.Add(prop.Name);
                        }
                        Walk(prop.Value, leaks);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in el.EnumerateArray()) Walk(item, leaks);
                    break;
            }
        }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Anon_post_detail_feed_and_comments_never_leak_identity()
    {
        await using var t = await TestDb.CreateAsync();
        var userId = Guid.NewGuid(); var catId = Guid.NewGuid();
        const string realName = "Sarah Chen";
        var user = new User(userId, "sarah@adelaide.edu.au", realName, DateTimeOffset.UtcNow, "Student", "hash",
            AdelaideDepartment.ComputerScience, AcademicDegree.Bachelor, UserSex.Other,
            avatarUrl: "https://x/y.png", isActive: true);
        user.AssignAnonHandle("quiet-koala-4821");
        t.Context.Users.Add(user);
        t.Context.ThreadCategories.Add(new ThreadCategory(catId, "general", "General", "x", "chat", 1));
        var post = new ThreadPost(Guid.NewGuid(), catId, userId, isAnonymous: true, "Title", "Body");
        t.Context.ThreadPosts.Add(post);
        var comment = new ThreadComment(Guid.NewGuid(), post.Id, null, userId, isAnonymous: true, "anon comment");
        t.Context.ThreadComments.Add(comment);
        post.RegisterCommentAdded(DateTimeOffset.UtcNow);
        await t.Context.SaveChangesAsync();

        var detail = await new GetThreadPostQueryHandler(t.Context).Handle(new GetThreadPostQuery(post.Id), default);
        var feed = await new GetThreadFeedQueryHandler(t.Context).Handle(new GetThreadFeedQuery(null, "new", null, 10), default);
        var comments = await new GetThreadCommentsQueryHandler(t.Context).Handle(new GetThreadCommentsQuery(post.Id), default);

        AssertNoIdentityLeak(JsonSerializer.Serialize(detail, Json), realName, userId);
        AssertNoIdentityLeak(JsonSerializer.Serialize(feed, Json), realName, userId);
        AssertNoIdentityLeak(JsonSerializer.Serialize(comments, Json), realName, userId);
    }
}
```

- [ ] **Step 2: Run it — it MUST PASS immediately** (the handlers already use `AuthorRefFactory`). If it fails, a read path is leaking — FIX the offending handler before committing (do not weaken the test).

- [ ] **Step 3: Commit** — `git add tests/Application.UnitTests/Threads/AnonLeakContractTests.cs && git commit -m "test: anon-leak contract guard across thread read paths"`

---

## Task 16: DI sanity, docs, and final verification

**Files:**
- Modify: `README.md`, `AGENTS.md`
- Verify: `src/Api/Program.cs` (no new DI needed — confirm)

- [ ] **Step 1: Confirm DI**

The new handlers depend only on `IApplicationDbContext`, `IObjectStorageService`, and `ISender` — all already registered (MediatR auto-registers handlers/validators by assembly scan; `IObjectStorageService` is registered in Plan-0 `Program.cs`). Grep to confirm there is nothing unregistered: `grep -rn "AddScoped\|AddSingleton" src/Api/Program.cs`. No changes expected. If the build/tests pass and the app starts, DI is fine.

- [ ] **Step 2: Build + FULL test suite — everything green.**

Run: `dotnet build Marketplace.sln --no-restore /m:1 /p:BuildInParallel=false` then `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false`.

- [ ] **Step 3: Docs**

In `README.md` and `AGENTS.md`, add a "Threads" endpoint section:
```
GET    /api/threads/categories                 list active categories (public)
POST   /api/threads/categories                 [admin] create category
PATCH  /api/threads/categories/{id}            [admin] update category
GET    /api/threads/feed?category=&sort=hot|new|top&cursor=&pageSize=
GET    /api/threads/posts/{id}                 post detail
GET    /api/threads/posts/{id}/comments        2-level comment tree
POST   /api/threads/posts                       create post (multipart; isAnonymous, images[])
PATCH  /api/threads/posts/{id}                  author edits title/body
DELETE /api/threads/posts/{id}                  author or admin soft-delete
POST   /api/threads/posts/{id}/like             toggle like
POST   /api/threads/posts/{id}/comments         add comment (parentCommentId optional, 1 level)
POST   /api/threads/comments/{id}/like          toggle like
```
Note: per-post identity is chosen at creation (`isAnonymous`) and is immutable; anonymous content is served under a stable per-user handle and never exposes real identity. The feed is Postgres-backed for now and moves to Elasticsearch in the Read Path plan.

- [ ] **Step 4: (Best-effort) docker smoke test** — if docker is available, `docker compose down -v` then `docker compose up --build -d`, confirm `/healthz` ok and `GET /api/threads/categories` returns the 7 seeded categories; then `docker compose down`. If docker is unavailable, note it and skip.

- [ ] **Step 5: Commit** — `git add README.md AGENTS.md && git commit -m "docs: document threads endpoints"`

---

## Done criteria for Plan 2

- Build green; full unit suite green (all new Threads tests + the unchanged Plan-1 tests).
- Admin-curated categories: seeded set, admin create/update, public active-only list.
- Posts: create (real or anon, with R2 images), detail, owner-edit, owner/admin soft-delete; anon posts trigger stable-handle assignment.
- Comments: 2-level enforced, anon-capable, bump post count + activity.
- Likes: idempotent toggle on posts + comments with count maintenance.
- Feed: Postgres Hot/New/Top with cursor pagination (provisional; Plan 3 swaps to Elasticsearch).
- **Anonymity trust model enforced at the API**: `AuthorRefFactory` is the single mapping point, and the `AnonLeakContractTests` guard asserts no read path leaks identity for anonymous content.
- Admin role claim flows from `User.IsAdmin` into the JWT so `[Authorize(Roles="Admin")]` works.

## Out of scope (later plans)
- Reports, admin moderation queue, moderation audit, anon-break-for-admin (**Plan 4**).
- Notifications (**Plan 4**).
- Outbox → RabbitMQ → Elasticsearch indexer, Redis feed cache, ES `search_after` feed (**Plan 3** — replaces Task 13's provisional feed).
- Image reordering / deletion endpoints, post pin/lock admin endpoints (future).
