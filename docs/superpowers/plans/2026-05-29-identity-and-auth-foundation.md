# Identity & Auth Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the user/profile model and replace stateless JWT auth with a Redis-backed refresh-token flow (rotation, logout-all) plus login rate limiting, laying the identity foundation the Threads subsystem builds on.

**Architecture:** All work lives inside the existing clean-architecture backend (`Domain`, `Application`, `Infrastructure`, `Api`, `Contracts`). New profile fields go on the existing `User` aggregate. Token issuance moves behind an `ITokenService` so handlers stay unit-testable. Refresh tokens and login-failure counters live in Redis behind `IRefreshTokenStore` / `ILoginRateLimiter` interfaces, registered via a new `IConnectionMultiplexer` singleton. This is Plan 1 of 4 (Threads Core, Read Path, Moderation & Notifications follow).

**Tech Stack:** ASP.NET Core 8, EF Core (Npgsql), MediatR, FluentValidation, StackExchange.Redis, xUnit, BCrypt.Net.

**Spec:** `docs/superpowers/specs/2026-05-29-threads-and-identity-overhaul-design.md` (Sections 4 profile fields, 5 profile/auth endpoints, 6 Redis keys & refresh flow).

---

## File Structure

**Create:**
- `backend/src/Application/Common/Interfaces/ITokenService.cs` — issues access JWTs + opaque refresh tokens
- `backend/src/Application/Common/Interfaces/IRefreshTokenStore.cs` — Redis-backed refresh-token persistence/revocation
- `backend/src/Application/Common/Interfaces/ILoginRateLimiter.cs` — Redis-backed login-failure throttle
- `backend/src/Application/Common/Interfaces/IAnonHandleGenerator.cs` — pure handle candidate generator
- `backend/src/Application/Users/Commands/UpdateProfile/UpdateProfileCommand.cs`
- `backend/src/Application/Users/Commands/UpdateProfile/UpdateProfileCommandHandler.cs`
- `backend/src/Application/Users/Commands/UpdateProfile/UpdateProfileCommandValidator.cs`
- `backend/src/Application/Users/Commands/GetOrCreateAnonHandle/GetOrCreateAnonHandleCommand.cs`
- `backend/src/Application/Users/Commands/GetOrCreateAnonHandle/GetOrCreateAnonHandleCommandHandler.cs`
- `backend/src/Application/Auth/Commands/RefreshToken/RefreshTokenCommand.cs`
- `backend/src/Application/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs`
- `backend/src/Application/Auth/Commands/Logout/LogoutCommand.cs`
- `backend/src/Application/Auth/Commands/Logout/LogoutCommandHandler.cs`
- `backend/src/Application/Auth/Commands/LogoutAll/LogoutAllCommand.cs`
- `backend/src/Application/Auth/Commands/LogoutAll/LogoutAllCommandHandler.cs`
- `backend/src/Infrastructure/Auth/AppJwtTokenService.cs` — `ITokenService` impl (moves logic out of `Api/Auth/AppJwt.cs`)
- `backend/src/Infrastructure/Auth/RedisRefreshTokenStore.cs`
- `backend/src/Infrastructure/Auth/RedisLoginRateLimiter.cs`
- `backend/src/Infrastructure/Auth/DefaultAnonHandleGenerator.cs`
- `backend/src/Contracts/DTO/Auth/RefreshTokenRequest.cs`
- `backend/src/Contracts/DTO/Users/UpdateProfileRequest.cs`
- `backend/src/Contracts/DTO/Users/AnonHandleResponse.cs`

**Modify:**
- `backend/src/Domain/Entities/Users/User.cs` — new fields + domain methods
- `backend/src/Infrastructure/Data/Configurations/UserConfiguration.cs` — map new columns
- `backend/src/Contracts/DTO/Auth/AuthResponse.cs` — add `RefreshToken` + new `AuthUserDto` fields
- `backend/src/Application/Auth/Commands/AuthenticateUser/AuthenticateUserCommand.cs` — carry IP, return result
- `backend/src/Application/Auth/Commands/AuthenticateUser/AuthenticateUserCommandHandler.cs` — rate-limit + new DTO fields
- `backend/src/Api/Controllers/AuthController.cs` — refresh/logout endpoints, token service, refresh issuance
- `backend/src/Api/Controllers/UsersController.cs` — profile + anon-handle endpoints
- `backend/src/Api/Auth/AppJwt.cs` — delete (logic moves to `AppJwtTokenService`)
- `backend/src/Infrastructure/Configuration/Options/AuthOptions.cs` — access/refresh TTLs, rate-limit thresholds
- `backend/src/Api/Program.cs` — register `IConnectionMultiplexer`, `ITokenService`, stores, rate limiter
- `backend/tests/Application.UnitTests/Auth/AuthCommandHandlerTests.cs` — adapt to new handler constructor
- `backend/db/seed.sql` — set `is_admin=true` for the seeded student account
- `README.md` / `AGENTS.md` — document new endpoints + env vars

**Test:**
- `backend/tests/Application.UnitTests/Users/UserProfileTests.cs`
- `backend/tests/Application.UnitTests/Users/AnonHandleTests.cs`
- `backend/tests/Application.UnitTests/Users/UpdateProfileCommandHandlerTests.cs`
- `backend/tests/Application.UnitTests/Auth/RefreshTokenCommandHandlerTests.cs`
- `backend/tests/Application.UnitTests/Auth/LoginRateLimitTests.cs`
- `backend/tests/Application.UnitTests/TestDoubles/` — in-memory fakes for the new interfaces

> **Build/test commands** (from `backend/`):
> Build: `dotnet build Marketplace.sln --no-restore /m:1 /p:BuildInParallel=false`
> Test: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false`
> Single test: append `--filter "FullyQualifiedName~<TestName>"`

---

## Task 1: Extend the User aggregate with profile fields

**Files:**
- Modify: `backend/src/Domain/Entities/Users/User.cs`
- Test: `backend/tests/Application.UnitTests/Users/UserProfileTests.cs`

- [ ] **Step 1: Write the failing test**

Create `backend/tests/Application.UnitTests/Users/UserProfileTests.cs`:

```csharp
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Xunit;

namespace Application.UnitTests.Users;

public sealed class UserProfileTests
{
    private static User NewUser() => new(
        id: Guid.NewGuid(),
        email: "student@adelaide.edu.au",
        displayName: "Local Student",
        createdAt: DateTimeOffset.UtcNow,
        role: "Student",
        passwordHash: "hash",
        department: AdelaideDepartment.ComputerScience,
        degree: AcademicDegree.Bachelor,
        sex: UserSex.Other);

    [Fact]
    public void New_user_has_default_identity_flags()
    {
        var user = NewUser();

        Assert.Null(user.Bio);
        Assert.Null(user.AnonHandle);
        Assert.False(user.AppearInDrawPool);
        Assert.False(user.IsAdmin);
    }

    [Fact]
    public void UpdateExtendedProfile_sets_bio_and_draw_pool_flag()
    {
        var user = NewUser();

        user.UpdateExtendedProfile("Second year CS, love board games.", appearInDrawPool: true);

        Assert.Equal("Second year CS, love board games.", user.Bio);
        Assert.True(user.AppearInDrawPool);
    }

    [Fact]
    public void AssignAnonHandle_sets_handle_once_and_rejects_reassignment()
    {
        var user = NewUser();

        user.AssignAnonHandle("quiet-koala-4821");
        var ex = Assert.Throws<InvalidOperationException>(() => user.AssignAnonHandle("loud-emu-0001"));

        Assert.Equal("quiet-koala-4821", user.AnonHandle);
        Assert.Contains("already", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false --filter "FullyQualifiedName~UserProfileTests"`
Expected: FAIL — `User` has no `Bio`, `AnonHandle`, `AppearInDrawPool`, `IsAdmin`, `UpdateExtendedProfile`, or `AssignAnonHandle`.

- [ ] **Step 3: Add fields and methods to `User`**

In `backend/src/Domain/Entities/Users/User.cs`, add four properties after the existing `ActivationTokenExpiresAt` property (around line 61):

```csharp
    public string? Bio { get; private set; }
    public string? AnonHandle { get; private set; }
    public bool AppearInDrawPool { get; private set; }
    public bool IsAdmin { get; private set; }
```

Then add these methods after `Activate()` (around line 105):

```csharp
    public void UpdateExtendedProfile(string? bio, bool appearInDrawPool)
    {
        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
        AppearInDrawPool = appearInDrawPool;
    }

    public void AssignAnonHandle(string handle)
    {
        if (!string.IsNullOrWhiteSpace(AnonHandle))
        {
            throw new InvalidOperationException("Anonymous handle is already assigned and cannot be changed.");
        }

        AnonHandle = handle;
    }

    public void SetAdmin(bool isAdmin)
    {
        IsAdmin = isAdmin;
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false --filter "FullyQualifiedName~UserProfileTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add backend/src/Domain/Entities/Users/User.cs backend/tests/Application.UnitTests/Users/UserProfileTests.cs
git commit -m "feat: add profile/identity fields to User aggregate"
```

---

## Task 2: Map new columns and create the migration

**Files:**
- Modify: `backend/src/Infrastructure/Data/Configurations/UserConfiguration.cs`
- Create: migration under `backend/src/Infrastructure/Data/Migrations/` (generated)

- [ ] **Step 1: Add column mappings**

In `backend/src/Infrastructure/Data/Configurations/UserConfiguration.cs`, inside `Configure`, after the `ActivationTokenExpiresAt` property mapping and before `builder.HasIndex(u => u.Email)`, add:

```csharp
        builder.Property(u => u.Bio)
            .HasMaxLength(280);

        builder.Property(u => u.AnonHandle)
            .HasMaxLength(64);

        builder.Property(u => u.AppearInDrawPool)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.IsAdmin)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(u => u.AnonHandle)
            .IsUnique()
            .HasFilter("\"AnonHandle\" IS NOT NULL");
```

> Note: the unique-filtered index allows many NULL handles (users who never posted anonymously) while keeping assigned handles unique. The quoted column name matches EF's default Npgsql casing; if the project uses snake_case naming, match the existing column-name convention seen in this file.

- [ ] **Step 2: Verify the project builds**

Run: `dotnet build Marketplace.sln --no-restore /m:1 /p:BuildInParallel=false`
Expected: Build succeeded.

- [ ] **Step 3: Generate the migration**

Run from `backend/`:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations add AddUserIdentityFields \
  --project src/Infrastructure/Infrastructure.csproj \
  --startup-project src/Api/Api.csproj \
  --output-dir Data/Migrations
```

Expected: a new `*_AddUserIdentityFields.cs` migration is created adding `Bio`, `AnonHandle`, `AppearInDrawPool`, `IsAdmin` columns and the filtered unique index.

- [ ] **Step 4: Inspect the migration**

Open the generated migration and confirm `Up()` adds exactly the four columns + index and `Down()` removes them. No data-destructive operations.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Infrastructure/Data/Configurations/UserConfiguration.cs backend/src/Infrastructure/Data/Migrations/
git commit -m "feat: migrate User identity columns"
```

---

## Task 3: Anonymous handle generator (pure)

**Files:**
- Create: `backend/src/Application/Common/Interfaces/IAnonHandleGenerator.cs`
- Create: `backend/src/Infrastructure/Auth/DefaultAnonHandleGenerator.cs`
- Test: `backend/tests/Application.UnitTests/Users/AnonHandleTests.cs`

- [ ] **Step 1: Write the failing test**

Create `backend/tests/Application.UnitTests/Users/AnonHandleTests.cs`:

```csharp
using System.Text.RegularExpressions;
using Infrastructure.Auth;
using Xunit;

namespace Application.UnitTests.Users;

public sealed class AnonHandleTests
{
    [Fact]
    public void Generate_matches_adjective_noun_number_pattern()
    {
        var generator = new DefaultAnonHandleGenerator();

        var handle = generator.Generate();

        Assert.Matches(new Regex("^[a-z]+-[a-z]+-[0-9]{4}$"), handle);
    }

    [Fact]
    public void Generate_produces_varied_values()
    {
        var generator = new DefaultAnonHandleGenerator();

        var handles = Enumerable.Range(0, 50).Select(_ => generator.Generate()).ToHashSet();

        // Not a strict uniqueness guarantee, but 50 draws should not all collide.
        Assert.True(handles.Count > 1);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false --filter "FullyQualifiedName~AnonHandleTests"`
Expected: FAIL — `DefaultAnonHandleGenerator` does not exist.

- [ ] **Step 3: Create the interface and implementation**

Create `backend/src/Application/Common/Interfaces/IAnonHandleGenerator.cs`:

```csharp
namespace Application.Common.Interfaces;

public interface IAnonHandleGenerator
{
    /// <summary>Returns a candidate handle of the form "adjective-noun-NNNN". Uniqueness is enforced by the caller.</summary>
    string Generate();
}
```

Create `backend/src/Infrastructure/Auth/DefaultAnonHandleGenerator.cs`:

```csharp
using System.Security.Cryptography;
using Application.Common.Interfaces;

namespace Infrastructure.Auth;

public sealed class DefaultAnonHandleGenerator : IAnonHandleGenerator
{
    private static readonly string[] Adjectives =
    {
        "quiet", "brave", "happy", "clever", "gentle", "swift", "calm", "bright",
        "lucky", "mellow", "nimble", "witty", "sunny", "bold", "cosy", "keen"
    };

    private static readonly string[] Nouns =
    {
        "koala", "emu", "quokka", "wombat", "magpie", "possum", "dingo", "echidna",
        "galah", "numbat", "bilby", "kelpie", "lorikeet", "platypus", "wallaby", "kookaburra"
    };

    public string Generate()
    {
        var adjective = Adjectives[RandomNumberGenerator.GetInt32(Adjectives.Length)];
        var noun = Nouns[RandomNumberGenerator.GetInt32(Nouns.Length)];
        var suffix = RandomNumberGenerator.GetInt32(0, 10000).ToString("D4");
        return $"{adjective}-{noun}-{suffix}";
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false --filter "FullyQualifiedName~AnonHandleTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add backend/src/Application/Common/Interfaces/IAnonHandleGenerator.cs backend/src/Infrastructure/Auth/DefaultAnonHandleGenerator.cs backend/tests/Application.UnitTests/Users/AnonHandleTests.cs
git commit -m "feat: add anonymous handle generator"
```

---

## Task 4: Get-or-create anon handle command (collision retry)

**Files:**
- Create: `backend/src/Application/Users/Commands/GetOrCreateAnonHandle/GetOrCreateAnonHandleCommand.cs`
- Create: `backend/src/Application/Users/Commands/GetOrCreateAnonHandle/GetOrCreateAnonHandleCommandHandler.cs`
- Test: `backend/tests/Application.UnitTests/Users/AnonHandleCommandTests.cs`

- [ ] **Step 1: Write the failing test**

Create `backend/tests/Application.UnitTests/Users/AnonHandleCommandTests.cs`:

```csharp
using Application.Common.Interfaces;
using Application.UnitTests.Common;
using Application.Users.Commands.GetOrCreateAnonHandle;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Users;

public sealed class AnonHandleCommandTests
{
    // Generator that returns a queued sequence so we can force a collision.
    private sealed class QueuedGenerator : IAnonHandleGenerator
    {
        private readonly Queue<string> _values;
        public QueuedGenerator(params string[] values) => _values = new Queue<string>(values);
        public string Generate() => _values.Dequeue();
    }

    private static User NewActiveUser(Guid id) => new(
        id: id,
        email: $"user-{id:N}@adelaide.edu.au",
        displayName: "Student",
        createdAt: DateTimeOffset.UtcNow,
        role: "Student",
        passwordHash: "hash",
        department: AdelaideDepartment.ComputerScience,
        degree: AcademicDegree.Bachelor,
        sex: UserSex.Other,
        isActive: true);

    [Fact]
    public async Task Returns_existing_handle_without_regenerating()
    {
        await using var db = await TestDb.CreateAsync();
        var user = NewActiveUser(Guid.NewGuid());
        user.AssignAnonHandle("existing-koala-1111");
        db.Context.Users.Add(user);
        await db.Context.SaveChangesAsync();

        var handler = new GetOrCreateAnonHandleCommandHandler(db.Context, new QueuedGenerator("new-emu-2222"));
        var result = await handler.Handle(new GetOrCreateAnonHandleCommand(user.Id), CancellationToken.None);

        Assert.Equal("existing-koala-1111", result);
    }

    [Fact]
    public async Task Generates_and_persists_handle_on_first_call()
    {
        await using var db = await TestDb.CreateAsync();
        var user = NewActiveUser(Guid.NewGuid());
        db.Context.Users.Add(user);
        await db.Context.SaveChangesAsync();

        var handler = new GetOrCreateAnonHandleCommandHandler(db.Context, new QueuedGenerator("quiet-koala-4821"));
        var result = await handler.Handle(new GetOrCreateAnonHandleCommand(user.Id), CancellationToken.None);

        var saved = await db.Context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal("quiet-koala-4821", result);
        Assert.Equal("quiet-koala-4821", saved.AnonHandle);
    }

    [Fact]
    public async Task Retries_when_first_candidate_collides()
    {
        await using var db = await TestDb.CreateAsync();
        var taken = NewActiveUser(Guid.NewGuid());
        taken.AssignAnonHandle("quiet-koala-4821");
        db.Context.Users.Add(taken);
        var caller = NewActiveUser(Guid.NewGuid());
        db.Context.Users.Add(caller);
        await db.Context.SaveChangesAsync();

        // First candidate collides with `taken`; second is free.
        var handler = new GetOrCreateAnonHandleCommandHandler(
            db.Context, new QueuedGenerator("quiet-koala-4821", "brave-emu-0007"));
        var result = await handler.Handle(new GetOrCreateAnonHandleCommand(caller.Id), CancellationToken.None);

        Assert.Equal("brave-emu-0007", result);
    }
}
```

> This task depends on the shared `TestDb` helper. If it has not yet been extracted (it currently lives privately inside `AuthCommandHandlerTests`), create it first — see Task 4a below — then return here.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false --filter "FullyQualifiedName~AnonHandleCommandTests"`
Expected: FAIL — command/handler do not exist (and `Application.UnitTests.Common.TestDb` may not exist yet).

- [ ] **Step 3a (Task 4a): Extract the shared `TestDb` helper**

Create `backend/tests/Application.UnitTests/Common/TestDb.cs`:

```csharp
using Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Common;

public sealed class TestDb : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private TestDb(SqliteConnection connection, MarketplaceDbContext context)
    {
        _connection = connection;
        Context = context;
    }

    public MarketplaceDbContext Context { get; }

    public static async Task<TestDb> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new MarketplaceDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new TestDb(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
```

Then in `backend/tests/Application.UnitTests/Auth/AuthCommandHandlerTests.cs`, delete the private nested `TestDb` class (lines defining `private sealed class TestDb ...`) and add `using Application.UnitTests.Common;` at the top so the existing tests use the shared helper.

> Note: SQLite's `EnsureCreatedAsync` builds the schema from the model, including the filtered unique index from Task 2. SQLite supports partial indexes, so the collision test exercises real uniqueness behaviour.

- [ ] **Step 3b: Create the command and handler**

Create `backend/src/Application/Users/Commands/GetOrCreateAnonHandle/GetOrCreateAnonHandleCommand.cs`:

```csharp
using MediatR;

namespace Application.Users.Commands.GetOrCreateAnonHandle;

public sealed record GetOrCreateAnonHandleCommand(Guid UserId) : IRequest<string>;
```

Create `backend/src/Application/Users/Commands/GetOrCreateAnonHandle/GetOrCreateAnonHandleCommandHandler.cs`:

```csharp
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Users.Commands.GetOrCreateAnonHandle;

public sealed class GetOrCreateAnonHandleCommandHandler : IRequestHandler<GetOrCreateAnonHandleCommand, string>
{
    private const int MaxAttempts = 5;

    private readonly IApplicationDbContext _dbContext;
    private readonly IAnonHandleGenerator _generator;

    public GetOrCreateAnonHandleCommandHandler(IApplicationDbContext dbContext, IAnonHandleGenerator generator)
    {
        _dbContext = dbContext;
        _generator = generator;
    }

    public async Task<string> Handle(GetOrCreateAnonHandleCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (!string.IsNullOrWhiteSpace(user.AnonHandle))
        {
            return user.AnonHandle;
        }

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = _generator.Generate();
            var taken = await _dbContext.Users.AnyAsync(u => u.AnonHandle == candidate, cancellationToken);
            if (taken)
            {
                continue;
            }

            user.AssignAnonHandle(candidate);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return candidate;
        }

        throw new InvalidOperationException("Could not allocate a unique anonymous handle after multiple attempts.");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false --filter "FullyQualifiedName~AnonHandleCommandTests"`
Then run the full suite to confirm the `TestDb` extraction did not break auth tests:
Run: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false`
Expected: PASS (all existing + 3 new).

- [ ] **Step 5: Commit**

```bash
git add backend/src/Application/Users/Commands/GetOrCreateAnonHandle/ backend/tests/Application.UnitTests/Common/TestDb.cs backend/tests/Application.UnitTests/Auth/AuthCommandHandlerTests.cs backend/tests/Application.UnitTests/Users/AnonHandleCommandTests.cs
git commit -m "feat: get-or-create anon handle with collision retry"
```

---

## Task 5: Update-profile command

**Files:**
- Create: `backend/src/Application/Users/Commands/UpdateProfile/UpdateProfileCommand.cs`
- Create: `backend/src/Application/Users/Commands/UpdateProfile/UpdateProfileCommandHandler.cs`
- Create: `backend/src/Application/Users/Commands/UpdateProfile/UpdateProfileCommandValidator.cs`
- Test: `backend/tests/Application.UnitTests/Users/UpdateProfileCommandHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `backend/tests/Application.UnitTests/Users/UpdateProfileCommandHandlerTests.cs`:

```csharp
using Application.UnitTests.Common;
using Application.Users.Commands.UpdateProfile;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Users;

public sealed class UpdateProfileCommandHandlerTests
{
    private static User NewActiveUser(Guid id) => new(
        id: id,
        email: "student@adelaide.edu.au",
        displayName: "Student",
        createdAt: DateTimeOffset.UtcNow,
        role: "Student",
        passwordHash: "hash",
        department: AdelaideDepartment.ComputerScience,
        degree: AcademicDegree.Bachelor,
        sex: UserSex.Other,
        isActive: true);

    [Fact]
    public async Task Updates_bio_and_draw_pool_flag()
    {
        await using var db = await TestDb.CreateAsync();
        var id = Guid.NewGuid();
        db.Context.Users.Add(NewActiveUser(id));
        await db.Context.SaveChangesAsync();

        var handler = new UpdateProfileCommandHandler(db.Context);
        await handler.Handle(new UpdateProfileCommand(id, "Hi, I'm in CS.", true), CancellationToken.None);

        var saved = await db.Context.Users.SingleAsync(u => u.Id == id);
        Assert.Equal("Hi, I'm in CS.", saved.Bio);
        Assert.True(saved.AppearInDrawPool);
    }

    [Fact]
    public async Task Throws_when_user_missing()
    {
        await using var db = await TestDb.CreateAsync();
        var handler = new UpdateProfileCommandHandler(db.Context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UpdateProfileCommand(Guid.NewGuid(), "x", false), CancellationToken.None));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false --filter "FullyQualifiedName~UpdateProfileCommandHandlerTests"`
Expected: FAIL — command/handler do not exist.

- [ ] **Step 3: Create command, handler, validator**

Create `backend/src/Application/Users/Commands/UpdateProfile/UpdateProfileCommand.cs`:

```csharp
using MediatR;

namespace Application.Users.Commands.UpdateProfile;

public sealed record UpdateProfileCommand(Guid UserId, string? Bio, bool AppearInDrawPool) : IRequest;
```

Create `backend/src/Application/Users/Commands/UpdateProfile/UpdateProfileCommandHandler.cs`:

```csharp
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Users.Commands.UpdateProfile;

public sealed class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateProfileCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.UpdateExtendedProfile(request.Bio, request.AppearInDrawPool);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

Create `backend/src/Application/Users/Commands/UpdateProfile/UpdateProfileCommandValidator.cs`:

```csharp
using FluentValidation;

namespace Application.Users.Commands.UpdateProfile;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(c => c.Bio)
            .MaximumLength(280)
            .When(c => c.Bio is not null);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false --filter "FullyQualifiedName~UpdateProfileCommandHandlerTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add backend/src/Application/Users/Commands/UpdateProfile/ backend/tests/Application.UnitTests/Users/UpdateProfileCommandHandlerTests.cs
git commit -m "feat: add update-profile command"
```

---

## Task 6: Token service abstraction (replaces static AppJwt)

**Files:**
- Create: `backend/src/Application/Common/Interfaces/ITokenService.cs`
- Create: `backend/src/Infrastructure/Auth/AppJwtTokenService.cs`
- Modify: `backend/src/Infrastructure/Configuration/Options/AuthOptions.cs`
- Test: `backend/tests/Application.UnitTests/Auth/AppJwtTokenServiceTests.cs`

> Note: `Api/Auth/AppJwt.cs` is NOT deleted in this task. It stays until the auth cutover (Task 8), where the controller stops calling it and it is removed in the same green-ending commit. This keeps the `Api` project building after every task.

- [ ] **Step 1: Add TTL/threshold options to `AuthOptions`**

In `backend/src/Infrastructure/Configuration/Options/AuthOptions.cs`, add these properties inside the class (after `ActivationBaseUrl`):

```csharp
    [Range(1, 120)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; init; } = 14;

    [Range(1, 100)]
    public int LoginMaxFailuresPerEmail { get; init; } = 5;

    [Range(1, 200)]
    public int LoginMaxFailuresPerIp { get; init; } = 10;

    [Range(1, 1440)]
    public int LoginFailureWindowMinutes { get; init; } = 15;
```

- [ ] **Step 2: Write the failing test**

Create `backend/tests/Application.UnitTests/Auth/AppJwtTokenServiceTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using Infrastructure.Auth;
using Infrastructure.Configuration.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace Application.UnitTests.Auth;

public sealed class AppJwtTokenServiceTests
{
    private static AppJwtTokenService NewService() => new(Options.Create(new AuthOptions
    {
        AppJwtIssuer = "http://localhost",
        AppJwtSigningKey = "local-development-signing-key-change-me-please",
        AllowedEmailDomain = "adelaide.edu.au",
        ActivationBaseUrl = "https://localhost:7123/api/auth/activate",
        AccessTokenMinutes = 15
    }));

    [Fact]
    public void IssueAccessToken_embeds_sub_email_and_role()
    {
        var service = NewService();
        var userId = Guid.NewGuid();

        var jwt = service.IssueAccessToken(userId, "student@adelaide.edu.au", "Student");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        Assert.Equal(userId.ToString(), token.Subject);
        Assert.Contains(token.Claims, c => c.Type == "email" && c.Value == "student@adelaide.edu.au");
        Assert.Contains(token.Claims, c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Student");
        Assert.True(token.ValidTo <= DateTime.UtcNow.AddMinutes(16));
    }

    [Fact]
    public void GenerateRefreshToken_returns_distinct_opaque_values()
    {
        var service = NewService();

        var a = service.GenerateRefreshToken();
        var b = service.GenerateRefreshToken();

        Assert.NotEqual(a, b);
        Assert.True(a.Length >= 32);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false --filter "FullyQualifiedName~AppJwtTokenServiceTests"`
Expected: FAIL — `ITokenService`/`AppJwtTokenService` do not exist.

- [ ] **Step 4: Create the interface and implementation, delete static helper**

Create `backend/src/Application/Common/Interfaces/ITokenService.cs`:

```csharp
namespace Application.Common.Interfaces;

public interface ITokenService
{
    string IssueAccessToken(Guid userId, string email, string? role);
    string GenerateRefreshToken();
}
```

Create `backend/src/Infrastructure/Auth/AppJwtTokenService.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Common.Interfaces;
using Infrastructure.Configuration.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Auth;

public sealed class AppJwtTokenService : ITokenService
{
    private readonly AuthOptions _options;

    public AppJwtTokenService(IOptions<AuthOptions> options)
    {
        _options = options.Value;
    }

    public string IssueAccessToken(Guid userId, string email, string? role)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };

        if (!string.IsNullOrWhiteSpace(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.AppJwtSigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.AppJwtIssuer,
            audience: _options.AppJwtIssuer,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
```

Leave `backend/src/Api/Auth/AppJwt.cs` in place for now — the controller still calls `AppJwt.Issue`. It is removed during the auth cutover in Task 8.

- [ ] **Step 5: Build + run the new unit test**

Run: `dotnet build Marketplace.sln --no-restore /m:1 /p:BuildInParallel=false`
Expected: Build succeeded (the new service coexists with the still-present `AppJwt`).
Then: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false --filter "FullyQualifiedName~AppJwtTokenServiceTests"`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add backend/src/Application/Common/Interfaces/ITokenService.cs backend/src/Infrastructure/Auth/AppJwtTokenService.cs backend/src/Infrastructure/Configuration/Options/AuthOptions.cs backend/tests/Application.UnitTests/Auth/AppJwtTokenServiceTests.cs
git commit -m "feat: add ITokenService for JWT + refresh-token issuance"
```

---

## Task 7: Refresh-token store + login rate limiter (interfaces + in-memory fakes + Redis impls)

**Files:**
- Create: `backend/src/Application/Common/Interfaces/IRefreshTokenStore.cs`
- Create: `backend/src/Application/Common/Interfaces/ILoginRateLimiter.cs`
- Create: `backend/src/Infrastructure/Auth/RedisRefreshTokenStore.cs`
- Create: `backend/src/Infrastructure/Auth/RedisLoginRateLimiter.cs`
- Create: `backend/tests/Application.UnitTests/TestDoubles/InMemoryRefreshTokenStore.cs`
- Create: `backend/tests/Application.UnitTests/TestDoubles/InMemoryLoginRateLimiter.cs`
- Test: `backend/tests/Application.UnitTests/Auth/InMemoryRefreshTokenStoreTests.cs`

- [ ] **Step 1: Write the failing test (behavioural contract via the in-memory fake)**

Create `backend/tests/Application.UnitTests/Auth/InMemoryRefreshTokenStoreTests.cs`:

```csharp
using Application.UnitTests.TestDoubles;
using Xunit;

namespace Application.UnitTests.Auth;

public sealed class InMemoryRefreshTokenStoreTests
{
    [Fact]
    public async Task Stored_token_validates_to_its_user()
    {
        var store = new InMemoryRefreshTokenStore();
        var userId = Guid.NewGuid();
        await store.StoreAsync(userId, "tok-1", TimeSpan.FromDays(14));

        Assert.Equal(userId, await store.ValidateAsync("tok-1"));
    }

    [Fact]
    public async Task Revoked_token_no_longer_validates()
    {
        var store = new InMemoryRefreshTokenStore();
        var userId = Guid.NewGuid();
        await store.StoreAsync(userId, "tok-1", TimeSpan.FromDays(14));

        await store.RevokeAsync("tok-1");

        Assert.Null(await store.ValidateAsync("tok-1"));
    }

    [Fact]
    public async Task RevokeAll_invalidates_every_token_for_user()
    {
        var store = new InMemoryRefreshTokenStore();
        var userId = Guid.NewGuid();
        await store.StoreAsync(userId, "tok-1", TimeSpan.FromDays(14));
        await store.StoreAsync(userId, "tok-2", TimeSpan.FromDays(14));

        await store.RevokeAllAsync(userId);

        Assert.Null(await store.ValidateAsync("tok-1"));
        Assert.Null(await store.ValidateAsync("tok-2"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false --filter "FullyQualifiedName~InMemoryRefreshTokenStoreTests"`
Expected: FAIL — interfaces and fake do not exist.

- [ ] **Step 3: Create interfaces**

Create `backend/src/Application/Common/Interfaces/IRefreshTokenStore.cs`:

```csharp
namespace Application.Common.Interfaces;

public interface IRefreshTokenStore
{
    Task StoreAsync(Guid userId, string refreshToken, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>Returns the owning user id if the token is valid, otherwise null.</summary>
    Task<Guid?> ValidateAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default);
}
```

Create `backend/src/Application/Common/Interfaces/ILoginRateLimiter.cs`:

```csharp
namespace Application.Common.Interfaces;

public interface ILoginRateLimiter
{
    Task<bool> IsBlockedAsync(string email, string ipAddress, CancellationToken cancellationToken = default);

    Task RecordFailureAsync(string email, string ipAddress, CancellationToken cancellationToken = default);

    Task ResetAsync(string email, string ipAddress, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Create the in-memory fakes**

Create `backend/tests/Application.UnitTests/TestDoubles/InMemoryRefreshTokenStore.cs`:

```csharp
using System.Collections.Concurrent;
using Application.Common.Interfaces;

namespace Application.UnitTests.TestDoubles;

public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, Guid> _tokens = new();

    public Task StoreAsync(Guid userId, string refreshToken, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        _tokens[refreshToken] = userId;
        return Task.CompletedTask;
    }

    public Task<Guid?> ValidateAsync(string refreshToken, CancellationToken cancellationToken = default)
        => Task.FromResult(_tokens.TryGetValue(refreshToken, out var userId) ? userId : (Guid?)null);

    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        _tokens.TryRemove(refreshToken, out _);
        return Task.CompletedTask;
    }

    public Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        foreach (var entry in _tokens.Where(kvp => kvp.Value == userId).ToList())
        {
            _tokens.TryRemove(entry.Key, out _);
        }

        return Task.CompletedTask;
    }
}
```

Create `backend/tests/Application.UnitTests/TestDoubles/InMemoryLoginRateLimiter.cs`:

```csharp
using System.Collections.Concurrent;
using Application.Common.Interfaces;

namespace Application.UnitTests.TestDoubles;

public sealed class InMemoryLoginRateLimiter : ILoginRateLimiter
{
    private readonly ConcurrentDictionary<string, int> _failures = new();
    private readonly int _threshold;

    public InMemoryLoginRateLimiter(int threshold = 5) => _threshold = threshold;

    public Task<bool> IsBlockedAsync(string email, string ipAddress, CancellationToken cancellationToken = default)
        => Task.FromResult(_failures.GetValueOrDefault(email) >= _threshold);

    public Task RecordFailureAsync(string email, string ipAddress, CancellationToken cancellationToken = default)
    {
        _failures.AddOrUpdate(email, 1, (_, count) => count + 1);
        return Task.CompletedTask;
    }

    public Task ResetAsync(string email, string ipAddress, CancellationToken cancellationToken = default)
    {
        _failures.TryRemove(email, out _);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 5: Create the Redis implementations**

Create `backend/src/Infrastructure/Auth/RedisRefreshTokenStore.cs`:

```csharp
using Application.Common.Interfaces;
using StackExchange.Redis;

namespace Infrastructure.Auth;

public sealed class RedisRefreshTokenStore : IRefreshTokenStore
{
    private readonly IConnectionMultiplexer _redis;

    public RedisRefreshTokenStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    private static string TokenKey(string token) => $"auth:refresh:{token}";
    private static string UserSetKey(Guid userId) => $"auth:refresh-by-user:{userId}";

    public async Task StoreAsync(Guid userId, string refreshToken, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync(TokenKey(refreshToken), userId.ToString(), ttl);
        await db.SetAddAsync(UserSetKey(userId), refreshToken);
        await db.KeyExpireAsync(UserSetKey(userId), ttl);
    }

    public async Task<Guid?> ValidateAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var value = await _redis.GetDatabase().StringGetAsync(TokenKey(refreshToken));
        return value.HasValue && Guid.TryParse(value!, out var userId) ? userId : null;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(TokenKey(refreshToken));
        await db.KeyDeleteAsync(TokenKey(refreshToken));
        if (value.HasValue && Guid.TryParse(value!, out var userId))
        {
            await db.SetRemoveAsync(UserSetKey(userId), refreshToken);
        }
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var tokens = await db.SetMembersAsync(UserSetKey(userId));
        foreach (var token in tokens)
        {
            await db.KeyDeleteAsync(TokenKey(token!));
        }

        await db.KeyDeleteAsync(UserSetKey(userId));
    }
}
```

Create `backend/src/Infrastructure/Auth/RedisLoginRateLimiter.cs`:

```csharp
using Application.Common.Interfaces;
using Infrastructure.Configuration.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infrastructure.Auth;

public sealed class RedisLoginRateLimiter : ILoginRateLimiter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly AuthOptions _options;

    public RedisLoginRateLimiter(IConnectionMultiplexer redis, IOptions<AuthOptions> options)
    {
        _redis = redis;
        _options = options.Value;
    }

    private static string EmailKey(string email) => $"auth:login-fail:email:{email.ToLowerInvariant()}";
    private static string IpKey(string ip) => $"auth:login-fail:ip:{ip}";

    public async Task<bool> IsBlockedAsync(string email, string ipAddress, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var emailCount = (int)await db.StringGetAsync(EmailKey(email));
        var ipCount = (int)await db.StringGetAsync(IpKey(ipAddress));
        return emailCount >= _options.LoginMaxFailuresPerEmail || ipCount >= _options.LoginMaxFailuresPerIp;
    }

    public async Task RecordFailureAsync(string email, string ipAddress, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var window = TimeSpan.FromMinutes(_options.LoginFailureWindowMinutes);
        await IncrementWithWindow(db, EmailKey(email), window);
        await IncrementWithWindow(db, IpKey(ipAddress), window);
    }

    public async Task ResetAsync(string email, string ipAddress, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(EmailKey(email));
        await db.KeyDeleteAsync(IpKey(ipAddress));
    }

    private static async Task IncrementWithWindow(IDatabase db, string key, TimeSpan window)
    {
        var count = await db.StringIncrementAsync(key);
        if (count == 1)
        {
            await db.KeyExpireAsync(key, window);
        }
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false --filter "FullyQualifiedName~InMemoryRefreshTokenStoreTests"`
Expected: PASS (3 tests).

> The `StackExchange.Redis` package is required by `Infrastructure`. If `dotnet build` reports it missing, add it: `dotnet add src/Infrastructure/Infrastructure.csproj package StackExchange.Redis`.

- [ ] **Step 7: Commit**

```bash
git add backend/src/Application/Common/Interfaces/IRefreshTokenStore.cs backend/src/Application/Common/Interfaces/ILoginRateLimiter.cs backend/src/Infrastructure/Auth/RedisRefreshTokenStore.cs backend/src/Infrastructure/Auth/RedisLoginRateLimiter.cs backend/tests/Application.UnitTests/TestDoubles/ backend/tests/Application.UnitTests/Auth/InMemoryRefreshTokenStoreTests.cs
git commit -m "feat: add Redis refresh-token store and login rate limiter"
```

---

## Task 8: Auth cutover (atomic — ends green)

> **Why one task:** changing `AuthResponse` to carry a refresh token, changing the `AuthenticateUser` handler's return type, adding the refresh/logout commands, rewiring `AuthController`, and deleting `AppJwt.cs` are mutually dependent — any subset leaves the `Api` project not building. Execute all steps before the final build/commit so the repository never has a broken-build commit. The steps are still individually small; only the build (Step 11) and commit (Step 12) are deferred to the end.

**Files:**
- Modify: `backend/src/Contracts/DTO/Auth/AuthResponse.cs`
- Create: `backend/src/Contracts/DTO/Auth/RefreshTokenRequest.cs`
- Modify: `backend/src/Application/Auth/Commands/AuthenticateUser/AuthenticateUserCommand.cs`
- Modify: `backend/src/Application/Auth/Commands/AuthenticateUser/AuthenticateUserCommandHandler.cs`
- Create: `backend/src/Application/Auth/Commands/RefreshToken/RefreshTokenCommand.cs` + `...Handler.cs`
- Create: `backend/src/Application/Auth/Commands/Logout/LogoutCommand.cs` + `...Handler.cs`
- Create: `backend/src/Application/Auth/Commands/LogoutAll/LogoutAllCommand.cs` + `...Handler.cs`
- Modify: `backend/src/Api/Controllers/AuthController.cs`
- Delete: `backend/src/Api/Auth/AppJwt.cs`
- Modify: `backend/tests/Application.UnitTests/Auth/AuthCommandHandlerTests.cs`
- Test: `backend/tests/Application.UnitTests/Auth/LoginRateLimitTests.cs`, `backend/tests/Application.UnitTests/Auth/RefreshTokenCommandHandlerTests.cs`

- [ ] **Step 1: Extend the auth DTOs**

In `backend/src/Contracts/DTO/Auth/AuthResponse.cs`, change `AuthResponse` to carry a refresh token and add the new identity fields to `AuthUserDto`:

```csharp
using Domain.Shared.Enums;

namespace Contracts.DTO.Auth;

public sealed record AuthResponse(string Token, string RefreshToken, AuthUserDto User);

public sealed record AuthUserDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    AdelaideDepartment Department,
    AcademicDegree Degree,
    UserSex Sex,
    string? AvatarUrl,
    Nationality? Nationality,
    int? Age,
    string? Bio,
    bool AppearInDrawPool,
    bool IsAdmin);
```

Also create `backend/src/Contracts/DTO/Auth/RefreshTokenRequest.cs` (used by the refresh/logout endpoints in Step 6):

```csharp
using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Auth;

public sealed class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}
```

- [ ] **Step 2: Write the failing rate-limit test**

Create `backend/tests/Application.UnitTests/Auth/LoginRateLimitTests.cs`:

```csharp
using Application.Auth.Commands.AuthenticateUser;
using Application.UnitTests.Common;
using Application.UnitTests.TestDoubles;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Xunit;

namespace Application.UnitTests.Auth;

public sealed class LoginRateLimitTests
{
    private static User NewActiveUser() => new(
        id: Guid.NewGuid(),
        email: "student@adelaide.edu.au",
        displayName: "Student",
        createdAt: DateTimeOffset.UtcNow,
        role: "Student",
        passwordHash: BCrypt.Net.BCrypt.HashPassword("ChangeMe123!"),
        department: AdelaideDepartment.ComputerScience,
        degree: AcademicDegree.Bachelor,
        sex: UserSex.Other,
        isActive: true);

    [Fact]
    public async Task Wrong_password_records_failure_and_returns_invalid()
    {
        await using var db = await TestDb.CreateAsync();
        db.Context.Users.Add(NewActiveUser());
        await db.Context.SaveChangesAsync();
        var limiter = new InMemoryLoginRateLimiter(threshold: 3);
        var handler = new AuthenticateUserCommandHandler(db.Context, limiter);

        var result = await handler.Handle(
            new AuthenticateUserCommand("student@adelaide.edu.au", "wrong", "1.2.3.4"), CancellationToken.None);

        Assert.False(result.IsRateLimited);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task Blocks_after_threshold_failures()
    {
        await using var db = await TestDb.CreateAsync();
        db.Context.Users.Add(NewActiveUser());
        await db.Context.SaveChangesAsync();
        var limiter = new InMemoryLoginRateLimiter(threshold: 3);
        var handler = new AuthenticateUserCommandHandler(db.Context, limiter);

        for (var i = 0; i < 3; i++)
        {
            await handler.Handle(new AuthenticateUserCommand("student@adelaide.edu.au", "wrong", "1.2.3.4"), CancellationToken.None);
        }

        var blocked = await handler.Handle(
            new AuthenticateUserCommand("student@adelaide.edu.au", "ChangeMe123!", "1.2.3.4"), CancellationToken.None);

        Assert.True(blocked.IsRateLimited);
    }

    [Fact]
    public async Task Successful_login_returns_user_and_resets_failures()
    {
        await using var db = await TestDb.CreateAsync();
        db.Context.Users.Add(NewActiveUser());
        await db.Context.SaveChangesAsync();
        var limiter = new InMemoryLoginRateLimiter(threshold: 3);
        var handler = new AuthenticateUserCommandHandler(db.Context, limiter);

        var result = await handler.Handle(
            new AuthenticateUserCommand("student@adelaide.edu.au", "ChangeMe123!", "1.2.3.4"), CancellationToken.None);

        Assert.False(result.IsRateLimited);
        Assert.NotNull(result.User);
        Assert.Equal("student@adelaide.edu.au", result.User!.Email);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false --filter "FullyQualifiedName~LoginRateLimitTests"`
Expected: FAIL — `AuthenticateUserCommand` has no IP param and the handler returns `AuthUserDto?`, not a result type.

- [ ] **Step 4: Update the command + handler**

Replace `backend/src/Application/Auth/Commands/AuthenticateUser/AuthenticateUserCommand.cs`:

```csharp
using Contracts.DTO.Auth;
using MediatR;

namespace Application.Auth.Commands.AuthenticateUser;

public sealed record AuthenticateUserCommand(string Email, string Password, string IpAddress)
    : IRequest<AuthenticationResult>;

public sealed record AuthenticationResult(AuthUserDto? User, bool IsRateLimited);
```

Replace `backend/src/Application/Auth/Commands/AuthenticateUser/AuthenticateUserCommandHandler.cs`:

```csharp
using Application.Common.Interfaces;
using Contracts.DTO.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Auth.Commands.AuthenticateUser;

public sealed class AuthenticateUserCommandHandler : IRequestHandler<AuthenticateUserCommand, AuthenticationResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILoginRateLimiter _rateLimiter;

    public AuthenticateUserCommandHandler(IApplicationDbContext dbContext, ILoginRateLimiter rateLimiter)
    {
        _dbContext = dbContext;
        _rateLimiter = rateLimiter;
    }

    public async Task<AuthenticationResult> Handle(AuthenticateUserCommand request, CancellationToken cancellationToken)
    {
        if (await _rateLimiter.IsBlockedAsync(request.Email, request.IpAddress, cancellationToken))
        {
            return new AuthenticationResult(null, IsRateLimited: true);
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        var valid = user is { IsActive: true } && BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!valid)
        {
            await _rateLimiter.RecordFailureAsync(request.Email, request.IpAddress, cancellationToken);
            return new AuthenticationResult(null, IsRateLimited: false);
        }

        await _rateLimiter.ResetAsync(request.Email, request.IpAddress, cancellationToken);

        var dto = new AuthUserDto(
            user!.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            user.Department,
            user.Degree,
            user.Sex,
            user.AvatarUrl,
            user.Nationality,
            user.Age,
            user.Bio,
            user.AppearInDrawPool,
            user.IsAdmin);

        return new AuthenticationResult(dto, IsRateLimited: false);
    }
}
```

- [ ] **Step 5: Fix the existing auth tests for the new shapes**

In `backend/tests/Application.UnitTests/Auth/AuthCommandHandlerTests.cs`:
- Add `using Application.UnitTests.TestDoubles;` and `using Application.UnitTests.Common;`.
- The `Login_returns_null_until_account_is_activated` test constructs `new AuthenticateUserCommandHandler(db.Context)` and calls with a 2-arg command; update it:

```csharp
        var handler = new AuthenticateUserCommandHandler(db.Context, new InMemoryLoginRateLimiter());

        var beforeActivation = await handler.Handle(
            new AuthenticateUserCommand(user.Email, "ChangeMe123!", "1.2.3.4"),
            CancellationToken.None);

        user.Activate();
        await db.Context.SaveChangesAsync();

        var afterActivation = await handler.Handle(
            new AuthenticateUserCommand(user.Email, "ChangeMe123!", "1.2.3.4"),
            CancellationToken.None);

        Assert.Null(beforeActivation.User);
        Assert.NotNull(afterActivation.User);
        Assert.Equal(user.Id, afterActivation.User!.UserId);
```

- [ ] **Step 6: Rewrite `AuthController` login + add refresh/logout endpoints**

Replace the body of `backend/src/Api/Controllers/AuthController.cs` with the version below. It injects `ITokenService` + `IRefreshTokenStore`, issues access + refresh tokens, returns 429 when rate-limited, and adds refresh/logout/logout-all:

```csharp
using Application.Auth.Commands.ActivateUser;
using Application.Auth.Commands.AuthenticateUser;
using Application.Auth.Commands.Logout;
using Application.Auth.Commands.LogoutAll;
using Application.Auth.Commands.RefreshToken;
using Application.Auth.Commands.RegisterUser;
using Application.Auth.Commands.ResendActivationEmail;
using Application.Common.Interfaces;
using Contracts.DTO.Auth;
using Infrastructure.Configuration.Options;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ISender _sender;
    private readonly AuthOptions _options;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenStore _refreshTokenStore;

    public AuthController(
        ISender sender,
        IOptions<AuthOptions> options,
        ITokenService tokenService,
        IRefreshTokenStore refreshTokenStore)
    {
        _sender = sender;
        _options = options.Value;
        _tokenService = tokenService;
        _refreshTokenStore = refreshTokenStore;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(
            request.Email,
            request.Password,
            request.DisplayName,
            request.AvatarUrl,
            request.Department,
            request.Degree,
            request.Sex,
            request.Nationality,
            request.Age,
            _options.AllowedEmailDomain,
            _options.ActivationBaseUrl);

        try
        {
            return Ok(await _sender.Send(command, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("resend-activation")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendActivation([FromBody] ResendActivationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _sender.Send(new ResendActivationEmailCommand(request.Email, _options.ActivationBaseUrl), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _sender.Send(new AuthenticateUserCommand(request.Email, request.Password, ip), cancellationToken);

        if (result.IsRateLimited)
        {
            Response.Headers.RetryAfter = (_options.LoginFailureWindowMinutes * 60).ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Too many failed login attempts. Try again later." });
        }

        if (result.User is null)
        {
            return Unauthorized(new { error = "Invalid credentials or account inactive." });
        }

        return Ok(await IssueAuthResponse(result.User, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("activate")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate([FromQuery] string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { error = "Activation token is required." });
        }

        try
        {
            var user = await _sender.Send(new ActivateUserCommand(token), cancellationToken);
            if (user is null)
            {
                return NotFound(new { error = "Activation token invalid." });
            }

            return Ok(await IssueAuthResponse(user, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var user = await _sender.Send(new RefreshTokenCommand(request.RefreshToken), cancellationToken);
        if (user is null)
        {
            return Unauthorized(new { error = "Invalid or expired refresh token." });
        }

        // Rotate: revoke the presented token, issue a fresh pair.
        await _refreshTokenStore.RevokeAsync(request.RefreshToken, cancellationToken);
        return Ok(await IssueAuthResponse(user, cancellationToken));
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await _sender.Send(new LogoutCommand(request.RefreshToken), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _sender.Send(new LogoutAllCommand(userId), cancellationToken);
        return NoContent();
    }

    private async Task<AuthResponse> IssueAuthResponse(AuthUserDto user, CancellationToken cancellationToken)
    {
        var accessToken = _tokenService.IssueAccessToken(user.UserId, user.Email, user.Role);
        var refreshToken = _tokenService.GenerateRefreshToken();
        await _refreshTokenStore.StoreAsync(
            user.UserId, refreshToken, TimeSpan.FromDays(_options.RefreshTokenDays), cancellationToken);
        return new AuthResponse(accessToken, refreshToken, user);
    }
}
```

> The command types referenced in the controller above (`RefreshTokenCommand`, `LogoutCommand`, `LogoutAllCommand`) are created in Steps 7–8. The `Application.UnitTests` project does not reference `Api`, so the unit tests run green before the controller compiles; the first full-solution build is Step 11.

- [ ] **Step 7: Create the refresh command + handler**

Create `backend/src/Application/Auth/Commands/RefreshToken/RefreshTokenCommand.cs`:

```csharp
using Contracts.DTO.Auth;
using MediatR;

namespace Application.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthUserDto?>;
```

Create `backend/src/Application/Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs`:

```csharp
using Application.Common.Interfaces;
using Contracts.DTO.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthUserDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IRefreshTokenStore _refreshTokenStore;

    public RefreshTokenCommandHandler(IApplicationDbContext dbContext, IRefreshTokenStore refreshTokenStore)
    {
        _dbContext = dbContext;
        _refreshTokenStore = refreshTokenStore;
    }

    public async Task<AuthUserDto?> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var userId = await _refreshTokenStore.ValidateAsync(request.RefreshToken, cancellationToken);
        if (userId is null)
        {
            return null;
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value && u.IsActive, cancellationToken);

        if (user is null)
        {
            return null;
        }

        return new AuthUserDto(
            user.Id, user.Email, user.DisplayName, user.Role, user.Department, user.Degree, user.Sex,
            user.AvatarUrl, user.Nationality, user.Age, user.Bio, user.AppearInDrawPool, user.IsAdmin);
    }
}
```

- [ ] **Step 8: Create logout + logout-all commands and handlers**

Create `backend/src/Application/Auth/Commands/Logout/LogoutCommand.cs`:

```csharp
using MediatR;

namespace Application.Auth.Commands.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest;
```

Create `backend/src/Application/Auth/Commands/Logout/LogoutCommandHandler.cs`:

```csharp
using Application.Common.Interfaces;
using MediatR;

namespace Application.Auth.Commands.Logout;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IRefreshTokenStore _refreshTokenStore;

    public LogoutCommandHandler(IRefreshTokenStore refreshTokenStore)
    {
        _refreshTokenStore = refreshTokenStore;
    }

    public Task Handle(LogoutCommand request, CancellationToken cancellationToken)
        => _refreshTokenStore.RevokeAsync(request.RefreshToken, cancellationToken);
}
```

Create `backend/src/Application/Auth/Commands/LogoutAll/LogoutAllCommand.cs`:

```csharp
using MediatR;

namespace Application.Auth.Commands.LogoutAll;

public sealed record LogoutAllCommand(Guid UserId) : IRequest;
```

Create `backend/src/Application/Auth/Commands/LogoutAll/LogoutAllCommandHandler.cs`:

```csharp
using Application.Common.Interfaces;
using MediatR;

namespace Application.Auth.Commands.LogoutAll;

public sealed class LogoutAllCommandHandler : IRequestHandler<LogoutAllCommand>
{
    private readonly IRefreshTokenStore _refreshTokenStore;

    public LogoutAllCommandHandler(IRefreshTokenStore refreshTokenStore)
    {
        _refreshTokenStore = refreshTokenStore;
    }

    public Task Handle(LogoutAllCommand request, CancellationToken cancellationToken)
        => _refreshTokenStore.RevokeAllAsync(request.UserId, cancellationToken);
}
```

- [ ] **Step 9: Write the refresh/logout-all handler tests**

Create `backend/tests/Application.UnitTests/Auth/RefreshTokenCommandHandlerTests.cs`:

```csharp
using Application.Auth.Commands.LogoutAll;
using Application.Auth.Commands.RefreshToken;
using Application.UnitTests.Common;
using Application.UnitTests.TestDoubles;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Xunit;

namespace Application.UnitTests.Auth;

public sealed class RefreshTokenCommandHandlerTests
{
    private static User NewActiveUser(Guid id) => new(
        id: id,
        email: "student@adelaide.edu.au",
        displayName: "Student",
        createdAt: DateTimeOffset.UtcNow,
        role: "Student",
        passwordHash: "hash",
        department: AdelaideDepartment.ComputerScience,
        degree: AcademicDegree.Bachelor,
        sex: UserSex.Other,
        isActive: true);

    [Fact]
    public async Task Valid_token_returns_owning_user()
    {
        await using var db = await TestDb.CreateAsync();
        var id = Guid.NewGuid();
        db.Context.Users.Add(NewActiveUser(id));
        await db.Context.SaveChangesAsync();
        var store = new InMemoryRefreshTokenStore();
        await store.StoreAsync(id, "tok-1", TimeSpan.FromDays(14));

        var handler = new RefreshTokenCommandHandler(db.Context, store);
        var result = await handler.Handle(new RefreshTokenCommand("tok-1"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(id, result!.UserId);
    }

    [Fact]
    public async Task Unknown_token_returns_null()
    {
        await using var db = await TestDb.CreateAsync();
        var handler = new RefreshTokenCommandHandler(db.Context, new InMemoryRefreshTokenStore());

        var result = await handler.Handle(new RefreshTokenCommand("nope"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LogoutAll_revokes_all_user_tokens()
    {
        var store = new InMemoryRefreshTokenStore();
        var id = Guid.NewGuid();
        await store.StoreAsync(id, "tok-1", TimeSpan.FromDays(14));
        await store.StoreAsync(id, "tok-2", TimeSpan.FromDays(14));

        var handler = new LogoutAllCommandHandler(store);
        await handler.Handle(new LogoutAllCommand(id), CancellationToken.None);

        Assert.Null(await store.ValidateAsync("tok-1"));
        Assert.Null(await store.ValidateAsync("tok-2"));
    }
}
```

- [ ] **Step 10: Delete the obsolete static JWT helper**

The controller no longer calls `AppJwt.Issue` (it uses `ITokenService`). Remove the dead file:

```bash
git rm backend/src/Api/Auth/AppJwt.cs
```

- [ ] **Step 11: Full-solution build + full unit suite (first green build of the cutover)**

Run: `dotnet build Marketplace.sln --no-restore /m:1 /p:BuildInParallel=false`
Expected: Build succeeded (DTO, handler, commands, controller, and `AppJwt` removal are all in place).
Then: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false`
Expected: ALL tests pass — `LoginRateLimitTests` (3), `RefreshTokenCommandHandlerTests` (3), and the adapted `AuthCommandHandlerTests`.

- [ ] **Step 12: Commit the cutover as one green change**

```bash
git add backend/src/Contracts/DTO/Auth/ \
        backend/src/Application/Auth/Commands/ \
        backend/src/Api/Controllers/AuthController.cs \
        backend/tests/Application.UnitTests/Auth/
git rm backend/src/Api/Auth/AppJwt.cs
git commit -m "feat: refresh-token auth cutover (rate-limited login, refresh/logout, remove static AppJwt)"
```

---

## Task 9: Profile + anon-handle controller endpoints

**Files:**
- Create: `backend/src/Contracts/DTO/Users/UpdateProfileRequest.cs`
- Create: `backend/src/Contracts/DTO/Users/AnonHandleResponse.cs`
- Modify: `backend/src/Api/Controllers/UsersController.cs`

- [ ] **Step 1: Read the current controller**

Open `backend/src/Api/Controllers/UsersController.cs` and note its existing constructor (it already injects `ISender` for the review endpoints) and how it reads the caller's id from claims. Follow that exact pattern.

- [ ] **Step 2: Create the DTOs**

Create `backend/src/Contracts/DTO/Users/UpdateProfileRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Contracts.DTO.Users;

public sealed class UpdateProfileRequest
{
    [MaxLength(280)]
    public string? Bio { get; init; }

    public bool AppearInDrawPool { get; init; }
}
```

Create `backend/src/Contracts/DTO/Users/AnonHandleResponse.cs`:

```csharp
namespace Contracts.DTO.Users;

public sealed record AnonHandleResponse(string AnonHandle);
```

- [ ] **Step 3: Add the endpoints**

In `backend/src/Api/Controllers/UsersController.cs`, add `[Authorize]` endpoints. Add the required usings at the top (`Application.Users.Commands.UpdateProfile;`, `Application.Users.Commands.GetOrCreateAnonHandle;`, `Contracts.DTO.Users;`, `Microsoft.AspNetCore.Authorization;`, `System.Security.Claims;`) and these actions inside the class:

```csharp
    [Authorize]
    [HttpPatch("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _sender.Send(new UpdateProfileCommand(userId, request.Bio, request.AppearInDrawPool), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me/anon-handle")]
    [ProducesResponseType(typeof(AnonHandleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAnonHandle(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var handle = await _sender.Send(new GetOrCreateAnonHandleCommand(userId), cancellationToken);
        return Ok(new AnonHandleResponse(handle));
    }
```

> If `UsersController` does not already have an `ISender _sender` field, add it to the constructor following the pattern in `AuthController`.

- [ ] **Step 4: Build**

Run: `dotnet build Marketplace.sln --no-restore /m:1 /p:BuildInParallel=false`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Contracts/DTO/Users/UpdateProfileRequest.cs backend/src/Contracts/DTO/Users/AnonHandleResponse.cs backend/src/Api/Controllers/UsersController.cs
git commit -m "feat: profile update and anon-handle endpoints"
```

---

## Task 10: Wire DI, health checks, seed, and docs

**Files:**
- Modify: `backend/src/Api/Program.cs`
- Modify: `backend/db/seed.sql`
- Modify: `README.md`, `AGENTS.md`

- [ ] **Step 1: Register Redis + new services in `Program.cs`**

In `backend/src/Api/Program.cs`, after the `redisOptions` binding (line ~70) and before/around the existing scoped registrations (line ~102-105), add:

```csharp
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(
    _ => StackExchange.Redis.ConnectionMultiplexer.Connect(redisOptions.ConnectionString));

builder.Services.AddScoped<Application.Common.Interfaces.ITokenService, Infrastructure.Auth.AppJwtTokenService>();
builder.Services.AddScoped<Application.Common.Interfaces.IRefreshTokenStore, Infrastructure.Auth.RedisRefreshTokenStore>();
builder.Services.AddScoped<Application.Common.Interfaces.ILoginRateLimiter, Infrastructure.Auth.RedisLoginRateLimiter>();
builder.Services.AddScoped<Application.Common.Interfaces.IAnonHandleGenerator, Infrastructure.Auth.DefaultAnonHandleGenerator>();
```

> Note: the health-check registration at line ~148 already calls `.AddRedis(redisOptions.ConnectionString, ...)`. Leave it; it can share the connection string. No change needed there for this plan.

- [ ] **Step 2: Build and run the full unit suite**

Run: `dotnet build Marketplace.sln --no-restore /m:1 /p:BuildInParallel=false`
Then: `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --no-restore /m:1 /p:BuildInParallel=false`
Expected: Build succeeded; ALL tests pass.

- [ ] **Step 3: Update the seed to mark the demo account as admin**

In `backend/db/seed.sql`, find the INSERT/UPSERT for the seeded `student@adelaide.edu.au` user and set the new column. If the seed uses an `INSERT ... ON CONFLICT DO UPDATE`, add `is_admin = true` to the column list/values (and `appear_in_draw_pool = false`, `bio = NULL` if columns are listed explicitly). Keep it idempotent — do not drop/recreate rows.

Example fragment to match the existing style:

```sql
-- after the existing column assignments for the seeded student account
    is_admin = true,
    appear_in_draw_pool = false
```

- [ ] **Step 4: Verify the stack boots with Docker Compose**

Run from `backend/`: `docker compose up --build`
Expected: API starts, applies the `AddUserIdentityFields` migration, seeds without error, `/healthz` returns ok. Stop with `docker compose down`.

- [ ] **Step 5: Document new endpoints + config**

In `README.md` and `AGENTS.md`:
- Add `POST /api/auth/refresh`, `POST /api/auth/logout`, `POST /api/auth/logout-all`, `PATCH /api/users/me`, `GET /api/users/me/anon-handle` to the endpoint lists.
- Document new `Auth__AccessTokenMinutes`, `Auth__RefreshTokenDays`, `Auth__LoginMaxFailuresPerEmail`, `Auth__LoginMaxFailuresPerIp`, `Auth__LoginFailureWindowMinutes` config keys (with the defaults from `AuthOptions`).
- Note that login now returns `{ token, refreshToken, user }` and that access tokens expire in 15 minutes by default.

- [ ] **Step 6: Commit**

```bash
git add backend/src/Api/Program.cs backend/db/seed.sql README.md AGENTS.md
git commit -m "chore: wire identity/auth services, seed admin, update docs"
```

---

## Done criteria for Plan 1

- `dotnet build` succeeds; `dotnet test` is all green (existing auth tests adapted + new profile/anon/refresh/rate-limit tests).
- Login returns access + refresh tokens; access tokens are short-lived; `refresh` rotates; `logout`/`logout-all` revoke via Redis.
- Repeated bad logins return 429 with `Retry-After`.
- `PATCH /api/users/me` updates bio + draw-pool opt-in; `GET /api/users/me/anon-handle` returns a stable handle (generated once).
- Migration `AddUserIdentityFields` applies cleanly; seeded student account is an admin.
- README/AGENTS document the new endpoints and config.

These satisfy the spec's Section 4 profile fields, Section 5 profile + auth endpoints, and Section 6 Redis refresh-token flow + login rate limiting. The remaining Section 6 items (category/feed caches, anon-handle lookup cache) and activation-token-to-Redis migration are deferred to Plans 2–3 where they are first exercised.
