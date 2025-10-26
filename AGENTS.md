Here’s a drop-in **AGENTS.md** you can place at the repo root to drive Codex CLI for a backend-first, step-by-step scaffold in .NET.

---

# AGENTS.md

## Purpose

You are an AI code agent working in this repository to build the **backend** for **Adelaide University Marketplace**. Work **incrementally**, committing small, reviewable changes. Your primary goals:

1. Set up an ASP.NET Core Web API with clean architecture.
2. Implement OAuth2/OIDC login flow (domain-enforced `@adelaide.edu.au`), issuing app JWTs.
3. Create primary entities & EF Core migrations targeting PostgreSQL.
4. Prepare integration points (Redis, RabbitMQ, Elasticsearch, Cloudflare R2, Stripe) with safe stubs.
5. Ship runnable local dev (docker-compose) and solid defaults.

Follow the steps in this file strictly. When in doubt, prefer smaller PRs, strong typing, and clear docs.

---

## Non-negotiable guardrails

-   **Language & Framework:** .NET 8, C#. Web API only (no MVC views).
-   **Architecture:** Solution with projects: `Api`, `Application`, `Domain`, `Infrastructure`, `Contracts` (+ later `Services/*`).
-   **Security:** OIDC login required for protected APIs; enforce email domain check. Use environment variables for secrets. No secrets committed.
-   **Tooling:** EF Core for migrations; Serilog for logging; Swagger for docs; FluentValidation for input validation.
-   **Testing:** Add minimal unit tests for domain and application layers as you go.
-   **Docs:** Update `README.md` and this `AGENT.md` whenever behavior or setup changes.

---

## Repository layout (target)

```
/backend
  /src
    /Api
    /Application
    /Domain
    /Infrastructure
    /Contracts
  /tests
    /Domain.Tests
    /Application.Tests
  /db
    seed.sql
  /scripts
    dev.ps1
    dev.sh
  /infra
    docker-compose.yml
    /azure-deploy   # IaC stubs later
/README.md
/AGENT.md
```

---

## Step plan (backend-first)

### Step B1 — Solution & packages

**Create solution and projects**

-   `dotnet new sln -n Marketplace`
-   `dotnet new webapi -n Api`
-   `dotnet new classlib -n Application`
-   `dotnet new classlib -n Domain`
-   `dotnet new classlib -n Infrastructure`
-   `dotnet new classlib -n Contracts`
-   Add to solution, set references:

    -   `Api -> Application, Infrastructure, Contracts`
    -   `Application -> Domain, Contracts`
    -   `Infrastructure -> Application, Domain, Contracts`

**NuGet packages (pin to latest stable where possible)**

-   Api:

    -   `Swashbuckle.AspNetCore`
    -   `Serilog.AspNetCore`, `Serilog.Sinks.Console`
    -   `Microsoft.AspNetCore.Authentication.JwtBearer`
    -   `Microsoft.AspNetCore.Authentication.OpenIdConnect`

-   Application:

    -   `MediatR`, `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`
    -   `AutoMapper` (or `Mapster`) — choose one; prefer Mapster for no runtime reflection: `Mapster`, `Mapster.DependencyInjection`

-   Domain: (no external packages)
-   Infrastructure:

    -   `Microsoft.EntityFrameworkCore`
    -   `Npgsql.EntityFrameworkCore.PostgreSQL`
    -   `StackExchange.Redis`
    -   `RabbitMQ.Client` (or `MassTransit` + `MassTransit.RabbitMQ`)
    -   `Elastic.Clients.Elasticsearch`
    -   `Stripe.net`
    -   `AWSSDK.S3` (R2 compatible S3 API) OR `Minio` SDK; choose S3 compatible
    -   `OpenIddict` **(optional later if we self-host OIDC)**

**Definition of done (B1)**

-   Builds successfully.
-   `Api` runs and serves `/swagger` and `/healthz`.
-   Serilog writes structured logs to console.
-   `README.md` updated with run instructions.

---

### Step B2 — Configuration & env

**appsettings.json & environment**

-   Add strongly-typed options classes in `Infrastructure`:

    -   `PostgresOptions`, `RedisOptions`, `RabbitMqOptions`, `ElasticsearchOptions`, `StripeOptions`, `R2Options`, `AuthOptions` (authority, clientId, etc.)

-   Wire them via `IOptions<T>`.
-   Require these env vars at runtime (document in README):

```
ASPNETCORE_ENVIRONMENT=Development
POSTGRES__CONNECTION_STRING=Host=localhost;Database=marketplace;Username=postgres;Password=postgres
REDIS__CONNECTION_STRING=localhost:6379
RABBITMQ__HOST=amqp://guest:guest@localhost:5672
ELASTIC__URI=http://localhost:9200
STRIPE__SECRET_KEY=sk_test_xxx
R2__ACCOUNT_ID=...
R2__ACCESS_KEY_ID=...
R2__SECRET_ACCESS_KEY=...
R2__BUCKET=marketplace-media
AUTH__AUTHORITY=https://<your-oidc-provider>
AUTH__CLIENT_ID=...
AUTH__CLIENT_SECRET=...
AUTH__APP_JWT_ISSUER=https://marketplace.api
AUTH__APP_JWT_SIGNING_KEY=<dev-only-long-random>
AUTH__ALLOWED_EMAIL_DOMAIN=adelaide.edu.au
```

**Definition of done (B2)**

-   `Api` validates required config on startup and fails fast with clear errors.
-   `/healthz` includes dependency readiness checks (ping Postgres, optional Redis).

---

### Step B3 — Primary domain model & migrations

**Entities (Domain project)**

-   `User` (Id, Email, DisplayName, CreatedAt, Role)
-   `Category` (Id, Name, Slug)
-   `Item` (Id, SellerId -> User, Title, Description, Price, CategoryId, Status, CreatedAt, UpdatedAt)
-   `ListingImage` (Id, ItemId, Url, SortOrder)
-   `Order` (Id, BuyerId -> User, Total, Status, PaymentProvider, PaymentRef, CreatedAt)
-   `OrderItem` (Id, OrderId, ItemId, Price, Qty)
-   `ChatMessage` (Id, ThreadId, FromUserId, ToUserId, ItemId?, Body, SentAt) — keep minimal now
-   Enums in `Domain.Shared` for `ItemStatus`, `OrderStatus`, `PaymentProvider`

**Infrastructure**

-   `MarketplaceDbContext` with DbSets
-   Configurations via `IEntityTypeConfiguration<>`
-   EF Core migrations:

    -   `dotnet ef migrations add Initial --project Infrastructure --startup-project Api`
    -   `dotnet ef database update --project Infrastructure --startup-project Api`

**Definition of done (B3)**

-   DB creates successfully.
-   Basic referential constraints enforced.
-   Seed placeholders exist (`db/seed.sql`, to be refined later).

---

### Step B4 — Auth flow (OIDC login + app JWT issuance)

**Approach**

-   The API will **accept OIDC login via Authorization Code** from mobile client using a hosted provider (dev: generic OIDC or Auth0/Keycloak; prod: uni’s OIDC).
-   After validating the OIDC ID token, the API **issues its own short-lived JWT** (HMAC) with user id/email/roles for app API calls.

**Implementation**

-   In `Api`, add:

    -   `OpenIdConnect` handler configured from `AUTH__AUTHORITY`, `AUTH__CLIENT_ID`, `AUTH__CLIENT_SECRET`, scopes `openid profile email`.
    -   Callback endpoint `/auth/oidc/callback` that:

        -   Validates ID token, extracts claims.
        -   **Domain enforcement:** reject if `email` does not end with `@adelaide.edu.au`.
        -   Upsert `User` in DB.
        -   Issue app JWT (HMAC using `AUTH__APP_JWT_SIGNING_KEY`).
        -   Return `{ token, user }` JSON to the mobile app.

    -   `JwtBearer` authentication for subsequent API calls.
    -   `[Authorize]` requirement for protected endpoints.

-   Provide `/auth/dev-login` behind `Development` environment flag for local testing (accepts mock email, applies same domain check).

**Definition of done (B4)**

-   `/auth/oidc/callback` returns app JWT for valid OIDC sign-in.
-   Requests with `Authorization: Bearer <token>` hit protected endpoints.
-   Rejected if email domain ≠ `adelaide.edu.au`.

---

### Step B5 — Basic APIs (CRUD & contracts)

**Contracts project**

-   Request/response DTOs for:

    -   `Items`: CreateItemRequest, UpdateItemRequest, ItemResponse
    -   `Categories`: CategoryResponse
    -   `Auth`: LoginResponse

-   Validation with FluentValidation in `Application`.

**Api endpoints (minimal)**

-   `GET /healthz`
-   `GET /categories`
-   `GET /items?categoryId=&q=&sort=` (no ES yet; use DB + simple filters)
-   `GET /items/{id}`
-   `POST /items` `[Authorize]` (seller only)
-   `PUT /items/{id}` `[Authorize]` (owner only)
-   `DELETE /items/{id}` `[Authorize]` (owner only)

**Definition of done (B5)**

-   Endpoints live with Swagger and model examples.
-   Validation errors return 400 with problem details.

---

### Step B6 — Integration stubs

-   **Redis:** register `IConnectionMultiplexer`, expose a small `ICache` abstraction.
-   **RabbitMQ:** create a lightweight `IEventBus` interface; stub publisher (no consumers yet).
-   **Elasticsearch:** register client; create `IItemSearch` interface stub (return “not implemented” until Step D).
-   **R2 signed uploads:** add `POST /uploads/sign` that returns pre-signed URL stub (implemented later).
-   **Stripe:** add service registration and config; keep webhook controller placeholder `/webhooks/stripe`.

**Definition of done (B6)**

-   All clients resolve from DI.
-   Health endpoints report their registration state (not necessarily connectivity yet).

---

### Step B7 — Local dev environment

Create `/infra/docker-compose.yml` with services:

-   `postgres:16` (port 5432, user/pass `postgres`)
-   `redis:7` (6379)
-   `rabbitmq:management` (5672/15672)
-   `elasticsearch:8` (9200, dev insecure mode)
-   (optional) `mailhog` for local email testing

Add `/scripts/dev.sh` and `dev.ps1` to export env vars and run:

```bash
docker compose -f infra/docker-compose.yml up -d
dotnet ef database update --project backend/src/Infrastructure --startup-project backend/src/Api
dotnet run --project backend/src/Api
```

**Definition of done (B7)**

-   One-liner scripts boot infra, migrate DB, and run the API.

---

## Coding standards

-   **Namespaces:** `Marketplace.Api`, `Marketplace.Application`, `Marketplace.Domain`, `Marketplace.Infrastructure`, `Marketplace.Contracts`.
-   **Nullable reference types ON**; treat warnings as errors in CI.
-   **CQRS-light:** Queries/Commands via MediatR; keep handlers small.
-   **Validation:** FluentValidation at the edge (DTOs); domain invariants inside entities.
-   **Mapping:** Mapster; centralize configs.
-   **Logging:** Serilog structured logs (`RequestId`, `UserId`, `TraceId`).
-   **Error model:** RFC 7807 (ProblemDetails) for 4xx/5xx.

---

## Commit & PR workflow

-   Small, atomic commits. Conventional commits:

    -   `feat(api): add jwt login callback`
    -   `chore(infra): docker-compose with postgres/redis`
    -   `refactor(domain): split order aggregates`

-   Each step above should be a separate PR with:

    -   Short description
    -   Checklist of Definition of Done
    -   Manual test notes (curl examples)

---

## Minimal code scaffolds (snippets)

**Program.cs (Api) – essentials**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddAuthentication()
    .AddJwtBearer("AppJwt", opts =>
    {
        opts.TokenValidationParameters = new()
        {
            ValidIssuer = builder.Configuration["AUTH__APP_JWT_ISSUER"],
            ValidAudience = builder.Configuration["AUTH__APP_JWT_ISSUER"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["AUTH__APP_JWT_SIGNING_KEY"]!))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<MarketplaceDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration["POSTGRES__CONNECTION_STRING"]));

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration["POSTGRES__CONNECTION_STRING"]!);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();
```

**App JWT issuing helper**

```csharp
public static class AppJwt
{
    public static string Issue(string issuer, string signingKey, string userId, string email, string? role = null, TimeSpan? ttl = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
        };
        if (!string.IsNullOrWhiteSpace(role))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: issuer,
            claims: claims,
            expires: DateTime.UtcNow.Add(ttl ?? TimeSpan.FromHours(2)),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

---

## Manual testing (quick)

-   `GET /healthz` → `{ status: "ok" }`
-   `GET /swagger` → API docs visible
-   After Step B4:

    -   Call `/auth/dev-login?email=you@adelaide.edu.au` (dev only) → receive `{ token, user }`
    -   Use `Authorization: Bearer <token>` for protected endpoints.

---

## Future steps (tracked but not now)

-   Search-sync consumer → Elasticsearch indexing
-   Notifications service → RabbitMQ consumers → push/email
-   Stripe checkout & webhooks
-   R2 signed upload implementation
-   WebSocket chat (scale via Redis pub/sub)
-   Azure deploy (Container Apps/App Service, Azure Postgres Flexible Server, Azure Cache for Redis)
-   Application Insights telemetry

---

## Definition of Done (backend MVP)

-   Clean architecture solution compiles and runs.
-   OIDC login callback working; app JWT issuance and domain enforcement in place.
-   Primary entities + migrations created; CRUD for Items and Categories.
-   Local infra with docker-compose.
-   Logging, health checks, Swagger, validation.
-   Updated `README.md` with precise run steps and env vars.

---

**Agent: proceed with Step B1 now.**
